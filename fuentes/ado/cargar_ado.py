#!/usr/bin/env python3
"""Carga el backlog del Acelerador de Consolidación BPC en Azure DevOps.

Crea (si no existen) el proyecto, las épicas, las historias de usuario con su
épica padre, los enlaces de dependencia Predecessor/Successor, y el Test Plan con
un suite basado en requerimientos por historia. Todo queda asignado al responsable.

Idempotente: el proyecto se busca por nombre, las épicas por título, las historias
por el tag HU-XX, los enlaces por relación existente y los suites por requirementId.
Una historia que ya existe se actualiza con los datos de backlog.json.

Autenticación: token de Azure CLI (`az login` con el usuario del responsable).
Uso:  python3 fuentes/ado/cargar_ado.py
"""
import ast
import json
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from html import escape
from pathlib import Path

ORG = "https://dev.azure.com/cmconsultingsas"
PROYECTO = "Acelerador Consolidacion BPC"
RESPONSABLE = "luis.maury@cmconsulting.com.co"
PLAN = "Acelerador BPC — Plan de pruebas"
API = "7.1"
ADO_RESOURCE = "499b84ac-1321-427f-aa17-267ca6975798"

RAIZ = Path(__file__).resolve().parents[1]
BACKLOG = json.loads((RAIZ / "hu" / "backlog.json").read_text(encoding="utf-8"))
PRIORIDAD = {"Alta": 1, "Media": 2, "Baja": 3}



def _foco_epicas():
    """Foco de cada épica, leído de la lista EPICAS de fuentes/hu/build_hu.py sin ejecutarlo."""
    arbol = ast.parse((RAIZ / "hu" / "build_hu.py").read_text(encoding="utf-8"))
    for nodo in arbol.body:
        if isinstance(nodo, ast.Assign) and any(getattr(t, "id", None) == "EPICAS" for t in nodo.targets):
            return {c: foco for c, _, foco in ast.literal_eval(nodo.value)}
    sys.exit("No se encontró EPICAS en build_hu.py")


FOCO = _foco_epicas()

_token = None


def token():
    global _token
    if _token is None:
        _token = subprocess.run(
            ["az", "account", "get-access-token", "--resource", ADO_RESOURCE, "--query", "accessToken", "-o", "tsv"],
            check=True, capture_output=True, text=True).stdout.strip()
    return _token


def api(method, path, body=None, content_type="application/json", proyecto=True, ok404=False, version=API):
    base = f"{ORG}/{urllib.parse.quote(PROYECTO)}" if proyecto else ORG
    sep = "&" if "?" in path else "?"
    url = f"{base}/{path}{sep}api-version={version}"
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method, headers={
        "Authorization": f"Bearer {token()}", "Content-Type": content_type, "Accept": "application/json"})
    for intento in range(5):
        try:
            with urllib.request.urlopen(req) as r:
                txt = r.read().decode()
                return json.loads(txt) if txt else {}
        except urllib.error.HTTPError as e:
            if e.code == 404 and ok404:
                return None
            if e.code in (429, 500, 502, 503) and intento < 4:
                time.sleep(2 ** intento)
                continue
            raise RuntimeError(f"{method} {url} -> {e.code}: {e.read().decode()[:800]}") from None


def wiql(query):
    ids = [w["id"] for w in api("POST", "_apis/wit/wiql", {"query": query}).get("workItems", [])]
    if not ids:
        return []
    items = []
    for i in range(0, len(ids), 200):
        lote = ",".join(map(str, ids[i:i + 200]))
        items += api("GET", f"_apis/wit/workitems?ids={lote}&$expand=relations")["value"]
    return items


# ---------------------------------------------------------------- proyecto
def asegurar_proyecto():
    if api("GET", f"_apis/projects/{urllib.parse.quote(PROYECTO)}", proyecto=False, ok404=True):
        print(f"Proyecto ya existe: {PROYECTO}")
        return
    print(f"Creando proyecto {PROYECTO} (Agile, privado)…")
    r = subprocess.run(["az", "devops", "project", "create", "--name", PROYECTO, "--process", "Agile",
                        "--visibility", "private", "--source-control", "git", "--org", ORG, "-o", "json"],
                       capture_output=True, text=True)
    if r.returncode != 0:
        print("ERROR al crear el proyecto (az devops project create):\n" + r.stderr.strip(), file=sys.stderr)
        sys.exit(2)
    for _ in range(30):  # la creación es asíncrona
        p = api("GET", f"_apis/projects/{urllib.parse.quote(PROYECTO)}", proyecto=False, ok404=True)
        if p and p.get("state") == "wellFormed":
            return
        time.sleep(5)
    sys.exit("El proyecto no quedó en estado wellFormed a tiempo.")


