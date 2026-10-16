"""Genera backlog.json (validador) y bloques_hu.json (skill base) de las Historias de Usuario del Acelerador BPC."""
import json
from pathlib import Path

D = Path(__file__).parent
ROOT = D.parent.parent
DAF = json.loads((ROOT / "fuentes" / "daf" / "daf.json").read_text(encoding="utf-8"))
UXI = json.loads((ROOT / "fuentes" / "ux" / "ux_inventario.json").read_text(encoding="utf-8"))

CI, AS, UF, SI = "Consultor implementador", "Administrador SAP del cliente", "Usuario financiero", "Sistema"

EPICAS = [
    ("E1", "Conexión y diagnóstico", "Prueba técnica del mecanismo de inyección, gestión de clientes, conexiones seguras a ERP, BW y BPC, identificación de la arquitectura e inventario de lo que ya existe en el cliente."),
    ("E2", "Configuración de alcance", "Lo que el Consultor implementador define por cliente antes de generar: módulos, sociedades, estructura inicial de participaciones, mapeo de cuentas y versiones de presupuesto."),
    ("E3", "Bodega de datos en BW", "Plan de generación, motor de ejecución paso a paso y creación de la bodega financiera en BW 7.5 y BW/4HANA, con campos Z, cadenas full y delta, activos fijos y deterioros."),
    ("E4", "Datos maestros", "Carga de planes de cuentas, CeCo y CeBe, monedas y tasas, jerarquía de cuentas y la información de las sociedades no SAP."),
    ("E5", "Modelo BPC y consolidación", "Dimensiones, importación desde BW, scripts por variante, eliminación intercompañía, conversión con diferencia en conversión, modelo de propiedad, eliminación de inversión, interés minoritario y coligadas."),
    ("E6", "Control del cierre", "Lo que usa el Usuario financiero cada mes: participaciones por periodo, validación de cuadre, conciliación intercompañía, asientos de ajuste, bloqueo por sociedad y estado del cierre."),
    ("E7", "Reportes EPM de consolidación", "Plantilla común de reportes con EPM add-in, cálculo de rubros por mapeo, los 20 reportes de consolidación por olas y el catálogo de descarga."),
    ("E8", "Presupuesto, activos fijos y deterioros", "Modelo de planeación de presupuesto con sus versiones, captura P05, reportes P01 a P04 y reportes de activos fijos y deterioros que leen de BW."),
    ("E9", "Seguridad, auditoría y compatibilidad", "Capacidades transversales: registro de ejecuciones, controles ASVS nivel 2, accesibilidad, rechazo controlado de la arquitectura B (fuera de la versión 1) y certificación de la variante BW/4HANA."),
]

