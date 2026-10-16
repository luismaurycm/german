"""Genera daf.json (validador) y bloques.json (skill base) para el DAF del Acelerador BPC."""
import json, sys
from pathlib import Path

OUT = Path(sys.argv[1])
G7 = "Sesión técnica 16-Oct-26, G7 "
G6 = "Sesión técnica 16-Oct-26, G6 "
SI = "Sesión interna CM 16-Oct-26"
PB = "Playbook CM §7.1"
S11 = "Sesión interna CM 16-Oct-26 (ampliación v1.1)"
CR = "Caso de referencia BW 7.5/BPC 10.1"

# ---------------- Requerimientos funcionales ----------------
MODULOS = [
    ("5.1 Conexión y diagnóstico", [
        ("RF-01", "Registrar y probar conexiones",
         "El sistema permite al Consultor implementador registrar los parámetros de conexión a SAP ERP, SAP BW y SAP BPC del cliente y probar cada conexión, mostrando para cada sistema el resultado (exitosa o fallida con el mensaje devuelto).",
         [], "Alta", G7 + "08:40-08:46; 21:05-21:10"),
        ("RF-02", "Validar conexión ERP–BW",
         "El sistema permite al Consultor implementador validar la conexión entre SAP ERP y SAP BW antes de iniciar las cargas, y registra el resultado de la validación.",
         ["RN-01"], "Alta", G7 + "03:14-03:25"),
        ("RF-03", "Inventariar objetos existentes",
         "El sistema lista los objetos ya existentes en el sistema del cliente: extractores activos, InfoCubos o aDSO, modelos BPC y campos Z, para que el Consultor implementador decida qué generar.",
         ["RN-03"], "Alta", G7 + "13:40-14:06; 14:45-14:55"),
        ("RF-04", "Identificar la arquitectura",
         "El sistema identifica si el cliente opera la arquitectura A (ERP, BW 7.5 o BW/4HANA y BPC modelo estándar en sistemas separados) o la B (ERP, BW y BPC integrados en el mismo sistema, p. ej. S/4HANA con BW embebido y BPC embebido), y usa la variante de generación de esa arquitectura en los pasos siguientes.",
         ["RN-02"], "Alta", G6 + "00:00-00:43; " + G7 + "12:31-13:21"),
    ]),
    ("5.2 Configuración de alcance", [
        ("RF-05", "Seleccionar módulos",
         "El sistema permite al Consultor implementador marcar o desmarcar los módulos a implementar (consolidación, presupuesto, activos fijos, deterioros); solo los módulos marcados se generan.",
         [], "Alta", G7 + "14:13-15:35"),
        ("RF-06", "Registrar sociedades del grupo",
         "El sistema permite registrar las sociedades del grupo a consolidar, indicando para cada una si su información está en SAP o se carga por fuera.",
         ["RN-07"], "Alta", G7 + "03:40-04:33"),
        ("RF-07", "Registrar mapeo de cuentas",
         "El sistema permite registrar el mapeo de las cuentas contables del cliente a los rubros de reporte (por ejemplo, rubro F-00-101 Efectivo).",
         ["RN-10"], "Alta", G7 + "03:34-03:40; 19:32-20:44"),
    ]),
    ("5.3 Bodega de datos en BW", [
        ("RF-08", "Generar la bodega",
         "Cuando el inventario (RF-03) indica que la bodega no existe, el sistema crea en BW los objetos de los módulos seleccionados: activa los extractores estándar de información financiera (0FI_GL_14 en ECC; el equivalente en S/4HANA se confirma en la pregunta abierta 11) y crea los InfoProviders (InfoCubos o aDSO).",
         ["RN-03"], "Alta", G6 + "01:10-01:17; " + G7 + "00:05-00:12; 13:46-13:58"),
        ("RF-09", "Incorporar campos Z",
         "El sistema incorpora a la bodega los campos adicionales (Z) que el cliente haya creado en SAP y que el Consultor implementador seleccione.",
         [], "Media", G7 + "14:02-14:06"),
        ("RF-10", "Generar cadenas de proceso",
         "El sistema crea las cadenas de proceso que ejecutan una carga full inicial del histórico y, después, una carga delta cada noche.",
         ["RN-04"], "Alta", G7 + "05:47-05:50; 09:40-09:57"),
    ]),
    ("5.4 Datos maestros", [
        ("RF-11", "Cargar planes de cuentas",
         "El sistema carga en BW el plan de cuentas de cada sociedad seleccionada.",
         ["RN-05"], "Alta", G7 + "04:44-04:57"),
        ("RF-12", "Cargar CeCo y CeBe",
         "El sistema carga en BW los centros de costo y de beneficio de todas las sociedades seleccionadas.",
         [], "Alta", G7 + "04:04-04:15"),
        ("RF-13", "Cargar monedas y tasas",
         "El sistema carga la moneda local de cada sociedad tal como la maneja SAP y las tasas de cambio entre moneda local y dólares.",
         ["RN-06"], "Alta", G7 + "05:02-05:35; " + SI + " (respuesta 6)"),
        ("RF-14", "Cargar jerarquía de cuentas",
         "El sistema carga en BW la jerarquía de cuentas del cliente.",
         [], "Alta", G7 + "05:38-05:44"),
        ("RF-15", "Cargar sociedades no SAP",
         "El sistema permite cargar la información contable de las sociedades no SAP registradas en RF-06 al mismo modelo que las sociedades SAP.",
         [], "Alta", G7 + "04:25-04:33"),
    ]),
    ("5.5 Modelo BPC y consolidación", [
        ("RF-16", "Configurar dimensiones",
         "El sistema crea en BPC las dimensiones del modelo de consolidación, entre ellas cuentas y terceros (la lista completa se define en arquitectura), y carga sus miembros con los datos del cliente.",
         [], "Alta", G7 + "06:02-06:12; 20:16-20:21"),
        ("RF-17", "Importar datos de BW a BPC",
         "En el modelo estándar de BPC, el sistema importa al modelo BPC los datos del InfoProvider de BW (InfoCubo o aDSO).",
         [], "Alta", G7 + "06:15-06:22"),
        ("RF-18", "Desplegar scripts de lógica",
         "El sistema despliega en BPC los scripts de consolidación completados con los datos del cliente, en la variante de la arquitectura identificada en RF-04.",
         ["RN-02"], "Alta", G7 + "06:28-06:40; 10:41-10:56"),
        ("RF-19", "Ejecutar la consolidación",
         "El sistema ejecuta la consolidación del grupo eliminando las transacciones entre sociedades del grupo, aplica según el método de cada sociedad las reglas de RF-32 y RF-33, y deja el resultado consultable en el modelo BPC.",
         ["RN-06", "RN-07", "RN-13"], "Alta", G7 + "03:50-04:00; 06:37-06:40"),
        ("RF-26", "Convertir a la otra moneda",
         "El sistema convierte los saldos de cada sociedad a la otra moneda de presentación (moneda local o dólares) con las tasas cargadas en RF-13, y deja la consolidación disponible en ambas monedas.",
         ["RN-06", "RN-11"], "Alta", G7 + "05:10-05:20; " + SI + " (respuesta 6)"),
    ]),
    ("5.6 Reportes EPM", [
        ("RF-20", "Entregar reportes EPM",
         "El sistema entrega los 20 reportes iniciales de la tabla 5.6.1, cada uno como un archivo Excel con EPM add-in conectado al modelo BPC, que el cliente conserva y actualiza.",
         ["RN-08"], "Alta", G7 + "15:44-17:10; " + SI),
        ("RF-21", "Seleccionar sociedad y filiales",
         "Cada reporte permite al Usuario financiero elegir la sociedad, si incluye sus filiales y la moneda de presentación (moneda local o dólares), y actualizar los datos desde BPC.",
         ["RN-06", "RN-09"], "Alta", G7 + "18:27-19:04; " + SI + " (respuesta 6)"),
        ("RF-22", "Calcular rubros por mapeo",
         "Cada rubro de un reporte muestra la suma de las cuentas mapeadas a ese rubro en RF-07.",
         ["RN-10"], "Alta", G7 + "19:29-20:44"),
    ]),
    ("5.7 Módulos de presupuesto, activos fijos y deterioros", [
        ("RF-23", "Generar módulo de presupuesto",
         "Cuando el módulo de presupuesto está seleccionado (RF-05), el sistema carga en BW el presupuesto desde el extractor 0FI_GL_14 y genera en BPC un modelo de planeación propio para el presupuesto, separado del modelo de consolidación.",
         [], "Alta", G7 + "00:05-00:19; 15:31-15:35; " + SI + " (respuestas 2, 7 y 8)"),
        ("RF-24", "Generar módulo de activos fijos",
         "Cuando el módulo de activos fijos está seleccionado (RF-05), el sistema activa el extractor estándar de activos fijos y genera en BW los InfoProviders y las cargas de esa información, sin crear un modelo en BPC.",
         ["RN-12"], "Alta", G7 + "14:13-14:15; " + SI + " (respuestas 2, 7 y 8)"),
        ("RF-25", "Generar módulo de deterioros",
         "Cuando el módulo de deterioros está seleccionado (RF-05), el sistema activa el extractor estándar de deterioros y genera en BW los InfoProviders y las cargas de esa información, sin crear un modelo en BPC.",
         ["RN-12"], "Alta", G7 + "14:16-14:18; " + SI + " (respuestas 2, 7 y 8)"),
    ]),
    ("5.8 Reportes de presupuesto, activos fijos y deterioros", [
        ("RF-27", "Entregar reportes de presupuesto",
         "El sistema entrega los reportes P01 a P04 de la tabla 5.8.1, cada uno como un archivo Excel con EPM add-in conectado al modelo de planeación de presupuesto en BPC.",
         ["RN-08"], "Alta", SI + " (respuesta 8, selección delegada)"),
        ("RF-28", "Capturar presupuesto",
         "El sistema permite al Usuario financiero ingresar y ajustar valores de presupuesto por sociedad, cuenta, centro de costo y periodo en el formulario P05, y guardarlos en el modelo de planeación de BPC.",
         ["RN-08"], "Alta", G7 + "00:12-00:19; " + SI + " (respuesta 8, selección delegada)"),
        ("RF-29", "Entregar reportes de activos fijos y deterioros",
         "El sistema entrega los reportes A01 a A03 y D01 a D02 de la tabla 5.8.1, que leen su información de BW.",
         ["RN-12"], "Alta", SI + " (respuesta 8, selección delegada)"),
        ("RF-30", "Alimentar reportes de consolidación",
         "La información de activos fijos y deterioros cargada en BW alimenta los movimientos de los reportes R08 Cuentas por cobrar, R10 Propiedad, planta y equipo y R11 Intangibles.",
         ["RN-12"], "Media", SI + " (respuesta 8)"),
    ]),
    ("5.9 Reglas de consolidación y control del cierre", [
        ("RF-31", "Generar el modelo de propiedad",
         "El sistema genera en BPC el modelo de propiedad y la plantilla C02, en la que el Usuario financiero registra por periodo, para cada sociedad y grupo, el porcentaje de control, el de consolidación, el de propiedad y el método de consolidación. El Consultor implementador registra la estructura inicial en el configurador.",
         ["RN-13", "RN-14"], "Alta", S11 + "; " + CR + " (participaciones y métodos)"),
        ("RF-32", "Eliminar inversión y calcular interés minoritario",
         "El sistema despliega en BPC las reglas que, para las sociedades con método de integración global, eliminan la inversión de la matriz contra el patrimonio de la subsidiaria y calculan el interés minoritario con los porcentajes del periodo registrados en RF-31.",
         ["RN-13", "RN-14"], "Alta", S11 + "; " + CR + " (eliminación de inversiones y capital)"),
        ("RF-33", "Registrar diferencia en conversión",
         "Al convertir los saldos (RF-26), el sistema calcula la diferencia que resulta de aplicar tasas distintas a balance, resultados y patrimonio, y la registra en la cuenta de patrimonio definida para ello.",
         ["RN-11", "RN-18"], "Alta", S11 + "; " + CR + " (diferencial cambiario)"),
        ("RF-34", "Conciliar saldos intercompañía",
         "El sistema entrega el reporte C01, que compara por pareja de sociedades y cuenta intercompañía los saldos que registra cada parte, muestra la diferencia y marca las parejas que superan el umbral definido, antes de ejecutar la eliminación (RF-19).",
         ["RN-07"], "Alta", S11 + "; " + CR + " (modelo de conciliación)"),
        ("RF-35", "Registrar asientos de ajuste",
         "El sistema configura en BPC los asientos de ajuste de consolidación, en los que el Usuario financiero registra por grupo, periodo y moneda líneas con sociedad, cuenta, intercompañía, débito, crédito y observación.",
         ["RN-15"], "Alta", S11 + "; " + CR + " (asientos de ajuste)"),
        ("RF-36", "Validar cuadre antes de importar",
         "Los paquetes de importación que genera el sistema (RF-17) y la carga de sociedades no SAP (RF-15) validan cada sociedad y periodo antes de pasar los datos a BPC; las sociedades que no cumplen no se importan y aparecen en el reporte C03 con la diferencia encontrada.",
         ["RN-16"], "Alta", S11 + "; " + CR + " (descuadres en saldos individuales)"),
        ("RF-37", "Bloquear saldos por sociedad",
         "El sistema configura en BPC el bloqueo de datos por sociedad y periodo, de modo que el Usuario financiero bloquea o desbloquea una sociedad sin afectar a las demás sociedades del grupo.",
         ["RN-17"], "Media", S11 + "; " + CR + " (bloqueo solo por grupo)"),
        ("RF-38", "Generar el flujo de cierre",
         "El sistema genera en BPC el flujo del cierre mensual con sus pasos en orden (carga, validación de cuadre, conciliación, conversión, consolidación y reportes) y el reporte C04, que muestra por sociedad y periodo qué pasos están completos, pendientes o con error.",
         [], "Media", S11 + "; " + CR + " (sin control del proceso de consolidación)"),
        ("RF-39", "Gestionar versiones de presupuesto",
         "Cuando el módulo de presupuesto está seleccionado (RF-05), el Consultor implementador define las versiones del modelo de planeación (presupuesto anual y proyecciones de cierre como 3+9, 6+6 y 9+3), y los reportes P01 a P05 permiten elegir la versión.",
         [], "Media", S11 + "; " + CR + " (escenarios de presupuesto y proyección)"),
    ]),
]

