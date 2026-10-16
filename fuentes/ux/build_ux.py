"""Genera bloques.json del Documento UX (F4 · Modo 2) del Acelerador BPC."""
import json
from pathlib import Path

D = Path(__file__).parent
UXDIR = D.parent.parent / "docs" / "03-diseno-ux"
IMG = lambda name, w: {"type": "image", "path": str(UXDIR / ("capturas" if name.startswith("UX-") else "diagramas") / name), "width_cm": w, "align": "center"}
PROTO = "https://claude.ai/artifact/2jZdXEfCaLtmLW7zTrtqnV"

# código, nombre, rol, RF, prioridad, [(zona, comportamiento, rf/rn)], estados{normal,vacío,carga,error}, validaciones, responsive
P = [
 ("UX-01", "Clientes", "Consultor implementador", "RF-01, RF-04", "Alta",
  [("Tabla de clientes", "Lista cliente, arquitectura, paso actual, estado, última ejecución y usuario. Clic en el cliente abre su paso actual.", "RF-01, RF-04"),
   ("Botón Nuevo cliente", "Abre un diálogo con el campo Nombre del cliente; Crear lleva a UX-02.", "RF-01"),
   ("Buscar cliente", "Filtra la tabla por nombre.", "RF-01")],
  ["Normal: tabla con los clientes.", "Vacío: «Aún no hay clientes. Crea el primero para empezar.» con el botón Nuevo cliente.", "Carga: filas esqueleto.", "Error: «No se pudo cargar la lista de clientes» con Reintentar."],
  ["El nombre del cliente es obligatorio y no puede repetirse."],
  "En 360 px cada cliente se muestra como tarjeta con nombre, arquitectura y estado."),
 ("UX-02", "Conexiones", "Consultor implementador", "RF-01, RF-02, RF-04", "Alta",
  [("Tarjetas SAP ERP, SAP BW, SAP BPC", "Host, mandante, usuario técnico y credencial por sistema; Probar conexión muestra Conexión exitosa o Fallida con el mensaje del sistema.", "RF-01"),
   ("Conexión ERP–BW", "Botón Validar conexión ERP–BW solo en arquitectura A; en la B se reemplaza por una nota.", "RF-02, RN-01"),
   ("Arquitectura detectada", "Insignia A o B, versiones detectadas y estado Variante disponible.", "RF-04, RNF-Comp-04"),
   ("Credencial", "Siempre enmascarada; Mostrar/Ocultar. Se guarda cifrada.", "RNF-Seg-02"),
   ("Continuar al diagnóstico", "Habilitado solo cuando las conexiones pasan la prueba.", "RF-01")],
  ["Normal: tres conexiones exitosas y arquitectura detectada.", "Vacío: formulario sin datos y ayuda para pedir el usuario técnico al Administrador SAP del cliente.", "Carga: Probando… dentro de cada tarjeta.", "Error: la tarjeta del sistema que falla en rojo con el mensaje devuelto y qué revisar."],
  ["Host, mandante, usuario técnico y credencial son obligatorios.", "Validación ERP–BW solo cuando ERP y BW son sistemas separados (RN-01).", "Versión sin variante disponible: aviso «Versión no soportada aún» y no se puede continuar (RNF-Comp-04). No prototipado."],
  "En 360 px las tarjetas se apilan. Botones de 44 px de alto mínimo."),
 ("UX-03", "Diagnóstico e inventario", "Consultor implementador", "RF-03, RF-04, RF-09", "Alta",
  [("Indicador Bodega existente", "Indica si el cliente ya tiene bodega en BW.", "RF-03, RN-03"),
   ("Pestañas", "Extractores activos, InfoProviders (InfoCubos o aDSO según la versión de BW), Modelos BPC y Campos Z.", "RF-03, RF-04"),
   ("Campos Z", "Casillas para elegir los campos que se incorporan a la bodega; pasan al plan de generación.", "RF-09"),
   ("Volver a escanear", "Repite el inventario.", "RF-03")],
  ["Normal: inventario por pestaña.", "Vacío: mensaje por pestaña (p. ej. «El cliente no tiene bodega en BW: se generará completa»).", "Carga: barra de progreso «Escaneando BW y BPC…».", "Error: «El escaneo no terminó» con el paso que falló."],
  ["Si la bodega existe se usa la existente y no se recrea (RN-03)."],
  "En 360 px las tablas pasan a tarjetas; las pestañas se ajustan en varias líneas."),
 ("UX-04", "Módulos", "Consultor implementador", "RF-05, RF-23, RF-24, RF-25, RF-39", "Alta",
  [("Tarjetas de módulo", "Consolidación, Presupuesto, Activos fijos y Deterioros, cada una con casilla y lo que genera.", "RF-05, RF-23, RF-24, RF-25"),
   ("Versiones de presupuesto", "En la tarjeta de Presupuesto: presupuesto anual y proyecciones 3+9, 6+6 y 9+3, editables.", "RF-39"),
   ("Resumen", "N módulos · N reportes, se actualiza al marcar.", "RF-05"),
   ("Continuar", "Deshabilitado sin módulos marcados.", "RF-05")],
  ["Normal: cuatro módulos marcados.", "Vacío: ninguno marcado y aviso «Elige al menos un módulo».", "Carga: tarjetas esqueleto.", "Error: «No se pudo guardar la selección»."],
  ["Activos fijos y Deterioros muestran «Se queda en BW; no crea modelo en BPC» (RN-12)."],
  "En 360 px las tarjetas quedan en una columna."),
 ("UX-05", "Sociedades del grupo", "Consultor implementador", "RF-06, RF-13, RF-31", "Alta",
  [("Tabla de sociedades", "Código, nombre, en SAP (interruptor), moneda local, sociedad matriz, plan de cuentas, % de propiedad y método inicial.", "RF-06, RF-13, RF-31"),
   ("Agregar sociedad", "Agrega una fila.", "RF-06"),
   ("Aviso de monedas", "Siempre visible: la consolidación se presenta en moneda local y en dólares.", "RN-06")],
  ["Normal: tabla con las sociedades y la nota de monedas.", "Vacío: «Agrega las sociedades del grupo».", "Carga: filas esqueleto.", "Error: «No se pudieron guardar las sociedades» con la causa."],
  ["Solo sociedades del grupo; clientes y proveedores no son entidades de consolidación (RN-07).", "Cada sociedad puede tener su propio plan de cuentas (RN-05).", "Siempre se informa: «La consolidación se presenta en moneda local y en dólares» (RN-06).", "Una sociedad marcada como no SAP habilita UX-07.", "El % de propiedad (0 a 100) y el método son la estructura inicial; luego se mantienen por periodo en UX-13 (RN-13, RN-14)."],
  "En 360 px cada sociedad es una tarjeta con código y nombre como título."),
 ("UX-06", "Mapeo de cuentas", "Consultor implementador", "RF-07", "Alta",
  [("Lista de rubros", "Rubros con código, nombre y cantidad de cuentas mapeadas.", "RF-07"),
   ("Tabla de cuentas", "Cuenta, descripción, nodo de jerarquía y rubro asignado; filtros por sociedad y Solo sin mapear.", "RF-07, RN-05"),
   ("Asignar al rubro", "Asigna las cuentas marcadas al rubro seleccionado.", "RF-07"),
   ("Vista previa del rubro", "Muestra la suma de las cuentas mapeadas.", "RN-10"),
   ("Contador", "Cuentas sin mapear.", "RF-07")],
  ["Normal: rubro seleccionado con sus cuentas.", "Vacío: «Aún no hay cuentas mapeadas».", "Carga: esqueleto en la lista y la tabla.", "Error: «No se pudo guardar el mapeo»."],
  ["El valor del rubro es la suma de sus cuentas (RN-10).", "Las cuentas se filtran por el plan de cuentas de la sociedad (RN-05)."],
  "En 360 px primero la lista de rubros y debajo las cuentas del rubro elegido."),
 ("UX-07", "Carga de sociedades no SAP", "Consultor implementador", "RF-15", "Alta",
  [("Zona de carga", "Arrastrar y soltar o Elegir archivo, por sociedad no SAP.", "RF-15"),
   ("Vista previa", "Sociedad, cuenta, periodo, tercero, CeCo, CeBe e importe.", "RF-15"),
   ("Errores por fila", "Fila, columna y motivo, con enlace a Mapeo de cuentas.", "RF-15"),
   ("Confirmar carga", "Marca la sociedad como cargada.", "RF-15")],
  ["Normal: vista previa y lista de errores.", "Vacío: «Ninguna sociedad está marcada como no SAP»; el paso no aparece en la navegación.", "Carga: barra de progreso de subida.", "Error: tabla de errores por fila; Confirmar deshabilitado."],
  ["El archivo debe ser de una sociedad del grupo (RN-07).", "Cuentas que no están en el mapeo se marcan como «cuenta sin mapear».", "Formato del archivo por definir (DAF, pregunta 5)."],
  "En 360 px la vista previa se desplaza horizontalmente dentro de su contenedor."),
 ("UX-08", "Plan de generación", "Consultor implementador", "RF-08, RF-09, RF-10, RF-16, RF-18, RF-23, RF-24, RF-25, RF-26, RF-31, RF-32, RF-33, RF-35, RF-36, RF-37, RF-38, RF-39", "Alta",
  [("Sección SAP BW", "Extractores, InfoProviders, campos Z y cadenas de proceso, cada uno como Nuevo o Existente, no se modifica.", "RF-08, RF-09, RF-10, RF-24, RF-25"),
   ("Sección SAP BPC", "Modelo de consolidación, script en la variante de la arquitectura, importación (solo A), modelo de propiedad y plantilla C02, reglas de eliminación de inversión e interés minoritario, validación de cuadre, asientos de ajuste, bloqueo por sociedad, flujo de cierre y modelo de presupuesto con sus versiones.", "RF-16, RF-18, RF-23, RF-31, RF-32, RF-35 a RF-39"),
   ("Monedas y tasas", "Moneda local por sociedad, presentación en moneda local y dólares, tipos de tasa y cuenta de patrimonio para la diferencia en conversión.", "RF-26, RF-33"),
   ("Generar", "Abre la confirmación «Se crearán N objetos en el sistema del cliente» y lleva a UX-09.", "RF-08")],
  ["Normal: plan completo con total de objetos.", "Vacío: «Completa los pasos anteriores para ver el plan» con el paso faltante.", "Carga: «Calculando el plan…».", "Error: paso previo con error y enlace para corregirlo."],
  ["Objetos existentes no se recrean (RN-03).", "Scripts en variante de modelo estándar o embebido (RN-02).", "Full inicial y luego delta cada noche (RN-04).", "Tasas: cierre, promedio e histórica (RN-11, por confirmar en la pregunta 3 del DAF).", "Activos fijos y deterioros solo en BW (RN-12).", "Método por sociedad y periodo (RN-13); solo se importan sociedades cuadradas (RN-16); asientos solo cuadrados (RN-15); diferencia en conversión en patrimonio (RN-18)."],
  "En 360 px las secciones se apilan; las secciones son desplegables con teclado."),
 ("UX-09", "Ejecución y registro", "Consultor implementador", "RF-08, RF-10, RF-11, RF-12, RF-13, RF-14, RF-16, RF-17, RF-18, RF-19, RF-23, RF-24, RF-25, RF-26, RF-32, RF-33, RF-36", "Alta",
  [("Línea de pasos", "Objetos BW y carga full inicial, planes de cuentas, CeCo y CeBe, monedas y tasas, jerarquía, activos fijos y deterioros en BW (si se eligieron), modelo BPC, propiedad y scripts, validación de cuadre, importación a BPC (solo A), modelo de presupuesto (si se eligió), consolidación, eliminación de inversión e interés minoritario, y conversión a dólares con diferencia en conversión, con estado y duración.", "RF-08, RF-10, RF-11 a RF-14, RF-16 a RF-19, RF-23 a RF-26, RF-32, RF-33, RF-36"),
   ("Paso con error", "Mensaje con la causa (p. ej. falta de permiso de escritura en BPC), enlace a la pantalla que lo corrige y Reintentar desde este paso.", "RF-19"),
   ("Registro", "Fecha y hora, usuario, sistema, paso, objetos y resultado; Descargar registro.", "RNF-Aud-01"),
   ("Ver reportes", "Lleva a UX-10 al terminar.", "RF-20")],
  ["Normal: todos los pasos completos y resumen «Consolidación disponible en moneda local y en dólares».", "Vacío: «Aún no hay ejecuciones».", "Carga: paso en curso y los siguientes pendientes.", "Error: paso fallido en rojo con la causa y la acción."],
  ["Solo participan las sociedades del grupo (RN-07).", "Resultado en dos monedas (RN-06).", "Cada ejecución queda registrada (RNF-Aud-01)."],
  "En 360 px la línea de pasos y el registro se muestran uno debajo del otro; el registro pasa a tarjetas."),
 ("UX-10", "Reportes", "Consultor implementador", "RF-20, RF-27, RF-29, RF-34", "Alta",
  [("Filtro por módulo", "Todos, Consolidación, Presupuesto, Activos fijos, Deterioros (solo los módulos elegidos).", "RF-20, RF-27, RF-29"),
   ("Catálogo", "ID, reporte, módulo, fuente (BPC o BW), ola y estado, incluidos C01 a C04; clic en el ID abre la vista previa (UX-11, UX-13 o UX-15).", "RF-20, RF-27, RF-29, RF-34"),
   ("Generar seleccionados / Descargar reportes (.zip)", "Genera los Excel y descarga el paquete para el cliente.", "RF-20")],
  ["Normal: 34 reportes y plantillas con su estado.", "Vacío: «Ejecuta la generación para habilitar los reportes».", "Carga: progreso por reporte.", "Error: reporte con error y su causa."],
  ["Consolidación y presupuesto leen de BPC (RN-08); activos fijos y deterioros leen de BW (RN-12)."],
  "En 360 px cada reporte es una tarjeta con ID, nombre y estado."),
 ("UX-11", "Plantilla de reporte EPM", "Usuario financiero", "RF-20, RF-21, RF-22, RF-27, RF-34, RF-36, RF-39", "Alta",
  [("Cinta EPM", "Iniciar sesión, Actualizar, estado de conexión a BPC.", "RF-20"),
   ("Parámetros", "Reportes de consolidación: Sociedad, Incluir filiales, Moneda (local o dólares) y Periodo en celdas con lista. P01 usa la sociedad y la versión del presupuesto.", "RF-21"),
   ("Cuerpo del reporte", "Rubros con código y descripción; columnas por sociedad y total cuando incluye filiales.", "RF-21, RF-22"),
   ("Hojas", "R07 Efectivo y P01 Presupuesto frente a ejecución como modelos de la estructura común; P01 permite elegir la versión del presupuesto.", "RF-20, RF-27, RF-39"),
   ("C01 Conciliación intercompañía", "Por pareja de sociedades y cuentas: lo que registra cada parte, la diferencia y si supera el umbral.", "RF-34"),
   ("C03 Validación de cuadre", "Por sociedad: diferencia de balance, diferencia entre utilidad y resultado del ejercicio, cuentas mapeadas e importada o no.", "RF-36")],
  ["Normal: datos del reporte.", "Vacío: «Sin datos para la sociedad y el periodo elegidos».", "Carga: Actualizando… en la cinta y valores en espera.", "Error: «No se pudo conectar a BPC; revisa la conexión de EPM»."],
  ["Incluir filiales cambia entre una sociedad y la sociedad con sus filiales (RN-09).", "Moneda solo local o dólares (RN-06).", "Cada valor es la suma de las cuentas mapeadas (RN-10).", "C01 solo incluye sociedades del grupo (RN-07); el umbral sigue la pregunta 14 del DAF.", "C03: una sociedad sin cuadre o con cuentas sin mapear no se importa (RN-16)."],
  "Excel de escritorio; no aplica responsive. Contraste de encabezados blanco sobre azul corporativo."),
 ("UX-12", "Captura de presupuesto", "Usuario financiero", "RF-28, RF-39", "Alta",
  [("Parámetros", "Sociedad, versión (presupuesto anual, 3+9, 6+6 o 9+3) y año.", "RF-28, RF-39"),
   ("Grilla de captura", "Filas por cuenta y centro de costo, columnas por mes; celdas editables en amarillo, totales calculados.", "RF-28"),
   ("Guardar datos", "Guarda en el modelo de planeación de BPC; P01 refleja los valores.", "RF-28, RN-08")],
  ["Normal: grilla con valores.", "Vacío: «Sin presupuesto para esta versión: ingresa los valores».", "Carga: Guardando… en la cinta.", "Error: «No se guardaron N celdas» con las celdas marcadas en rojo."],
  ["Solo se aceptan números.", "Los totales no se editan.", "Se guarda en el modelo de planeación de BPC, no en BW (RN-08)."],
  "Excel de escritorio; no aplica responsive. Las celdas con error se marcan con color y borde, no solo con color."),
 ("UX-13", "Participaciones y métodos", "Usuario financiero", "RF-31", "Alta",
  [("Parámetros", "Grupo y periodo.", "RF-31"),
   ("Grilla C02", "Por sociedad: % control, % consolidación, % propiedad (editables) y método (lista: matriz, integración global, participación, fuera del perímetro).", "RF-31, RN-13"),
   ("Cambio frente al periodo anterior", "Muestra qué valor cambió y su valor anterior.", "RN-14, RNF-Aud-02"),
   ("Copiar del periodo anterior / Guardar datos", "Copia las participaciones del periodo anterior; Guardar escribe en el modelo de propiedad de BPC.", "RF-31")],
  ["Normal: grilla con las sociedades del grupo.", "Vacío: «Sin participaciones para este periodo: copia las del periodo anterior o ingrésalas».", "Carga: Guardando… en la cinta.", "Error: «No se guardó la fila 2000. Los porcentajes deben estar entre 0 y 100», con la celda marcada."],
  ["Porcentajes entre 0 y 100.", "Método obligatorio por sociedad (RN-13).", "Un cambio aplica desde el periodo elegido y no altera los anteriores (RN-14).", "Cada cambio queda registrado con usuario, fecha, valor anterior y nuevo (RNF-Aud-02)."],
  "Excel de escritorio; no aplica responsive. Las celdas con error se marcan con color y borde."),
 ("UX-14", "Asientos de ajuste", "Usuario financiero", "RF-35", "Alta",
  [("Encabezado", "Grupo, periodo y moneda del asiento.", "RF-35"),
   ("Líneas", "Sociedad, cuenta, intercompañía, débito, crédito y observación; Agregar línea.", "RF-35"),
   ("Totales", "Suma de débitos y créditos e indicador Cuadrado o Descuadrado con la diferencia.", "RN-15"),
   ("Guardar asiento", "Habilitado solo si el asiento cuadra.", "RN-15"),
   ("Asientos del periodo", "Número, descripción, usuario, fecha, valor y estado.", "RNF-Aud-02")],
  ["Normal: asiento cuadrado y lista de asientos del periodo.", "Vacío: «Aún no hay líneas: usa Agregar línea».", "Carga: Guardando… en la cinta.", "Error: «El asiento no cuadra» y aviso si una línea es de una sociedad bloqueada."],
  ["Débitos iguales a créditos para guardar (RN-15).", "No se aceptan líneas de sociedades bloqueadas en el periodo (RN-17).", "Cada asiento queda con usuario y fecha (RNF-Aud-02)."],
  "Excel o cliente web de BPC de escritorio; no aplica responsive. El estado Descuadrado usa texto e ícono, no solo color."),
 ("UX-15", "Estado del cierre", "Usuario financiero", "RF-37, RF-38", "Media",
  [("Matriz C04", "Por sociedad, el estado de cada paso del cierre en orden: carga, cuadre, conciliación intercompañía, conversión, consolidación y reportes.", "RF-38"),
   ("Paso con error", "Enlace al reporte que explica la causa (p. ej. C03 para cuadre).", "RF-38, RF-36"),
   ("Bloquear / Desbloquear", "Bloquea o desbloquea una sociedad en el periodo sin afectar a las demás.", "RF-37, RN-17"),
   ("Hojas", "Acceso a C02 Participaciones (UX-13) y a Asientos (UX-14).", "RF-38")],
  ["Normal: matriz con el avance del periodo.", "Vacío: «El cierre de Sep-2026 aún no ha iniciado».", "Carga: «Consultando el estado del cierre…».", "Error: aviso de sociedades que no cuadran, con el paso marcado y el enlace a C03."],
  ["Una sociedad bloqueada no admite cargas, asientos ni reprocesos del periodo (RN-17).", "Cada bloqueo y desbloqueo queda registrado (RNF-Aud-02)."],
  "Excel de escritorio; no aplica responsive. Estados con ícono y texto, no solo color."),
]