# ---------------------------------------------------------------- work items
def campos_ops(campos, actuales=None):
    ops = []
    for ref, valor in campos.items():
        if actuales is not None and _igual(ref, actuales.get(ref), valor):
            continue
        ops.append({"op": "add", "path": f"/fields/{ref}", "value": valor})
    return ops


def _igual(ref, actual, valor):
    if isinstance(actual, dict):  # identidad
        return actual.get("uniqueName", "").lower() == str(valor).lower()
    if ref == "System.Tags":
        return sorted(t.strip() for t in (actual or "").split(";")) == sorted(t.strip() for t in valor.split(";"))
    if isinstance(actual, str) and isinstance(valor, str) and valor.startswith("<"):
        return _html(actual) == _html(valor)  # ADO agrega espacios antes de cerrar etiquetas
    return actual == valor


def _html(v):
    return re.sub(r"\s+(?=<)|(?<=>)\s+", "", v).strip()


def crear(tipo, ops):
    return api("POST", f"_apis/wit/workitems/${urllib.parse.quote(tipo)}", ops, "application/json-patch+json")


def actualizar(wid, ops):
    return api("PATCH", f"_apis/wit/workitems/{wid}", ops, "application/json-patch+json")


def url_wi(wid):
    return f"{ORG}/_apis/wit/workItems/{wid}"


def tiene_rel(item, rel, wid):
    return any(r["rel"] == rel and r["url"].rstrip("/").split("/")[-1] == str(wid) for r in item.get("relations") or [])


def asegurar_epicas():
    existentes = {w["fields"]["System.Title"]: w for w in wiql(
        "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.WorkItemType] = 'Epic'")}
    epicas = {}
    for e in BACKLOG["epicas"]:
        titulo = f"{e['codigo']} — {e['nombre']}"
        campos = {
            "System.Title": titulo,
            "System.Description": f"<p><b>Foco:</b> {escape(FOCO[e['codigo']])}</p>",
            "System.AssignedTo": RESPONSABLE,
            "System.Tags": e["codigo"],
        }
        w = existentes.get(titulo)
        if w:
            ops = campos_ops(campos, w["fields"])
            if ops:
                actualizar(w["id"], ops)
            print(f"  = {titulo} (#{w['id']})")
        else:
            w = crear("Epic", campos_ops(campos))
            print(f"  + {titulo} (#{w['id']})")
        epicas[e["codigo"]] = w["id"]
    return epicas


def html_historia(h):
    narrativa = (f"<p><b>Como</b> {escape(h['rol'])}, <b>quiero</b> {escape(h['quiero'])}, "
                 f"<b>para</b> {escape(h['para'])}.</p>")
    traz = ", ".join(h["trazabilidad"]) or "—"
    ux = ", ".join(h["ux"]) or "Sin interfaz"
    partes = [narrativa, f"<p><b>Trazabilidad:</b> {escape(traz)}</p>", f"<p><b>Pantallas UX:</b> {escape(ux)}</p>"]
    if h.get("nota_prioridad"):
        partes.append(f"<p><b>Nota de prioridad:</b> {escape(h['nota_prioridad'])}</p>")
    return "".join(partes)


def asegurar_historias(epicas):
    existentes = {}
    # WIQL compara tags completos (CONTAINS 'HU-' no encuentra 'HU-01'): se filtra aquí.
    for w in wiql("SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project "
                  "AND [System.WorkItemType] = 'User Story' ORDER BY [System.Id]"):
        for t in (w["fields"].get("System.Tags") or "").split(";"):
            if t.strip().startswith("HU-"):
                existentes.setdefault(t.strip(), w)
    historias = {}
    for h in BACKLOG["hus"]:
        rfs = [t for t in h["trazabilidad"] if t.startswith("RF-")]
        campos = {
            "System.Title": f"{h['codigo']} — {h['titulo']}",
            "System.Description": html_historia(h),
            "Microsoft.VSTS.Common.AcceptanceCriteria": "<ul>" + "".join(f"<li>{escape(c)}</li>" for c in h["criterios"]) + "</ul>",
            "Microsoft.VSTS.Scheduling.StoryPoints": h["sp"],
            "Microsoft.VSTS.Common.Priority": PRIORIDAD[h["prioridad"]],
            "System.AssignedTo": RESPONSABLE,
            "System.Tags": "; ".join([h["codigo"], h["epica"], *rfs]),
        }
        padre = epicas[h["epica"]]
        w = existentes.get(h["codigo"])
        if w:
            ops = campos_ops(campos, w["fields"])
            padre_actual = next((r for r in w.get("relations") or [] if r["rel"] == "System.LinkTypes.Hierarchy-Reverse"), None)
            if padre_actual and not tiene_rel(w, "System.LinkTypes.Hierarchy-Reverse", padre):
                idx = w["relations"].index(padre_actual)
                ops.append({"op": "remove", "path": f"/relations/{idx}"})
                padre_actual = None
            if not padre_actual:
                ops.append({"op": "add", "path": "/relations/-",
                            "value": {"rel": "System.LinkTypes.Hierarchy-Reverse", "url": url_wi(padre)}})
            if ops:
                w = actualizar(w["id"], ops)
            print(f"  = {h['codigo']} (#{w['id']}){' actualizada' if ops else ''}")
        else:
            ops = campos_ops(campos) + [{"op": "add", "path": "/relations/-",
                                         "value": {"rel": "System.LinkTypes.Hierarchy-Reverse", "url": url_wi(padre)}}]
            w = crear("User Story", ops)
            print(f"  + {h['codigo']} (#{w['id']})")
        historias[h["codigo"]] = w["id"]
    return historias