REPORTES = [
    ("R01", "Estado de situación financiera", "Estados financieros", G7 + "17:55-18:00", "1"),
    ("R02", "Estado de resultados integral", "Estados financieros", SI + " (selección delegada)", "2"),
    ("R03", "Estado de cambios en el patrimonio", "Estados financieros", SI + " (selección delegada)", "2"),
    ("R04", "Estado de flujos de efectivo", "Estados financieros", SI + " (selección delegada)", "2"),
    ("R05", "Hoja de eliminaciones intercompañía", "Control de consolidación", SI + " (selección delegada)", "1"),
    ("R06", "Saldos y transacciones con partes relacionadas", "Control de consolidación", SI + " (selección delegada)", "2"),
    ("R07", "Efectivo y equivalentes de efectivo", "Notas de activo", G7 + "18:16-18:26", "1"),
    ("R08", "Cuentas por cobrar", "Notas de activo", G7 + "17:49-17:51", "3"),
    ("R09", "Carteras colectivas", "Notas de activo", G7 + "17:46-17:47", "3"),
    ("R10", "Propiedad, planta y equipo", "Notas de activo", G7 + "18:02 (por confirmar)", "3"),
    ("R11", "Intangibles", "Notas de activo", G7 + "18:03", "3"),
    ("R12", "Cuentas por pagar", "Notas de pasivo", G7 + "17:51-17:52", "3"),
    ("R13", "Obligaciones financieras", "Notas de pasivo", G7 + "18:05", "3"),
    ("R14", "Beneficios a empleados", "Notas de pasivo", G7 + "17:45", "3"),
    ("R15", "Provisiones y contingencias", "Notas de pasivo", SI + " (selección delegada)", "3"),
    ("R16", "Impuesto a las ganancias (corriente y diferido)", "Notas de pasivo", SI + " (selección delegada)", "3"),
    ("R17", "Dividendos pagados", "Patrimonio y resultados", G7 + "17:53", "3"),
    ("R18", "Dividendos recibidos", "Patrimonio y resultados", G7 + "17:52 (por confirmar)", "3"),
    ("R19", "Ingresos de actividades ordinarias", "Patrimonio y resultados", SI + " (selección delegada)", "3"),
    ("R20", "Informe a junta directiva", "Gobierno", G7 + "18:04", "3"),
]