RF_UI = ["RF-%02d" % i for i in range(1, 40) if i != 30]
FLUJOS = [("D1_implementacion.png", "4.1 Implementación de un cliente nuevo (Consultor implementador)", 4.6,
           "Recorrido principal del configurador. Si la prueba de conexiones falla vuelve a UX-02; si hay sociedades no SAP pasa por UX-07."),
          ("D2_reintento.png", "4.2 Reintento tras un paso fallido (Consultor implementador)", 12.5,
           "Desde UX-09 el error enlaza con la pantalla que lo corrige (p. ej. Conexiones, si falta un permiso) y la ejecución se reanuda desde el paso fallido."),
          ("D3_reporte.png", "4.3 Consulta de un reporte (Usuario financiero)", 11,
           "El Usuario financiero elige parámetros y actualiza; sin conexión a BPC ve el error, sin datos ve el estado vacío."),
          ("D4_presupuesto.png", "4.4 Captura y seguimiento de presupuesto (Usuario financiero)", 10,
           "Valores no numéricos se marcan en rojo; al guardar, P01 muestra el presupuesto frente a la ejecución."),
          ("D5_cierre.png", "4.5 Cierre mensual del grupo (Usuario financiero)", 9,
           "Desde el estado del cierre revisa participaciones, cuadre y conciliación intercompañía; registra asientos de ajuste cuadrados, consolida y bloquea las sociedades.")]