def asegurar_dependencias(historias):
    """Cada dependencia D de una HU X: D es Predecessor de X (X Successor de D).
    Sincroniza: agrega las que faltan y quita las que ya no están en backlog.json."""
    ids = ",".join(str(historias[h["codigo"]]) for h in BACKLOG["hus"])
    actuales = {w["id"]: w for w in api("GET", f"_apis/wit/workitems?ids={ids}&$expand=relations")["value"]}
    de_historia = set(historias.values())
    nuevos = quitados = 0
    for h in BACKLOG["hus"]:
        x = actuales[historias[h["codigo"]]]
        esperados = {historias[d] for d in h["dependencias"]}
        ops = [{"op": "remove", "path": f"/relations/{i}"}
               for i, r in reversed(list(enumerate(x.get("relations") or [])))
               if r["rel"] == "System.LinkTypes.Dependency-Reverse"
               and int(r["url"].rstrip("/").split("/")[-1]) in de_historia - esperados]
        quitados += len(ops)
        ops += [{"op": "add", "path": "/relations/-",
                 "value": {"rel": "System.LinkTypes.Dependency-Reverse", "url": url_wi(d)}}
                for d in sorted(esperados) if not tiene_rel(x, "System.LinkTypes.Dependency-Reverse", d)]
        if ops:
            actualizar(x["id"], ops)
            nuevos += sum(1 for o in ops if o["op"] == "add")
    print(f"  Enlaces de dependencia nuevos: {nuevos} · quitados: {quitados}")


# ---------------------------------------------------------------- test plan
def asegurar_plan(historias):
    yo = api("GET", "_apis/connectionData", proyecto=False, version="7.1-preview")["authenticatedUser"]
    planes = api("GET", "_apis/testplan/plans")["value"]
    plan = next((p for p in planes if p["name"] == PLAN), None)
    if plan:
        plan = api("GET", f"_apis/testplan/plans/{plan['id']}")
        print(f"  = Test plan #{plan['id']}")
    else:
        plan = api("POST", "_apis/testplan/plans", {
            "name": PLAN, "areaPath": PROYECTO, "iteration": PROYECTO,
            "owner": {"id": yo["id"]},
            "description": "Plan de pruebas del Acelerador de Consolidación BPC. Un suite basado en requerimientos "
                           "por historia de usuario; los casos de prueba los genera la fase F7.",
        })
        print(f"  + Test plan #{plan['id']}")
    if (plan.get("owner") or {}).get("id") != yo["id"]:
        api("PATCH", f"_apis/testplan/plans/{plan['id']}", {"owner": {"id": yo["id"]}})
    raiz = plan["rootSuite"]["id"]
    suites = api("GET", f"_apis/testplan/Plans/{plan['id']}/suites")["value"]
    con_req = {s.get("requirementId") for s in suites if s.get("suiteType") == "requirementTestSuite"}
    nuevos = 0
    for h in BACKLOG["hus"]:
        wid = historias[h["codigo"]]
        if wid in con_req:
            continue
        api("POST", f"_apis/testplan/Plans/{plan['id']}/suites", {
            "suiteType": "requirementTestSuite", "requirementId": wid, "parentSuite": {"id": raiz}})
        nuevos += 1
    print(f"  Suites nuevos: {nuevos}")
    return plan["id"]


def main():
    asegurar_proyecto()
    print("Épicas:")
    epicas = asegurar_epicas()
    print("Historias:")
    historias = asegurar_historias(epicas)
    print("Dependencias:")
    asegurar_dependencias(historias)
    print("Plan de pruebas:")
    plan_id = asegurar_plan(historias)
    print(f"\nListo. Proyecto: {ORG}/{urllib.parse.quote(PROYECTO)}  ·  Test plan #{plan_id}")


if __name__ == "__main__":
    main()