REPORTES2 = [
    ("P01", "Presupuesto frente a ejecución (estado de resultados)", "Presupuesto", "BPC (presupuesto)"),
    ("P02", "Presupuesto de gastos por centro de costo", "Presupuesto", "BPC (presupuesto)"),
    ("P03", "Presupuesto de ingresos por centro de beneficio", "Presupuesto", "BPC (presupuesto)"),
    ("P04", "Ejecución acumulada y proyección de cierre del año", "Presupuesto", "BPC (presupuesto)"),
    ("P05", "Formulario de captura y ajuste de presupuesto", "Presupuesto", "BPC (presupuesto)"),
    ("A01", "Movimiento de activos fijos por clase (saldo inicial, adiciones, retiros, depreciación, saldo final)", "Activos fijos", "BW"),
    ("A02", "Depreciación del periodo por sociedad y centro de costo", "Activos fijos", "BW"),
    ("A03", "Listado de activos fijos por sociedad", "Activos fijos", "BW"),
    ("D01", "Movimiento del deterioro (saldo inicial, cargos, recuperaciones, castigos, saldo final)", "Deterioros", "BW"),
    ("D02", "Deterioro por cuenta y sociedad", "Deterioros", "BW"),
]

REPORTES3 = [
    ("C01", "Conciliación intercompañía por pareja de sociedades", "Reporte", "BPC (consolidación)", "RF-34"),
    ("C02", "Participaciones y métodos de consolidación por periodo", "Plantilla de captura", "BPC (propiedad)", "RF-31"),
    ("C03", "Validación de cuadre por sociedad", "Reporte", "BPC (consolidación)", "RF-36"),
    ("C04", "Estado del cierre por sociedad y paso", "Reporte", "BPC (consolidación)", "RF-38"),
]

RNF = [
    ("RNF-Comp-01", "Comp", "El acelerador soporta las dos arquitecturas: A (BW 7.5 o BW/4HANA con BPC modelo estándar) y B (ERP, BW y BPC integrados, con BPC modelo embebido).",
     "Todos los RF aplicables a cada arquitectura se ejecutan completos en un ambiente de prueba de esa arquitectura (2 de 2).", G7 + "12:47-12:57"),
    ("RNF-Comp-02", "Comp", "Los reportes funcionan con el EPM add-in de Excel. Analysis for Office no se soporta en esta versión.",
     "34 de 34 reportes y plantillas (tablas 5.6.1, 5.8.1 y 5.9.1) abren y actualizan datos con EPM add-in.", SI),
    ("RNF-Comp-03", "Comp", "Para el prototipo, el configurador web se ejecuta en el equipo del Consultor implementador, sin servidores adicionales. Dónde se aloja el producto se define en la pregunta abierta 4.",
     "Prototipo instalado en 1 equipo con 0 servidores adicionales.", G7 + "20:56-21:27"),
    ("RNF-Comp-04", "Comp", "El acelerador admite varias versiones de BW y BPC dentro de cada arquitectura; agregar una versión nueva solo agrega una variante de los generadores.",
     "Agregar una versión requiere 0 cambios en la configuración de los clientes y 0 cambios en las variantes existentes.", SI + " (respuesta 5)"),
    ("RNF-Comp-05", "Comp", "La matriz de soporte de la arquitectura A incluye, además de BW 7.5, la variante BW/4HANA con BPC 2021 modelo estándar, en la que los InfoProviders se generan como aDSO.",
     "Todos los RF de la arquitectura A se ejecutan completos en un ambiente BW/4HANA con BPC 2021 modelo estándar; 0 InfoCubos generados en esa variante.", S11 + "; " + CR + " (fin de mantenimiento de BW 7.5)"),
    ("RNF-Rend-01", "Rend", "La carga delta deja disponibles en los reportes las transacciones registradas en SAP el día anterior.",
     "1 ejecución cada 24 horas; antigüedad de los datos ≤ 24 horas al abrir un reporte. Duración máxima de la ventana: pregunta abierta 7.", G7 + "09:51-09:57"),
    ("RNF-Seg-01", "Seg", "La solución se verifica con OWASP ASVS nivel 2, según la clasificación de datos de la sección 9.",
     "Verificación ASVS nivel 2 sin hallazgos altos abiertos antes de la entrega.", PB + "; sección 9"),
    ("RNF-Seg-02", "Seg", "Las credenciales de conexión a SAP no se guardan en texto plano en el equipo del Consultor implementador.",
     "0 credenciales legibles en archivos de configuración o registros.", PB + "; sección 9"),
    ("RNF-Seg-03", "Seg", "El usuario técnico de conexión a SAP opera con los permisos mínimos que requiere cada paso.",
     "Rol técnico documentado; 0 permisos de administración total (SAP_ALL).", PB),
    ("RNF-Aud-01", "Aud", "Cada ejecución del acelerador queda registrada: usuario, fecha y hora, sistema destino, paso, objetos creados o modificados y resultado.",
     "100 % de las ejecuciones con registro; retención mínima de 12 meses.", PB + " (observabilidad)"),
    ("RNF-Aud-02", "Aud", "Los cambios en participaciones y métodos (RF-31), asientos de ajuste (RF-35) y bloqueos (RF-37) quedan registrados con usuario, fecha y hora, valor anterior y valor nuevo.",
     "100 % de los cambios con registro consultable en BPC; retención mínima de 12 meses.", S11 + "; " + CR + " (sin registro de modificaciones)"),
    ("RNF-Usa-01", "Usa", "El configurador web cumple accesibilidad WCAG 2.1 nivel AA.",
     "0 incumplimientos de nivel A y AA en una revisión automática de las pantallas.", PB + "; " + SI + " (respuesta 3)"),
]

