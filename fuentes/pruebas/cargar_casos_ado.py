#!/usr/bin/env python3
"""Carga los casos de prueba del Plan de Pruebas (F7) del Acelerador BPC en Azure DevOps.

Crea cada caso CP-XXX de plan_pruebas.json como work item Test Case (título, pasos con su
resultado esperado, prioridad, asignado al responsable) y lo agrega al suite basado en
requerimientos de su historia dentro del Test Plan. El proyecto, el plan, las historias y
los suites los crea fuentes/ado/cargar_ado.py: si faltan, este script se detiene sin crearlos.

Idempotente: los casos se buscan por el código CP-XXX al inicio del título; un caso que ya
existe se actualiza solo si cambió, y no se agrega dos veces al mismo suite.

Autenticación: token de Azure CLI (`az login` con el usuario del responsable).
Uso:  python3 fuentes/pruebas/build_pruebas.py && python3 fuentes/pruebas/cargar_casos_ado.py [--solo CP-XXX]
"""
import json
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
from html import escape, unescape
from pathlib import Path
from xml.sax.saxutils import escape as xml_escape

ORG = "https://dev.azure.com/cmconsultingsas"
PROYECTO = "Acelerador Consolidacion BPC"
RESPONSABLE = "luis.maury@cmconsulting.com.co"
PLAN = "Acelerador BPC — Plan de pruebas"
API = "7.1"
ADO_RESOURCE = "499b84ac-1321-427f-aa17-267ca6975798"

D = Path(__file__).resolve().parent
CASOS = json.loads((D / "plan_pruebas.json").read_text(encoding="utf-8"))["casos"]
if len(sys.argv) == 3 and sys.argv[1] == "--solo":  # prueba con un caso: --solo CP-001
    CASOS = [c for c in CASOS if c["codigo"] == sys.argv[2]]
PRIORIDAD = {"Alta": 1, "Media": 2, "Baja": 3}
RE_CP = re.compile(r"^(CP-\d{3})\b")

_token = None


def token():
    global _token
    if _token is None:
        _token = subprocess.run(
            ["az", "account", "get-access-token", "--resource", ADO_RESOURCE, "--query", "accessToken", "-o", "tsv"],
            check=True, capture_output=True, text=True).stdout.strip()
    return _token


def api(method, path, body=None, content_type="application/json", ok404=False):
    sep = "&" if "?" in path else "?"
    url = f"{ORG}/{urllib.parse.quote(PROYECTO)}/{path}{sep}api-version={API}"
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
    items = []
    for i in range(0, len(ids), 200):
        lote = ",".join(map(str, ids[i:i + 200]))
        items += api("GET", f"_apis/wit/workitems?ids={lote}")["value"]
    return items


def paginar(path):
    """GET con continuationToken (APIs de testplan)."""
    out, cont = [], None
    while True:
        p = path + (f"{'&' if '?' in path else '?'}continuationToken={urllib.parse.quote(cont)}" if cont else "")
        sep = "&" if "?" in p else "?"
        url = f"{ORG}/{urllib.parse.quote(PROYECTO)}/{p}{sep}api-version={API}"
        req = urllib.request.Request(url, headers={"Authorization": f"Bearer {token()}", "Accept": "application/json"})
        with urllib.request.urlopen(req) as r:
            out += json.loads(r.read().decode())["value"]
            cont = r.headers.get("x-ms-continuationtoken")
        if not cont:
            return out


# ---------------------------------------------------------------- contenido del work item
def _html(texto):
    return xml_escape(f"<DIV><P>{escape(texto)}</P></DIV>")


def pasos_xml(c):
    pasos = c["pasos"]
    partes = [f'<steps id="0" last="{len(pasos)}">']
    for i, p in enumerate(pasos, 1):
        esperado = c["resultado"] if i == len(pasos) else ""
        tipo = "ValidateStep" if esperado else "ActionStep"
        partes.append(f'<step id="{i}" type="{tipo}"><parameterizedString isformatted="true">{_html(p)}</parameterizedString>'
                      f'<parameterizedString isformatted="true">{_html(esperado) if esperado else ""}</parameterizedString>'
                      f'<description/></step>')
    partes.append("</steps>")
    return "".join(partes)


