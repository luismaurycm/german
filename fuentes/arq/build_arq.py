"""Genera arquitectura.json (validador), bloques_arq.json (skill base) y los ADR en Markdown del Acelerador BPC."""
import json
from pathlib import Path

D = Path(__file__).parent
ROOT = D.parent.parent
OUT = ROOT / "docs" / "05-arquitectura"
DIA = OUT / "diagramas"
DAF = json.loads((ROOT / "fuentes" / "daf" / "daf.json").read_text(encoding="utf-8"))
HU = json.loads((ROOT / "fuentes" / "hu" / "backlog.json").read_text(encoding="utf-8"))
IMG = lambda n, w: {"type": "image", "path": str(DIA / f"{n}.png"), "width_cm": w, "align": "center"}
TRM = 4100  # COP por USD, supuesto de la estimación

ESTADO = "APROBADO"  # pasa a "APROBADO" cuando los riesgos altos queden cerrados o aceptados

# ---------------- Componentes (C4 nivel 2) ----------------
COMP = [
    ("Configurador web", "Pantallas UX-01 a UX-10 del Consultor implementador; solo escucha en 127.0.0.1.",
     "ASP.NET Core 8 · Razor Pages", ["RF-01", "RF-05", "RF-06", "RF-07", "RF-15", "RNF-Comp-03", "RNF-Usa-01"]),
    ("Motor de generación", "Orquesta los pasos del plan y ejecuta escáner, generadores BW y BPC, cargador de datos maestros y generador de reportes.",
     ".NET 8 · monolito modular en el mismo proceso", ["RF-03", "RF-08", "RF-10", "RF-11", "RF-16", "RF-17", "RF-18", "RF-19", "RF-31", "RF-36"]),
    ("Catálogo de variantes", "Plantillas versionadas por versión de la arquitectura A: objetos BW (InfoCubo o aDSO), scripts de BPC modelo estándar y reportes EPM.",
     "Archivos JSON, ABAP y .xlsx empaquetados con la aplicación", ["RNF-Comp-01", "RNF-Comp-04", "RNF-Comp-05", "RN-02"]),
    ("Base local", "Clientes, conexiones (sin credencial), sociedades, mapeos, plan de generación y registro de ejecuciones.",
     "SQLite en el perfil del usuario", ["RF-01", "RF-06", "RF-07", "RNF-Aud-01"]),
    ("Almacén de credenciales", "Guarda las credenciales de los usuarios técnicos de SAP cifradas por usuario de Windows.",
     "Administrador de credenciales de Windows (DPAPI)", ["RNF-Seg-02"]),
    ("Paquete Z del acelerador", "Módulos de función RFC que inventarían y crean objetos en BW y BPC, y ejecutan UJBR, UJFS y UJKT.",
     "ABAP en el landscape del cliente, entregado por transporte", ["INT-02", "INT-03", "RNF-Seg-03"]),
    ("Generador de reportes EPM", "Produce los 34 Excel con fórmulas EPM a partir de plantillas, sin necesitar Excel instalado.",
     "Open XML SDK (.NET)", ["RF-20", "RF-27", "RF-28", "RF-29", "RF-34", "INT-06"]),
]
OBJETOS = [
    ("Bodega financiera, campos Z, activos fijos y deterioros", "SAP BW", "RF-08, RF-09, RF-24, RF-25, INT-01"),
    ("Cadenas de proceso full y delta nocturna", "SAP BW", "RF-10, RN-04, RNF-Rend-01"),
    ("Datos maestros (cuentas, CeCo, CeBe, monedas, tasas, jerarquía, no SAP)", "SAP BW", "RF-11 a RF-15, INT-04"),
    ("Modelos de consolidación, propiedad y planeación", "SAP BPC", "RF-16, RF-23, RF-31, RF-39"),
    ("Paquetes de importación con validación de cuadre", "SAP BPC", "RF-17, RF-36, INT-05"),
    ("Scripts de consolidación, conversión, eliminación de inversión y minoritario", "SAP BPC", "RF-18, RF-19, RF-26, RF-32, RF-33"),
    ("Asientos, bloqueo por sociedad (work status) y flujo de cierre (BPF)", "SAP BPC", "RF-35, RF-37, RF-38"),
]