RN = [
    ("RN-01", "La validación de conexión ERP–BW aplica cuando ERP y BW están en sistemas separados.", "RF-02", G6 + "00:13-00:43"),
    ("RN-02", "Los scripts de cálculo difieren entre BPC modelo estándar y BPC modelo embebido; cada arquitectura usa su propia variante.", "RF-04, RF-18", G7 + "10:49-10:56"),
    ("RN-03", "Si el cliente ya tiene bodega en BW, no se recrea: se parte de la existente.", "RF-03, RF-08", G7 + "13:40-13:51"),
    ("RN-04", "Después de la carga full inicial, las cargas siguientes son delta y se ejecutan cada noche.", "RF-10", G7 + "09:40-09:57"),
    ("RN-05", "Cada sociedad puede tener un plan de cuentas distinto.", "RF-11", G7 + "04:50-04:57"),
    ("RN-06", "La consolidación se presenta en dos monedas: la moneda local que maneja SAP y dólares.", "RF-13, RF-19, RF-21, RF-26", G7 + "05:10-05:20; " + SI + " (respuesta 6)"),
    ("RN-07", "Solo las sociedades del grupo participan como entidades de la consolidación; clientes y proveedores no.", "RF-06, RF-19, RF-34", G7 + "03:55-04:00"),
    ("RN-08", "En el modelo estándar, los reportes EPM de consolidación y de presupuesto se conectan a su modelo BPC, no directamente a BW.", "RF-20, RF-27, RF-28", G7 + "16:38-16:47"),
    ("RN-09", "Un reporte puede presentar una sociedad sola o la sociedad con todas sus filiales.", "RF-21", G7 + "18:44-19:01"),
    ("RN-10", "El valor de un rubro es la suma de las cuentas mapeadas a ese rubro.", "RF-07, RF-22", G7 + "20:02-20:44"),
    ("RN-11", "La conversión usa tasa de cierre para cuentas de balance, tasa promedio para resultados y tasa histórica para patrimonio.", "RF-26, RF-33", "NIC 21 (por confirmar en la pregunta abierta 3)"),
    ("RN-12", "Activos fijos y deterioros se quedan en BW: no tienen modelo en BPC y sus reportes leen de BW.", "RF-24, RF-25, RF-29, RF-30", SI + " (respuesta 8)"),
    ("RN-13", "Cada sociedad tiene en cada periodo un método de consolidación: matriz, integración global, participación (coligada) o fuera del perímetro. La integración global elimina inversión y patrimonio y calcula interés minoritario; la participación reconoce la proporción del resultado y no elimina transacciones intercompañía.", "RF-19, RF-31, RF-32", S11 + "; NIIF 10 y NIC 28 (por confirmar en la pregunta abierta 15)"),
    ("RN-14", "Los porcentajes y el método se registran por periodo; un cambio aplica desde el periodo en que se registra y no altera los periodos anteriores.", "RF-31, RF-32", S11 + "; " + CR + " (pérdida del porcentaje anterior)"),
    ("RN-15", "Un asiento de ajuste solo se guarda si la suma de sus débitos es igual a la suma de sus créditos.", "RF-35", S11),
    ("RN-16", "Una sociedad solo se importa a BPC si en el periodo el activo es igual al pasivo más el patrimonio, la utilidad del estado de resultados es igual al resultado del ejercicio del balance y todas sus cuentas con saldo están mapeadas; la tolerancia de redondeo se define en la pregunta abierta 14.", "RF-36", S11 + "; " + CR + " (validaciones de cuadre)"),
    ("RN-17", "Una sociedad bloqueada en un periodo no admite cargas, asientos de ajuste ni reprocesos de ese periodo hasta que se desbloquee.", "RF-37", S11),
    ("RN-18", "La diferencia en conversión se registra en el patrimonio y no en el estado de resultados.", "RF-33", "NIC 21 (por confirmar en la pregunta abierta 3)"),
]

INT = [
    ("INT-01", "SAP ERP → SAP BW", "Entrada", "Extractores estándar SAP (0FI_GL_14 en ECC; extractores estándar de activos fijos y de deterioros)", "Movimientos y saldos contables, presupuesto, activos fijos y deterioros", G6 + "01:10-01:17; " + SI + " (respuesta 7)"),
    ("INT-02", "SAP BW", "Bidireccional", "RFC con paquete Z (confirmado por CM; se valida con prueba técnica)", "Inventario de objetos; creación de objetos, cadenas y cargas", G7 + "14:45-14:55; " + SI),
    ("INT-03", "SAP BPC", "Bidireccional", "RFC con paquete Z (confirmado por CM; se valida con prueba técnica)", "Dimensiones, miembros y scripts de lógica", G7 + "08:40-08:46; " + SI),
    ("INT-04", "Fuentes no SAP → SAP BW", "Entrada", "Archivo (formato por definir)", "Información contable de sociedades no SAP", G7 + "04:25-04:33"),
    ("INT-05", "SAP BW → SAP BPC", "Entrada", "Importación desde InfoProvider (modelo estándar)", "Datos financieros para el modelo", G7 + "06:15-06:22"),
    ("INT-06", "SAP BPC → Excel (EPM add-in)", "Salida", "Conexión EPM add-in", "Datos consolidados por sociedad y filiales", G7 + "16:38-16:47"),
]