PREG = [("Inicio de sesión del configurador: depende de dónde se aloje (DAF, pregunta 4). El prototipo no incluye pantalla de login.", "No", "CM Consulting – Arquitectura", "30-Oct-26"),
        ("Volumen típico de sociedades y cuentas por cliente para paginar UX-05 y UX-06 (DAF, pregunta 7). Supuesto: hasta 50 sociedades y 5.000 cuentas.", "No", "Consultor SAP BPC", "30-Oct-26"),
        ("Formato del archivo de sociedades no SAP para la vista previa de UX-07 (DAF, pregunta 5).", "No", "Consultor SAP BPC", "30-Oct-26"),
        ("Cómo leen BW con EPM los reportes de activos fijos y deterioros (DAF, pregunta 12). UX-11 muestra solo la conexión a BPC.", "No", "CM Consulting – Arquitectura", "30-Oct-26"),
        ("UX-11 y UX-12 suponen EPM add-in también en la arquitectura B (DAF, pregunta 13).", "No", "CM Consulting – Arquitectura", "30-Oct-26"),
        ("UX-14 y UX-15 suponen asientos, bloqueo y flujo de cierre disponibles en la arquitectura B (DAF, pregunta 17).", "No", "CM Consulting – Arquitectura", "30-Oct-26"),
        ("Umbral de la conciliación (C01) y tolerancia de cuadre (C03); el prototipo usa valores de ejemplo (DAF, pregunta 14).", "No", "Consultor SAP BPC", "30-Oct-26")]