# ---------------- ADRs ----------------
# código, título, contexto, decisión, alternativa simple, razón de descarte, consecuencias, rnf, costo relativo
ADRS = [
 ("ADR-01", "Monolito modular en un solo proceso",
  "El acelerador lo usa un Consultor implementador a la vez, sobre un cliente a la vez; el equipo de construcción es de 4 a 5 personas.",
  "Una sola aplicación .NET 8 con módulos separados por interfaz: configurador, orquestador, escáner, generadores BW, generadores BPC, cargador de datos maestros, reportes y registro.",
  "Es la opción simple.", "—",
  "Despliegue de un solo ejecutable. Los generadores se prueban de forma aislada contra la interfaz ISapGateway, sin SAP real (Regla 6).",
  ["RNF-Comp-03"], "Bajo"),
 ("ADR-02", "Aplicación web local en el equipo del consultor",
  "La pregunta 4 del DAF pide definir si el configurador corre en el equipo del consultor o en un servidor de CM. RNF-Comp-03 exige cero servidores adicionales para el prototipo.",
  "El configurador corre en el equipo del Consultor implementador como ejecutable autocontenido que abre el navegador en http://127.0.0.1. No hay servidor de CM. Cierra la pregunta 4 del DAF para la versión 1.",
  "Es la opción simple.", "—",
  "Sin costo de hosting ni identidad central. La configuración de cada cliente vive en el equipo del consultor: para compartirla se exporta e importa (decisión diferida D-01).",
  ["RNF-Comp-03"], "Bajo"),
 ("ADR-03", ".NET 8 con SAP .NET Connector 3.1",
  "El acelerador necesita RFC hacia BW y BPC (INT-02, INT-03) y generar archivos Excel.",
  ".NET 8 para todo el producto, con SAP NCo 3.1 para RFC y Open XML SDK para los Excel. Un solo runtime más ABAP en el cliente.",
  "Python con PyRFC.",
  "SAP descontinuó PyRFC y no publica parches de seguridad; una dependencia sin soporte incumple RNF-Seg-01 (ASVS nivel 2, componentes con soporte vigente).",
  "Requiere aceptar la licencia de SAP NCo y distribuirlo con el instalador. La compatibilidad de NCo 3.1 con .NET 8 se verifica en HU-01.",
  ["RNF-Seg-01", "RNF-Comp-01"], "Bajo"),
 ("ADR-04", "SQLite local y credenciales en el almacén de Windows",
  "Se guardan configuración por cliente y el registro de ejecuciones con retención de 12 meses (RNF-Aud-01); las credenciales no pueden quedar legibles (RNF-Seg-02).",
  "SQLite en el perfil del usuario para configuración y registro. Las credenciales se guardan en el Administrador de credenciales de Windows (DPAPI) y la base solo guarda una referencia. El registro conserva 12 meses y se puede exportar.",
  "Es la opción simple.", "—",
  "Cero servicios de base de datos. El cifrado en reposo del equipo lo da BitLocker, obligatorio en los equipos de CM.",
  ["RNF-Seg-02", "RNF-Aud-01"], "Bajo"),
 ("ADR-05", "Identidad: sesión de Windows y usuario técnico mínimo",
  "Hay un único usuario local por instalación. En SAP, el acelerador actúa con el usuario técnico que crea el Administrador SAP del cliente.",
  "No hay pantalla de inicio de sesión: la aplicación escucha solo en 127.0.0.1 y exige un token aleatorio por sesión, generado al iniciar, en una cookie HttpOnly. En SAP se usa un rol Z por paso, sin SAP_ALL, y SNC si el cliente lo exige.",
  "Es la opción simple.", "—",
  "Si el configurador pasa a un servidor (D-01), se agrega Microsoft Entra ID. El rol técnico se documenta en el paquete Z.",
  ["RNF-Seg-01", "RNF-Seg-03"], "Bajo"),
 ("ADR-06", "Inyección por RFC con paquete Z, transportes y UJBR",
  "El DAF fija RFC con paquete Z como mecanismo (INT-02, INT-03). Se debe inventariar en línea, crear objetos paso a paso y poder reintentar.",
  "El Administrador SAP importa una vez por transporte el paquete Z con módulos de función RFC. El acelerador los llama para inventariar, crear y activar objetos. Antes de cambiar BPC se toma un respaldo UJBR.",
  "Generar archivos y transportes para que el Administrador SAP los importe a mano.",
  "Sin lectura en línea no hay inventario (RF-03) ni reintento por paso con registro (RF-08, RNF-Aud-01).",
  "Depende de que el cliente permita código Z (supuesto del DAF) y de la prueba técnica HU-01 (riesgo R-02).",
  ["RNF-Seg-03", "RNF-Aud-01", "RNF-Comp-01"], "Medio"),
 ("ADR-07", "Catálogo de variantes por arquitectura y versión",
  "La versión 1 soporta la arquitectura A con BW 7.5 o BW/4HANA y BPC modelo estándar; la B queda fuera (RN-19). Agregar una versión no debe tocar las existentes (RNF-Comp-04).",
  "Cada generador define una interfaz y una implementación por variante (BW 7.5 con InfoCubo, BW/4HANA con aDSO, BPC 10.1 y BPC 2021 modelo estándar). La variante se elige según la detección de RF-04. Las plantillas de cada variante viven en el catálogo versionado. Una variante de BPC embebido se agrega después sin tocar estas (D-04).",
  "Un solo generador con condicionales por versión.",
  "Cada versión nueva obligaría a modificar el código de las existentes, contra RNF-Comp-04 (0 cambios en variantes existentes).",
  "Más archivos por variante, pero cada una se prueba sola. Agregar BW/4HANA (RNF-Comp-05) es una variante más.",
  ["RNF-Comp-01", "RNF-Comp-04", "RNF-Comp-05"], "Bajo"),
 ("ADR-08", "Reportes EPM generados desde plantillas Open XML",
  "Se entregan 34 reportes y plantillas que el cliente conserva (RF-20, RNF-Comp-02).",
  "Plantillas .xlsx con fórmulas EPM y conexión con nombre; el generador reemplaza parámetros (modelo, sociedades, rubros) con Open XML SDK. Los reportes de activos fijos y deterioros usan una conexión local de EPM add-in a consultas BW (cierra la pregunta 12 del DAF, sujeto a HU-01).",
  "Es la opción simple.", "—",
  "No requiere Excel en el equipo de generación. Si la conexión local a BW no funciona, se abre control de cambios sobre RN-12 (riesgo R-03).",
  ["RNF-Comp-02"], "Bajo"),
 ("ADR-09", "Control del cierre con funciones estándar de BPC",
  "RF-31 a RF-38 piden propiedad, eliminación de inversión, asientos, validación de cuadre, bloqueo y flujo de cierre, con registro de cambios (RNF-Aud-02).",
  "Se configuran las funciones estándar de BPC: modelo de propiedad, reglas de negocio de eliminación y conversión, journals, work status por sociedad, controles y BPF. Solo la validación de cuadre previa a la importación usa script logic o un BAdI del paquete Z. La auditoría usa la auditoría estándar de BPC.",
  "Es la opción simple.", "—",
  "Todas son funciones de BPC modelo estándar, disponibles en BPC 10.1 y 2021. Su disponibilidad en BPC embebido se evalúa con la arquitectura B (D-04).",
  ["RNF-Aud-02", "RNF-Comp-01"], "Bajo"),
 ("ADR-10", "Cargas delta con cadenas de proceso nativas de BW",
  "Los datos del día anterior deben estar en los reportes (RNF-Rend-01: una ejecución cada 24 horas).",
  "Cadenas de proceso de BW programadas cada noche, sin borrado de peticiones solapadas; la importación a BPC encadenada al final de la delta.",
  "Es la opción simple.", "—",
  "La duración de la ventana se confirma con el volumen real (pregunta 7 del DAF).",
  ["RNF-Rend-01"], "Bajo"),
 ("ADR-11", "Distribución firmada desde Azure DevOps",
  "El ejecutable se instala en equipos de consultores y escribe en sistemas de clientes; debe pasar SAST, SCA y escaneo de secretos (RNF-Seg-01).",
  "Pipeline de Azure DevOps con build, pruebas unitarias, SAST, SCA y escaneo de secretos en cada PR. El .exe autocontenido se firma con certificado de firma de código y se publica en Azure Artifacts junto al transporte del paquete Z.",
  "Es la opción simple.", "—",
  "Costo del certificado de firma de código (sección 15).",
  ["RNF-Seg-01"], "Bajo"),
]