CLASIF = [
    ("Saldos y movimientos contables por sociedad", "Confidencial", "SAP ERP", "Transmite y almacena (en SAP del cliente)", "Información financiera no publicada; base de los reportes de consolidación."),
    ("Estructura del grupo: sociedades, filiales, CeCo y CeBe", "Interna", "SAP ERP; configuración", "Captura, almacena, transmite", "—"),
    ("Plan de cuentas, jerarquías y mapeo a rubros", "Interna", "SAP ERP; configuración", "Captura, almacena, transmite", "—"),
    ("Terceros (clientes, proveedores, contrapartes)", "Personal", "SAP ERP", "Transmite y almacena (en SAP del cliente)", "Pueden ser personas naturales identificadas por nombre y documento. Aplica Ley 1581/2012."),
    ("Beneficios a empleados", "Confidencial", "SAP ERP", "Transmite y almacena (en SAP del cliente)", "Si el reporte desagrega por empleado pasa a Personal (pregunta abierta 9)."),
    ("Informe a junta directiva", "Confidencial", "Modelo BPC", "Transmite", "—"),
    ("Información contable de sociedades no SAP", "Confidencial", "Archivo del cliente", "Captura y transmite (equipo local)", "Pasa por el equipo del Consultor implementador; ver RNF-Seg-02."),
    ("Scripts de lógica parametrizados", "Interna", "Acelerador", "Almacena y transmite", "Contienen códigos de sociedades y cuentas del cliente."),
    ("Presupuesto", "Confidencial", "SAP ERP (0FI_GL_14); captura en EPM", "Captura, almacena, transmite", "Lo ingresa el Usuario financiero en P05."),
    ("Activos fijos y deterioros", "Confidencial", "SAP ERP (extractores estándar)", "Transmite y almacena (en BW del cliente)", "El listado A03 identifica activos individuales, no personas."),
    ("Tasas de cambio", "Interna", "SAP ERP", "Transmite y almacena", "—"),
    ("Participaciones y métodos de consolidación", "Confidencial", "Usuario financiero (C02); configuración", "Captura, almacena, transmite", "Estructura societaria no publicada; cambios auditados (RNF-Aud-02)."),
    ("Asientos de ajuste de consolidación", "Confidencial", "Usuario financiero", "Captura y almacena (en BPC del cliente)", "Incluyen usuario y observación; cambios auditados (RNF-Aud-02)."),
    ("Credenciales de conexión a SAP", "Confidencial", "Consultor implementador", "Captura y almacena (equipo local)", "Secretos; ver RNF-Seg-02."),
    ("Registro de ejecuciones", "Interna", "Acelerador", "Almacena", "Incluye el usuario que ejecuta; ver RNF-Aud-01."),
]

PREGUNTAS = [
    ("Validar los 10 reportes propuestos por CM para presupuesto, activos fijos y deterioros (tabla 5.8.1) y cuáles reportes de consolidación alimentan (RF-30 propone R08, R10 y R11).", False, "Consultor SAP BPC", "23-Oct-26"),
    ("Versiones exactas de los dos ambientes disponibles, que forman la matriz de soporte inicial. A: ¿BW 7.5 con BPC 10.1 modelo estándar o BPC 7.5 NW? B: ¿S/4HANA 2025 con BW y BPC embebidos, o BW/4HANA? (en la grabación se oye 'BWU 2025' y 'BPC 11').", False, "Consultor SAP BPC", "23-Oct-26"),
    ("Confirmar los tipos de tasa de RN-11 (NIC 21) y de qué tabla de SAP se toman las tasas de cambio.", False, "Consultor SAP BPC", "30-Oct-26"),
    ("¿El configurador web se ejecuta en el equipo del consultor o en un servidor de CM? Define autenticación y despliegue.", False, "CM Consulting – Arquitectura", "30-Oct-26"),
    ("Formato de carga de la información de sociedades no SAP (INT-04).", False, "Consultor SAP BPC", "30-Oct-26"),
    ("Validar los 8 reportes agregados por CM (R02 a R06, R15, R16, R19) contra el catálogo de cerca de 50 reportes, e identificar dos reportes dictados que no se entendieron: 'certificación proyecti nominal' y 'el de cuentas'.", False, "Consultor SAP BPC", "30-Oct-26"),
    ("Volumen esperado (sociedades, movimientos por día) y duración de la ventana nocturna para la carga delta (RNF-Rend-01).", False, "Consultor SAP BPC", "30-Oct-26"),
    ("Número aproximado de Usuarios financieros por cliente.", False, "Consultor SAP BPC", "30-Oct-26"),
    ("¿El reporte de beneficios a empleados desagrega por empleado? Cambia la clasificación del dato.", False, "Consultor SAP BPC", "30-Oct-26"),
    ("¿Algún cliente objetivo está vigilado por la Superintendencia Financiera (Circular 007)?", False, "CM Consulting – Dirección", "30-Oct-26"),
    ("Confirmar nombres inferidos de la grabación: extractor financiero (0FI_GL_14 en ECC; en S/4HANA, extractores sobre ACDOCA), el rubro 'PIGs' (propiedad, planta y equipo), los dividendos de R17 y R18, los nombres de los extractores de activos fijos (p. ej. 0FI_AA_11 / 0FI_AA_12) y de deterioros, si deterioros se refiere a cartera o a activos, y que el presupuesto quede registrado en el ledger que lee 0FI_GL_14.", False, "Consultor SAP BPC", "30-Oct-26"),
    ("¿Cómo leen BW los reportes de activos fijos y deterioros con EPM add-in: conexión directa a consultas BW o un resumen en BPC? Se define en arquitectura.", False, "CM Consulting – Arquitectura", "30-Oct-26"),
    ("¿EPM add-in sirve como herramienta de reportes con BPC modelo embebido (arquitectura B), donde SAP orienta hacia Analysis for Office?", False, "CM Consulting – Arquitectura", "30-Oct-26"),
    ("Tolerancia de redondeo para la validación de cuadre (RN-16) y umbral de diferencia que marca una pareja en la conciliación intercompañía (RF-34). ¿Fijos o configurables por cliente?", False, "Consultor SAP BPC", "30-Oct-26"),
    ("Confirmar los métodos de consolidación de RN-13 y su tratamiento, y si las cuentas de destino de la eliminación de inversión, el interés minoritario y la diferencia en conversión se configuran por cliente.", False, "Consultor SAP BPC", "30-Oct-26"),
    ("¿La moneda de presentación del grupo debe poder configurarse (p. ej. peso chileno o peso colombiano) o siempre es el dólar (RN-06)?", False, "CM Consulting – Dirección", "30-Oct-26"),
    ("¿Los asientos de ajuste (RF-35), el bloqueo por sociedad (RF-37) y el flujo de cierre (RF-38) están disponibles en BPC modelo embebido (arquitectura B) en las versiones de la matriz de soporte?", False, "CM Consulting – Arquitectura", "30-Oct-26"),
    ("Versiones exactas de BW/4HANA y BPC 2021 para la variante de RNF-Comp-05 y ambiente disponible para probarla.", False, "CM Consulting – Dirección", "30-Oct-26"),
]

COMPONENTES = {
    "M1 Conector y escáner": ["RF-01", "RF-02", "RF-03", "RF-04"],
    "M2 Configurador de alcance": ["RF-05", "RF-06", "RF-07", "RF-39"],
    "M3 Generador de bodega BW": ["RF-08", "RF-09", "RF-10", "RF-24", "RF-25"],
    "M4 Cargador de datos maestros": ["RF-11", "RF-12", "RF-13", "RF-14", "RF-15"],
    "M5 Generador de modelo BPC": ["RF-16", "RF-17", "RF-18", "RF-19", "RF-23", "RF-26", "RF-31", "RF-32", "RF-33", "RF-35", "RF-36", "RF-37", "RF-38"],
    "M6 Reportes EPM (34 iniciales)": ["RF-20", "RF-21", "RF-22", "RF-27", "RF-28", "RF-29", "RF-30", "RF-34"],
}