B = []
H = lambda l, t: B.append({"type": "heading", "level": l, "text": t})
Pp = lambda t: B.append({"type": "paragraph", "text": t})
UL = lambda it: B.append({"type": "bullets", "items": it})
T = lambda rows, w: B.append({"type": "table", "rows": rows, "col_widths": w})

B += [{"type": "portada", "text": "DOCUMENTO DE DISEÑO UX — ACELERADOR DE CONSOLIDACIÓN FINANCIERA SAP BW/BPC"},
      {"type": "paragraph", "text": "Producto interno CM Consulting", "align": "center", "bold": True, "color": "144E8C", "size": 28},
      {"type": "page_break"}]
H(1, "Control de versiones")
T([["Versión", "Elaborado por", "Fecha", "Observaciones"], ["1.0", "CM Consulting", "16-Oct-26", "Versión para aprobación"], ["1.1", "CM Consulting", "16-Oct-26", "Alineado con el DAF v1.1: pantallas UX-13 a UX-15, flujo de cierre mensual y ajustes en UX-04, UX-05 y UX-08 a UX-12 para RF-31 a RF-39."]], [1200, 2200, 1500, 3938])
H(1, "Confidencialidad")
Pp([{"text": "La información contenida en este documento es propiedad intelectual de "}, {"text": "CM CONSULTING SAS", "bold": True}, {"text": "."}])
Pp([{"text": "Este documento es de uso interno de "}, {"text": "CM CONSULTING SAS", "bold": True}, {"text": ". Su reproducción, distribución o comunicación por cualquier medio queda expresamente prohibida sin el consentimiento escrito de "}, {"text": "CM CONSULTING SAS", "bold": True}, {"text": "."}])
B += [{"type": "page_break"}, {"type": "toc"}, {"type": "page_break"}]