MATRIZ = {
    "RNF-Comp-01": ["ADR-06", "ADR-07", "ADR-09"],
    "RNF-Comp-02": ["ADR-08"],
    "RNF-Comp-03": ["ADR-01", "ADR-02"],
    "RNF-Comp-04": ["ADR-07"],
    "RNF-Comp-05": ["ADR-07"],
    "RNF-Rend-01": ["ADR-10"],
    "RNF-Seg-01": ["ADR-03", "ADR-05", "ADR-11"],
    "RNF-Seg-02": ["ADR-04", "Almacén de credenciales"],
    "RNF-Seg-03": ["ADR-05", "ADR-06"],
    "RNF-Aud-01": ["ADR-04", "Base local"],
    "RNF-Aud-02": ["ADR-09"],
    "RNF-Usa-01": ["Configurador web"],
}

SEGURIDAD = [
    ("IAM", "Sin login local: la aplicación escucha solo en 127.0.0.1 con un token por sesión en una cookie HttpOnly y SameSite=Strict. En SAP, un usuario técnico por cliente con rol Z mínimo por paso, sin SAP_ALL; SNC si el cliente lo exige. En BPC los usuarios financieros usan los roles del cliente.", "RNF-Seg-01, RNF-Seg-03"),
    ("Cifrado", "RFC con SNC cuando el cliente lo tenga habilitado; BitLocker obligatorio en el equipo del consultor; la credencial se cifra con DPAPI.", "RNF-Seg-01, RNF-Seg-02"),
    ("Secretos", "Credenciales de SAP en el Administrador de credenciales de Windows; 0 secretos en SQLite, archivos o registros. El certificado de firma vive en la biblioteca segura de Azure DevOps.", "RNF-Seg-02"),
    ("Auditoría", "Registro de cada paso en SQLite (usuario, fecha, sistema, paso, objetos, resultado) con 12 meses de retención y exportación. En el cliente, la auditoría estándar de BPC cubre participaciones, asientos y bloqueos.", "RNF-Aud-01, RNF-Aud-02"),
    ("Hardening", "La aplicación escucha solo en loopback, sin puertos expuestos. El paquete Z valida las entradas y solo crea objetos en su namespace.", "RNF-Seg-01"),
    ("Vulnerabilidades", "SAST, SCA y escaneo de secretos en cada PR; verificación ASVS nivel 2 antes de cada versión; CVE críticos corregidos en menos de 7 días.", "RNF-Seg-01"),
    ("Backup", "Antes de cambiar BPC, respaldo UJBR del ambiente (ADR-06). La base local se exporta por cliente. El backup de los sistemas SAP es responsabilidad del cliente.", "RNF-Aud-01"),
    ("Cumplimiento", "Ley 1581/2012 para datos de terceros: el acelerador no copia terceros a la base local, solo los mueve dentro del SAP del cliente. Los archivos no SAP se borran del equipo después de cargarlos.", "RNF-Seg-01"),
]