# (código, épica, rol, título, quiero, para, criterios, prioridad, SP, dependencias, trazabilidad, ux, nota_prioridad)
H = [
 # ---------------- E1 ----------------
 ("HU-01", "E1", CI, "Spike: prueba técnica de inyección en la arquitectura A",
  "una prueba técnica documentada que cree un objeto de prueba en BW y en BPC por RFC con paquete Z, UJFS, UJKT y respaldo UJBR",
  "confirmar que el acelerador puede crear objetos en el sistema del cliente antes de construir los generadores",
  ["La prueba se ejecuta en el ambiente de arquitectura A con el usuario técnico del cliente, sin permisos SAP_ALL (RNF-Seg-03).",
   "Se crea por RFC un objeto de prueba en BW y un miembro de dimensión en BPC, y se verifica su existencia desde SAP GUI.",
   "Se genera y restaura un respaldo UJBR del ambiente BPC de prueba.",
   "El entregable es una nota técnica con el resultado, los permisos mínimos requeridos y la recomendación (seguir o cambiar de mecanismo).",
   "La duración está limitada a un sprint; si el mecanismo falla, la decisión se escala antes de iniciar E3."],
  "Alta", 2, [], ["INT-02", "INT-03", "RNF-Seg-03"], [], ""),
 ("HU-02", "E1", CI, "Gestionar clientes",
  "crear un cliente, verlo en una lista con su arquitectura y paso actual, y buscarlo por nombre",
  "llevar varias implementaciones con el mismo acelerador sin mezclar su configuración",
  ["En la pantalla UX-01 se listan los clientes con arquitectura, paso actual, estado, última ejecución y usuario.",
   "El botón Nuevo cliente pide el nombre; el nombre es obligatorio y no puede repetir uno existente.",
   "Al crear el cliente, el sistema lleva a UX-02 Conexiones.",
   "Sin clientes se muestra «Aún no hay clientes. Crea el primero para empezar.»",
   "La búsqueda filtra la lista por nombre.",
   "En el prototipo, el configurador se ejecuta en el equipo del Consultor implementador sin servidores adicionales (RNF-Comp-03)."],
  "Alta", 3, [], ["RF-01", "RNF-Comp-03"], ["UX-01"], ""),
 ("HU-03", "E1", CI, "Registrar y probar conexiones a SAP",
  "registrar host, mandante, usuario técnico y credencial de ERP, BW y BPC y probar cada conexión",
  "saber antes de generar si el acelerador puede llegar a cada sistema del cliente",
  ["En la pantalla UX-02 cada sistema tiene su tarjeta con host, mandante, usuario técnico y credencial, todos obligatorios.",
   "Probar conexión muestra «Conexión exitosa» o «Fallida» con el mensaje que devuelve SAP.",
   "Continuar al diagnóstico solo se habilita con las conexiones requeridas en estado exitoso.",
   "Cada prueba queda en el registro de ejecuciones (RNF-Aud-01)."],
  "Alta", 5, ["HU-01", "HU-02"], ["RF-01"], ["UX-02"], ""),
 ("HU-04", "E1", CI, "Proteger las credenciales de conexión",
  "que las credenciales de SAP se guarden cifradas y se muestren enmascaradas",
  "evitar que alguien con acceso al equipo lea las claves de los sistemas del cliente",
  ["En UX-02 la credencial se muestra enmascarada y solo se revela con Mostrar.",
   "Ningún archivo de configuración ni registro contiene la credencial legible (RNF-Seg-02).",
   "La documentación del rol técnico lista los permisos mínimos por paso, sin SAP_ALL (RNF-Seg-03).",
   "Una credencial inválida se reporta sin escribirla en el mensaje de error."],
  "Alta", 3, ["HU-03"], ["RNF-Seg-02", "RNF-Seg-03"], ["UX-02"], ""),
 ("HU-05", "E1", CI, "Validar la conexión ERP–BW",
  "validar que BW recibe datos del ERP antes de iniciar las cargas",
  "no descubrir en mitad de la generación que los extractores no pueden llegar a BW",
  ["En UX-02 el botón Validar conexión ERP–BW aparece solo si ERP y BW están en sistemas separados (RN-01).",
   "El resultado de la validación queda registrado con fecha, usuario y mensaje.",
   "Si la validación falla, se muestra el mensaje del sistema y Continuar al diagnóstico queda deshabilitado."],
  "Alta", 3, ["HU-03"], ["RF-02"], ["UX-02"], ""),
 ("HU-06", "E1", CI, "Identificar arquitectura y versión",
  "que el acelerador detecte si el cliente opera la arquitectura A o B y qué versiones de BW y BPC tiene",
  "que los pasos siguientes usen la variante de generación correcta",
  ["En UX-02 se muestra la insignia A o B y las versiones detectadas de BW y BPC.",
   "La arquitectura A reconoce BW 7.5 y BW/4HANA con BPC modelo estándar (RNF-Comp-05).",
   "Si la versión no tiene variante disponible se muestra «Versión no soportada aún» y no se puede continuar (RNF-Comp-04).",
   "La arquitectura y la versión quedan guardadas en el cliente y se ven en UX-01."],
  "Alta", 5, ["HU-03"], ["RF-04", "RNF-Comp-04", "RNF-Comp-05"], ["UX-02", "UX-01"], ""),
 ("HU-07", "E1", CI, "Inventariar objetos existentes",
  "ver los extractores activos, InfoProviders, modelos BPC y campos Z que ya existen en el cliente",
  "decidir qué generar y no recrear lo que el cliente ya tiene",
  ["En la pantalla UX-03 el inventario se muestra en pestañas: extractores, InfoProviders (InfoCubos o aDSO según la versión), modelos BPC y campos Z.",
   "El indicador Bodega existente se marca si BW ya tiene la bodega; en ese caso no se recrea (RN-03).",
   "Volver a escanear repite el inventario y actualiza la fecha del último escaneo.",
   "Si el escaneo falla se muestra el paso que falló y el mensaje del sistema."],
  "Alta", 8, ["HU-01", "HU-06"], ["RF-03"], ["UX-03"], ""),
 # ---------------- E2 ----------------
 ("HU-08", "E2", CI, "Seleccionar módulos",
  "marcar los módulos a implementar: consolidación, presupuesto, activos fijos y deterioros",
  "generar solo lo que el cliente contrató",
  ["En la pantalla UX-04 cada módulo es una tarjeta con casilla y descripción de lo que genera.",
   "El resumen muestra la cantidad de módulos y de reportes y se actualiza al marcar.",
   "Continuar está deshabilitado sin módulos marcados.",
   "Activos fijos y deterioros muestran «Se queda en BW; no crea modelo en BPC» (RN-12)."],
  "Alta", 2, ["HU-02"], ["RF-05"], ["UX-04"], ""),
 ("HU-09", "E2", CI, "Registrar sociedades del grupo",
  "registrar las sociedades a consolidar con su moneda local, sociedad matriz, plan de cuentas y si están en SAP",
  "definir el perímetro de la consolidación",
  ["En la pantalla UX-05 la tabla permite agregar y editar sociedades con código, nombre, en SAP, moneda local, matriz y plan de cuentas.",
   "Solo se registran sociedades del grupo; clientes y proveedores no son entidades (RN-07).",
   "Cada sociedad puede tener su propio plan de cuentas (RN-05).",
   "La moneda local es obligatoria; si falta, se muestra «La sociedad X no tiene moneda local» y no se guarda.",
   "Una sociedad marcada como no SAP habilita el paso UX-07."],
  "Alta", 5, ["HU-02"], ["RF-06", "RF-13"], ["UX-05"], ""),
 ("HU-10", "E2", CI, "Registrar la estructura inicial de participaciones",
  "registrar para cada sociedad el porcentaje de propiedad y el método de consolidación inicial",
  "que el modelo de propiedad nazca con la estructura societaria real del grupo",
  ["En UX-05 cada sociedad tiene % de propiedad (0 a 100) y método: matriz, integración global, participación o fuera del perímetro (RN-13).",
   "Solo una sociedad del grupo puede tener método matriz.",
   "Un porcentaje fuera de 0 a 100 se marca en la fila y no se guarda.",
   "La estructura se usa como primer periodo del modelo de propiedad (HU-32)."],
  "Alta", 3, ["HU-09"], ["RF-31"], ["UX-05"], ""),
 ("HU-11", "E2", CI, "Mapear cuentas a rubros de reporte",
  "asignar las cuentas contables de cada sociedad a los rubros de los reportes",
  "que los reportes muestren cada rubro con las cuentas correctas",
  ["En la pantalla UX-06 se elige un rubro y se le asignan cuentas marcadas, filtradas por el plan de cuentas de la sociedad (RN-05).",
   "La vista previa del rubro muestra la suma de sus cuentas mapeadas (RN-10).",
   "El contador muestra las cuentas sin mapear y el filtro Solo sin mapear las lista.",
   "Las cuentas se leen del ERP del cliente por la conexión registrada en HU-03.",
   "Si el guardado falla se informa cuántas asignaciones no se guardaron."],
  "Alta", 8, ["HU-07", "HU-09"], ["RF-07"], ["UX-06"], ""),
 ("HU-12", "E2", CI, "Definir versiones de presupuesto",
  "definir las versiones del modelo de planeación: presupuesto anual y proyecciones como 3+9, 6+6 y 9+3",
  "que el cliente pueda planear y reproyectar el año sin crear modelos nuevos",
  ["En UX-04, si Presupuesto está marcado, se muestran las versiones propuestas y se pueden agregar, renombrar o quitar.",
   "Debe existir al menos la versión de presupuesto anual.",
   "Los códigos de versión no se repiten.",
   "Las versiones pasan al plan de generación (UX-08)."],
  "Media", 2, ["HU-08"], ["RF-39"], ["UX-04", "UX-08"], ""),
 # ---------------- E3 ----------------
 ("HU-13", "E3", CI, "Revisar el plan de generación",
  "ver todo lo que el acelerador va a crear en BW y BPC antes de ejecutar",
  "confirmar el alcance con el cliente y evitar cambios no esperados en sus sistemas",
  ["En la pantalla UX-08 el plan se agrupa en SAP BW, SAP BPC y Monedas y tasas, y marca cada objeto como Nuevo o Existente.",
   "Los objetos existentes no se recrean ni se modifican (RN-03).",
   "El total de objetos a crear se muestra y Generar pide confirmación con ese número.",
   "Si un paso anterior está incompleto, el plan muestra cuál falta con un enlace para completarlo."],
  "Alta", 5, ["HU-07", "HU-08", "HU-11"], ["RF-08", "RF-16", "RF-18"], ["UX-08"], ""),
 ("HU-14", "E3", CI, "Ejecutar el plan paso a paso",
  "que la generación se ejecute en orden, con el estado y la duración de cada paso, y reintentar desde el paso que falló",
  "no repetir lo que ya quedó bien cuando un paso falla",
  ["En la pantalla UX-09 la línea de pasos muestra cada paso como completo, en curso, pendiente o con error, y su duración.",
   "Un paso con error muestra la causa, el enlace a la pantalla que la corrige y Reintentar desde este paso.",
   "Reintentar no vuelve a ejecutar los pasos completos.",
   "Al terminar se muestra el resumen y el botón Ver reportes (UX-10)."],
  "Alta", 8, ["HU-13"], ["RF-08", "RNF-Aud-01"], ["UX-09"], ""),
 ("HU-15", "E3", SI, "Generar la bodega financiera en BW 7.5",
  "activar el extractor financiero estándar y crear los InfoCubos de movimientos financieros si la bodega no existe",
  "tener la base de datos de la consolidación sin construirla a mano",
  ["Si el inventario indica que no hay bodega, se activa 0FI_GL_14 y se crean los InfoProviders del módulo de consolidación.",
   "Si la bodega existe, el paso queda como Existente y no crea objetos (RN-03).",
   "Cada objeto creado queda en el registro con su nombre técnico (RNF-Aud-01).",
   "El resultado se ve en UX-09 con el número de objetos creados."],
  "Alta", 8, ["HU-01", "HU-14"], ["RF-08", "INT-01"], ["UX-09"], ""),
 ("HU-16", "E3", SI, "Generar la bodega en la variante BW/4HANA",
  "crear la bodega con aDSO en lugar de InfoCubos cuando el cliente opera BW/4HANA con BPC 2021",
  "soportar a los clientes que migran desde BW 7.5, cuyo mantenimiento termina",
  ["Si la versión detectada es BW/4HANA, la generación usa la variante BW/4HANA sin cambiar la configuración del cliente (RNF-Comp-04).",
   "Los InfoProviders se crean como aDSO; no se genera ningún InfoCubo (RNF-Comp-05).",
   "Los extractores se conectan por ODP.",
   "El plan UX-08 muestra «aDSO» en los InfoProviders de esta variante."],
  "Alta", 8, ["HU-06", "HU-15"], ["RF-08", "RNF-Comp-05"], ["UX-08", "UX-09"], ""),
 ("HU-17", "E3", CI, "Incorporar campos Z a la bodega",
  "elegir los campos Z del cliente que se agregan a la bodega",
  "conservar en la consolidación la información propia del cliente",
  ["En UX-03, pestaña Campos Z, se marcan los campos a incorporar.",
   "Los campos marcados aparecen como Nuevo en el plan UX-08.",
   "Un campo Z sin tipo de dato compatible se marca con el motivo y no se incorpora."],
  "Media", 3, ["HU-07", "HU-15"], ["RF-09"], ["UX-03", "UX-08"], ""),
 ("HU-18", "E3", SI, "Generar cadenas de proceso full y delta",
  "crear las cadenas que cargan el histórico una vez y después la delta cada noche",
  "que los reportes tengan los movimientos del día anterior sin intervención manual",
  ["Se crea una cadena de carga full inicial y una cadena delta programada cada 24 horas (RN-04).",
   "Después de la carga delta, la antigüedad de los datos es de 24 horas o menos (RNF-Rend-01).",
   "Las cadenas no usan borrado de peticiones solapadas que duplique datos.",
   "La ejecución de la carga full se ve como paso en UX-09."],
  "Alta", 8, ["HU-15"], ["RF-10", "RNF-Rend-01"], ["UX-09"], ""),
 ("HU-19", "E3", SI, "Generar el módulo de activos fijos en BW",
  "activar el extractor de activos fijos y crear sus InfoProviders y cargas si el módulo está seleccionado",
  "alimentar los reportes de activos fijos sin crear un modelo en BPC",
  ["Solo se genera si Activos fijos está marcado en UX-04.",
   "Se activa el extractor estándar de activos fijos y se crean sus InfoProviders y cargas en BW, sin modelo en BPC (RN-12).",
   "El plan UX-08 y la ejecución UX-09 muestran el paso «Activos fijos en BW»."],
  "Alta", 5, ["HU-08", "HU-18"], ["RF-24"], ["UX-08", "UX-09"], ""),
 ("HU-20", "E3", SI, "Generar el módulo de deterioros en BW",
  "activar el extractor de deterioros y crear sus InfoProviders y cargas si el módulo está seleccionado",
  "alimentar los reportes de deterioros sin crear un modelo en BPC",
  ["Solo se genera si Deterioros está marcado en UX-04.",
   "Se activa el extractor estándar de deterioros y se crean sus InfoProviders y cargas en BW, sin modelo en BPC (RN-12).",
   "El plan UX-08 y la ejecución UX-09 muestran el paso «Deterioros en BW»."],
  "Alta", 5, ["HU-08", "HU-18"], ["RF-25"], ["UX-08", "UX-09"], ""),
 # ---------------- E4 ----------------
 ("HU-21", "E4", SI, "Cargar los planes de cuentas",
  "cargar en BW el plan de cuentas de cada sociedad seleccionada",
  "que cada sociedad conserve su propio plan de cuentas en la bodega",
  ["Se carga el plan de cuentas de cada sociedad registrada en UX-05 (RN-05).",
   "El paso «Planes de cuentas» se ve en UX-09 con la cantidad de cuentas cargadas.",
   "Una sociedad sin plan de cuentas en el ERP se reporta como error del paso con su código."],
  "Alta", 5, ["HU-09", "HU-15"], ["RF-11"], ["UX-09"], ""),
 ("HU-22", "E4", SI, "Cargar centros de costo y de beneficio",
  "cargar en BW los centros de costo y de beneficio de las sociedades seleccionadas",
  "permitir el detalle por centro en los reportes y el presupuesto",
  ["Se cargan CeCo y CeBe de todas las sociedades seleccionadas.",
   "El paso se ve en UX-09 con las cantidades cargadas.",
   "Un error de carga muestra la sociedad y el mensaje del sistema."],
  "Alta", 3, ["HU-21"], ["RF-12"], ["UX-09"], ""),
 ("HU-23", "E4", SI, "Cargar monedas y tasas de cambio",
  "cargar la moneda local de cada sociedad y las tasas de cierre, promedio e histórica frente al dólar",
  "convertir la consolidación a la otra moneda de presentación",
  ["La moneda local de cada sociedad se toma tal como la maneja SAP (RN-06).",
   "Se cargan tasas de cierre, promedio e histórica por moneda y periodo (RN-11).",
   "Una moneda sin tasa para el periodo se reporta como error del paso en UX-09.",
   "UX-08 muestra la moneda local por sociedad y los tipos de tasa."],
  "Alta", 5, ["HU-09", "HU-15"], ["RF-13"], ["UX-08", "UX-09"], ""),
 ("HU-24", "E4", SI, "Cargar la jerarquía de cuentas",
  "cargar en BW la jerarquía de cuentas del cliente",
  "agrupar las cuentas en los niveles que usan los reportes",
  ["Se carga la jerarquía de cuentas desde el ERP.",
   "Una cuenta en la jerarquía que no existe en el plan de cuentas se reporta en el paso.",
   "El paso se ve en UX-09 con la cantidad de nodos cargados."],
  "Alta", 3, ["HU-21"], ["RF-14"], ["UX-09"], ""),
 ("HU-25", "E4", CI, "Cargar la información de sociedades no SAP",
  "subir el archivo contable de cada sociedad no SAP, ver la vista previa y los errores por fila",
  "que las sociedades fuera de SAP entren al mismo modelo que las demás",
  ["En la pantalla UX-07 se sube un archivo por sociedad no SAP y se ve la vista previa con sociedad, cuenta, periodo, tercero, CeCo, CeBe e importe.",
   "Los errores se listan por fila, columna y motivo; una cuenta fuera del mapeo se marca como «cuenta sin mapear».",
   "Confirmar carga se habilita solo sin errores.",
   "El archivo debe ser de una sociedad del grupo registrada (RN-07).",
   "La carga pasa por la validación de cuadre de HU-36 antes de llegar a BPC."],
  "Alta", 8, ["HU-09", "HU-11"], ["RF-15", "INT-04"], ["UX-07"], ""),
 # ---------------- E5 ----------------
 ("HU-26", "E5", SI, "Configurar las dimensiones del modelo de consolidación",
  "crear en BPC las dimensiones del modelo de consolidación y cargar sus miembros con los datos del cliente",
  "tener el modelo listo para recibir los datos financieros",
  ["Se crean las dimensiones definidas en arquitectura, entre ellas cuentas, entidades, intercompañía y terceros.",
   "Los miembros se cargan desde los datos maestros de E4.",
   "UX-08 lista el modelo de consolidación como objeto a crear y UX-09 muestra el paso.",
   "Un miembro rechazado por BPC se reporta con su código."],
  "Alta", 8, ["HU-01", "HU-21", "HU-22", "HU-23", "HU-24"], ["RF-16"], ["UX-08", "UX-09"], ""),
 ("HU-27", "E5", SI, "Importar los datos de BW a BPC",
  "importar al modelo BPC los datos del InfoProvider de BW en el modelo estándar",
  "consolidar con la información que ya está en la bodega",
  ["Aplica a la arquitectura A, la única soportada en la versión 1 (RN-19).",
   "La importación se genera como paquete de Data Manager y se ejecuta por sociedad y periodo.",
   "Solo se importan las sociedades que pasan la validación de cuadre (HU-36).",
   "El paso «Importación a BPC» se ve en UX-09 con la cantidad de registros."],
  "Alta", 5, ["HU-18", "HU-26"], ["RF-17", "INT-05"], ["UX-09"], ""),
 ("HU-28", "E5", SI, "Desplegar los scripts de consolidación por variante",
  "desplegar en BPC los scripts completados con los datos del cliente en la variante de su arquitectura",
  "no reescribir la lógica de consolidación en cada cliente",
  ["El script usa la variante de BPC modelo estándar (RN-02); la variante embebida queda fuera de la versión 1 (RN-19).",
   "Los códigos de sociedades y cuentas del cliente se insertan sin edición manual.",
   "UX-08 muestra la variante que se va a desplegar.",
   "Antes de reemplazar un script existente se genera respaldo UJBR."],
  "Alta", 8, ["HU-26"], ["RF-18"], ["UX-08", "UX-09"], ""),
 ("HU-29", "E5", CI, "Ejecutar la consolidación con eliminación intercompañía",
  "ejecutar la consolidación del grupo eliminando las transacciones entre sus sociedades",
  "dejar el resultado consolidado consultable en BPC",
  ["Solo participan las sociedades del grupo (RN-07).",
   "Las transacciones intercompañía entre sociedades con integración global se eliminan.",
   "El paso «Consolidación» en UX-09 muestra el resultado; si falla, muestra la causa y Reintentar desde este paso.",
   "El resultado queda consultable en R05 Hoja de eliminaciones intercompañía."],
  "Alta", 8, ["HU-27", "HU-28"], ["RF-19"], ["UX-09"], ""),
 ("HU-30", "E5", SI, "Convertir a la otra moneda de presentación",
  "convertir los saldos de cada sociedad a la otra moneda de presentación con las tasas cargadas",
  "presentar la consolidación en moneda local y en dólares",
  ["Balance con tasa de cierre, resultados con tasa promedio y patrimonio con tasa histórica (RN-11).",
   "La consolidación queda disponible en ambas monedas (RN-06).",
   "UX-09 muestra el resumen «Consolidación disponible en moneda local y en dólares».",
   "Si falta una tasa del periodo, el paso falla e indica la moneda."],
  "Alta", 5, ["HU-23", "HU-29"], ["RF-26"], ["UX-09"], ""),
 ("HU-31", "E5", SI, "Registrar la diferencia en conversión",
  "calcular la diferencia que deja aplicar tasas distintas y registrarla en la cuenta de patrimonio definida",
  "que balance y resultados cuadren después de convertir",
  ["La diferencia se registra en la cuenta de patrimonio configurada, no en resultados (RN-18).",
   "Después de convertir, activo menos pasivo y patrimonio da cero en la moneda de presentación.",
   "UX-08 muestra la cuenta de destino y UX-09 el paso con su resultado."],
  "Alta", 5, ["HU-30"], ["RF-33"], ["UX-08", "UX-09"], ""),
 ("HU-32", "E5", SI, "Generar el modelo de propiedad",
  "generar en BPC el modelo de propiedad con la estructura inicial y la plantilla C02",
  "que el cliente mantenga sus participaciones sin construir el modelo a mano",
  ["Se crea el modelo de propiedad con la estructura registrada en HU-10 como primer periodo.",
   "Se genera la plantilla C02 conectada al modelo de propiedad.",
   "UX-08 lista el modelo de propiedad y la plantilla C02 como objetos a crear.",
   "C02 aparece en el catálogo de UX-10."],
  "Alta", 5, ["HU-10", "HU-26"], ["RF-31"], ["UX-08", "UX-10"], ""),
 ("HU-33", "E5", SI, "Eliminar inversión y calcular interés minoritario",
  "eliminar la inversión de la matriz contra el patrimonio de cada subsidiaria con integración global y calcular el interés minoritario",
  "presentar estados consolidados que cumplan NIIF 10",
  ["Aplica a las sociedades con método integración global en el periodo (RN-13).",
   "Usa los porcentajes del periodo; un cambio de porcentaje no altera periodos anteriores (RN-14).",
   "El interés minoritario se presenta separado en el patrimonio consolidado.",
   "UX-09 muestra el paso «Eliminación de inversión e interés minoritario».",
   "La diferencia entre inversión y patrimonio eliminado queda en una cuenta identificable."],
  "Alta", 8, ["HU-29", "HU-32"], ["RF-32"], ["UX-08", "UX-09"], ""),
 ("HU-34", "E5", SI, "Aplicar el método de participación a coligadas",
  "reconocer la proporción del resultado de las coligadas sin eliminar sus transacciones intercompañía",
  "incluir las coligadas en la consolidación según NIC 28",
  ["Las sociedades con método participación no se integran línea a línea (RN-13).",
   "Se reconoce la proporción del resultado según el porcentaje de propiedad del periodo.",
   "Las transacciones con coligadas no se eliminan.",
   "Las sociedades fuera del perímetro no participan en el periodo."],
  "Alta", 5, ["HU-33"], ["RF-19"], ["UX-09"], ""),
 # ---------------- E6 ----------------
 ("HU-35", "E6", UF, "Mantener participaciones y métodos por periodo",
  "registrar cada mes en la plantilla C02 los porcentajes de control, consolidación y propiedad y el método de cada sociedad",
  "que la consolidación del periodo use la estructura societaria vigente",
  ["En la pantalla UX-13 se elige grupo y periodo y se editan % control, % consolidación, % propiedad y método por sociedad.",
   "Copiar del periodo anterior trae las participaciones del mes previo.",
   "Los porcentajes deben estar entre 0 y 100 y el método es obligatorio; un error marca la celda y no guarda la fila.",
   "Un cambio aplica desde el periodo elegido y no altera los anteriores (RN-14).",
   "Cada cambio queda registrado con usuario, fecha, valor anterior y nuevo (RNF-Aud-02)."],
  "Alta", 5, ["HU-32"], ["RF-31", "RNF-Aud-02"], ["UX-13"], ""),
 ("HU-36", "E6", SI, "Validar cuadre antes de importar",
  "validar cada sociedad y periodo antes de pasar los datos a BPC",
  "no consolidar sociedades descuadradas ni con cuentas sin mapear",
  ["El paquete de importación y la carga no SAP comprueban que activo = pasivo + patrimonio dentro de la tolerancia definida (RN-16).",
   "Se comprueba que la utilidad del estado de resultados sea igual al resultado del ejercicio del balance.",
   "Se comprueba que todas las cuentas con saldo estén mapeadas.",
   "Una sociedad que no cumple no se importa y queda en C03 con la diferencia.",
   "UX-09 muestra el paso «Validación de cuadre» con las sociedades rechazadas."],
  "Alta", 8, ["HU-11", "HU-27"], ["RF-36"], ["UX-09"], ""),
 ("HU-37", "E6", UF, "Consultar la validación de cuadre",
  "ver en C03 qué sociedades no se importaron y por qué",
  "pedir la corrección a la sociedad antes de consolidar",
  ["En la pantalla UX-11, hoja C03, se ve por sociedad la diferencia de balance, la diferencia entre utilidad y resultado, las cuentas mapeadas y si se importó.",
   "Una sociedad no importada se marca «No importada» con texto e ícono.",
   "Sin importaciones en el periodo se muestra «Aún no hay importaciones en el periodo elegido».",
   "Sin conexión a BPC se muestra el error de conexión de EPM."],
  "Alta", 3, ["HU-36", "HU-42"], ["RF-36"], ["UX-11"], ""),
 ("HU-38", "E6", UF, "Conciliar saldos intercompañía",
  "comparar en C01 lo que registra cada sociedad frente a su contraparte por cuenta intercompañía",
  "corregir las diferencias antes de ejecutar la eliminación",
  ["En la pantalla UX-11, hoja C01, se ve por pareja de sociedades y cuentas lo que registra cada parte y la diferencia.",
   "Las parejas que superan el umbral se marcan «Supera el umbral» con texto e ícono.",
   "Solo aparecen sociedades del grupo (RN-07).",
   "C01 se puede ejecutar antes de la consolidación del periodo."],
  "Alta", 8, ["HU-27", "HU-42"], ["RF-34"], ["UX-11", "UX-10"], ""),
 ("HU-39", "E6", UF, "Registrar asientos de ajuste de consolidación",
  "registrar asientos con líneas de sociedad, cuenta, intercompañía, débito, crédito y observación",
  "corregir la consolidación con un registro trazable",
  ["En la pantalla UX-14 el asiento tiene grupo, periodo y moneda, y líneas con sociedad, cuenta, intercompañía, débito, crédito y observación.",
   "Guardar asiento solo se habilita si los débitos son iguales a los créditos (RN-15); si no, se muestra «Descuadrado» con la diferencia.",
   "No se aceptan líneas de una sociedad bloqueada en el periodo (RN-17).",
   "Cada asiento queda con número, usuario y fecha en la lista del periodo (RNF-Aud-02)."],
  "Alta", 8, ["HU-26"], ["RF-35", "RNF-Aud-02"], ["UX-14"], ""),
 ("HU-40", "E6", UF, "Bloquear saldos por sociedad",
  "bloquear o desbloquear una sociedad en un periodo sin afectar a las demás",
  "cerrar cada sociedad cuando está lista aunque otras sigan en revisión",
  ["En la pantalla UX-15 cada sociedad tiene el botón Bloquear o Desbloquear.",
   "Una sociedad bloqueada no admite cargas, asientos ni reprocesos del periodo (RN-17).",
   "Bloquear una sociedad no bloquea a las demás del grupo.",
   "Cada bloqueo y desbloqueo queda registrado con usuario y fecha (RNF-Aud-02)."],
  "Media", 5, ["HU-26"], ["RF-37", "RNF-Aud-02"], ["UX-15"], ""),
 ("HU-41", "E6", UF, "Seguir el estado del cierre",
  "ver en C04 qué pasos del cierre están completos, pendientes o con error por sociedad",
  "saber qué falta para cerrar el mes y dónde está cada problema",
  ["En la pantalla UX-15 la matriz muestra por sociedad el estado de carga, cuadre, conciliación, conversión, consolidación y reportes, en ese orden.",
   "Un paso con error enlaza al reporte que explica la causa (p. ej. C03).",
   "Las hojas dan acceso a C02 (UX-13) y a Asientos (UX-14).",
   "Si el cierre no ha iniciado se muestra «El cierre del periodo aún no ha iniciado»."],
  "Media", 8, ["HU-36", "HU-38", "HU-40"], ["RF-38"], ["UX-15"], ""),
 # ---------------- E7 ----------------
 ("HU-42", "E7", UF, "Usar la plantilla común de reportes EPM",
  "elegir sociedad, si incluye filiales, moneda y periodo en un reporte y actualizarlo desde BPC",
  "consultar la consolidación sin pedir reportes nuevos a TI",
  ["En la pantalla UX-11 los parámetros Sociedad, Incluir filiales, Moneda y Periodo están en celdas con lista.",
   "Incluir filiales cambia entre la sociedad sola y la sociedad con sus filiales, con columna Total (RN-09).",
   "Moneda permite solo moneda local o dólares (RN-06).",
   "Sin conexión a BPC se muestra «No se pudo conectar a BPC; revisa la conexión de EPM»; sin datos, «Sin datos para la sociedad y el periodo elegidos»."],
  "Alta", 8, ["HU-29"], ["RF-20", "RF-21", "INT-06"], ["UX-11"], ""),
 ("HU-43", "E7", UF, "Calcular rubros por mapeo",
  "que cada rubro del reporte sea la suma de las cuentas mapeadas",
  "que un cambio de mapeo se refleje en todos los reportes sin editarlos",
  ["En UX-11 cada rubro muestra la suma de las cuentas mapeadas en UX-06 (RN-10).",
   "Un cambio en el mapeo se refleja al actualizar el reporte.",
   "El detalle de un rubro permite ver las cuentas que lo forman."],
  "Alta", 5, ["HU-11", "HU-42"], ["RF-22"], ["UX-11"], ""),
 ("HU-44", "E7", UF, "Consultar los reportes de la ola 1",
  "consultar R01 Estado de situación financiera, R05 Hoja de eliminaciones y R07 Efectivo",
  "validar el prototipo con los reportes base del cierre",
  ["R01, R05 y R07 se entregan como Excel con EPM conectados al modelo BPC (UX-11).",
   "Las cifras de R01 cuadran con el balance consolidado del modelo para el periodo de prueba.",
   "R05 muestra las eliminaciones intercompañía por pareja de sociedades.",
   "Cada reporte abre y actualiza con EPM add-in (RNF-Comp-02)."],
  "Alta", 5, ["HU-43"], ["RF-20", "RNF-Comp-02"], ["UX-11"], ""),
 ("HU-45", "E7", UF, "Consultar los reportes de la ola 2",
  "consultar R02, R03, R04 y R06",
  "completar los estados financieros consolidados y las partes relacionadas",
  ["R02, R03, R04 y R06 se entregan con la estructura común de UX-11.",
   "R03 muestra por separado el interés minoritario y la diferencia en conversión.",
   "R04 clasifica los rubros del flujo de efectivo; un rubro sin clasificar se reporta en UX-10 como error del reporte."],
  "Alta", 8, ["HU-44", "HU-33", "HU-31"], ["RF-20"], ["UX-11", "UX-10"], ""),
 ("HU-46", "E7", UF, "Consultar las notas de la ola 3 (R08 a R14)",
  "consultar las notas de activo y pasivo R08 a R14",
  "preparar las revelaciones del cierre con los datos del modelo",
  ["R08 a R14 se entregan con la estructura común de UX-11.",
   "R08, R10 y R11 incluyen los movimientos que vienen de activos fijos y deterioros en BW (RF-30).",
   "Cada nota cuadra con el rubro correspondiente de R01."],
  "Alta", 8, ["HU-44", "HU-19", "HU-20"], ["RF-20", "RF-30"], ["UX-11"], ""),
 ("HU-47", "E7", UF, "Consultar los reportes de la ola 3 (R15 a R20)",
  "consultar R15 a R20, incluido el informe a junta directiva",
  "completar los 20 reportes de consolidación",
  ["R15 a R20 se entregan con la estructura común de UX-11.",
   "R20 resume las cifras principales del grupo en moneda local y en dólares.",
   "Los 20 reportes abren y actualizan con EPM add-in (RNF-Comp-02)."],
  "Alta", 8, ["HU-44"], ["RF-20", "RNF-Comp-02"], ["UX-11"], ""),
 ("HU-48", "E7", CI, "Generar y descargar el paquete de reportes",
  "ver el catálogo de reportes de los módulos elegidos, generarlos y descargarlos en un .zip",
  "entregar al cliente sus reportes listos para usar",
  ["En la pantalla UX-10 el catálogo lista ID, reporte, módulo, fuente (BPC o BW), ola y estado, filtrable por módulo.",
   "El catálogo incluye C01 a C04; el clic en el ID abre su vista previa (UX-11, UX-13 o UX-15).",
   "Descargar reportes genera un .zip con los Excel de los módulos elegidos.",
   "Un reporte con error muestra la causa en su estado."],
  "Alta", 5, ["HU-44"], ["RF-20", "RF-27", "RF-29", "RF-34"], ["UX-10"], ""),
 # ---------------- E8 ----------------
 ("HU-49", "E8", SI, "Generar el modelo de planeación de presupuesto",
  "cargar en BW el presupuesto desde 0FI_GL_14 y generar en BPC un modelo de planeación separado del de consolidación",
  "que el cliente planee sin afectar la consolidación",
  ["Solo se genera si Presupuesto está marcado en UX-04.",
   "El modelo de planeación es independiente del modelo de consolidación.",
   "UX-08 lista el modelo de presupuesto y UX-09 muestra el paso «Modelo de presupuesto».",
   "El presupuesto cargado desde el ERP queda disponible en el modelo."],
  "Alta", 8, ["HU-08", "HU-18", "HU-26"], ["RF-23"], ["UX-08", "UX-09"], ""),
 ("HU-50", "E8", UF, "Capturar y ajustar el presupuesto",
  "ingresar valores por cuenta, centro de costo y mes en el formulario P05 y guardarlos en BPC",
  "construir el presupuesto del año en el mismo modelo que lo compara con la ejecución",
  ["En la pantalla UX-12 se eligen sociedad, versión y año; las celdas editables están en amarillo y los totales se calculan.",
   "Solo se aceptan números; una celda inválida se marca en rojo y no se guarda.",
   "Guardar datos escribe en el modelo de planeación de BPC (RN-08).",
   "Sin presupuesto para la versión se muestra «Sin presupuesto para esta versión: ingresa los valores»."],
  "Alta", 5, ["HU-49"], ["RF-28"], ["UX-12"], ""),
 ("HU-51", "E8", UF, "Elegir la versión del presupuesto",
  "elegir la versión (anual, 3+9, 6+6, 9+3) en la captura y en los reportes de presupuesto",
  "comparar la ejecución con la proyección vigente",
  ["En UX-12 y en la hoja P01 de UX-11 la versión se elige de una lista con las versiones definidas en HU-12.",
   "Los valores guardados en una versión no cambian las demás.",
   "P01 a P05 muestran en el encabezado la versión elegida."],
  "Media", 3, ["HU-12", "HU-50"], ["RF-39"], ["UX-12", "UX-11"], ""),
 ("HU-52", "E8", UF, "Consultar los reportes de presupuesto P01 a P04",
  "consultar presupuesto frente a ejecución, gastos por CeCo, ingresos por CeBe y proyección de cierre",
  "hacer seguimiento del presupuesto del año",
  ["P01 a P04 se entregan como Excel con EPM conectados al modelo de planeación (RN-08).",
   "En UX-11, P01 muestra presupuesto, ejecución, variación y porcentaje de ejecución.",
   "Los valores guardados en P05 se reflejan en P01 al actualizar."],
  "Alta", 8, ["HU-42", "HU-50"], ["RF-27"], ["UX-11", "UX-10"], ""),
 ("HU-53", "E8", UF, "Consultar los reportes de activos fijos y deterioros",
  "consultar A01 a A03 y D01 a D02 con datos de BW",
  "controlar activos y deterioros sin un modelo adicional en BPC",
  ["A01 a A03 y D01 a D02 leen su información de BW (RN-12).",
   "En UX-10 la fuente de estos reportes se muestra como BW.",
   "A01 y D01 muestran saldo inicial, movimientos y saldo final por clase o cuenta."],
  "Alta", 8, ["HU-19", "HU-20", "HU-42"], ["RF-29"], ["UX-10", "UX-11"], ""),
 # ---------------- E9 ----------------
 ("HU-54", "E9", CI, "Consultar y descargar el registro de ejecuciones",
  "ver y descargar el registro de cada paso ejecutado en el cliente",
  "auditar qué cambió el acelerador en los sistemas del cliente",
  ["En UX-09 el registro muestra fecha y hora, usuario, sistema, paso, objetos y resultado.",
   "El 100 % de las ejecuciones queda registrado y se conserva al menos 12 meses (RNF-Aud-01).",
   "Descargar registro entrega un archivo con los mismos campos.",
   "El registro no contiene credenciales (RNF-Seg-02)."],
  "Alta", 5, ["HU-14"], ["RNF-Aud-01"], ["UX-09"], ""),
 ("HU-55", "E9", SI, "Aplicar los controles ASVS nivel 2 al configurador",
  "que el configurador cumpla los controles de OWASP ASVS nivel 2",
  "proteger la información financiera y los datos personales de terceros del cliente",
  ["La verificación ASVS nivel 2 termina sin hallazgos altos abiertos (RNF-Seg-01).",
   "Las entradas de archivos y formularios se validan del lado del servidor.",
   "Las dependencias del configurador no tienen vulnerabilidades altas conocidas.",
   "Los datos de terceros se tratan según la Ley 1581/2012 (DAF, sección 9)."],
  "Alta", 5, ["HU-04"], ["RNF-Seg-01"], [], ""),
 ("HU-56", "E9", CI, "Verificar la accesibilidad del configurador",
  "que las pantallas del configurador cumplan WCAG 2.1 AA",
  "que cualquier consultor pueda usar el configurador con teclado o lector de pantalla",
  ["Las pantallas UX-01 a UX-10 tienen 0 incumplimientos A y AA en una revisión automática (RNF-Usa-01).",
   "El foco es visible y todos los controles se operan con teclado.",
   "Los estados usan texto e ícono, no solo color."],
  "Alta", 3, ["HU-14"], ["RNF-Usa-01"], ["UX-01"], ""),
 ("HU-57", "E9", CI, "Rechazar la arquitectura B en la versión 1",
  "que el acelerador me avise si el cliente opera la arquitectura B y no me deje configurarlo ni generar",
  "no crear en el cliente objetos que la versión 1 no soporta",
  ["En la pantalla UX-02, si RF-04 detecta la arquitectura B, se muestra «Arquitectura B no soportada en la versión 1» con la explicación.",
   "Continuar al diagnóstico queda deshabilitado y no se crea ningún objeto en el cliente (RN-19).",
   "El cliente queda en UX-01 con estado «No soportado» y el motivo.",
   "La detección queda en el registro de ejecuciones (RNF-Aud-01)."],
  "Alta", 2, ["HU-06"], ["RF-04", "RNF-Comp-01"], ["UX-02", "UX-01"], ""),
 ("HU-58", "E9", CI, "Certificar la variante BW/4HANA con BPC 2021",
  "ejecutar todos los RF de la arquitectura A en un ambiente BW/4HANA con BPC 2021 modelo estándar",
  "confirmar el soporte para los clientes que dejan BW 7.5",
  ["Todos los RF de la arquitectura A se ejecutan completos en el ambiente BW/4HANA (RNF-Comp-05).",
   "No se genera ningún InfoCubo en esta variante.",
   "Agregar la variante no cambia la configuración de clientes existentes (RNF-Comp-04)."],
  "Alta", 8, ["HU-16", "HU-41"], ["RNF-Comp-05", "RNF-Comp-04"], [], ""),
]