H(1, "1. Introducción")
Pp([{"text": "Este documento es la línea base de diseño UX del Acelerador de consolidación financiera sobre SAP BW y SAP BPC, producto interno de "}, {"text": "CM", "bold": True}, {"text": "CONSULTING", "bold": True, "color": "F2B707"},
    {"text": ". Deriva del Documento de Análisis Funcional v1.1 (16-Oct-26) y del brief de diseño de la misma fecha. Las historias de usuario referencian los códigos UX-XX de este documento en sus criterios de aceptación."}])
Pp("Prototipo navegable: " + PROTO + " (privado; se comparte desde su menú Compartir). Permite recorrer las 15 pantallas, ver los estados normal, vacío, carga y error de cada una y cambiar entre la arquitectura A y la B. Los datos que muestra son de ejemplo.")
Pp("Alcance del diseño: dos superficies. El configurador web (UX-01 a UX-10), que usa el Consultor implementador, y las plantillas de Excel con EPM add-in y las funciones de BPC (UX-11 a UX-15), que usa el Usuario financiero.")

H(1, "2. Lineamientos visuales")
Pp("Se aplica la identidad base de CM Consulting; el producto es interno y no tiene sistema de diseño de cliente.")
T([["Elemento", "Definición"],
   ["Color primario", "Azul #144E8C (barra superior, botones primarios, encabezados de tabla)"],
   ["Acento", "Amarillo #F2B707, solo para resaltar (insignia de arquitectura, foco de teclado); nunca texto sobre blanco"],
   ["Títulos", "Azul #365F91"],
   ["Estados", "Verde (completo), ámbar (en curso o advertencia), rojo (error); siempre con ícono y texto, no solo color"],
   ["Tipografía", "Ubuntu (respaldo Segoe UI); Ubuntu Mono para códigos técnicos"],
   ["Componentes globales", "Barra superior con cliente activo e insignia de arquitectura; navegación lateral por pasos con estado; notificaciones; diálogos de confirmación"],
   ["Accesibilidad", "WCAG 2.1 AA (RNF-Usa-01): contraste 4,5:1, foco visible, controles de 44 px, etiquetas visibles"]],
  [2400, 6438])