AMBIENTES = [
    ("DEV", "Equipo del desarrollador y sandbox SAP de arquitectura A (BW 7.5, BPC 10.1)", "Desarrollo y prueba técnica HU-01"),
    ("QA", "BW/4HANA con BPC 2021 modelo estándar en SAP CAL sobre Azure, encendido bajo demanda", "Certificación de la variante BW/4HANA (HU-58)"),
    ("UAT", "Sistema de calidad del primer cliente piloto", "Validación con datos reales"),
    ("PROD", "Equipos de los consultores (.exe firmado) y sistema productivo del cliente vía sus transportes", "Uso en implementaciones"),
]
COSTOS = [  # ambiente, recurso, sku, usd/mes
    ("DEV", "Sandbox SAP arquitectura A", "Ambiente existente del consultor SAP BPC", 0),
    ("DEV", "Azure DevOps (repositorio, pipelines, tableros)", "Basic, primeros 5 usuarios sin costo", 0),
    ("QA", "VM para BW/4HANA + BPC 2021", "Azure E32ds v5 · 60 h/mes con apagado", 138),
    ("QA", "Disco del ambiente BW/4HANA", "Premium SSD P30 1 TiB", 135),
    ("QA", "Azure Test Plans (1 usuario)", "Basic + Test Plans, si no hay Visual Studio Enterprise", 52),
    ("UAT", "Sistema de calidad del cliente piloto", "Lo aporta el cliente", 0),
    ("PROD", "Certificado de firma de código", "OV, USD 400/año", 33),
    ("PROD", "Azure Artifacts", "2 GiB sin costo", 0),
]

RIESGOS = [
    ("R-01", "Arquitectura B: SAP orienta la consolidación sobre S/4HANA a Group Reporting, y EPM add-in no es la herramienta de reportes del BPC embebido (preguntas 13 y 17 del DAF). La consolidación y los reportes EPM podrían no aplicar a B tal como está definida.", "Media", "Alta",
     "Cerrado por decisión de CM Consulting del 16-Oct-26: la arquitectura B queda fuera de la versión 1 (DAF v1.2, RN-19). HU-57 la detecta y la rechaza; se reevalúa en D-04.", "cerrado", "Alta"),
    ("R-02", "El mecanismo de inyección (RFC, paquete Z, UJBR) no funciona con los permisos que da el cliente, o NCo 3.1 no es compatible con .NET 8.", "Media", "Alta",
     "Aceptado por el líder técnico de CM Consulting el 16-Oct-26 hasta el resultado de HU-01, que va en el primer sprint con duración limitada. Si falla, se cambia de mecanismo antes de E3; la alternativa documentada es entregar archivos y transportes (ADR-06).", "aceptado", "Alta"),
    ("R-03", "EPM add-in no lee BW por conexión local para los reportes de activos fijos y deterioros.", "Media", "Media",
     "Se verifica en HU-01. Si falla, control de cambios sobre RN-12 para resumir en un modelo BPC de reporte.", "mitigado", "Media"),
    ("R-04", "Datos confidenciales del cliente (mapeos, archivos no SAP) en el equipo del consultor.", "Media", "Media",
     "BitLocker obligatorio, credenciales en DPAPI, borrado de archivos no SAP después de cargarlos y base local en el perfil del usuario.", "mitigado", "Media"),
    ("R-05", "La ventana nocturna no alcanza para la delta y la importación con el volumen real.", "Baja", "Media",
     "Medir en UAT; ADR-10 sin borrados solapados. Volumen por confirmar en la pregunta 7 del DAF.", "mitigado", "Media"),
    ("R-06", "Fin del mantenimiento de BW 7.5 (2027, extendido a 2030).", "Alta", "Media",
     "Variante BW/4HANA desde la versión 1 (RNF-Comp-05, HU-16, HU-58).", "mitigado", "Media"),
    ("R-07", "No hay ambiente de BW/4HANA para certificar la variante.", "Media", "Media",
     "Ambiente SAP CAL bajo demanda (sección 14); HU-58 queda al final del backlog.", "mitigado", "Media"),
]
DIFERIDAS = [
    ("D-01 Configurador en un servidor de CM con Microsoft Entra ID", "Más de un consultor necesita trabajar el mismo cliente o compartir configuraciones con frecuencia mensual o mayor", "Base local pasa a Azure SQL; se agrega Entra ID; el motor sigue igual"),
    ("D-02 Ejecución desatendida de generaciones", "Un cliente exige generar desde su red sin el equipo del consultor conectado", "El motor se empaqueta como servicio dentro de la red del cliente"),
    ("D-04 Arquitectura B (ERP, BW y BPC integrados, BPC embebido)", "Un cliente objetivo con B y confirmación en la matriz PAM de SAP de consolidación y reportes sobre esa versión", "Se agregan variantes de generadores BPC embebido al catálogo (ADR-07) y se decide la herramienta de reportes"),
    ("D-03 Nuevas variantes (p. ej. BW/4HANA 2025, Datasphere)", "Un cliente objetivo con esa versión", "Se agrega una variante al catálogo (ADR-07) sin tocar las existentes"),
]
EPICA_CAP = [("E1 Conexión y diagnóstico", "5, 8, 10, 14 (ADR-02, 03, 05, 06)"), ("E2 Configuración de alcance", "5, 7"), ("E3 Bodega de datos en BW", "6, 8, 14 (ADR-07, 10)"),
             ("E4 Datos maestros", "6, 8"), ("E5 Modelo BPC y consolidación", "6, 8, 14 (ADR-07, 09)"), ("E6 Control del cierre", "8.4, 14 (ADR-09)"),
             ("E7 Reportes EPM", "5, 14 (ADR-08)"), ("E8 Presupuesto, activos fijos y deterioros", "14 (ADR-08, 09)"), ("E9 Seguridad, auditoría y compatibilidad", "10, 11, 14 (ADR-04, 05, 11)")]