ROLES = [(CI, "Configura y ejecuta el acelerador en cada cliente desde el configurador web."),
         (AS, "Crea el usuario técnico e importa transportes y paquetes Z en el cliente (participa en HU-01 y en las dependencias)."),
         (UF, "Consulta reportes, mantiene participaciones, registra asientos, captura presupuesto y lleva el cierre mensual."),
         (SI, "Capacidades automáticas del acelerador y de los objetos que genera (generación, cargas, reglas y validaciones).")]

rf_daf = {r["codigo"]: r["prioridad"] for r in DAF["rf"]}
ux_ok = [p["codigo"] for p in UXI["pantallas"]]
backlog = {
    "epicas": [{"codigo": c, "nombre": n} for c, n, _ in EPICAS],
    "hus": [{"codigo": c, "epica": e, "rol": r, "titulo": t, "quiero": q, "para": pa, "criterios": cr, "prioridad": p, "sp": sp,
             "dependencias": dep, "trazabilidad": tz, "ux": ux, "tiene_interfaz": bool(ux), "nota_prioridad": nota}
            for c, e, r, t, q, pa, cr, p, sp, dep, tz, ux, nota in H],
    "roles_daf": [CI, AS, UF], "rf_daf": rf_daf, "ux_pantallas": ux_ok,
}
(D / "backlog.json").write_text(json.dumps(backlog, ensure_ascii=False, indent=2), encoding="utf-8")