# ---------------- JSON para el validador ----------------
daf = {
    "rf": [{"codigo": c, "nombre": n, "descripcion": d, "reglas": r, "prioridad": p, "origen": o, "modulo": m}
           for m, items in MODULOS for (c, n, d, r, p, o) in items],
    "rnf": [{"codigo": c, "categoria": cat, "requerimiento": req, "metrica": met, "origen": o} for c, cat, req, met, o in RNF],
    "rn": [{"codigo": c, "regla": r, "origen": o} for c, r, _, o in RN],
    "int": [{"codigo": c, "sistema": s, "mecanismo": mec, "origen": o} for c, s, _, mec, _, o in INT],
    "alcance_propuesta": list(COMPONENTES),
    "matriz_alcance": COMPONENTES,
    "clasificacion_datos": [{"grupo": g, "categoria": c} for g, c, *_ in CLASIF],
    "nivel_asvs": 2,
    "preguntas_abiertas": [{"texto": t, "critica": c} for t, c, *_ in PREGUNTAS],
}
(OUT / "daf.json").write_text(json.dumps(daf, ensure_ascii=False, indent=2), encoding="utf-8")

# ---------------- Bloques del documento ----------------
B = []
H = lambda lvl, t: B.append({"type": "heading", "level": lvl, "text": t})
P = lambda t: B.append({"type": "paragraph", "text": t})
UL = lambda items: B.append({"type": "bullets", "items": items})
T = lambda rows, w: B.append({"type": "table", "rows": rows, "col_widths": w})
CM = [{"text": "CM", "bold": True}, {"text": "CONSULTING", "bold": True, "color": "F2B707"}]

B += [{"type": "portada", "text": "DOCUMENTO DE ANÁLISIS FUNCIONAL — ACELERADOR DE CONSOLIDACIÓN FINANCIERA SAP BW/BPC"},
      {"type": "paragraph", "text": "Producto interno CM Consulting", "align": "center", "bold": True, "color": "144E8C", "size": 28},
      {"type": "page_break"}]
H(1, "Control de versiones")
T([["Versión", "Elaborado por", "Fecha", "Observaciones"],
   ["0.1", "CM Consulting", "16-Oct-26", "Versión inicial a partir de la sesión técnica"],
   ["0.2", "CM Consulting", "16-Oct-26", "BORRADOR PARA REVISIÓN — pendiente de resolver preguntas abiertas. Incorpora las respuestas 1 a 6 de CM."],
   ["1.0", "CM Consulting", "16-Oct-26", "Versión para aprobación: incorpora las respuestas 7 y 8 de CM; sin preguntas críticas abiertas."],
   ["1.1", "CM Consulting", "16-Oct-26", "Amplía las reglas de consolidación y el control del cierre (RF-31 a RF-39, RN-13 a RN-18, RNF-Comp-05, RNF-Aud-02, reportes C01 a C04) y agrega la variante BW/4HANA con BPC 2021. Sin preguntas críticas abiertas."]],
  [1200, 2200, 1500, 3938])
H(1, "Confidencialidad")
P([{"text": "La información contenida en este documento es propiedad intelectual de "}, {"text": "CM CONSULTING SAS", "bold": True}, {"text": "."}])
P([{"text": "Este documento es de uso interno de "}, {"text": "CM CONSULTING SAS", "bold": True},
   {"text": ". Su reproducción, distribución o comunicación por cualquier medio queda expresamente prohibida sin el consentimiento escrito de "},
   {"text": "CM CONSULTING SAS", "bold": True}, {"text": "."}])
P([{"text": "CM CONSULTING SAS", "bold": True},
   {"text": " se reserva el derecho de tomar las acciones que estime necesarias conforme a las leyes nacionales e internacionales, si considera que esta condición ha sido transgredida y si las razones o perjuicios derivados pudiesen afectar su imagen o buen nombre."}])
B += [{"type": "page_break"}, {"type": "toc"}, {"type": "page_break"}]

H(1, "1. Introducción")
P([{"text": "Este documento establece la línea base funcional del Acelerador de consolidación financiera sobre SAP BW y SAP BPC, un producto interno de "}] + CM +
  [{"text": " para reducir el trabajo manual de cada implementación de consolidación en los clientes. Sus códigos (RF, RNF, RN, INT) serán referenciados por el Documento UX, las historias de usuario, la arquitectura y el plan de pruebas."}])
P("No existe una propuesta comercial: al ser un producto interno, CM Consulting formalizó como alcance el borrador de solución v3 del 16-Oct-26, con los ajustes de la sesión interna (todos los módulos en el primer alcance, configurador web, varias versiones y consolidación en moneda local y dólares).")
P("Fuentes del levantamiento y forma de citarlas en la columna Origen:")
T([["Identificador", "Fuente"],
   ["Sesión técnica 16-Oct-26, G6 mm:ss", "Sesión técnica con el consultor SAP BPC, grabación 6 (1,5 min). Transcripción automática."],
   ["Sesión técnica 16-Oct-26, G7 mm:ss", "Sesión técnica con el consultor SAP BPC, grabación 7 (21,5 min), con demostración del ambiente BW/BPC. Transcripción automática."],
   ["Sesión interna CM 16-Oct-26", "Decisiones confirmadas por CM Consulting: producto interno, solo EPM, 20 reportes iniciales, código Z permitido, mecanismo RFC con paquete Z, y respuestas a las preguntas críticas de la versión 0.1: 1 a 6 (alcance formalizado, todos los módulos, configurador web, varias versiones, moneda local y dólares) y 7 y 8 (fuentes y alcance de presupuesto, activos fijos y deterioros; reportes propuestos por CM por selección delegada)."],
   ["Sesión interna CM 16-Oct-26 (ampliación v1.1)", "Decisión de CM Consulting de ampliar la línea base con las reglas de consolidación y el control del cierre, y de agregar la variante BW/4HANA con BPC 2021, tras revisar el caso de referencia."],
   ["Caso de referencia BW 7.5/BPC 10.1", "Diagnóstico técnico y propuesta de mejora de un grupo empresarial multinacional que consolida en BW 7.5 y BPC 10.1, revisados por CM Consulting el 16-Oct-26. Son documentos confidenciales de terceros: no se incluyen y solo se cita el hallazgo entre paréntesis."],
   ["Borrador de solución v3", "Página de solución elaborada por CM Consulting el 16-Oct-26; usada como referencia de alcance en la sección 11."],
   ["Playbook CM §7.1", "Mínimos de seguridad, observabilidad y accesibilidad del Playbook de Desarrollo CM."]],
  [3000, 5838])