# ---------------- JSON del validador ----------------
arq = {
    "componentes": [{"nombre": n, "tecnologia": t, "justificacion": j} for n, _, t, j in COMP],
    "adrs": [{"codigo": c, "titulo": t, "decision": d, "alternativa_simple": alt, "razon_descarte": raz, "rnf": rnf, "costo_mensual_cop": 0}
             for c, t, _, d, alt, raz, _, rnf, _ in ADRS],
    "rnf_daf": [r["codigo"] for r in DAF["rnf"]],
    "int_daf": [i["codigo"] for i in DAF["int"]],
    "integraciones_detalladas": [i["codigo"] for i in DAF["int"]],
    "matriz_rnf": MATRIZ,
    "dominios_seguridad": [d for d, *_ in SEGURIDAD],
    "nivel_asvs": DAF["nivel_asvs"],
    "ambientes": [a for a, *_ in AMBIENTES],
    "equipo_personas": 4,
    "tecnologias_runtime": [".NET 8", "ABAP"],
    "banderas": {"microservicios": False, "kubernetes": False, "multi_region": False, "mensajeria": False},
    "justificacion_banderas": {},
    "riesgos": [{"riesgo": r[1], "criticidad": r[6], "estado": r[5]} for r in RIESGOS],
    "migracion_datos": "no aplica",
    "costos_incluidos": True,
    "integraciones_escritura": ["INT-02", "INT-03", "INT-05"],
    "diagramas_secuencia": ["INT-02", "INT-03", "INT-05"],
    "entidad_central_estados": 8, "maquina_estados": True, "apis_terceros": 0,
}
(D / "arquitectura.json").write_text(json.dumps(arq, ensure_ascii=False, indent=2), encoding="utf-8")

# ---------------- Documento ----------------
B = []
H = lambda l, t: B.append({"type": "heading", "level": l, "text": t})
P = lambda t: B.append({"type": "paragraph", "text": t})
UL = lambda it: B.append({"type": "bullets", "items": it})
T = lambda rows, w: B.append({"type": "table", "rows": rows, "col_widths": w})
AZ = "144E8C"
cop = lambda usd: f"{usd * TRM:,.0f}".replace(",", ".")

B += [{"type": "portada", "text": "DOCUMENTO DE ARQUITECTURA — ACELERADOR DE CONSOLIDACIÓN FINANCIERA SAP BW/BPC"},
      {"type": "paragraph", "text": "Producto interno CM Consulting", "align": "center", "bold": True, "color": AZ, "size": 28},
      {"type": "page_break"}]
H(1, "Control de versiones")
T([["Versión", "Elaborado por", "Fecha", "Observaciones"],
   ["0.9", "CM Consulting", "16-Oct-26", "Borrador para revisión: riesgos R-01 y R-02 abiertos."]] +
  ([["1.0", "CM Consulting", "16-Oct-26", "Aprobada internamente; gate F6 cumplido. Arquitectura B fuera de la versión 1 (R-01 cerrado); R-02 aceptado hasta el resultado de HU-01."]] if ESTADO == "APROBADO" else []),
  [1200, 2200, 1500, 3938])
H(1, "Confidencialidad")
P([{"text": "La información contenida en este documento es propiedad intelectual de "}, {"text": "CM CONSULTING SAS", "bold": True}, {"text": ". Su reproducción, distribución o comunicación por cualquier medio queda expresamente prohibida sin el consentimiento escrito de "}, {"text": "CM CONSULTING SAS", "bold": True}, {"text": "."}])
B += [{"type": "page_break"}, {"type": "toc"}, {"type": "page_break"}]

H(1, "1. Introducción")
P("Este documento define cómo se construye el Acelerador de consolidación financiera sobre SAP BW y SAP BPC: componentes, decisiones de arquitectura, seguridad, ambientes, costos y riesgos. Lo usan el equipo de construcción, el plan de pruebas (F7) y la revisión técnica de CM Consulting.")
T([["Documento de referencia", "Versión"], ["Documento de Análisis Funcional (DAF)", "1.2 · 16-Oct-26"], ["Documento de Diseño UX", "1.1 · 16-Oct-26"], ["Documento de Historias de Usuario", "1.1 · 16-Oct-26"]], [5000, 3838])
H(2, "1.1 Siglas")
T([["Sigla", "Significado"], ["BPC", "SAP Business Planning and Consolidation"], ["BPF", "Business Process Flow de BPC"], ["CAL", "SAP Cloud Appliance Library"], ["DPAPI", "API de protección de datos de Windows"],
   ["EPM", "Complemento de Excel para BPC"], ["NCo", "SAP .NET Connector"], ["RFC", "Remote Function Call de SAP"], ["SNC", "Secure Network Communications de SAP"],
   ["UJBR", "Respaldo y restauración de ambientes BPC"], ["UJFS / UJKT", "Sistema de archivos y prueba de scripts de BPC"], ["aDSO", "Advanced DataStore Object de BW/4HANA"]], [1800, 7038])