H(1, "3. Mapa de navegación")
Pp("Pantallas agrupadas por rol. Las líneas punteadas son entregas de archivos, no navegación dentro de la aplicación.")
B.append(IMG("D0_navegacion.png", 9))

H(1, "4. Flujos de usuario por rol")
for img, titulo, w, txt in FLUJOS:
    H(2, titulo)
    Pp(txt)
    B.append(IMG(img, w))

H(1, "5. Inventario de pantallas")
T([["Código", "Pantalla", "Rol", "RF que soporta", "Prioridad"]] + [[p[0], p[1], p[2], p[3], p[4]] for p in P], [1000, 2200, 1900, 2738, 1000])

H(1, "6. Especificación por pantalla")
for i, p in enumerate(P, 1):
    cod, nom, rol, rf, pri, comps, estados, vals, resp = p
    B.append({"type": "page_break"})
    H(2, f"6.{i} {cod} {nom}")
    Pp([{"text": "Rol: ", "bold": True}, {"text": rol}, {"text": " · RF: ", "bold": True}, {"text": rf}])
    B.append(IMG(f"{cod}.png", 15.5))
    T([["Zona / componente", "Comportamiento", "RF / RN"]] + [list(c) for c in comps], [2200, 4838, 1800])
    H(3, "Estados")
    UL(estados)
    H(3, "Validaciones visibles")
    UL(vals)
    H(3, "Responsive y accesibilidad")
    Pp(resp)