H(1, "2. Alcance")
H(2, "2.1 Dentro del alcance")
UL(["Conexión a SAP ERP, BW y BPC del cliente, validación de conexiones e inventario de objetos existentes.",
    "Configurador web para definir el alcance por cliente: módulos (consolidación, presupuesto, activos fijos y deterioros), sociedades del grupo y mapeo de cuentas.",
    "Consolidación presentada en moneda local y en dólares.",
    "Generación de la bodega en BW cuando no existe, incluidos campos Z y cadenas de proceso con carga full y delta.",
    "Carga de datos maestros: planes de cuentas, CeCo y CeBe, monedas, jerarquías y sociedades no SAP.",
    "Configuración del modelo BPC, despliegue de scripts y ejecución de la consolidación.",
    "Reglas de consolidación: participaciones y métodos por periodo, eliminación de inversión, interés minoritario, diferencia en conversión, conciliación intercompañía y asientos de ajuste.",
    "Control del cierre: validación de cuadre antes de importar, bloqueo por sociedad y flujo de cierre con su estado.",
    "34 reportes y plantillas iniciales en Excel con EPM add-in: 20 de consolidación (tabla 5.6.1), 10 de presupuesto, activos fijos y deterioros (tabla 5.8.1) y 4 de control de consolidación (tabla 5.9.1).",
    "Soporte de dos arquitecturas: A (BW 7.5 o BW/4HANA con BPC modelo estándar) y B (ERP, BW y BPC integrados, con BPC modelo embebido; varias versiones por arquitectura, RNF-Comp-04)."])
H(2, "2.2 Fuera del alcance")
UL(["Reportes en Analysis for Office.",
    "Mejorar, migrar u optimizar un modelo BW/BPC existente del cliente (cadenas, código ABAP, migración de base de datos a HANA): el acelerador parte de lo existente (RN-03) pero no lo corrige.",
    "Los reportes del catálogo existente que no están en la tabla 5.6.1 (entre 30 y 38, según la pregunta abierta 6).",
    "Licencias de SAP ERP, BW, BPC y EPM, que aporta cada cliente."])

H(1, "3. Actores y roles del sistema")
T([["Actor", "Tipo", "Descripción", "Volumen aproximado"],
   ["Consultor implementador", "Humano", "Consultor de CM que usa el configurador web del acelerador para implementar la consolidación en un cliente.", "Por confirmar"],
   ["Administrador SAP del cliente", "Humano", "Responsable Basis del cliente: crea el usuario técnico e importa transportes y paquetes Z.", "1 por cliente"],
   ["Usuario financiero", "Humano", "Persona del área financiera del cliente que abre y actualiza los reportes EPM.", "Por confirmar (pregunta 8)"],
   ["SAP ERP", "Sistema", "ECC o S/4HANA del cliente; origen de la información contable.", "1 por cliente"],
   ["SAP BW", "Sistema", "Bodega de datos: BW 7.5 (arquitectura A) o BW integrado al ERP (arquitectura B).", "1 por cliente"],
   ["SAP BPC", "Sistema", "Modelo de consolidación: BPC modelo estándar (A) o modelo embebido (B).", "1 por cliente"],
   ["EPM add-in", "Sistema", "Complemento de Excel que conecta los reportes con BPC.", "1 por equipo de usuario"]],
  [2000, 1100, 4238, 1500])

H(1, "4. Contexto de negocio")
H(2, "4.1 Proceso actual (as-is)")
P("Hoy el consultor implementa cada cliente a mano: valida la conexión ERP–BW, activa extractores, carga datos maestros, crea las cadenas de proceso, configura el modelo BPC, escribe los scripts de consolidación y construye cada reporte de Excel desde cero (Sesión técnica 16-Oct-26, G7 03:05-06:40 y 19:09-19:15).")
UL(["Cada implementación repite los mismos pasos con variaciones por cliente.",
    "Los reportes de Excel se desarrollan de cero en cada cliente.",
    "Los scripts de cálculo cambian según la versión de BPC.",
    "El paso a paso vive en la documentación personal del consultor (G7 06:42-06:47)."])
H(2, "4.2 Proceso objetivo (to-be)")
P("El Consultor implementador usa el configurador web del acelerador: conecta los sistemas del cliente, revisa lo que ya existe, marca los módulos y sociedades, y el acelerador genera la bodega, carga los datos maestros, configura el modelo BPC y despliega los scripts. Al final entrega los reportes EPM, que el Usuario financiero actualiza por su cuenta; las cargas delta nocturnas mantienen los datos al día.")

H(1, "5. Requerimientos funcionales")
for titulo, items in MODULOS:
    H(2, titulo)
    T([["Código", "Nombre", "Descripción", "Reglas", "Prioridad", "Origen"]] +
      [[c, n, d, ", ".join(r) if r else "—", p, o] for c, n, d, r, p, o in items],
      [900, 1500, 3238, 900, 900, 1400])
    if titulo.startswith("5.8"):
        H(3, "5.8.1 Catálogo de reportes de presupuesto, activos fijos y deterioros")
        T([["ID", "Reporte", "Módulo", "Fuente de datos"]] + [list(r) for r in REPORTES2], [800, 4638, 1700, 1700])
        P("Reportes propuestos por CM Consulting por selección delegada (Sesión interna CM 16-Oct-26, respuesta 8), sujetos a la pregunta abierta 1.")
    if titulo.startswith("5.9"):
        H(3, "5.9.1 Reportes y plantillas de control de consolidación")
        T([["ID", "Reporte o plantilla", "Tipo", "Fuente de datos", "RF"]] + [list(r) for r in REPORTES3], [800, 3838, 1600, 1700, 900])
        P("Agregados en la versión 1.1 (Sesión interna CM 16-Oct-26, ampliación v1.1).")
    if titulo.startswith("5.6"):
        H(3, "5.6.1 Catálogo de reportes iniciales")
        T([["ID", "Reporte", "Grupo", "Origen", "Ola"]] + [list(r) for r in REPORTES], [800, 3000, 2000, 1838, 1200])
        P("Ola 1 corresponde al prototipo; olas 2 y 3 completan los 20 reportes. Los reportes con origen \"selección delegada\" fueron elegidos por CM Consulting y están sujetos a la pregunta abierta 6.")

H(1, "6. Requerimientos no funcionales")
T([["Código", "Categoría", "Requerimiento", "Métrica verificable", "Origen"]] + [list(r) for r in RNF], [1300, 1000, 2900, 2238, 1400])

H(1, "7. Reglas de negocio")
T([["Código", "Regla", "RF relacionados", "Origen"]] + [list(r) for r in RN], [900, 4838, 1500, 1600])

H(1, "8. Integraciones")
T([["Código", "Sistema", "Dirección", "Mecanismo", "Datos intercambiados", "Origen"]] + [list(r) for r in INT], [900, 1500, 1200, 1700, 2138, 1400])

H(1, "9. Clasificación de datos")
T([["Grupo de datos", "Categoría", "Origen", "Tratamiento", "Observaciones"]] + [list(r) for r in CLASIF], [2000, 1300, 1500, 1700, 2338])
P("Conclusión: la categoría más alta presente es Personal (terceros que pueden ser personas naturales), junto con información financiera confidencial. La solución completa se verifica con OWASP ASVS nivel 2 (RNF-Seg-01), aplica las obligaciones de la Ley 1581/2012 sobre los datos de terceros y registra cada ejecución (RNF-Aud-01). Si algún cliente está vigilado por la Superintendencia Financiera, se verifica además la Circular 007 (pregunta abierta 10).")