# ---------------- Métricas ----------------
def met(hs):
    return len(hs), sum(h[8] for h in hs), [sum(1 for h in hs if h[7] == p) for p in ("Alta", "Media", "Baja")]
TOT_N, TOT_SP, TOT_P = met(H)
SPRINTS = -(-TOT_SP // 30)

# ---------------- Documento ----------------
B = []
Hd = lambda l, t: B.append({"type": "heading", "level": l, "text": t})
P = lambda t: B.append({"type": "paragraph", "text": t})
UL = lambda it: B.append({"type": "bullets", "items": it})
T = lambda rows, w, hdr=True: B.append({"type": "table", "rows": rows, "col_widths": w, **({} if hdr else {"has_header": False})})
AZ = "144E8C"
lab = lambda t: {"text": t, "fill": AZ, "align": "center"}
FILL_P = {"Alta": "F8D7DA", "Media": "FFF3CD", "Baja": "D1E7DD"}

B += [{"type": "portada", "text": "DOCUMENTO DE HISTORIAS DE USUARIO — ACELERADOR DE CONSOLIDACIÓN FINANCIERA SAP BW/BPC"},
      {"type": "paragraph", "text": "Producto interno CM Consulting", "align": "center", "bold": True, "color": AZ, "size": 28},
      {"type": "page_break"}]
Hd(1, "Control de versiones")
T([["Versión", "Elaborado por", "Fecha", "Observaciones"],
   ["1.0", "CM Consulting", "16-Oct-26", "Versión para revisión y aprobación, derivada del DAF v1.1 y del Documento UX v1.1."], ["1.1", "CM Consulting", "16-Oct-26", "Alineada con el DAF v1.2: la arquitectura B queda fuera de la versión 1; HU-57 pasa a ser el rechazo controlado de B (2 SP en lugar de 8)."]], [1200, 2200, 1500, 3938])
Hd(1, "Confidencialidad")
P([{"text": "La información contenida en este documento es propiedad intelectual de "}, {"text": "CM CONSULTING SAS", "bold": True}, {"text": "."}])
P([{"text": "Este documento es de uso interno de "}, {"text": "CM CONSULTING SAS", "bold": True},
   {"text": ". Su reproducción, distribución o comunicación por cualquier medio queda expresamente prohibida sin el consentimiento escrito de "},
   {"text": "CM CONSULTING SAS", "bold": True}, {"text": "."}])
B += [{"type": "page_break"}, {"type": "toc"}, {"type": "page_break"}]

Hd(1, "1. Introducción")
Hd(2, "1.1 Objetivo")
P("Este documento descompone en épicas e historias de usuario la línea base funcional del Acelerador de consolidación financiera sobre SAP BW y SAP BPC, y propone el orden de construcción. Es la hoja de ruta para el diseño de arquitectura (F6), el plan de pruebas (F7) y la planeación de sprints.")
Hd(2, "1.2 Alcance del documento")
P(f"{len(EPICAS)} épicas y {TOT_N} historias de usuario que cubren los {len(rf_daf)} RF del DAF v1.1, sus RNF relevantes y las {len(ux_ok)} pantallas del Documento UX v1.1. Los story points incluyen las pruebas unitarias de cada historia: no hay historias separadas para escribir pruebas.")
Hd(2, "1.3 Fuentes y trazabilidad")
T([["Fuente", "Uso en este documento"],
   ["Documento de Análisis Funcional v1.2 (16-Oct-26)", "Fuente autoritativa de RF, RNF, RN, roles y prioridades. Cada HU cita sus códigos."],
   ["Documento de Diseño UX v1.1 (16-Oct-26)", "Pantallas UX-01 a UX-15 citadas en los criterios de aceptación."],
   ["Prototipo navegable", "Comportamiento detallado de cada pantalla y de sus estados normal, vacío, carga y error."]], [3400, 5438])

Hd(1, "2. Marco de referencia")
Hd(2, "2.1 Estructura de cada historia de usuario")
T([["Elemento", "Contenido"],
   ["Tarjeta", "Código HU-XX, título, prioridad, story points, dependencias y trazabilidad a RF, RNF e INT del DAF."],
   ["Narrativa", "Como [rol del DAF], quiero [funcionalidad], para [beneficio]."],
   ["Criterios de aceptación", "Afirmaciones verificables; las HU con interfaz citan su pantalla UX-XX."]], [2400, 6438])
Hd(2, "2.2 Escala de story points")
T([["SP", "Significado"], ["1", "Trivial: configuración o ajuste menor."], ["2", "Sencillo: lógica directa y validaciones básicas."],
   ["3", "Estándar: CRUD o carga acotada sin integraciones nuevas."], ["5", "Complejo: varias validaciones, estados o integración ligera."],
   ["8", "Muy complejo: generación de objetos SAP, reglas de consolidación o varios actores."], ["13", "Alta incertidumbre: se subdivide antes del sprint."]], [1000, 7838])
Hd(2, "2.3 Convenciones de prioridad")
T([["Prioridad", "Definición"], ["Alta", "Sin ella el acelerador no implementa una consolidación operable."],
   ["Media", "Soporte o mejora operativa; puede llegar después del primer cliente piloto."], ["Baja", "Complementaria o de uso ocasional."]], [1500, 7338])
P("La prioridad de cada HU hereda la del RF principal que implementa (DAF, sección 5).")

Hd(1, "3. Roles y actores del sistema")
T([["Rol", "Responsabilidad principal"]] + [list(r) for r in ROLES], [2800, 6038])

Hd(1, "4. Resumen de épicas")
rows = [["Épica", "Nombre", "Foco", "HU", "SP", "A / M / B"]]
for c, n, f in EPICAS:
    k, sp, pr = met([h for h in H if h[1] == c])
    rows.append([{"text": c, "bold": True}, {"text": n, "bold": True}, f, str(k), str(sp), " / ".join(map(str, pr))])
rows.append([{"text": "TOTAL", "bold": True}, "—", "—", {"text": str(TOT_N), "bold": True}, {"text": str(TOT_SP), "bold": True}, {"text": " / ".join(map(str, TOT_P)), "bold": True}])
T(rows, [700, 1900, 3838, 600, 700, 1100])
Hd(2, "4.1 Recomendación de planeación")
P("Se sugiere iniciar con HU-01, la prueba técnica del mecanismo de inyección: es el mayor riesgo del producto y condiciona todos los generadores. En paralelo se construye la base de E1 y E2 (clientes, conexiones seguras, arquitectura, inventario y configuración de alcance). Después se avanza en el orden del proceso de implementación: E3 bodega en BW, E4 datos maestros y E5 modelo BPC y consolidación. Las capacidades de E9 (registro de ejecuciones, ASVS y accesibilidad) se construyen desde los primeros sprints, porque cada generador escribe en el registro.")
P("E6 control del cierre y E7 reportes arrancan cuando E5 deja una consolidación ejecutable (HU-29), y avanzan en paralelo. E8 puede construirse en paralelo con E6 una vez exista la bodega (HU-18) y el modelo BPC (HU-26). Puntos de demostración sugeridos: conexión, diagnóstico y configuración (fin de E2); bodega y datos maestros cargados (fin de E4); consolidación en dos monedas con los reportes de la ola 1 (HU-44); cierre mensual completo con C01 a C04.")
P(f"La estimación de {TOT_SP} story points equivale a unos {SPRINTS} sprints de dos semanas, con una velocidad de referencia de 30 story points por sprint para un equipo de 4 a 5 personas. Es referencial y se ajusta en el Sprint Planning. La certificación de la variante BW/4HANA (HU-58) depende de que exista ese ambiente. La arquitectura B queda fuera de la versión 1; HU-57 solo la detecta y la rechaza.")

for i, (c, n, f) in enumerate(EPICAS, 5):
    hs = [h for h in H if h[1] == c]
    k, sp, pr = met(hs)
    B.append({"type": "page_break"})
    Hd(1, f"{i}. {c} — {n}")
    T([[lab("Foco de la épica"), f], [lab("Métricas"), f"{k} historia(s) de usuario · {sp} story points · {pr[0]} Alta / {pr[1]} Media / {pr[2]} Baja"]], [2400, 6438], hdr=False)
    for (code, e, rol, tit, q, pa, cr, p, spp, dep, tz, ux, nota) in hs:
        B.append({"type": "blank"})
        T([[lab(code), {"text": tit, "bold": True}, lab("Prioridad"), {"text": p, "align": "center", "fill": FILL_P[p]}],
           [lab("Story Points"), {"text": str(spp), "align": "center"}, lab("Dependencias"), {"text": ", ".join(dep) if dep else "—", "align": "center"}],
           [lab("Trazabilidad RF/RNF"), {"text": " · ".join(tz + ux), "colspan": 3}]], [1500, 3938, 1700, 1700], hdr=False)
        P([{"text": "Como ", "italic": True}, {"text": rol, "bold": True, "italic": True}, {"text": ", quiero ", "italic": True},
           {"text": q, "italic": True}, {"text": ", para ", "italic": True}, {"text": pa, "bold": True, "italic": True}, {"text": ".", "italic": True}])
        P([{"text": "Criterios de aceptación:", "bold": True, "color": AZ}])
        UL(cr)
        if nota:
            P([{"text": "Nota de prioridad: ", "bold": True}, {"text": nota}])

B.append({"type": "page_break"})
n_sec = 5 + len(EPICAS)
Hd(1, f"{n_sec}. Matriz de trazabilidad HU ↔ RF/RNF")
T([["HU", "Título", "Épica", "Prioridad", "SP", "RF / RNF / INT"]] +
  [[{"text": h[0], "bold": True}, h[3], h[1], h[7], str(h[8]), ", ".join(h[10])] for h in H], [900, 3338, 700, 1000, 600, 2300])
Hd(2, f"{n_sec}.1 Cobertura de los RF del DAF")
cov = {r: [h[0] for h in H if r in h[10]] for r in rf_daf}
T([["RF", "Prioridad DAF", "HU que lo implementan"]] + [[r, rf_daf[r], ", ".join(cov[r])] for r in sorted(rf_daf)], [1200, 1600, 6038])

Hd(1, f"{n_sec + 1}. Próximos pasos y gobierno de las historias")
Hd(2, f"{n_sec + 1}.1 Validación y aprobación")
P("Las historias se revisan con el área de CM Consulting responsable del producto y con el consultor SAP BPC que aportó el levantamiento. Una vez aprobadas, son la hoja de ruta funcional y la base del documento de arquitectura, el plan de pruebas y el plan de sprints.")
Hd(2, f"{n_sec + 1}.2 Gestión del backlog")
UL(["El backlog se mantiene en Azure DevOps durante la construcción.",
    "Los cambios de alcance, criterios o prioridad siguen el control de cambios del producto.",
    "No hay historias con 13 story points; si una estimación sube a 13 en el refinamiento, se divide antes de comprometerla.",
    "Cada historia se da por terminada con sus pruebas unitarias y con el registro de ejecución de RNF-Aud-01 cuando aplica."])
Hd(2, f"{n_sec + 1}.3 Dependencias críticas")
UL(["HU-01 y todo E3 a E5: acceso al ambiente de arquitectura A, usuario técnico y permiso para paquete Z y transportes (DAF, dependencias 12.2).",
    "HU-16 y HU-58: ambiente BW/4HANA con BPC 2021 modelo estándar y sus versiones exactas (DAF, pregunta 18).",
    "HU-25: formato del archivo de sociedades no SAP (DAF, pregunta 5).",
    "HU-23 y HU-30: tipos de tasa y tabla de origen en SAP (DAF, pregunta 3).",
    "HU-33 y HU-34: métodos de consolidación y cuentas de destino (DAF, pregunta 15).",
    "HU-36 a HU-38: tolerancia de cuadre y umbral de conciliación (DAF, pregunta 14).",
    "HU-53: cómo leen BW los reportes con EPM add-in (DAF, pregunta 12)."])
Hd(2, f"{n_sec + 1}.4 Próximas acciones")
UL(["Sesión de revisión de las historias con el consultor SAP BPC.",
    "Resolver las dependencias de HU-01 para iniciar el primer sprint.",
    "Documento de arquitectura (F6) con base en este backlog: alojamiento del configurador, versiones y lectura de BW desde EPM.",
    "Plan de pruebas (F7) derivado de los criterios de aceptación.",
    "Sprint Planning del Sprint 1 con HU-01 a HU-06 y HU-08."])

(D / "bloques_hu.json").write_text(json.dumps({"blocks": B}, ensure_ascii=False, indent=1), encoding="utf-8")
print("épicas", len(EPICAS), "HU", TOT_N, "SP", TOT_SP, "A/M/B", TOT_P, "sprints", SPRINTS)
for c, n, _ in EPICAS:
    k, sp, pr = met([h for h in H if h[1] == c]); print(" ", c, n, k, "HU", sp, "SP", pr)