def _texto(html):
    """Texto plano sin etiquetas ni diferencias de espacios (ADO normaliza el HTML al guardar)."""
    return " ".join(unescape(re.sub(r"<[^>]+>", " ", html or "")).split())


def pasos_de(xml):
    """Lista (acción, esperado) de un campo Steps, para comparar sin depender del formato."""
    if not xml:
        return []
    out = []
    for st in ET.fromstring(xml).iter("step"):
        ps = [_texto(p.text) for p in st.findall("parameterizedString")]
        out.append((ps[0] if ps else "", ps[1] if len(ps) > 1 else ""))
    return out


def descripcion(c):
    filas = [("Origen", f"{c['origen']} · {c['categoria']}"), ("Precondiciones", c["precondiciones"]),
             ("Ejecución", c["tipo"] + (f" — {c['justificacion_manual']}" if c["tipo"] == "Manual" else f" — {c['herramienta']}"))]
    if c["smoke"]:
        filas.append(("Suite de humo", "Sí: corre en cada despliegue a QA y UAT."))
    return "".join(f"<p><b>{k}:</b> {escape(v)}</p>" for k, v in filas)


def tags(c):
    t = [c["codigo"], c["hu"], c["epica"], c["categoria"], c["tipo"]]
    if c["smoke"]:
        t.append("Humo")
    return "; ".join(t)


def campos(c):
    return {
        "System.Title": f"{c['codigo']} — {c['titulo']}",
        "System.Description": descripcion(c),
        "Microsoft.VSTS.TCM.Steps": pasos_xml(c),
        "Microsoft.VSTS.Common.Priority": PRIORIDAD[c["prioridad"]],
        "System.AssignedTo": RESPONSABLE,
        "System.Tags": tags(c),
    }


def cambios(deseado, actual):
    ops = []
    for ref, valor in deseado.items():
        a = actual.get(ref)
        if ref == "System.AssignedTo":
            igual = isinstance(a, dict) and a.get("uniqueName", "").lower() == valor.lower()
        elif ref == "System.Tags":
            igual = sorted(x.strip() for x in (a or "").split(";")) == sorted(x.strip() for x in valor.split(";"))
        elif ref == "Microsoft.VSTS.TCM.Steps":
            igual = pasos_de(a) == pasos_de(valor)
        elif ref == "System.Description":
            igual = _texto(a) == _texto(valor)
        else:
            igual = a == valor
        if not igual:
            ops.append({"op": "add", "path": f"/fields/{ref}", "value": valor})
    return ops


# ---------------------------------------------------------------- carga
def prerequisitos():
    plan = next((p for p in api("GET", "_apis/testplan/plans")["value"] if p["name"] == PLAN), None)
    if not plan:
        sys.exit(f"No existe el Test Plan «{PLAN}». Lo crea fuentes/ado/cargar_ado.py; ejecútalo primero.")
    historias = {}
    # En WIQL, CONTAINS sobre Tags compara etiquetas completas: el filtro HU-XX se hace aquí.
    for w in wiql("SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project "
                  "AND [System.WorkItemType] = 'User Story'"):
        for t in (w["fields"].get("System.Tags") or "").split(";"):
            if re.fullmatch(r"HU-\d{2}", t.strip()):
                historias[t.strip()] = w["id"]
    suites = {s["requirementId"]: s["id"] for s in paginar(f"_apis/testplan/Plans/{plan['id']}/suites")
              if s.get("suiteType") == "requirementTestSuite"}
    faltan = sorted({c["hu"] for c in CASOS if c["hu"] not in historias or historias[c["hu"]] not in suites})
    if faltan:
        sys.exit(f"Faltan historias o suites para: {', '.join(faltan)}. Ejecuta primero fuentes/ado/cargar_ado.py.")
    return plan["id"], historias, suites