H(1, "10. Diccionario de datos preliminar")
T([["Entidad", "Atributos clave", "Tipo de dato", "Clasificación"],
   ["Conexión", "Sistema, host, mandante, usuario técnico, credencial, arquitectura", "Texto; secreto", "Confidencial"],
   ["Sociedad", "Código, nombre, en SAP (sí/no), moneda local, sociedad matriz", "Texto; booleano", "Interna"],
   ["Cuenta", "Código, plan de cuentas, descripción, nodo de jerarquía", "Texto", "Interna"],
   ["Rubro de reporte", "Código (p. ej. F-00-101), nombre, cuentas mapeadas", "Texto; lista", "Interna"],
   ["Movimiento contable", "Sociedad, cuenta, periodo, tercero, CeCo, CeBe, importe en moneda local", "Texto; fecha; decimal", "Confidencial / Personal"],
   ["Presupuesto", "Sociedad, cuenta, centro de costo, periodo, versión, importe", "Texto; fecha; decimal", "Confidencial"],
   ["Activo fijo", "Número, clase, sociedad, fecha de capitalización, valor, depreciación acumulada, deterioro", "Texto; fecha; decimal", "Confidencial"],
   ["Tasa de cambio", "Moneda origen, moneda destino, tipo (cierre, promedio, histórica), fecha, valor", "Texto; fecha; decimal", "Interna"],
   ["Participación", "Sociedad, grupo, periodo, % control, % consolidación, % propiedad, método", "Texto; fecha; decimal", "Confidencial"],
   ["Asiento de ajuste", "Número, grupo, periodo, moneda, líneas (sociedad, cuenta, intercompañía, débito, crédito, observación), usuario, fecha", "Texto; fecha; decimal", "Confidencial"],
   ["Versión de presupuesto", "Código (p. ej. presupuesto anual, 3+9), descripción, año", "Texto", "Interna"],
   ["Módulo", "Nombre, seleccionado (sí/no)", "Texto; booleano", "Interna"],
   ["Reporte", "ID (R01-R20, P01-P05, A01-A03, D01-D02, C01-C04), nombre, parámetros (sociedad, filiales, moneda, versión)", "Texto", "Interna"],
   ["Ejecución", "Usuario, fecha y hora, sistema, paso, objetos, resultado", "Texto; fecha", "Interna"]],
  [1700, 3638, 2000, 1500])

H(1, "11. Matriz alcance ↔ RF")
P("Componentes del borrador de solución v3, formalizado como alcance por CM Consulting. Todos los RF quedan cubiertos por un componente. La versión 1.1 amplía M2, M5 y M6 con RF-31 a RF-39 por decisión de CM Consulting (Sesión interna CM 16-Oct-26, ampliación v1.1); al ser un producto interno, no hay candidatos a control de cambios frente a un cliente.")
T([["Componente (alcance formalizado)", "RF que lo cubren"]] + [[k, ", ".join(v)] for k, v in COMPONENTES.items()], [3000, 5838])

H(1, "12. Supuestos, dependencias y preguntas abiertas")
H(2, "12.1 Supuestos")
UL(["El acelerador es un producto interno de CM Consulting, reutilizable en varios clientes.",
    "El prototipo se construye sobre la arquitectura A, el ambiente con acceso disponible (Sesión técnica 16-Oct-26, G7 13:23-13:30), y se ejecuta desde el equipo del Consultor implementador (G7 20:56-21:27).",
    "Los clientes permiten instalar código Z y aplicar transportes (Sesión interna CM 16-Oct-26).",
    "La información se levantó en una sola sesión técnica grabada; los nombres técnicos inferidos de la transcripción están sujetos a las preguntas abiertas 2 y 11.",
    "El mecanismo de inyección es RFC con paquete Z y transportes, con respaldo UJBR para el modelo estándar (Sesión interna CM 16-Oct-26). Se valida con una prueba técnica en la arquitectura A antes del desarrollo.",
    "La matriz de soporte inicial son las versiones de los dos ambientes disponibles; otras versiones se agregan como variantes (RNF-Comp-04).",
    "Las tasas de cambio se toman de SAP y siguen NIC 21 (RN-11), sujeto a la pregunta abierta 3.",
    "Las reglas de consolidación de la sección 5.9 se generan con las funciones estándar de BPC (modelo de propiedad, reglas de negocio, asientos, bloqueo y flujo de proceso); su disponibilidad en la arquitectura B se confirma en la pregunta abierta 17."])
H(2, "12.2 Dependencias")
T([["Dependencia", "Responsable", "Fecha propuesta", "Impacto"],
   ["Acceso al ambiente de arquitectura A desde el equipo del consultor", "Consultor SAP BPC", "23-Oct-26", "Sin acceso no hay prueba técnica ni prototipo."],
   ["Documento de pasos de implementación y casos de uso existentes", "Consultor SAP BPC", "23-Oct-26", "Detalla RF-08 a RF-19 para las historias de usuario."],
   ["Catálogo de cerca de 50 reportes existentes", "Consultor SAP BPC", "23-Oct-26", "Necesario para cerrar la pregunta 6."],
   ["Usuario técnico y permiso para paquete Z y transportes en el ambiente de prueba", "Administrador SAP del cliente", "23-Oct-26", "Bloquea la prueba técnica del mecanismo de inyección."],
   ["Ambiente de arquitectura B para pruebas", "CM Consulting – Dirección", "Por definir", "Bloquea RNF-Comp-01 para la segunda arquitectura."],
   ["Ambiente BW/4HANA con BPC 2021 modelo estándar para pruebas", "CM Consulting – Dirección", "Por definir", "Bloquea RNF-Comp-05."]],
  [3000, 1800, 1400, 2638])
H(2, "12.3 Preguntas abiertas")
T([["#", "Pregunta", "Crítica", "Responsable", "Fecha objetivo"]] +
  [[str(i), t, "Sí" if c else "No", r, f] for i, (t, c, r, f) in enumerate(PREGUNTAS, 1)],
  [500, 4438, 900, 1800, 1200])
P("No quedan preguntas críticas abiertas. Las preguntas de esta tabla se resuelven durante las fases de UX y arquitectura sin cambiar el alcance.")
B.append({"type": "page_break"})
H(1, "13. Aprobación")
P("Mediante la firma del presente documento, el área de CM Consulting SAS responsable del producto aprueba esta línea base funcional. A partir de la firma, cualquier cambio a los requerimientos RF, RNF, RN e INT sigue el control de cambios formal.")
B += [{"type": "blank"}, {"type": "blank"}]
T([["Campo", "Información"], ["Área responsable del producto", ""], ["Nombre", ""], ["Cargo", ""], ["Fecha de aprobación", ""], ["Firma", ""]], [2800, 6038])

(OUT / "bloques.json").write_text(json.dumps({"blocks": B}, ensure_ascii=False, indent=2), encoding="utf-8")
print("RF", len(daf["rf"]), "RNF", len(RNF), "RN", len(RN), "INT", len(INT), "preguntas", len(PREGUNTAS))