B.append({"type": "page_break"})
H(1, "7. Matriz RF ↔ UX")
cov = {r: [p[0] for p in P if r in [x.strip() for x in p[3].split(",")]] for r in RF_UI}
T([["RF con interfaz", "Pantallas"]] + [[r, ", ".join(cov[r])] for r in RF_UI], [3000, 5838])
H(2, "7.1 Comportamientos de diseño derivados")
Pp("No amplían el alcance del DAF. Si se consideran alcance nuevo, pasan por control de cambios.")
T([["Comportamiento", "Pantalla", "Se deriva de"], ["Gestión de varios clientes (lista, búsqueda, nombre único)", "UX-01", "RF-01 y supuesto 12.1 del DAF (producto reutilizable en varios clientes)"], ["Reintentar desde el paso fallido", "UX-09", "RF-19 y el registro de RNF-Aud-01"], ["Descargar registro", "UX-09", "RNF-Aud-01 (consulta del registro)"], ["Aviso «Versión no soportada aún»", "UX-02", "RNF-Comp-04 (no prototipado)"]], [3200, 1300, 4338])
Pp("RF sin interfaz: RF-30 (la información de activos fijos y deterioros alimenta R08, R10 y R11 de forma automática; se ve en esos reportes). Los 38 RF con interfaz tienen al menos una pantalla.")

H(1, "8. Preguntas abiertas de diseño")
T([["#", "Pregunta", "Crítica", "Responsable", "Fecha objetivo"]] + [[str(i), *q] for i, q in enumerate(PREG, 1)], [500, 4438, 900, 1800, 1200])
Pp("Ninguna pregunta bloquea el arranque de las historias de usuario.")

B.append({"type": "page_break"})
H(1, "9. Aprobación")
Pp("Mediante la firma del presente documento, el área de CM Consulting SAS responsable del producto aprueba la navegación, la estructura de pantallas y su comportamiento. Los ajustes posteriores siguen el control de cambios.")
B += [{"type": "blank"}, {"type": "blank"}]
T([["Campo", "Información"], ["Área responsable del producto", ""], ["Nombre", ""], ["Cargo", ""], ["Fecha de aprobación", ""], ["Firma", ""]], [2800, 6038])

missing = [r for r in RF_UI if not cov[r]]
print("RF sin pantalla:", missing)
(D / "bloques_ux.json").write_text(json.dumps({"blocks": B}, ensure_ascii=False, indent=1), encoding="utf-8")
print("bloques:", len(B))