H(1, "2. Resumen ejecutivo de arquitectura")
P("El acelerador es una aplicación .NET 8 que corre en el equipo del Consultor implementador y se abre en el navegador (127.0.0.1). No hay servidores de CM. Se conecta por RFC a un paquete Z que el cliente importa por transporte, inventaría lo que existe y genera en BW y BPC los objetos que faltan, según la variante de su arquitectura y versión. Al final produce los reportes EPM como archivos Excel.")
UL(["Monolito modular local (ADR-01, ADR-02): un ejecutable firmado, sin hosting.",
    "Catálogo de variantes (ADR-07): BW 7.5 o BW/4HANA con BPC modelo estándar; agregar versiones no toca las existentes. La arquitectura B queda fuera de la versión 1.",
    "Funciones estándar de BPC para el control del cierre (ADR-09): propiedad, journals, work status, controles y BPF.",
    f"Costo de operación: unos COP {cop(sum(c[3] for c in COSTOS if c[0] == 'PROD'))} al mes en PROD (certificado de firma) y unos COP {cop(sum(c[3] for c in COSTOS if c[0] != 'PROD'))} al mes en ambientes de certificación encendidos bajo demanda. Referencial."])

H(1, "3. Contexto de negocio")
P("Hoy cada implementación de consolidación repite a mano los mismos pasos en BW, BPC y Excel. El acelerador los automatiza: el Consultor implementador conecta, revisa lo existente, define el alcance y genera; el Usuario financiero recibe un modelo de consolidación con control del cierre y sus reportes EPM (DAF, sección 4.2).")
P("Principio: arquitectura mínima suficiente. Cada componente traza a un RF, RNF o INT del DAF; si la opción simple cumple los RNF, gana la simple.")

H(1, "4. Vista de contexto (C4 nivel 1)")
B.append(IMG("A1_contexto", 14))
T([["Actor o sistema", "Relación con el acelerador"],
   ["Consultor implementador", "Usa el configurador para conectar, configurar, generar y descargar reportes."],
   ["Administrador SAP del cliente", "Crea el usuario técnico e importa el transporte del paquete Z."],
   ["Usuario financiero", "Usa en Excel los reportes y plantillas que el acelerador entrega; no usa el configurador."],
   ["SAP ERP", "Origen contable; alimenta BW por extractores estándar (INT-01)."],
   ["SAP BW", "Recibe la bodega, las cadenas y los datos maestros (INT-02); alimenta BPC (INT-05)."],
   ["SAP BPC", "Recibe modelos, reglas y paquetes (INT-03); sirve los reportes EPM (INT-06)."]], [3000, 5838])

H(1, "5. Vista de contenedores (C4 nivel 2)")
B.append(IMG("A2_contenedores", 15))
T([["Contenedor", "Responsabilidad", "Tecnología", "Justificación"]] + [[n, r, t, ", ".join(j)] for n, r, t, j in COMP], [1800, 3438, 1900, 1700])
H(2, "5.1 Objetos que el acelerador genera en el cliente")
P("No son contenedores del acelerador: quedan en el landscape del cliente y él los opera.")
T([["Objeto", "Sistema", "Requerimiento"]] + [list(o) for o in OBJETOS], [4600, 1400, 2838])

H(1, "6. Vista de componentes del motor (C4 nivel 3)")
B.append(IMG("A3_componentes", 15))
T([["Módulo", "Responsabilidad", "HU"],
   ["Orquestador de pasos", "Ejecuta el plan en orden, registra cada paso y permite reintentar desde el paso fallido.", "HU-13, HU-14"],
   ["Conector SAP (ISapGateway)", "Única puerta a SAP: llamadas RFC al paquete Z. Se reemplaza por un simulador en las pruebas unitarias.", "HU-01, HU-03"],
   ["Escáner e inventario", "Detecta arquitectura y versión e inventaría objetos existentes.", "HU-06, HU-07"],
   ["Generadores BW", "Bodega, campos Z, cadenas, activos fijos y deterioros por variante.", "HU-15 a HU-20"],
   ["Cargador de datos maestros", "Planes de cuentas, CeCo, CeBe, monedas, tasas, jerarquía y no SAP.", "HU-21 a HU-25"],
   ["Generadores BPC", "Dimensiones, importación, scripts, propiedad, reglas y control del cierre por variante.", "HU-26 a HU-36, HU-39 a HU-41, HU-49"],
   ["Generador de reportes EPM", "Plantillas Excel con fórmulas EPM y parámetros del cliente.", "HU-42 a HU-48, HU-50 a HU-53"],
   ["Registro de ejecuciones", "Escribe y exporta el registro de RNF-Aud-01.", "HU-54"]], [2400, 4638, 1800])