def main():
    plan_id, historias, suites = prerequisitos()
    print(f"Test plan #{plan_id} · {len(suites)} suites de requerimientos")

    existentes = {}
    for w in wiql("SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project "
                  "AND [System.WorkItemType] = 'Test Case' AND [System.Title] CONTAINS 'CP-'"):
        m = RE_CP.match(w["fields"]["System.Title"])
        if m:
            if m.group(1) in existentes:
                print(f"  ! {m.group(1)} está duplicado (#{existentes[m.group(1)]['id']} y #{w['id']}); se usa el primero")
                continue
            existentes[m.group(1)] = w

    ids, creados, actualizados, errores = {}, 0, 0, []
    for c in CASOS:
        deseado = campos(c)
        try:
            w = existentes.get(c["codigo"])
            if w:
                ops = cambios(deseado, w["fields"])
                if ops:
                    api("PATCH", f"_apis/wit/workitems/{w['id']}", ops, "application/json-patch+json")
                    actualizados += 1
            else:
                ops = [{"op": "add", "path": f"/fields/{k}", "value": v} for k, v in deseado.items()]
                w = api("POST", "_apis/wit/workitems/$Test%20Case", ops, "application/json-patch+json")
                creados += 1
            ids[c["codigo"]] = w["id"]
        except RuntimeError as e:
            errores.append((c["codigo"], str(e)))
            print(f"  ✗ {c['codigo']}: {e}")
    print(f"Test cases: {creados} creados, {actualizados} actualizados, {len(ids) - creados - actualizados} sin cambios")

    agregados = 0
    for hu in dict.fromkeys(c["hu"] for c in CASOS):
        suite = suites[historias[hu]]
        en_suite = {tc["workItem"]["id"] for tc in paginar(f"_apis/testplan/Plans/{plan_id}/Suites/{suite}/TestCase")}
        nuevos = [ids[c["codigo"]] for c in CASOS if c["hu"] == hu and c["codigo"] in ids and ids[c["codigo"]] not in en_suite]
        for i in range(0, len(nuevos), 50):
            try:
                api("POST", f"_apis/testplan/Plans/{plan_id}/Suites/{suite}/TestCase",
                    [{"workItem": {"id": x}} for x in nuevos[i:i + 50]])
                agregados += len(nuevos[i:i + 50])
            except RuntimeError as e:
                errores.append((hu, str(e)))
                print(f"  ✗ suite de {hu}: {e}")
    print(f"Casos agregados a suites: {agregados}")

    verificar(plan_id, historias, suites, ids)
    if errores:
        sys.exit(f"{len(errores)} errores; vuelve a ejecutar el script: es idempotente.")


def verificar(plan_id, historias, suites, ids):
    esperado = {}
    for c in CASOS:
        esperado.setdefault(c["hu"], set()).add(ids.get(c["codigo"]))
    malos, total = [], 0
    for hu, quiero in esperado.items():
        en_suite = {tc["workItem"]["id"] for tc in paginar(f"_apis/testplan/Plans/{plan_id}/Suites/{suites[historias[hu]]}/TestCase")}
        total += len(quiero & en_suite)
        if not quiero <= en_suite:
            malos.append(hu)
    asignados = 0
    lista = list(ids.values())
    for i in range(0, len(lista), 200):
        for w in api("GET", f"_apis/wit/workitems?ids={','.join(map(str, lista[i:i + 200]))}&fields=System.AssignedTo")["value"]:
            if (w["fields"].get("System.AssignedTo") or {}).get("uniqueName", "").lower() == RESPONSABLE:
                asignados += 1
    print(f"Verificación: {len(ids)}/{len(CASOS)} test cases · {total}/{len(CASOS)} en su suite · "
          f"{asignados}/{len(ids)} asignados a {RESPONSABLE}" + (f" · suites incompletos: {', '.join(malos)}" if malos else ""))


if __name__ == "__main__":
    main()