H(1, "7. Modelo de datos")
P("Datos locales del acelerador (configuración y registro). Los datos financieros no se copian: quedan en el SAP del cliente.")
B.append(IMG("A4_datos", 14))
T([["Entidad", "Contenido", "Clasificación (DAF 9)", "Reglas"],
   ["Cliente y conexión", "Arquitectura, versiones, host, mandante, usuario técnico y referencia a la credencial", "Confidencial (la credencial vive en DPAPI)", "RN-01"],
   ["Sociedad y participación inicial", "Código, moneda local, matriz, en SAP, % propiedad, método", "Interna / Confidencial", "RN-05, RN-07, RN-13"],
   ["Cuenta, rubro y mapeo", "Plan de cuentas y asignación a rubros", "Interna", "RN-10"],
   ["Plan, paso y ejecución", "Objetos a crear, estado de cada paso y registro", "Interna", "RN-03, RNF-Aud-01"]], [2000, 3438, 2000, 1400])

H(1, "8. Vista de integración")
H(2, "8.1 Modelo de integración")
P("Todas las escrituras del acelerador pasan por el paquete Z vía RFC. Las integraciones entre sistemas SAP (INT-01, INT-05, INT-06) las ejecutan los objetos generados, no el acelerador.")
H(2, "8.2 Detalle por integración")
T([["INT", "Contrato", "Autenticación", "Mecanismo"],
   ["INT-01 ERP → BW", "Extractor 0FI_GL_14 en ECC; en S/4HANA, extractores sobre ACDOCA vía ODP; extractores de activos fijos y deterioros", "Conexión de origen BW existente del cliente", "Delta nocturna (ADR-10)"],
   ["INT-02 Acelerador ↔ BW", "Módulos de función Z_ACC_* (inventario, crear objeto, activar, ejecutar cadena), con definición en JSON y resultado con mensajes", "Usuario técnico con rol Z; SNC opcional", "RFC síncrono con SAP NCo 3.1"],
   ["INT-03 Acelerador ↔ BPC", "Módulos Z_ACC_BPC_* para dimensiones, miembros, scripts (UJFS/UJKT), paquetes y UJBR", "Usuario técnico con rol Z", "RFC síncrono con SAP NCo 3.1"],
   ["INT-04 No SAP → Acelerador", "Archivo por sociedad (formato por definir, pregunta 5 del DAF), validado en UX-07", "Archivo local", "Carga a BW por INT-02"],
   ["INT-05 BW → BPC", "Paquete de Data Manager con validación de cuadre previa (RN-16)", "Usuario de sistema de BPC", "Encadenado a la delta nocturna"],
   ["INT-06 BPC → Excel", "Conexión con nombre de EPM add-in al modelo; conexión local a consultas BW para A01 a D02", "Usuario del cliente en BPC", "EPM add-in"]], [1700, 3638, 1800, 1700])
H(2, "8.3 Errores y resiliencia")
UL(["Timeout de 120 s por llamada RFC; un reintento automático solo en errores de comunicación, nunca en errores de negocio.",
    "Cada paso es idempotente: si el objeto existe, el paquete Z responde EXISTE y no lo modifica (RN-03).",
    "Un paso fallido detiene el plan y se reanuda desde ese paso (HU-14). No hay circuit breaker: ningún RNF lo exige."])
H(2, "8.4 Secuencias de las integraciones críticas")
H(3, "Generación de objetos en BW y BPC (HU-14, HU-15, HU-26 / INT-02, INT-03)")
B.append(IMG("A5_seq_generacion", 15))
H(3, "Importación nocturna con validación de cuadre (HU-27, HU-36 / INT-05)")
B.append(IMG("A6_seq_importacion", 15))

H(1, "9. Vista de seguridad")
P(f"Nivel OWASP ASVS {DAF['nivel_asvs']}, según la clasificación de datos del DAF (sección 9): datos financieros confidenciales y datos personales de terceros.")
T([["Dominio", "Decisión", "RNF"]] + [list(s) for s in SEGURIDAD], [1600, 5638, 1600])

H(1, "10. Vista de despliegue")
B.append(IMG("A8_despliegue", 15))
T([["Ambiente", "Infraestructura", "Uso"]] + [list(a) for a in AMBIENTES], [1100, 4738, 3000])
H(2, "10.1 Pipeline")
UL(["Rama main protegida; todo cambio entra por PR con un revisor.",
    "El PR ejecuta build, pruebas unitarias, SAST, SCA y escaneo de secretos; falla con hallazgos altos.",
    "Artefacto único: .exe autocontenido firmado y transporte del paquete Z, publicados en Azure Artifacts.",
    "Promoción DEV → QA → UAT con aprobación del líder técnico; el transporte al productivo del cliente lo importa su Administrador SAP."])
H(2, "10.2 Respaldo y recuperación")
P("No hay RNF-Disp en el DAF: el acelerador no es un servicio en línea. Antes de cada cambio en BPC se toma un respaldo UJBR; la base local se exporta por cliente; el backup de SAP es del cliente.")

H(1, "11. Vista de comportamiento")
P("Estados de la implementación de un cliente en el configurador.")
B.append(IMG("A7_estados", 13))

H(1, "12. Matriz RNF ↔ decisión (gate F6)")
rnfd = {r["codigo"]: r for r in DAF["rnf"]}
T([["RNF", "Métrica del DAF", "Decisión o componente"]] + [[k, rnfd[k]["metrica"], ", ".join(v)] for k, v in MATRIZ.items()], [1500, 4838, 2500])

H(1, "13. Decisiones de arquitectura (ADR)")
for c, t, ctx, dec, alt, raz, cons, rnf, cr in ADRS:
    H(2, f"{c} — {t}")
    T([["Contexto", ctx], ["Decisión", dec], ["Alternativa más simple descartada", alt], ["Razón de descarte", raz], ["Consecuencias", cons], ["RNF", ", ".join(rnf)], ["Costo relativo", cr]], [2600, 6238])

H(1, "14. Costos de infraestructura (referencial)")
P(f"Valores mensuales en COP con una tasa supuesta de {TRM:,} COP por USD; referenciales, pendientes de validación comercial. No productivos con apagado programado (60 horas al mes). No incluye licencias SAP de los ambientes de prueba, por definir con SAP.".replace(",", "."))
T([["Ambiente", "Recurso", "SKU", "COP/mes"]] + [[a, r, s, cop(u)] for a, r, s, u in COSTOS] +
  [[{"text": "Total PROD", "bold": True}, "", "", {"text": cop(sum(c[3] for c in COSTOS if c[0] == "PROD")), "bold": True}],
   [{"text": "Total no productivos", "bold": True}, "", "", {"text": cop(sum(c[3] for c in COSTOS if c[0] != "PROD")), "bold": True}]], [1200, 3138, 3000, 1500])

H(1, "15. Camino de evolución y decisiones diferidas")
T([["Decisión diferida", "Disparador medible", "Qué cambiaría"]] + [list(d) for d in DIFERIDAS], [2900, 3038, 2900])

H(1, "16. Riesgos técnicos")
T([["Código", "Riesgo", "Prob.", "Impacto", "Mitigación", "Estado"]] + [[r[0], r[1], r[2], r[3], r[4], r[5]] for r in RIESGOS], [700, 2938, 700, 800, 2900, 800])

H(1, "17. Migración de datos")
P("No aplica: el acelerador no migra datos históricos; la carga full inicial del histórico la hacen las cadenas generadas en el BW del cliente (RF-10).")

H(1, "18. Anexos")
H(2, "18.1 Épicas y capítulos de arquitectura")
T([["Épica", "Capítulos que la soportan"]] + [list(e) for e in EPICA_CAP], [3800, 5038])
H(2, "18.2 Preguntas del DAF cerradas o acotadas en F6")
T([["Pregunta del DAF", "Resolución"], ["4 · Dónde se aloja el configurador", "Equipo del consultor, sin servidor (ADR-02); servidor diferido (D-01)."],
   ["12 · Cómo leen BW los reportes de activos fijos y deterioros", "Conexión local de EPM add-in a consultas BW (ADR-08), a verificar en HU-01."],
   ["13 y 17 · EPM y funciones de cierre con BPC embebido", "No aplican a la versión 1: la arquitectura B queda fuera (RN-19, R-01 cerrado); se retoman con D-04."],
   ["18 · Versiones de la variante BW/4HANA", "Se propone BW/4HANA 2023 con BPC 2021; por confirmar con el ambiente de QA."]], [3800, 5038])
P("Estrategia de pruebas: ver el Plan de Pruebas (F7).")

H(1, "19. Aprobación interna")
P("Gate F6: matriz RNF ↔ decisión completa (12 de 12) y riesgos altos cerrados o aceptados. " + ("Pendiente: R-01 y R-02." if ESTADO == "BORRADOR" else "Cumplido: R-01 cerrado y R-02 aceptado."))
T([["Campo", "Información"], ["Rol", "Líder técnico CM Consulting"], ["Estado", "Aprobado" if ESTADO == "APROBADO" else "Pendiente"], ["Fecha", "16-Oct-26" if ESTADO == "APROBADO" else ""], ["Firma", ""]], [2800, 6038])

(D / "bloques_arq.json").write_text(json.dumps({"blocks": B}, ensure_ascii=False, indent=1), encoding="utf-8")

# ---------------- ADRs en Markdown para la Wiki ----------------
adr_dir = OUT / "adr"
adr_dir.mkdir(parents=True, exist_ok=True)
import re, unicodedata
slug = lambda s: re.sub(r"[^a-z0-9]+", "-", unicodedata.normalize("NFKD", s).encode("ascii", "ignore").decode().lower()).strip("-")
for c, t, ctx, dec, alt, raz, cons, rnf, cr in ADRS:
    (adr_dir / f"{c}-{slug(t)}.md").write_text(
        f"# {c} — {t}\n\n**Estado:** {'aceptado' if ESTADO == 'APROBADO' else 'propuesto'} · **Fecha:** 16-Oct-26 · **RNF:** {', '.join(rnf)} · **Costo relativo:** {cr}\n\n"
        f"## Contexto\n{ctx}\n\n## Decisión\n{dec}\n\n## Alternativa más simple descartada\n{alt}\n\n## Razón de descarte\n{raz}\n\n## Consecuencias\n{cons}\n",
        encoding="utf-8")
print("componentes", len(COMP), "ADR", len(ADRS), "riesgos", len(RIESGOS), "PROD COP", cop(sum(c[3] for c in COSTOS if c[0] == "PROD")), "no-PROD COP", cop(sum(c[3] for c in COSTOS if c[0] != "PROD")))
