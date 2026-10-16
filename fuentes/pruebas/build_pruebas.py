"""Genera plan_pruebas.json (validador F7) y bloques_pruebas.json (skill base) del Plan de Pruebas del Acelerador BPC.

Los casos se derivan de los documentos aprobados:
  1. cada criterio de aceptación de backlog.json -> un caso funcional (el resultado esperado es el criterio);
     un criterio con condición y excepción agrega su caso negativo;
  2. los estados vacío, carga y error de cada pantalla UX -> casos negativos (si un criterio ya lo cubre, se cita);
  3. las reglas y criterios con umbral -> casos de límite (bajo, exacto, sobre);
  4. cada RNF del DAF -> prueba no funcional con el umbral numérico del DAF;
  5. cada INT del DAF -> casos de resiliencia (feliz, timeout, error del tercero, reintento) según la
     estrategia de errores de la arquitectura (timeout de 120 s, un reintento solo en errores de comunicación).
"""
import ast
import json
from pathlib import Path

D = Path(__file__).parent
ROOT = D.parent.parent
BACKLOG = json.loads((ROOT / "fuentes" / "hu" / "backlog.json").read_text(encoding="utf-8"))
DAF = json.loads((ROOT / "fuentes" / "daf" / "daf.json").read_text(encoding="utf-8"))
UXI = json.loads((ROOT / "fuentes" / "ux" / "ux_inventario.json").read_text(encoding="utf-8"))
ARQ = json.loads((ROOT / "fuentes" / "arq" / "arquitectura.json").read_text(encoding="utf-8"))


def _estados_ux():
    """Lee los textos de estado de cada pantalla desde la lista P de build_ux.py (sin ejecutarlo)."""
    tree = ast.parse((ROOT / "fuentes" / "ux" / "build_ux.py").read_text(encoding="utf-8"))
    for node in tree.body:
        if isinstance(node, ast.Assign) and getattr(node.targets[0], "id", None) == "P":
            pantallas = ast.literal_eval(node.value)
            break
    out = {}
    for p in pantallas:
        out[p[0]] = {}
        for e in p[6]:
            k, txt = e.split(":", 1)
            out[p[0]][k.strip().lower()] = txt.strip()
    return out


ESTADOS = _estados_ux()
HUS = {h["codigo"]: h for h in BACKLOG["hus"]}
EPICAS = {e["codigo"]: e["nombre"] for e in BACKLOG["epicas"]}

# ---------------------------------------------------------------- datos de prueba
GRUPO = [  # código, nombre, en SAP, moneda local, método, % propiedad
    ("D100", "Matriz Demo", "Sí", "COP", "Matriz", "100"),
    ("D200", "Filial Demo US", "Sí", "USD", "Integración global", "80"),
    ("D300", "Coligada Demo", "Sí", "COP", "Participación", "30"),
    ("D400", "Filial No SAP", "No", "COP", "Integración global", "100"),
    ("D900", "Sociedad Inactiva", "Sí", "COP", "Fuera del perímetro", "0"),
]
T_CUADRE, U_CONC = "1,00", "1.000,00"  # provisionales hasta cerrar la pregunta 14 del DAF

# ---------------------------------------------------------------- precondiciones
SANDBOX = "Sandbox de arquitectura A (BW 7.5 con BPC 10.1 modelo estándar) con el paquete Z importado y el usuario técnico de prueba con el rol Z del acelerador"
PRE_EPICA = {
    "E1": f"Configurador instalado en el equipo de pruebas. {SANDBOX}.",
    "E2": "Cliente «Cliente QA Demo» con conexiones exitosas y diagnóstico completo (E1). Datos sintéticos del Grupo Demo cargados en el ERP del sandbox.",
    "E3": "Cliente QA Demo con la configuración de alcance completa (E2): cuatro módulos, sociedades D100 a D900 y mapeo de cuentas.",
    "E4": "Bodega financiera generada en el sandbox (E3). Planes de cuentas CO01 y US01, CeCo, CeBe, tasas y jerarquía sintéticos en el ERP.",
    "E5": "Bodega y datos maestros cargados (E3, E4) con los movimientos sintéticos del periodo 2026.06.",
    "E6": "Modelo de consolidación generado y periodo 2026.06 importado (E5). Excel con EPM add-in conectado al modelo con el usuario financiero de prueba.",
    "E7": "Consolidación del periodo 2026.06 ejecutada (E5, E6) y paquete de reportes generado. Excel con EPM add-in conectado al modelo.",
    "E8": "Cliente QA Demo con Presupuesto, Activos fijos y Deterioros marcados en UX-04 y la bodega generada (E3).",
    "E9": f"Configurador instalado en el equipo de pruebas. {SANDBOX}.",
}
PRE_HU = {
    "HU-01": f"{SANDBOX}. Sin el configurador: el spike usa las llamadas RFC directas.",
    "HU-02": "Configurador instalado en el equipo de pruebas con la base local vacía.",
    "HU-03": f"Cliente QA Demo creado. {SANDBOX}.",
    "HU-05": "Cliente QA Demo con ERP y BW registrados en sistemas separados y conexiones exitosas.",
    "HU-07": "Cliente QA Demo con arquitectura A detectada (HU-06).",
    "HU-13": "Cliente QA Demo con la configuración de alcance completa (E2) y un sandbox sin bodega.",
    "HU-15": "Plan de generación confirmado sobre el sandbox BW 7.5 sin bodega previa.",
    "HU-16": "Ambiente QA BW/4HANA con BPC 2021 modelo estándar (SAP CAL) encendido, con el paquete Z importado; cliente «Cliente QA BW4» con la configuración del Grupo Demo.",
    "HU-17": "Tabla de prueba del ERP con los campos ZZSEGMENTO (CHAR 10) y ZZBLOB (RAWSTRING); inventario ejecutado.",
    "HU-25": "Sociedad D400 marcada como no SAP. Archivos sintéticos D400_2026_06 (válido, con errores y descuadrado) en la carpeta de datos de prueba.",
    "HU-55": "Build de release candidato del configurador instalado en el ambiente de QA, con las herramientas de verificación del pipeline.",
    "HU-56": "Configurador con el Cliente QA Demo en cada paso, para recorrer UX-01 a UX-10 con datos.",
    "HU-57": "Doble de prueba del paquete Z que responde arquitectura B (ERP, BW y BPC integrados con BPC embebido) en la detección de RF-04.",
    "HU-58": "Ambiente QA BW/4HANA con BPC 2021 modelo estándar (SAP CAL) encendido; configuración exportada de los clientes existentes.",
}

# ---------------------------------------------------------------- casos por criterio
# "Título :: paso 1 | paso 2 | ..."; el resultado esperado es el criterio de aceptación tal cual.
C = {
 "HU-01": [
  "Ejecutar el spike con el usuario técnico sin SAP_ALL :: Consultar en SU01 los roles y perfiles del usuario técnico | Ejecutar la prueba técnica de inyección con ese usuario | Revisar el resultado de cada llamada RFC",
  "Crear por RFC un objeto en BW y un miembro en BPC :: Invocar el módulo Z de creación con el InfoObject de prueba ZQA_TEST | Invocar el módulo Z de BPC para crear el miembro QA_TEST en la dimensión de prueba | Buscar ambos objetos en RSA1 y en la administración de BPC desde SAP GUI",
  "Generar y restaurar un respaldo UJBR :: Generar el respaldo UJBR del ambiente BPC de prueba | Borrar el miembro QA_TEST | Restaurar el respaldo con UJBR | Consultar la dimensión de prueba",
  "Revisar la nota técnica del spike :: Abrir la nota técnica adjunta a HU-01 en Azure DevOps | Buscar el resultado, los permisos mínimos y la recomendación",
  "Verificar el límite de un sprint y el escalamiento :: Revisar en Azure DevOps las fechas de inicio y cierre de HU-01 | Si el resultado fue fallido, buscar la decisión registrada antes del inicio de E3",
 ],
 "HU-02": [
  "Listar los clientes con sus columnas :: Crear dos clientes de arquitectura A en pasos distintos | Abrir UX-01 | Leer las columnas y valores de la tabla",
  "Exigir un nombre de cliente único :: Pulsar Nuevo cliente y dejar el nombre vacío | Pulsar Crear | Escribir el nombre de un cliente existente y pulsar Crear",
  "Crear un cliente y pasar a Conexiones :: Pulsar Nuevo cliente | Escribir «Cliente QA Demo» | Pulsar Crear",
  "Mostrar el estado vacío sin clientes :: Abrir UX-01 con la base local vacía",
  "Filtrar clientes por nombre :: Crear los clientes «Demo Uno», «Demo Dos» y «Piloto» | Escribir «Demo» en Buscar",
  "Ejecutar el configurador sin servidores adicionales :: Iniciar el configurador en el equipo de pruebas | Listar con netstat los puertos en escucha del proceso | Revisar las conexiones salientes del proceso hacia hosts distintos del SAP del cliente",
 ],
 "HU-03": [
  "Exigir los campos de conexión de cada sistema :: Abrir UX-02 | Dejar vacío, uno a la vez, host, mandante, usuario técnico y credencial de la tarjeta ERP | Intentar guardar",
  "Probar una conexión exitosa y una fallida :: Registrar datos válidos de BW y pulsar Probar conexión | Cambiar el mandante por uno inexistente y pulsar Probar conexión",
  "Habilitar Continuar solo con conexiones exitosas :: Dejar ERP exitosa y BW fallida | Revisar el botón Continuar al diagnóstico | Corregir BW, probar de nuevo y revisar el botón",
  "Registrar cada prueba de conexión :: Probar las conexiones de ERP, BW y BPC | Abrir el registro de ejecuciones en UX-09 y filtrar por el cliente",
 ],
 "HU-04": [
  "Enmascarar la credencial :: Abrir UX-02 con una credencial guardada | Revisar el campo credencial | Pulsar Mostrar",
  "Verificar que ningún archivo contiene la credencial :: Registrar como credencial un valor centinela único | Probar conexión y ejecutar el diagnóstico | Buscar el centinela en la base SQLite, la configuración, los registros y los temporales del perfil",
  "Revisar la documentación del rol técnico :: Abrir la documentación del rol técnico | Comparar los permisos por paso con los roles Z asignados en SU01 | Buscar SAP_ALL y SAP_NEW",
  "No exponer la credencial en los errores :: Registrar como credencial inválida un valor centinela único | Pulsar Probar conexión | Revisar el mensaje en pantalla y el registro de ejecuciones",
 ],
 "HU-05": [
  "Mostrar la validación ERP–BW con sistemas separados :: Abrir UX-02 del cliente con ERP y BW en hosts distintos | Buscar el botón Validar conexión ERP–BW",
  "Registrar el resultado de la validación ERP–BW :: Pulsar Validar conexión ERP–BW | Abrir el registro de ejecuciones",
  "Bloquear el avance si falla la validación ERP–BW :: Desactivar en BW (RSA1) el sistema origen del ERP | Pulsar Validar conexión ERP–BW | Revisar Continuar al diagnóstico",
 ],
 "HU-06": [
  "Mostrar la arquitectura y las versiones detectadas :: Probar las conexiones contra el sandbox A | Leer en UX-02 la insignia y las versiones de BW y BPC",
  "Reconocer BW 7.5 y BW/4HANA como arquitectura A :: Detectar el sandbox BW 7.5 con BPC 10.1 | Detectar el ambiente QA BW/4HANA con BPC 2021 | Comparar la insignia y las versiones de ambos",
  "Bloquear una versión sin variante :: Configurar el doble de prueba del paquete Z para responder BW 7.4 | Probar las conexiones | Revisar el mensaje y Continuar al diagnóstico",
  "Guardar la arquitectura y la versión en el cliente :: Detectar la arquitectura | Volver a UX-01 | Reiniciar el configurador y abrir de nuevo UX-01",
 ],
 "HU-07": [
  "Mostrar el inventario en pestañas :: Abrir UX-03 y pulsar Escanear | Recorrer las pestañas Extractores, InfoProviders, Modelos BPC y Campos Z",
  "Marcar la bodega existente y no recrearla :: Escanear un sandbox que ya tiene la bodega financiera | Revisar el indicador Bodega existente | Abrir UX-08 y revisar los objetos de la bodega",
  "Volver a escanear :: Anotar la fecha del último escaneo | Agregar el campo ZZNUEVO a la tabla de prueba del ERP | Pulsar Volver a escanear",
  "Informar el paso que falló en el escaneo :: Quitar al usuario técnico la autorización de lectura de BPC | Pulsar Escanear",
 ],
 "HU-08": [
  "Mostrar los módulos como tarjetas :: Abrir UX-04 | Revisar las tarjetas de consolidación, presupuesto, activos fijos y deterioros",
  "Actualizar el resumen al marcar módulos :: Marcar Consolidación | Marcar Presupuesto | Desmarcar Presupuesto, leyendo el resumen después de cada cambio",
  "Deshabilitar Continuar sin módulos :: Desmarcar todos los módulos | Revisar el botón Continuar",
  "Avisar que activos fijos y deterioros se quedan en BW :: Revisar las tarjetas de Activos fijos y de Deterioros",
 ],
 "HU-09": [
  "Agregar y editar sociedades :: Abrir UX-05 | Agregar D100 a D400 con los datos del Grupo Demo | Editar el nombre de D300, guardar y recargar",
  "Registrar solo sociedades del grupo :: Abrir el selector de código de sociedad | Buscar el código del tercero de prueba T9001 | Intentar guardarlo como sociedad",
  "Asignar un plan de cuentas por sociedad :: Asignar el plan CO01 a D100 y US01 a D200 | Guardar | Recargar UX-05",
  "Exigir la moneda local :: Agregar la sociedad D500 sin moneda local | Pulsar Guardar",
  "Habilitar la carga no SAP :: Marcar D400 como no SAP y guardar | Revisar la navegación lateral",
 ],
 "HU-10": [
  "Registrar porcentaje y método por sociedad :: Abrir UX-05 | Revisar las columnas % propiedad y Método | Abrir la lista de métodos",
  "Permitir una sola sociedad matriz :: Asignar el método matriz a D100 | Asignar el método matriz a D200 | Pulsar Guardar",
  "Rechazar un porcentaje fuera de rango :: Escribir 120 en % propiedad de D200 | Pulsar Guardar",
  "Usar la estructura como primer periodo :: Guardar la estructura del Grupo Demo | Ejecutar el paso Modelo de propiedad | Consultar C02 del primer periodo",
 ],
 "HU-11": [
  "Asignar cuentas filtradas por plan :: Abrir UX-06 | Elegir la sociedad D200 y el rubro Efectivo | Revisar que la lista solo trae cuentas del plan US01 | Marcar dos cuentas y asignarlas",
  "Mostrar la suma del rubro :: Mapear las cuentas 1105 y 1110 al rubro Efectivo | Seleccionar el rubro | Comparar la vista previa con la suma de los saldos sintéticos",
  "Listar las cuentas sin mapear :: Leer el contador de cuentas sin mapear | Activar el filtro Solo sin mapear | Mapear una cuenta y leer de nuevo el contador",
  "Leer las cuentas del ERP :: Crear en el ERP la cuenta 1199 en el plan CO01 | Recargar UX-06 | Buscar la cuenta 1199",
  "Informar las asignaciones no guardadas :: Activar el doble de prueba que hace fallar la escritura en la base local | Asignar tres cuentas | Pulsar Guardar",
 ],
 "HU-12": [
  "Editar las versiones propuestas :: Marcar Presupuesto en UX-04 | Agregar la versión V2 | Renombrar 3+9 | Quitar 9+3",
  "Exigir la versión de presupuesto anual :: Intentar quitar la versión de presupuesto anual | Guardar",
  "Rechazar códigos de versión repetidos :: Agregar una versión con el código de una existente | Guardar",
  "Pasar las versiones al plan :: Guardar las versiones | Abrir UX-08 y buscar el modelo de presupuesto",
 ],
 "HU-13": [
  "Agrupar el plan y marcar Nuevo o Existente :: Abrir UX-08 | Revisar los grupos SAP BW, SAP BPC y Monedas y tasas | Revisar la marca de cada objeto",
  "No recrear ni modificar objetos existentes :: Escanear un sandbox con bodega existente | Anotar en RSA1 la fecha del último cambio de sus objetos | Generar | Comparar las fechas",
  "Confirmar la generación con el total de objetos :: Leer el total de objetos a crear | Pulsar Generar | Leer el diálogo de confirmación",
  "Enlazar el paso anterior incompleto :: Borrar el mapeo de cuentas del cliente | Abrir UX-08 | Pulsar el enlace del paso faltante",
 ],
 "HU-14": [
  "Mostrar estado y duración de cada paso :: Iniciar la ejecución del plan | Observar la línea de pasos durante la ejecución y al terminar",
  "Mostrar la causa y la acción de un paso con error :: Quitar al usuario técnico el permiso de escritura en BPC | Ejecutar el plan | Revisar el paso fallido",
  "Reintentar sin repetir los pasos completos :: Devolver el permiso de escritura | Pulsar Reintentar desde este paso | Revisar en el registro qué pasos se ejecutaron",
  "Mostrar el resumen y Ver reportes al terminar :: Ejecutar el plan completo | Leer el resumen | Pulsar Ver reportes",
 ],
 "HU-15": [
  "Activar 0FI_GL_14 y crear los InfoProviders :: Ejecutar el paso Bodega BW | Consultar en RSA6 el estado de 0FI_GL_14 | Consultar en RSA1 los InfoProviders del módulo de consolidación",
  "No crear objetos si la bodega existe :: Ejecutar el paso Bodega BW en un sandbox con bodega | Revisar el estado del paso | Contar los objetos creados en el registro",
  "Registrar cada objeto con su nombre técnico :: Ejecutar el paso Bodega BW | Comparar los nombres técnicos del registro con los objetos de RSA1",
  "Mostrar el número de objetos creados :: Ejecutar el paso Bodega BW | Leer el resultado en UX-09 y compararlo con el registro",
 ],
 "HU-16": [
  "Usar la variante BW/4HANA sin cambiar la configuración :: Exportar la configuración del Cliente QA BW4 | Ejecutar la generación | Exportar de nuevo y comparar | Revisar la variante en el registro",
  "Crear aDSO y ningún InfoCubo :: Ejecutar la generación | Listar los InfoProviders creados por tipo",
  "Conectar los extractores por ODP :: Revisar en BW/4HANA las fuentes de datos creadas y su tipo de sistema origen",
  "Mostrar aDSO en el plan :: Abrir UX-08 del Cliente QA BW4 | Revisar el tipo de los InfoProviders",
 ],
 "HU-17": [
  "Marcar campos Z para incorporar :: Abrir UX-03, pestaña Campos Z | Marcar ZZSEGMENTO",
  "Mostrar los campos Z como Nuevo en el plan :: Marcar ZZSEGMENTO | Abrir UX-08",
  "Rechazar un campo Z sin tipo compatible :: Marcar ZZBLOB | Revisar la fila y el plan UX-08",
 ],
 "HU-18": [
  "Crear la cadena full y la delta cada 24 horas :: Ejecutar el paso Cadenas de proceso | Revisar en RSPC las dos cadenas y la programación de la delta",
  "Dejar los datos con 24 horas o menos de antigüedad :: Registrar hoy en el ERP un documento sintético | Ejecutar la delta nocturna programada | Al día siguiente, comparar la hora de la última carga del reporte con la hora actual",
  "Evitar duplicados por peticiones solapadas :: Revisar las variantes de la cadena delta | Ejecutar la delta dos veces seguidas | Comparar el total de la bodega con el del ERP",
  "Mostrar la carga full en la ejecución :: Ejecutar el plan | Revisar en UX-09 el paso de carga full",
 ],
 "HU-19": [
  "Generar activos fijos solo si el módulo está marcado :: Marcar Activos fijos en UX-04 | Ejecutar el plan | Revisar el paso y los objetos creados",
  "Crear activos fijos en BW sin modelo BPC :: Ejecutar el paso | Consultar el extractor en RSA6 y los InfoProviders en RSA1 | Consultar los modelos de BPC",
  "Mostrar el paso Activos fijos en BW :: Abrir UX-08 | Ejecutar y abrir UX-09",
 ],
 "HU-20": [
  "Generar deterioros solo si el módulo está marcado :: Marcar Deterioros en UX-04 | Ejecutar el plan | Revisar el paso y los objetos creados",
  "Crear deterioros en BW sin modelo BPC :: Ejecutar el paso | Consultar el extractor en RSA6 y los InfoProviders en RSA1 | Consultar los modelos de BPC",
  "Mostrar el paso Deterioros en BW :: Abrir UX-08 | Ejecutar y abrir UX-09",
 ],
 "HU-21": [
  "Cargar el plan de cuentas de cada sociedad :: Ejecutar el paso Planes de cuentas | Comparar por plan las cuentas de BW con las del ERP (SKA1)",
  "Mostrar la cantidad de cuentas cargadas :: Ejecutar el paso | Comparar la cantidad de UX-09 con el conteo en BW",
  "Reportar una sociedad sin plan de cuentas :: Registrar la sociedad D600 con un plan inexistente en el ERP | Ejecutar el paso",
 ],
 "HU-22": [
  "Cargar CeCo y CeBe de las sociedades :: Ejecutar el paso | Comparar CeCo y CeBe de BW con CSKS y CEPC del ERP",
  "Mostrar las cantidades de CeCo y CeBe :: Ejecutar el paso | Comparar las cantidades de UX-09 con el conteo en BW",
  "Mostrar la sociedad y el mensaje de un error de carga :: Asignar a D200 un área de controlling inexistente | Ejecutar el paso",
 ],
 "HU-23": [
  "Tomar la moneda local de SAP :: Ejecutar el paso Monedas y tasas | Comparar la moneda de cada sociedad con T001 del ERP",
  "Cargar tasas de cierre, promedio e histórica :: Ejecutar el paso | Consultar en BW las tasas por moneda y periodo 2026.06",
  "Reportar una moneda sin tasa :: Borrar en el sandbox la tasa USD de 2026.06 | Ejecutar el paso",
  "Mostrar monedas y tipos de tasa en el plan :: Abrir UX-08 | Revisar el grupo Monedas y tasas",
 ],
 "HU-24": [
  "Cargar la jerarquía de cuentas :: Ejecutar el paso Jerarquía | Comparar la jerarquía de BW con la del ERP",
  "Reportar una cuenta de la jerarquía fuera del plan :: Agregar a la jerarquía del ERP la cuenta 8888 inexistente en el plan | Ejecutar el paso",
  "Mostrar la cantidad de nodos :: Ejecutar el paso | Comparar la cantidad de UX-09 con los nodos en BW",
 ],
 "HU-25": [
  "Ver la vista previa del archivo no SAP :: Abrir UX-07 | Subir D400_2026_06_valido.csv | Revisar las columnas de la vista previa",
  "Listar los errores por fila :: Subir D400_2026_06_errores.csv (importe no numérico en la fila 3 y cuenta 9999 sin mapear en la fila 5) | Revisar la lista de errores",
  "Habilitar Confirmar carga solo sin errores :: Subir el archivo con errores y revisar Confirmar carga | Subir el archivo válido y revisar Confirmar carga",
  "Rechazar el archivo de una sociedad no registrada :: Subir un archivo con el código de sociedad D999 | Revisar el resultado",
  "Pasar la carga no SAP por la validación de cuadre :: Subir D400_2026_06_descuadrado.csv | Confirmar la carga | Ejecutar la importación | Consultar C03",
 ],
 "HU-26": [
  "Crear las dimensiones del modelo :: Ejecutar el paso Modelo BPC | Listar las dimensiones del ambiente BPC",
  "Cargar los miembros desde los datos maestros :: Ejecutar el paso | Comparar la cantidad de miembros por dimensión con los datos maestros de BW",
  "Mostrar el modelo en el plan y en la ejecución :: Abrir UX-08 | Ejecutar y abrir UX-09",
  "Reportar un miembro rechazado por BPC :: Agregar al maestro la cuenta «12/34» con un carácter que BPC no admite | Ejecutar el paso",
 ],
 "HU-27": [
  "Generar la importación solo en arquitectura A :: Abrir UX-08 de un cliente A y buscar el paso Importación a BPC | Abrir UX-08 de un cliente B (doble de prueba) y buscar el mismo paso",
  "Importar por sociedad y periodo con Data Manager :: Revisar en BPC el paquete de Data Manager generado | Ejecutarlo para D100 y 2026.06 | Consultar los datos en BPC",
  "Importar solo las sociedades que cuadran :: Descuadrar D200 en 2026.06 | Ejecutar la importación | Consultar en BPC las sociedades con datos",
  "Mostrar la cantidad de registros importados :: Ejecutar la importación | Comparar la cantidad de UX-09 con los registros en BPC",
 ],
 "HU-28": [
  "Desplegar la variante de modelo estándar :: Ejecutar el paso Scripts | Abrir en UJFS el script desplegado y su encabezado de variante",
  "Insertar los códigos del cliente sin edición manual :: Ejecutar el paso | Comparar las sociedades y cuentas del script con las del Grupo Demo",
  "Mostrar la variante en el plan :: Abrir UX-08 | Revisar la variante de scripts",
  "Respaldar con UJBR antes de reemplazar un script :: Desplegar los scripts | Modificar el script en UJFS | Desplegar de nuevo | Revisar los respaldos UJBR",
 ],
 "HU-29": [
  "Consolidar solo las sociedades del grupo :: Ejecutar la consolidación de 2026.06 | Listar las entidades del resultado",
  "Eliminar las transacciones intercompañía :: Cargar una venta intercompañía D100 a D200 de 1.000.000 COP | Consolidar 2026.06 | Consultar los saldos intercompañía consolidados",
  "Mostrar el resultado o la causa del paso Consolidación :: Consolidar y revisar el paso en UX-09 | Desactivar la regla de eliminación en BPC | Consolidar de nuevo y revisar el paso",
  "Consultar las eliminaciones en R05 :: Consolidar 2026.06 | Abrir R05 con D100 e Incluir filiales",
 ],
 "HU-30": [
  "Convertir con la tasa de cada tipo de cuenta :: Convertir 2026.06 | Comparar una cuenta de balance, una de resultados y una de patrimonio de D100 con su importe local por la tasa sintética",
  "Dejar la consolidación en ambas monedas :: Consultar en BPC el resultado consolidado en COP y en USD",
  "Mostrar el resumen de las dos monedas :: Ejecutar el plan | Leer el resumen en UX-09",
  "Fallar e indicar la moneda sin tasa :: Borrar la tasa de cierre USD de 2026.06 | Convertir | Revisar el paso",
 ],
 "HU-31": [
  "Registrar la diferencia en conversión en patrimonio :: Convertir 2026.06 | Consultar la cuenta de patrimonio configurada y las cuentas de resultados",
  "Cuadrar el balance convertido :: Convertir 2026.06 | Calcular activo menos pasivo y patrimonio en la moneda de presentación",
  "Mostrar la cuenta de destino y el paso :: Abrir UX-08 | Ejecutar y abrir UX-09",
 ],
 "HU-32": [
  "Crear el modelo de propiedad con la estructura inicial :: Ejecutar el paso Modelo de propiedad | Consultar en BPC los porcentajes y métodos del primer periodo",
  "Generar la plantilla C02 conectada :: Ejecutar el paso | Abrir C02 y actualizar",
  "Listar el modelo de propiedad y C02 en el plan :: Abrir UX-08 | Buscar los dos objetos",
  "Mostrar C02 en el catálogo :: Abrir UX-10 | Buscar C02",
 ],
 "HU-33": [
  "Eliminar inversión solo con integración global :: Consolidar 2026.06 | Revisar las eliminaciones de inversión de D200, D300 y D400",
  "Usar los porcentajes del periodo :: Cambiar el % de D200 a 90 en 2026.07 | Consolidar 2026.06 y 2026.07 | Comparar el interés minoritario de cada periodo",
  "Presentar el interés minoritario separado :: Consolidar 2026.06 | Abrir el patrimonio consolidado en R03",
  "Mostrar el paso de eliminación de inversión :: Ejecutar el plan | Revisar UX-09",
  "Dejar la diferencia de eliminación en una cuenta identificable :: Consolidar 2026.06 | Consultar la cuenta de diferencia entre inversión y patrimonio eliminado",
 ],
 "HU-34": [
  "No integrar línea a línea las coligadas :: Consolidar 2026.06 | Revisar las cuentas de D300 en el consolidado",
  "Reconocer la proporción del resultado de la coligada :: Consolidar 2026.06 | Comparar el resultado reconocido con el 30 % del resultado de D300",
  "No eliminar transacciones con coligadas :: Cargar una venta D100 a D300 de 500.000 COP | Consolidar | Consultar la transacción en el consolidado",
  "Excluir sociedades fuera del perímetro :: Consolidar 2026.06 | Buscar D900 en el consolidado",
 ],
 "HU-35": [
  "Editar participaciones y métodos por periodo :: Abrir UX-13 | Elegir Grupo Demo y 2026.07 | Editar los cuatro porcentajes y el método de D200 | Guardar",
  "Copiar las participaciones del periodo anterior :: Elegir 2026.08 sin datos | Pulsar Copiar del periodo anterior",
  "Validar rango y método por celda :: Escribir 150 en % control de D200 | Dejar sin método a D300 | Guardar",
  "Aplicar el cambio desde el periodo elegido :: Cambiar el % propiedad de D200 a 90 en 2026.07 | Consultar 2026.06 y 2026.07",
  "Registrar cada cambio de participación :: Cambiar el % de D200 | Consultar la auditoría de BPC del cambio",
 ],
 "HU-36": [
  "Validar activo igual a pasivo más patrimonio :: Ejecutar la importación de 2026.06 con D100 cuadrada y D200 descuadrada en 5.000.000 | Revisar el resultado de la validación",
  "Validar utilidad igual a resultado del ejercicio :: Alterar la utilidad del estado de resultados de D300 | Ejecutar la importación",
  "Validar que las cuentas con saldo estén mapeadas :: Registrar saldo en una cuenta sin mapear de D100 | Ejecutar la importación",
  "Dejar en C03 la sociedad que no cumple :: Ejecutar la importación con D200 descuadrada | Consultar BPC y C03",
  "Mostrar las sociedades rechazadas en el paso :: Ejecutar la importación con D200 descuadrada | Revisar el paso en UX-09",
 ],
 "HU-37": [
  "Ver la validación de cuadre en C03 :: Abrir C03 en UX-11 | Elegir 2026.06 y actualizar",
  "Marcar la sociedad no importada :: Ejecutar la importación con D200 descuadrada | Abrir C03",
  "Mostrar C03 sin importaciones :: Elegir el periodo 2026.12 sin importaciones | Actualizar C03",
  "Mostrar el error de conexión en C03 :: Desconectar EPM de BPC | Actualizar C03",
 ],
 "HU-38": [
  "Conciliar intercompañía por pareja :: Cargar D100 cuenta por cobrar a D200 de 1.000.000 y D200 cuenta por pagar de 990.000 | Abrir C01 y actualizar",
  "Marcar las parejas que superan el umbral :: Cargar una diferencia de 10.000 entre D100 y D200 | Actualizar C01",
  "Mostrar solo sociedades del grupo en C01 :: Cargar una transacción con el tercero T9001 | Actualizar C01",
  "Ejecutar C01 antes de consolidar :: Importar 2026.07 sin consolidar | Actualizar C01",
 ],
 "HU-39": [
  "Registrar un asiento con encabezado y líneas :: Abrir UX-14 | Ingresar grupo, periodo y moneda | Agregar dos líneas con sociedad, cuenta, intercompañía, débito, crédito y observación",
  "Guardar solo asientos cuadrados :: Ingresar débitos por 1.000.000 y créditos por 900.000 | Revisar Guardar asiento | Igualar los créditos y revisar de nuevo",
  "Rechazar líneas de sociedades bloqueadas :: Bloquear D200 en 2026.06 | Agregar una línea de D200 | Guardar",
  "Listar cada asiento con número, usuario y fecha :: Guardar un asiento cuadrado | Revisar la lista de asientos del periodo",
 ],
 "HU-40": [
  "Mostrar Bloquear o Desbloquear por sociedad :: Abrir UX-15 | Revisar el botón de cada sociedad",
  "Impedir cargas, asientos y reprocesos de una sociedad bloqueada :: Bloquear D200 en 2026.06 | Ejecutar la carga, registrar un asiento y reprocesar la consolidación de D200",
  "Bloquear una sociedad sin afectar a las demás :: Bloquear D200 | Registrar un asiento de D100 en el mismo periodo",
  "Registrar bloqueos y desbloqueos :: Bloquear y desbloquear D200 | Consultar la auditoría de BPC",
 ],
 "HU-41": [
  "Mostrar la matriz del cierre en orden :: Abrir UX-15 en 2026.06 | Leer las columnas de la matriz",
  "Enlazar el reporte que explica un error :: Descuadrar D200 | Abrir UX-15 | Pulsar el enlace del paso de cuadre",
  "Acceder a C02 y a Asientos desde el cierre :: Abrir UX-15 | Pulsar las hojas C02 y Asientos",
  "Mostrar el cierre no iniciado :: Elegir el periodo 2026.09 sin cargas | Abrir UX-15",
 ],
 "HU-42": [
  "Elegir los parámetros de la plantilla :: Abrir un reporte en UX-11 | Abrir la lista de Sociedad, Incluir filiales, Moneda y Periodo",
  "Cambiar entre sociedad sola y con filiales :: Elegir D100 sin filiales y actualizar | Elegir Incluir filiales y actualizar",
  "Limitar la moneda a local o dólares :: Abrir la lista Moneda | Escribir EUR en la celda",
  "Mostrar sin conexión y sin datos :: Desconectar EPM y actualizar | Reconectar, elegir 2026.12 y actualizar",
 ],
 "HU-43": [
  "Calcular cada rubro como suma de cuentas :: Actualizar R01 de D100 en 2026.06 | Comparar el rubro Efectivo con la suma de las cuentas mapeadas",
  "Reflejar un cambio de mapeo :: Mapear la cuenta 1199 al rubro Efectivo en UX-06 | Regenerar la plantilla | Actualizar el reporte",
  "Ver las cuentas de un rubro :: Abrir el detalle del rubro Efectivo",
 ],
 "HU-44": [
  "Entregar R01, R05 y R07 conectados a BPC :: Abrir R01, R05 y R07 | Revisar la conexión EPM de cada uno",
  "Cuadrar R01 con el balance consolidado :: Actualizar R01 del Grupo Demo en 2026.06 | Comparar cada total con el consolidado de BPC",
  "Mostrar en R05 las eliminaciones por pareja :: Actualizar R05 de 2026.06 | Revisar las parejas D100-D200",
  "Abrir y actualizar la ola 1 con EPM :: Abrir R01, R05 y R07 en Excel con EPM add-in | Actualizar cada uno",
 ],
 "HU-45": [
  "Entregar R02, R03, R04 y R06 con la estructura común :: Abrir los cuatro reportes | Revisar parámetros y tabla de rubros",
  "Separar interés minoritario y diferencia en conversión en R03 :: Actualizar R03 del Grupo Demo en 2026.06",
  "Reportar un rubro de flujo sin clasificar :: Agregar al mapeo un rubro de flujo sin clasificación | Generar los reportes | Revisar R04 en UX-10",
 ],
 "HU-46": [
  "Entregar R08 a R14 con la estructura común :: Abrir las siete notas | Revisar parámetros y tabla de rubros",
  "Incluir movimientos de activos fijos y deterioros :: Cargar en BW movimientos sintéticos de activos fijos y deterioros de 2026.06 | Actualizar R08, R10 y R11",
  "Cuadrar las notas con R01 :: Actualizar R01 y R08 a R14 de 2026.06 | Comparar cada nota con su rubro de R01",
 ],
 "HU-47": [
  "Entregar R15 a R20 con la estructura común :: Abrir los seis reportes | Revisar parámetros y tabla de rubros",
  "Resumir el grupo en dos monedas en R20 :: Actualizar R20 de 2026.06 en COP y en USD",
  "Abrir y actualizar los 20 reportes con EPM :: Abrir R01 a R20 en Excel con EPM add-in | Actualizar cada uno",
 ],
 "HU-48": [
  "Listar y filtrar el catálogo :: Abrir UX-10 | Revisar las columnas | Filtrar por el módulo Presupuesto",
  "Abrir la vista previa de C01 a C04 :: Pulsar los ID C01, C02, C03 y C04",
  "Descargar el paquete de reportes :: Elegir Consolidación y Presupuesto | Pulsar Descargar reportes | Abrir el .zip",
  "Mostrar la causa de un reporte con error :: Quitar el rubro de una plantilla del catálogo | Generar | Revisar el estado del reporte",
 ],
 "HU-49": [
  "Generar el modelo de presupuesto solo si está marcado :: Marcar Presupuesto en UX-04 | Ejecutar el plan | Revisar el paso y el modelo en BPC",
  "Separar el modelo de planeación del de consolidación :: Guardar un valor en el modelo de planeación | Consultar el modelo de consolidación",
  "Mostrar el modelo de presupuesto en el plan y la ejecución :: Abrir UX-08 | Ejecutar y abrir UX-09",
  "Disponer del presupuesto del ERP en el modelo :: Cargar presupuesto sintético 2027 en el ERP | Ejecutar el paso | Consultar el modelo de planeación",
 ],
 "HU-50": [
  "Capturar con celdas editables y totales :: Abrir UX-12 | Elegir D100, versión anual y 2027 | Ingresar valores en dos celdas",
  "Aceptar solo números :: Escribir «abc» en una celda editable | Pulsar Guardar datos",
  "Guardar en el modelo de planeación :: Ingresar 1.000.000 en una celda | Pulsar Guardar datos | Consultar el valor en BPC",
  "Mostrar la versión sin presupuesto :: Elegir la versión 6+6 sin datos",
 ],
 "HU-51": [
  "Elegir la versión de una lista :: Abrir la lista de versión en UX-12 y en P01",
  "Mantener independientes las versiones :: Guardar un valor en la versión 3+9 | Consultar la misma celda en la versión anual",
  "Mostrar la versión en el encabezado :: Elegir 3+9 | Abrir P01 a P05",
 ],
 "HU-52": [
  "Entregar P01 a P04 conectados al modelo de planeación :: Abrir P01 a P04 | Revisar la conexión EPM de cada uno",
  "Mostrar presupuesto, ejecución y variación en P01 :: Actualizar P01 de D100 en 2027",
  "Reflejar en P01 lo guardado en P05 :: Guardar un valor en P05 | Actualizar P01",
 ],
 "HU-53": [
  "Leer de BW los reportes de activos fijos y deterioros :: Abrir A01 a A03 y D01 a D02 | Revisar la conexión de cada uno",
  "Mostrar BW como fuente en el catálogo :: Abrir UX-10 | Revisar la fuente de A01 a D02",
  "Mostrar saldo inicial, movimientos y saldo final :: Actualizar A01 y D01 de 2026.06 | Sumar saldo inicial y movimientos de cada clase",
 ],
 "HU-54": [
  "Mostrar los campos del registro :: Ejecutar el plan | Abrir el registro en UX-09",
  "Registrar el 100 % de las ejecuciones con 12 meses de retención :: Ejecutar el plan completo | Comparar los pasos ejecutados con las entradas del registro | Revisar la política de retención de la base local",
  "Descargar el registro con los mismos campos :: Pulsar Descargar registro | Abrir el archivo",
  "Excluir credenciales del registro :: Ejecutar el plan con una credencial centinela | Buscar el centinela en el registro y en su descarga",
 ],
 "HU-55": [
  "Verificar ASVS nivel 2 sin hallazgos altos :: Ejecutar la lista de verificación ASVS nivel 2 sobre el release candidato | Consolidar los hallazgos por severidad",
  "Validar entradas en el servidor :: Enviar directamente al configurador, sin la interfaz, formularios con valores fuera de rango y un archivo no SAP con tipo falso | Revisar las respuestas y la base local",
  "Verificar que las dependencias no tienen vulnerabilidades altas :: Ejecutar el análisis SCA del pipeline sobre el release candidato",
  "Verificar el tratamiento de datos de terceros :: Revisar que la base local no guarda datos de terceros | Revisar que los archivos no SAP se borran del equipo después de cargarlos | Contrastar con la sección 9 del DAF",
 ],
 "HU-56": [
  "Revisar la accesibilidad automática de UX-01 a UX-10 :: Ejecutar axe-core con las reglas WCAG 2.1 A y AA sobre UX-01 a UX-10 en sus cuatro estados",
  "Operar el configurador con teclado y foco visible :: Recorrer UX-01 a UX-10 solo con Tab, Shift+Tab, Enter y Espacio | Revisar el indicador de foco en cada control",
  "Mostrar los estados con texto e ícono :: Revisar en escala de grises los estados de UX-02, UX-09 y UX-10",
 ],
 "HU-57": [
  "Avisar que la arquitectura B no está soportada :: Probar las conexiones con el doble de prueba de arquitectura B | Leer el aviso en UX-02",
  "Bloquear el avance con arquitectura B :: Revisar Continuar al diagnóstico | Consultar en el registro y en el doble de prueba las llamadas de creación",
  "Marcar el cliente B como No soportado :: Volver a UX-01 | Leer el estado y el motivo del cliente",
  "Registrar la detección de arquitectura B :: Abrir el registro de ejecuciones del cliente",
 ],
 "HU-58": [
  "Ejecutar todos los RF de arquitectura A en BW/4HANA :: Ejecutar la suite de regresión completa contra el ambiente QA BW/4HANA | Consolidar el resultado por RF",
  "No generar InfoCubos en BW/4HANA :: Ejecutar la generación completa | Listar en BW/4HANA los InfoProviders creados por tipo",
  "Agregar la variante sin cambiar clientes existentes :: Comparar la configuración exportada de los clientes antes y después de instalar la versión con la variante",
 ],
}

MANUAL = {
    ("HU-01", 1): "Spike de ejecución única: automatizarlo cuesta más que ejecutarlo una vez. Las mismas llamadas RFC quedan automatizadas en los casos de resiliencia de INT-02 e INT-03.",
    ("HU-01", 2): "Spike de ejecución única: automatizarlo cuesta más que ejecutarlo una vez. Las mismas llamadas RFC quedan automatizadas en los casos de resiliencia de INT-02 e INT-03.",
    ("HU-01", 3): "Spike de ejecución única: automatizarlo cuesta más que ejecutarlo una vez. El respaldo UJBR antes de cambiar BPC queda automatizado en HU-28.",
    ("HU-01", 4): "Revisión documental de un entregable: no hay comportamiento de software que ejecutar.",
    ("HU-01", 5): "Verificación de gestión del sprint en Azure DevOps: no hay comportamiento de software que ejecutar.",
    ("HU-04", 3): "Revisión documental del rol técnico. La ausencia de SAP_ALL en el usuario se automatiza en la prueba no funcional de RNF-Seg-03.",
    ("HU-55", 1): "La verificación ASVS nivel 2 incluye controles que requieren revisión experta; la parte automatizable corre como DAST, SAST y SCA.",
    ("HU-55", 4): "Revisión de cumplimiento legal (Ley 1581/2012) contra el diseño: requiere criterio experto.",
    ("HU-56", 2): "Validación visual del indicador de foco; la navegación por teclado se apoya en la revisión automática de axe-core.",
    ("HU-56", 3): "Validación visual del uso de color: la revisión automática no detecta si un estado depende solo del color.",
}

# Criterios demasiado breves para ser un resultado observable: se detalla lo que se ve.
RESULTADO = {
    ("HU-25", 3): "Con el archivo con errores, Confirmar carga queda deshabilitado; con el archivo válido, Confirmar carga se habilita.",
}

SMOKE = {("HU-02", 3), ("HU-03", 2), ("HU-06", 1), ("HU-07", 1), ("HU-08", 2), ("HU-09", 1), ("HU-11", 1),
         ("HU-13", 3), ("HU-14", 4), ("HU-15", 1), ("HU-27", 4), ("HU-29", 2), ("HU-30", 3), ("HU-44", 4)}

# ---------------------------------------------------------------- negativos de condición y excepción
NEG = [  # hu, criterio, título, pasos, resultado
 ("HU-05", 1, "Ocultar la validación ERP–BW con un solo sistema",
  ["Registrar ERP y BW con el mismo host y mandante", "Abrir UX-02", "Buscar el botón Validar conexión ERP–BW"],
  "El botón Validar conexión ERP–BW no aparece y Continuar al diagnóstico depende solo de las pruebas de conexión (RN-01)."),
 ("HU-07", 2, "Generar la bodega completa si no existe",
  ["Escanear un sandbox sin bodega financiera", "Revisar el indicador Bodega existente", "Abrir UX-08"],
  "El indicador Bodega existente queda sin marcar y UX-08 muestra los objetos de la bodega como Nuevo."),
 ("HU-12", 1, "Ocultar las versiones sin el módulo Presupuesto",
  ["Desmarcar Presupuesto en UX-04", "Buscar la sección de versiones", "Abrir UX-08"],
  "UX-04 no muestra la sección de versiones y UX-08 no lista el modelo de presupuesto."),
 ("HU-19", 1, "No generar activos fijos sin el módulo",
  ["Desmarcar Activos fijos en UX-04", "Ejecutar el plan", "Consultar en RSA1 los InfoProviders de activos fijos"],
  "UX-08 y UX-09 no muestran el paso «Activos fijos en BW» y en BW no se crea ningún objeto de activos fijos."),
 ("HU-20", 1, "No generar deterioros sin el módulo",
  ["Desmarcar Deterioros en UX-04", "Ejecutar el plan", "Consultar en RSA1 los InfoProviders de deterioros"],
  "UX-08 y UX-09 no muestran el paso «Deterioros en BW» y en BW no se crea ningún objeto de deterioros."),
 ("HU-49", 1, "No generar el modelo de presupuesto sin el módulo",
  ["Desmarcar Presupuesto en UX-04", "Ejecutar el plan", "Listar los modelos del ambiente BPC"],
  "UX-09 no muestra el paso «Modelo de presupuesto» y en BPC solo existe el modelo de consolidación."),
]

# ---------------------------------------------------------------- estados UX
UX_HU = {"UX-01": "HU-02", "UX-02": "HU-03", "UX-03": "HU-07", "UX-04": "HU-08", "UX-05": "HU-09", "UX-06": "HU-11",
         "UX-07": "HU-25", "UX-08": "HU-13", "UX-09": "HU-14", "UX-10": "HU-48", "UX-11": "HU-42", "UX-12": "HU-50",
         "UX-13": "HU-35", "UX-14": "HU-39", "UX-15": "HU-41"}
# Estados que un criterio ya verifica: se cita el criterio en lugar de duplicar el caso.
UX_CUBIERTO = {("UX-01", "vacío"): ("HU-02", 4), ("UX-02", "error"): ("HU-03", 2), ("UX-03", "error"): ("HU-07", 4),
               ("UX-06", "error"): ("HU-11", 5), ("UX-07", "error"): ("HU-25", 2), ("UX-08", "vacío"): ("HU-13", 4),
               ("UX-09", "error"): ("HU-14", 2), ("UX-10", "error"): ("HU-48", 4), ("UX-11", "vacío"): ("HU-42", 4),
               ("UX-11", "error"): ("HU-42", 4), ("UX-12", "vacío"): ("HU-50", 4), ("UX-13", "error"): ("HU-35", 3),
               ("UX-14", "error"): ("HU-39", 2), ("UX-15", "vacío"): ("HU-41", 4)}
RETARDO = "Activar el doble de prueba con 5 s de retardo en la respuesta"
UX_PASOS = {
 ("UX-01", "carga"): [RETARDO + " de la lista de clientes", "Abrir UX-01"],
 ("UX-01", "error"): ["Activar el doble de prueba que hace fallar la lectura de la base local", "Abrir UX-01", "Restaurar la base y pulsar Reintentar"],
 ("UX-02", "vacío"): ["Crear un cliente nuevo", "Abrir UX-02 sin datos de conexión"],
 ("UX-02", "carga"): [RETARDO + " de la llamada RFC de prueba de conexión", "Pulsar Probar conexión en la tarjeta BW"],
 ("UX-03", "vacío"): ["Escanear un sandbox sin bodega en BW y sin campos Z", "Abrir cada pestaña del inventario"],
 ("UX-03", "carga"): [RETARDO + " de cada llamada RFC de inventario", "Pulsar Escanear"],
 ("UX-04", "vacío"): ["Abrir UX-04 de un cliente nuevo sin módulos marcados"],
 ("UX-04", "carga"): [RETARDO + " de la configuración del cliente", "Abrir UX-04"],
 ("UX-04", "error"): ["Activar el doble de prueba que hace fallar la escritura en la base local", "Marcar un módulo", "Pulsar Continuar"],
 ("UX-05", "vacío"): ["Abrir UX-05 de un cliente sin sociedades"],
 ("UX-05", "carga"): [RETARDO + " de la lista de sociedades", "Abrir UX-05"],
 ("UX-05", "error"): ["Activar el doble de prueba que hace fallar la escritura en la base local", "Agregar la sociedad D100", "Pulsar Guardar"],
 ("UX-06", "vacío"): ["Abrir UX-06 de un cliente sin mapeos"],
 ("UX-06", "carga"): [RETARDO + " de la lectura de cuentas del ERP", "Abrir UX-06"],
 ("UX-07", "vacío"): ["Dejar todas las sociedades como SAP en UX-05", "Revisar la navegación lateral", "Abrir UX-07 por su dirección directa"],
 ("UX-07", "carga"): ["Subir el archivo de volumen D400_volumen.csv (100.000 filas)", "Observar la pantalla durante la subida"],
 ("UX-08", "carga"): [RETARDO + " del cálculo del plan", "Abrir UX-08"],
 ("UX-08", "error"): ["Dejar el diagnóstico con el escaneo fallido", "Abrir UX-08"],
 ("UX-09", "vacío"): ["Abrir UX-09 de un cliente sin ejecuciones"],
 ("UX-09", "carga"): ["Iniciar la ejecución del plan", "Observar la línea de pasos mientras corre el primer paso"],
 ("UX-10", "vacío"): ["Abrir UX-10 de un cliente sin generación ejecutada"],
 ("UX-10", "carga"): ["Elegir los 34 reportes y plantillas", "Pulsar Generar seleccionados", "Observar el catálogo durante la generación"],
 ("UX-11", "carga"): ["Simular con el proxy de red de pruebas 5 s de retardo hacia BPC", "Pulsar Actualizar en la cinta de EPM"],
 ("UX-12", "carga"): ["Ingresar valores en tres celdas", "Pulsar Guardar datos con 5 s de retardo simulado hacia BPC"],
 ("UX-12", "error"): ["Ingresar valores válidos en tres celdas", "Quitar al usuario financiero el permiso de escritura en el modelo de planeación", "Pulsar Guardar datos"],
 ("UX-13", "vacío"): ["Elegir Grupo Demo y el periodo 2026.12 sin participaciones"],
 ("UX-13", "carga"): ["Editar un porcentaje", "Pulsar Guardar con 5 s de retardo simulado hacia BPC"],
 ("UX-14", "vacío"): ["Crear un asiento nuevo sin líneas"],
 ("UX-14", "carga"): ["Registrar un asiento cuadrado", "Pulsar Guardar asiento con 5 s de retardo simulado hacia BPC"],
 ("UX-15", "carga"): ["Simular con el proxy de red de pruebas 5 s de retardo hacia BPC", "Abrir UX-15"],
 ("UX-15", "error"): ["Descuadrar D200 en 2026.06", "Ejecutar la importación", "Abrir UX-15"],
}

# ---------------------------------------------------------------- límites
LIM = [  # hu, criterio, origen, título, pasos, resultado
 ("HU-10", 3, "HU-10 / criterio 3 · límite inferior", "Rechazar un porcentaje de −0,01",
  ["Escribir −0,01 en % propiedad de D200", "Pulsar Guardar"], "La fila de D200 se marca con el error de rango y no se guarda."),
 ("HU-10", 3, "HU-10 / criterio 3 · exacto inferior", "Aceptar un porcentaje de 0",
  ["Escribir 0 en % propiedad de D900", "Pulsar Guardar", "Recargar UX-05"], "La fila se guarda sin error y D900 muestra 0 después de recargar."),
 ("HU-10", 3, "HU-10 / criterio 3 · exacto superior", "Aceptar un porcentaje de 100",
  ["Escribir 100 en % propiedad de D400", "Pulsar Guardar", "Recargar UX-05"], "La fila se guarda sin error y D400 muestra 100 después de recargar."),
 ("HU-10", 3, "HU-10 / criterio 3 · sobre el límite", "Rechazar un porcentaje de 100,01",
  ["Escribir 100,01 en % propiedad de D200", "Pulsar Guardar"], "La fila de D200 se marca con el error de rango y no se guarda."),
 ("HU-35", 3, "HU-35 / criterio 3 · límite inferior", "Rechazar una participación de −0,01 en C02",
  ["Escribir −0,01 en % consolidación de D200 en 2026.07", "Pulsar Guardar"], "La celda se marca, se muestra «No se guardó la fila…» con el rango 0 a 100 y la fila no se guarda."),
 ("HU-35", 3, "HU-35 / criterio 3 · sobre el límite", "Rechazar una participación de 100,01 en C02",
  ["Escribir 100,01 en % control de D200 en 2026.07", "Pulsar Guardar"], "La celda se marca, se muestra «No se guardó la fila…» con el rango 0 a 100 y la fila no se guarda."),
 ("HU-35", 4, "RN-14 · periodo anterior", "No alterar el periodo anterior al cambio",
  ["Cambiar el % propiedad de D200 de 80 a 90 en 2026.07", "Consultar C02 y consolidar 2026.06"], "2026.06 conserva 80 % y su interés minoritario no cambia."),
 ("HU-35", 4, "RN-14 · periodo del cambio", "Aplicar el cambio en el periodo en que se registra",
  ["Cambiar el % propiedad de D200 de 80 a 90 en 2026.07", "Consultar C02 y consolidar 2026.07"], "2026.07 muestra 90 % y el interés minoritario de D200 se calcula con 10 %."),
 ("HU-35", 4, "RN-14 · periodo posterior", "Heredar el cambio en el periodo siguiente",
  ["Con 90 % registrado en 2026.07, elegir 2026.08", "Pulsar Copiar del periodo anterior y guardar", "Consolidar 2026.08"], "2026.08 muestra 90 % y el interés minoritario se calcula con 10 %."),
 ("HU-39", 2, "RN-15 · bajo el cuadre", "Rechazar créditos menores en 0,01",
  ["Ingresar débitos por 1.000.000,00 y créditos por 999.999,99", "Revisar Guardar asiento"], "Guardar asiento queda deshabilitado y se muestra «Descuadrado» con la diferencia 0,01."),
 ("HU-39", 2, "RN-15 · exacto", "Guardar un asiento con débitos iguales a créditos",
  ["Ingresar débitos y créditos por 1.000.000,00", "Pulsar Guardar asiento"], "Guardar asiento está habilitado y el asiento aparece en la lista del periodo con su número."),
 ("HU-39", 2, "RN-15 · sobre el cuadre", "Rechazar créditos mayores en 0,01",
  ["Ingresar débitos por 1.000.000,00 y créditos por 1.000.000,01", "Revisar Guardar asiento"], "Guardar asiento queda deshabilitado y se muestra «Descuadrado» con la diferencia 0,01."),
 ("HU-36", 1, "RN-16 · bajo la tolerancia", f"Importar con diferencia de balance menor que la tolerancia ({T_CUADRE} − 0,01)",
  ["Ajustar los datos de D100 para que activo − (pasivo + patrimonio) sea 0,99", "Ejecutar la importación de 2026.06", "Consultar C03"], "D100 se importa a BPC y C03 la muestra como importada con diferencia 0,99."),
 ("HU-36", 1, "RN-16 · exacto en la tolerancia", f"Importar con diferencia de balance igual a la tolerancia ({T_CUADRE})",
  ["Ajustar los datos de D100 para que la diferencia sea 1,00", "Ejecutar la importación de 2026.06", "Consultar C03"], "D100 se importa a BPC: la diferencia igual a la tolerancia está dentro de ella. C03 la muestra como importada con diferencia 1,00."),
 ("HU-36", 1, "RN-16 · sobre la tolerancia", f"Rechazar con diferencia de balance mayor que la tolerancia ({T_CUADRE} + 0,01)",
  ["Ajustar los datos de D100 para que la diferencia sea 1,01", "Ejecutar la importación de 2026.06", "Consultar BPC y C03"], "D100 no se importa a BPC y C03 la marca «No importada» con diferencia 1,01."),
 ("HU-38", 2, "HU-38 / criterio 2 · bajo el umbral", f"No marcar una diferencia intercompañía bajo el umbral ({U_CONC} − 0,01)",
  ["Cargar entre D100 y D200 una diferencia de 999,99", "Actualizar C01"], "La pareja D100-D200 muestra la diferencia 999,99 sin la marca «Supera el umbral»."),
 ("HU-38", 2, "HU-38 / criterio 2 · exacto en el umbral", f"No marcar una diferencia igual al umbral ({U_CONC})",
  ["Cargar entre D100 y D200 una diferencia de 1.000,00", "Actualizar C01"], "La pareja D100-D200 muestra la diferencia 1.000,00 sin la marca «Supera el umbral»: solo se marca lo que lo supera."),
 ("HU-38", 2, "HU-38 / criterio 2 · sobre el umbral", f"Marcar una diferencia sobre el umbral ({U_CONC} + 0,01)",
  ["Cargar entre D100 y D200 una diferencia de 1.000,01", "Actualizar C01"], "La pareja D100-D200 se marca «Supera el umbral» con texto e ícono."),
]
RN_LIMITE = {"RN-14": "HU-35", "RN-15": "HU-39", "RN-16": "HU-36"}

# ---------------------------------------------------------------- no funcionales
NF = [  # hu, rnf, área, título, pasos, resultado, herramienta, tipo, justificación
 ("HU-57", "RNF-Comp-01", "Compatibilidad", "Regresión completa en arquitectura A y rechazo de B",
  ["Ejecutar la suite de regresión completa contra el sandbox BW 7.5 con BPC 10.1", "Ejecutar la detección con el doble de prueba de arquitectura B", "Consolidar el resultado por RF"],
  "Los 39 RF pasan en el ambiente de arquitectura A (1 de 1). La arquitectura B se detecta, se rechaza con mensaje y no crea objetos.", "Suite de regresión (Playwright, xUnit y EPM)", "Automatizado", ""),
 ("HU-47", "RNF-Comp-02", "Compatibilidad", "Abrir y actualizar los 34 reportes y plantillas con EPM add-in",
  ["Descargar el paquete completo de reportes", "Abrir cada reporte y plantilla de las tablas 5.6.1, 5.8.1 y 5.9.1 del DAF en Excel con EPM add-in", "Actualizar cada uno con el periodo 2026.06"],
  "34 de 34 reportes y plantillas abren y actualizan datos con EPM add-in, sin errores de conexión ni de fórmula.", "Excel COM con la API de automatización de EPM add-in", "Automatizado", ""),
 ("HU-02", "RNF-Comp-03", "Compatibilidad", "Instalar el prototipo en un equipo sin servidores",
  ["Instalar el .exe firmado desde Azure Artifacts en una VM Windows limpia", "Iniciar el configurador y crear un cliente", "Listar servicios, procesos y puertos en escucha"],
  "El configurador funciona instalado en 1 equipo con 0 servidores adicionales; solo escucha en 127.0.0.1.", "Script de pipeline sobre VM efímera", "Automatizado", ""),
 ("HU-58", "RNF-Comp-04", "Compatibilidad", "Agregar una variante sin tocar clientes ni variantes existentes",
  ["Calcular el hash de la configuración de los clientes y de las variantes del catálogo", "Agregar al catálogo una variante de prueba", "Calcular de nuevo los hashes y comparar"],
  "Agregar la variante requiere 0 cambios en la configuración de los clientes y 0 cambios en las variantes existentes.", "xUnit sobre el catálogo de variantes", "Automatizado", ""),
 ("HU-16", "RNF-Comp-05", "Compatibilidad", "Certificar la variante BW/4HANA con BPC 2021",
  ["Ejecutar la suite de regresión completa contra el ambiente QA BW/4HANA", "Listar los InfoProviders creados por tipo"],
  "Todos los RF de la arquitectura A se ejecutan completos en BW/4HANA con BPC 2021 modelo estándar y se generan 0 InfoCubos.", "Suite de regresión (Playwright, xUnit y EPM)", "Automatizado", ""),
 ("HU-18", "RNF-Rend-01", "Rendimiento", "Medir la frescura de los datos con la carga delta nocturna",
  ["Generar en el ERP el volumen diario sintético del Grupo Demo (o el del cliente piloto, si ya se conoce)", "Dejar correr la cadena delta y la importación encadenada durante 5 noches", "Registrar cada noche la hora de inicio y fin de la ventana y la hora de la última carga visible en R01"],
  "1 ejecución cada 24 horas en las 5 noches y antigüedad de los datos ≤ 24 horas al abrir un reporte. La duración de la ventana se registra para cerrar la pregunta abierta 7 del DAF.", "Monitor de cadenas de BW (RSPC) con extracción automática", "Automatizado", ""),
 ("HU-55", "RNF-Seg-01", "Seguridad", "Ejecutar DAST sobre el configurador",
  ["Iniciar el release candidato en el ambiente de QA", "Ejecutar OWASP ZAP (escaneo activo autenticado con el token de sesión) contra 127.0.0.1", "Clasificar los hallazgos por severidad"],
  "0 hallazgos altos o críticos abiertos antes de la entrega.", "OWASP ZAP en el pipeline", "Automatizado", ""),
 ("HU-55", "RNF-Seg-01", "Seguridad", "Ejecutar SAST, SCA y escaneo de secretos",
  ["Ejecutar el pipeline de release del configurador y del paquete Z", "Revisar los reportes de SAST, SCA y secretos"],
  "0 hallazgos altos abiertos en SAST y SCA, y 0 secretos detectados en el código y los artefactos.", "Pipeline de Azure DevOps (ADR-11)", "Automatizado", ""),
 ("HU-55", "RNF-Seg-01", "Seguridad", "Verificar la lista ASVS nivel 2",
  ["Recorrer los capítulos aplicables de ASVS 4.0 nivel 2 con la evidencia de diseño, código y pruebas", "Registrar cada control como cumple, no cumple o no aplica"],
  "Verificación ASVS nivel 2 sin hallazgos altos abiertos y con justificación de cada control no aplicable.", "Lista de verificación ASVS", "Manual",
  "Controles ASVS que requieren revisión experta de diseño y código; lo automatizable corre en DAST, SAST y SCA."),
 ("HU-04", "RNF-Seg-02", "Seguridad", "Buscar credenciales legibles después de una implementación completa",
  ["Registrar las tres conexiones con credenciales centinela únicas", "Ejecutar el plan completo y descargar el registro", "Buscar los centinelas en el perfil del usuario, la base SQLite, los registros, los temporales y el volcado de memoria del proceso cerrado"],
  "0 credenciales legibles en archivos de configuración o registros; las credenciales solo están en el Administrador de credenciales de Windows.", "Script de búsqueda de centinelas", "Automatizado", ""),
 ("HU-04", "RNF-Seg-03", "Seguridad", "Verificar los permisos mínimos del usuario técnico",
  ["Leer por RFC los roles y perfiles del usuario técnico", "Quitar uno a uno los roles Z de cada paso y ejecutar el plan"],
  "El usuario técnico tiene 0 perfiles SAP_ALL o SAP_NEW, y al quitar un rol Z solo falla el paso que lo necesita.", "xUnit con SAP NCo", "Automatizado", ""),
 ("HU-54", "RNF-Aud-01", "Auditoría", "Registrar el 100 % de las ejecuciones y conservarlas 12 meses",
  ["Ejecutar el plan completo con un error y un reintento", "Comparar los pasos ejecutados con las entradas del registro", "Insertar entradas sintéticas con 11 meses y 29 días, 12 meses y 12 meses y 1 día de antigüedad y ejecutar la depuración"],
  "100 % de las ejecuciones con registro (usuario, fecha y hora, sistema, paso, objetos y resultado). La depuración conserva las entradas de hasta 12 meses.", "xUnit sobre la base local", "Automatizado", ""),
 ("HU-35", "RNF-Aud-02", "Auditoría", "Auditar participaciones, asientos y bloqueos en BPC",
  ["Hacer 10 cambios de participación, 10 asientos y 10 bloqueos o desbloqueos", "Consultar la auditoría estándar de BPC", "Revisar la configuración de retención de la auditoría"],
  "30 de 30 cambios con usuario, fecha y hora, valor anterior y nuevo consultables en BPC, y retención configurada de al menos 12 meses.", "Excel COM con EPM y lectura RFC de la auditoría", "Automatizado", ""),
 ("HU-56", "RNF-Usa-01", "Usabilidad", "Revisar WCAG 2.1 AA de forma automática",
  ["Ejecutar axe-core con las reglas WCAG 2.1 A y AA sobre cada pantalla del configurador en sus cuatro estados"],
  "0 incumplimientos de nivel A y AA en la revisión automática de las pantallas.", "Playwright con axe-core", "Automatizado", ""),
 ("HU-56", "RNF-Usa-01", "Usabilidad", "Recorrer el configurador con lector de pantalla",
  ["Recorrer con NVDA el flujo de crear cliente, conexiones, diagnóstico y generación", "Registrar controles sin nombre, orden de lectura incorrecto o mensajes no anunciados"],
  "El flujo completo se puede ejecutar con NVDA y teclado; todos los mensajes de estado y error se anuncian.", "NVDA", "Manual",
  "Prueba exploratoria con tecnología de asistencia: la revisión automática no evalúa la experiencia con lector de pantalla."),
]

# ---------------------------------------------------------------- resiliencia de integraciones
INT_HU = {"INT-01": "HU-18", "INT-02": "HU-15", "INT-03": "HU-26", "INT-04": "HU-25", "INT-05": "HU-27", "INT-06": "HU-42"}
RES = {
 "INT-01": [
  ("feliz", "Extraer la delta del ERP a BW", ["Registrar en el ERP 10 documentos sintéticos", "Ejecutar la cadena delta", "Comparar totales por sociedad entre ERP y BW"], "BW recibe los 10 documentos y los totales por sociedad coinciden con el ERP."),
  ("timeout", "Perder el sistema origen durante la delta", ["Iniciar la cadena delta", "Cortar la conexión RFC entre BW y el ERP durante la extracción", "Revisar la cadena en RSPC"], "La cadena queda en error con la causa de comunicación, sin petición parcial activada y sin importación a BPC."),
  ("error", "Fallar la delta por un extractor inactivo", ["Desactivar 0FI_GL_14 en el ERP", "Ejecutar la cadena delta"], "La cadena queda en error con el mensaje del extractor y no carga datos parciales."),
  ("reintento", "Repetir la delta sin duplicar datos", ["Restablecer la conexión o el extractor", "Volver a ejecutar la cadena delta", "Comparar totales entre ERP y BW"], "La repetición carga los documentos pendientes una sola vez y los totales coinciden con el ERP."),
 ],
 "INT-02": [
  ("feliz", "Crear un objeto en BW por RFC", ["Invocar Z_ACC_* de creación con la definición JSON de un InfoObject de prueba", "Consultar el objeto en RSA1"], "La llamada responde OK con mensajes y el objeto queda activo en BW."),
  ("timeout", "Agotar el tiempo de una llamada RFC a BW", ["Activar el doble de prueba del gateway con 130 s de retardo", "Ejecutar el paso Bodega BW", "Revisar el registro"], "A los 120 s la llamada se corta, se hace un solo reintento automático y el paso queda en error de comunicación con los dos intentos en el registro."),
  ("error", "Recibir un error de negocio de BW", ["Quitar al usuario técnico la autorización de creación de InfoProviders", "Ejecutar el paso Bodega BW", "Revisar el registro"], "El paso queda en error con el mensaje de SAP, sin reintento automático, y el plan se detiene en ese paso."),
  ("reintento", "Reintentar la creación en BW sin duplicar", ["Devolver la autorización", "Pulsar Reintentar desde este paso", "Contar los objetos en RSA1 y en el registro"], "Los objetos ya creados responden EXISTE y no se modifican; se crean solo los faltantes, sin duplicados."),
 ],
 "INT-03": [
  ("feliz", "Crear dimensiones y miembros en BPC por RFC", ["Ejecutar el paso Modelo BPC", "Consultar dimensiones y miembros en BPC"], "Las llamadas Z_ACC_BPC_* responden OK y BPC muestra las dimensiones y miembros esperados."),
  ("timeout", "Agotar el tiempo de una llamada RFC a BPC", ["Activar el doble de prueba del gateway con 130 s de retardo en Z_ACC_BPC_*", "Ejecutar el paso Modelo BPC", "Revisar el registro"], "A los 120 s la llamada se corta, se hace un solo reintento automático y el paso queda en error de comunicación con los dos intentos en el registro."),
  ("error", "Recibir un error de negocio de BPC", ["Quitar al usuario técnico el permiso de escritura en el ambiente BPC", "Ejecutar el paso Modelo BPC"], "El paso queda en error con el mensaje de BPC, sin reintento automático, y el respaldo UJBR previo queda disponible."),
  ("reintento", "Reintentar la configuración de BPC sin duplicar", ["Devolver el permiso", "Pulsar Reintentar desde este paso", "Contar dimensiones y miembros en BPC"], "Lo ya creado responde EXISTE; BPC queda con una sola copia de cada dimensión y miembro."),
 ],
 "INT-04": [
  ("feliz", "Cargar a BW un archivo no SAP válido", ["Subir y confirmar D400_2026_06_valido.csv", "Consultar en BW los registros de D400"], "BW contiene todas las filas del archivo con importes iguales y D400 queda marcada como cargada."),
  ("timeout", "Interrumpir la carga de un archivo no SAP", ["Subir D400_volumen.csv", "Cerrar el configurador durante la confirmación", "Reabrir y consultar BW"], "No quedan datos parciales de D400 en BW y la sociedad no figura como cargada."),
  ("error", "Rechazar un archivo no SAP con formato inválido", ["Subir un archivo con columnas faltantes y codificación distinta", "Revisar UX-07"], "El archivo se rechaza con la lista de errores por fila y columna y Confirmar carga queda deshabilitado."),
  ("reintento", "Cargar de nuevo el archivo corregido", ["Subir y confirmar la versión corregida del archivo de D400 para 2026.06", "Consultar en BW los registros de D400"], "La nueva carga reemplaza la anterior del periodo y BW no duplica importes."),
 ],
 "INT-05": [
  ("feliz", "Importar de BW a BPC al final de la delta", ["Ejecutar la delta nocturna con la importación encadenada", "Comparar totales por sociedad entre BW y BPC"], "Las sociedades que cuadran quedan en BPC con totales iguales a BW."),
  ("timeout", "Fallar la importación por tiempo agotado", ["Activar en BPC un bloqueo del modelo durante la ejecución del paquete", "Ejecutar la importación", "Revisar la cadena y el paso"], "El paquete de Data Manager termina en error, la cadena lo muestra en rojo y el periodo no queda con datos parciales de la sociedad."),
  ("error", "Rechazar por cuadre en la importación", ["Descuadrar D200", "Ejecutar la importación"], "D200 no se importa (error de negocio, sin reintento automático) y queda en C03 con la diferencia."),
  ("reintento", "Repetir la importación de una sociedad y periodo", ["Corregir el cuadre de D200", "Ejecutar de nuevo el paquete para D200 y 2026.06", "Comparar totales entre BW y BPC"], "D200 queda en BPC con totales iguales a BW y sin importes duplicados."),
 ],
 "INT-06": [
  ("feliz", "Actualizar un reporte EPM desde BPC", ["Abrir R01 con la conexión con nombre", "Actualizar 2026.06"], "R01 muestra los datos del modelo y los totales coinciden con BPC."),
  ("timeout", "Perder la conexión de EPM durante la actualización", ["Iniciar la actualización de R01", "Cortar la red hacia BPC durante la actualización"], "Se muestra «No se pudo conectar a BPC; revisa la conexión de EPM» y el reporte no queda con valores a medias."),
  ("error", "Actualizar sin permiso en BPC", ["Iniciar sesión en EPM con un usuario sin acceso al modelo", "Actualizar R01"], "EPM muestra el error de autorización de BPC y el reporte no muestra datos."),
  ("reintento", "Actualizar de nuevo al recuperar la conexión", ["Restablecer la red o el permiso", "Pulsar Actualizar"], "R01 se actualiza con los datos del modelo sin reabrir el archivo."),
 ],
}
INT_NOMBRE = {"feliz": "caso feliz", "timeout": "timeout", "error": "error del tercero", "reintento": "reintento"}


# ---------------------------------------------------------------- herramienta por caso
def herramienta(hu):
    ux = HUS[hu]["ux"]
    if hu in ("HU-01",):
        return "SAP GUI y RFC directo"
    if any(u in ("UX-11", "UX-12", "UX-13", "UX-14", "UX-15") for u in ux[:1]):
        return "Excel COM con la API de automatización de EPM add-in"
    if ux:
        return "Playwright (.NET) sobre el configurador y verificación RFC con SAP NCo"
    return "xUnit con SAP NCo contra el ambiente SAP"


# ---------------------------------------------------------------- construcción de casos
casos = []
for h in BACKLOG["hus"]:
    hu = h["codigo"]
    entradas = C[hu]
    assert len(entradas) == len(h["criterios"]), f"{hu}: {len(entradas)} casos para {len(h['criterios'])} criterios"
    pre = PRE_HU.get(hu, PRE_EPICA[h["epica"]])
    base = {"hu": hu, "epica": h["epica"], "prioridad": h["prioridad"], "precondiciones": pre}

    def agrega(**k):
        c = dict(base)
        c.update(k)
        c.setdefault("tipo", "Automatizado")
        c.setdefault("justificacion_manual", "")
        c.setdefault("herramienta", herramienta(hu) if c["tipo"] == "Automatizado" else "Ejecución manual")
        c.setdefault("smoke", False)
        casos.append(c)

    for i, (e, crit) in enumerate(zip(entradas, h["criterios"]), 1):
        titulo, pasos = e.split(" :: ")
        just = MANUAL.get((hu, i), "")
        agrega(categoria="Funcional", titulo=titulo, criterio=i, origen=f"{hu} / criterio {i}",
               pasos=[p.strip() for p in pasos.split("|")], resultado=RESULTADO.get((hu, i), crit),
               tipo="Manual" if just else "Automatizado", justificacion_manual=just, smoke=(hu, i) in SMOKE)
    for (nh, i, t, p, r) in NEG:
        if nh == hu:
            agrega(categoria="Negativo", titulo=t, criterio=i, origen=f"{hu} / criterio {i} · excepción", pasos=p, resultado=r)
    for ux, h_ux in UX_HU.items():
        if h_ux != hu:
            continue
        for est in ("vacío", "carga", "error"):
            if (ux, est) in UX_CUBIERTO:
                continue
            nombre = next(p["nombre"] for p in UXI["pantallas"] if p["codigo"] == ux)
            agrega(categoria="Negativo", titulo=f"Mostrar el estado {est} de {ux} {nombre}", criterio=None,
                   origen=f"{ux} / estado {est}", pasos=UX_PASOS[(ux, est)], resultado=ESTADOS[ux][est])
    for (lh, i, o, t, p, r) in LIM:
        if lh == hu:
            agrega(categoria="Límite", titulo=t, criterio=i, origen=o, pasos=p, resultado=r)
    for (nh, rnf, area, t, p, r, tool, tipo, just) in NF:
        if nh == hu:
            agrega(categoria="No funcional", titulo=t, criterio=None, origen=rnf, rnf=rnf, area=area, pasos=p, resultado=r,
                   herramienta=tool if tipo == "Automatizado" else "Ejecución manual", tipo=tipo, justificacion_manual=just)
    for integ, ih in INT_HU.items():
        if ih == hu:
            for (k, t, p, r) in RES[integ]:
                agrega(categoria="Resiliencia", titulo=t, criterio=None, origen=f"{integ} / {INT_NOMBRE[k]}", int=integ,
                       pasos=p, resultado=r, herramienta="xUnit con dobles de prueba del gateway RFC y de la red" if integ != "INT-06"
                       else "Excel COM con EPM add-in y proxy de red de pruebas")

for n, c in enumerate(casos, 1):
    c["codigo"] = f"CP-{n:03d}"

# ---------------------------------------------------------------- JSON del validador
rnf_daf = [r["codigo"] for r in DAF["rnf"]]
int_daf = [i["codigo"] for i in DAF["int"]]
plan = {
    "casos": casos,
    "hus": {h["codigo"]: len(h["criterios"]) for h in BACKLOG["hus"]},
    "rnf_daf": rnf_daf,
    "rnf_cubiertos": sorted({c["rnf"] for c in casos if c.get("rnf")}),
    "int_daf": int_daf,
    "int_cubiertas": sorted(i for i in int_daf  # feliz, timeout, error del tercero y reintento
                            if {c["origen"] for c in casos if c.get("int") == i} >= {f"{i} / {n}" for n in INT_NOMBRE.values()}),
    "rn_con_umbral": sorted(RN_LIMITE),
    "rn_con_casos_limite": sorted({rn for rn, hu in RN_LIMITE.items()
                                   if sum(1 for c in casos if c["categoria"] == "Límite" and c["origen"].startswith(rn)) >= 3}),
    "datos_prueba_estrategia": "Sintéticos: Grupo Demo de cinco sociedades con movimientos, maestros, tasas y archivos no SAP generados por script; sin datos reales de clientes.",
    "smoke_definida": any(c["smoke"] for c in casos),
}
(D / "plan_pruebas.json").write_text(json.dumps(plan, ensure_ascii=False, indent=1), encoding="utf-8")

# ---------------------------------------------------------------- métricas
CATS = ["Funcional", "Negativo", "Límite", "No funcional", "Resiliencia"]
TOT = len(casos)
POR_CAT = {k: sum(1 for c in casos if c["categoria"] == k) for k in CATS}
MAN = [c for c in casos if c["tipo"] == "Manual"]
PCT_AUTO = round(100 * (TOT - len(MAN)) / TOT, 1)
SMK = [c for c in casos if c["smoke"]]

# ---------------------------------------------------------------- documento
B = []
Hd = lambda l, t: B.append({"type": "heading", "level": l, "text": t})
P = lambda t: B.append({"type": "paragraph", "text": t})
UL = lambda it: B.append({"type": "bullets", "items": it})
T = lambda rows, w, hdr=True: B.append({"type": "table", "rows": rows, "col_widths": w, **({} if hdr else {"has_header": False})})
AZ = "144E8C"
lab = lambda t: {"text": t, "fill": AZ, "align": "center"}
FILL_T = {"Automatizado": "D1E7DD", "Manual": "FFF3CD"}

B += [{"type": "portada", "text": "PLAN DE PRUEBAS — ACELERADOR DE CONSOLIDACIÓN FINANCIERA SAP BW/BPC"},
      {"type": "paragraph", "text": "Producto interno CM Consulting", "align": "center", "bold": True, "color": AZ, "size": 28},
      {"type": "page_break"}]
Hd(1, "Control de versiones")
T([["Versión", "Elaborado por", "Fecha", "Observaciones"],
   ["1.0", "CM Consulting", "16-Oct-26", "Versión para aprobación interna, derivada del DAF v1.2, el Documento UX v1.1, las Historias de Usuario v1.1 y la Arquitectura v1.0."]],
  [1200, 2200, 1500, 3938])
Hd(1, "Confidencialidad")
P([{"text": "La información contenida en este documento es propiedad intelectual de "}, {"text": "CM CONSULTING SAS", "bold": True}, {"text": "."}])
P([{"text": "Este documento es de uso interno de "}, {"text": "CM CONSULTING SAS", "bold": True},
   {"text": ". Su reproducción, distribución o comunicación por cualquier medio queda expresamente prohibida sin el consentimiento escrito de "},
   {"text": "CM CONSULTING SAS", "bold": True}, {"text": "."}])
B += [{"type": "page_break"}, {"type": "toc"}, {"type": "page_break"}]

# 1
Hd(1, "1. Introducción")
Hd(2, "1.1 Objetivo")
P("Este plan define cómo se verifica el Acelerador de consolidación financiera sobre SAP BW y SAP BPC antes de su uso con el primer cliente piloto. Los casos se derivan de los documentos aprobados, de modo que cada historia, criterio de aceptación, requerimiento no funcional e integración tiene al menos una prueba que lo verifica.")
Hd(2, "1.2 Documentos base")
T([["Documento", "Versión", "Uso en este plan"],
   ["Documento de Análisis Funcional (DAF)", "1.2", f"{len(rnf_daf)} RNF con su métrica, {len(DAF['rn'])} reglas de negocio y {len(int_daf)} integraciones."],
   ["Documento de Diseño UX", "1.1", f"{len(UXI['pantallas'])} pantallas con sus estados normal, vacío, carga y error."],
   ["Historias de Usuario", "1.1", f"{len(BACKLOG['hus'])} historias y {sum(plan['hus'].values())} criterios de aceptación."],
   ["Documento de Arquitectura", "1.0", "Ambientes, estrategia de errores de las integraciones y pipeline."]], [3200, 1000, 4638])
Hd(2, "1.3 Alcance")
UL(["Pruebas funcionales de QA de las 58 historias, incluidos los estados de las pantallas y los límites de las reglas con umbral.",
    "Pruebas no funcionales de los 12 RNF del DAF: compatibilidad, rendimiento de la carga delta, seguridad, auditoría y accesibilidad.",
    "Pruebas de resiliencia de las 6 integraciones según la estrategia de errores de la arquitectura.",
    "Gobierno de las pruebas unitarias que el equipo de desarrollo escribe en cada historia."])
Hd(2, "1.4 Exclusiones")
UL(["La arquitectura B: en la versión 1 solo se prueba que se detecta y se rechaza (HU-57).",
    "Pruebas de aceptación del cliente: el guion de UAT se deriva en la fase F9 a partir de los casos de prioridad Alta de este plan.",
    "Pruebas de los sistemas SAP del cliente y de su respaldo: son responsabilidad del cliente.",
    "Pruebas responsive: el configurador corre en el equipo del consultor y los reportes son Excel de escritorio; el DAF no define viewport."])

# 2
Hd(1, "2. Estrategia de pruebas")
T([["Nivel", "Responsable", "Qué cubre", "Cuándo corre"],
   ["Unitarias", "Equipo de desarrollo", "Reglas de negocio, generadores y validaciones; cobertura ≥ 70 % del código nuevo (DoD).", "Cada PR en el pipeline"],
   ["Integración", "Equipo de desarrollo", "Contratos RFC con el paquete Z, plantillas EPM y base local.", "Cada PR y cada despliegue a QA"],
   ["Funcionales de QA", "QA", f"{POR_CAT['Funcional'] + POR_CAT['Negativo'] + POR_CAT['Límite']} casos de este plan (secciones 5.1 a 5.9).", "Cada sprint y regresión por release"],
   ["No funcionales y resiliencia", "QA con el líder técnico", f"{POR_CAT['No funcional']} pruebas de RNF y {POR_CAT['Resiliencia']} de integraciones (sección 6).", "Antes de cada release y de UAT"],
   ["UAT", "Cliente piloto", "Guion derivado de los casos de prioridad Alta.", "Fase F9"]], [1700, 1700, 3638, 1800])
Hd(2, "2.1 Base de la pirámide: pruebas unitarias")
P("Las pruebas unitarias viven en el repositorio y las escribe el equipo de desarrollo como parte de cada historia. Este plan las gobierna: toda regla de negocio tiene su prueba unitaria en el módulo que la implementa. En cada ciclo QA verifica que el reporte de cobertura cumple el piso y que las reglas del ciclo aparecen en la suite. Un criterio cuya regla ya está probada unitariamente se prueba en QA una sola vez, de extremo a extremo.")
RN_MOD = {
    "RN-01": "Escáner (validación ERP–BW)", "RN-02": "Catálogo de variantes", "RN-03": "Escáner y orquestador; paquete Z (respuesta EXISTE)",
    "RN-04": "Generadores BW (cadenas de proceso)", "RN-05": "Configurador (sociedades y mapeo)", "RN-06": "Cargador de datos maestros (monedas)",
    "RN-07": "Configurador (sociedades) y cargador no SAP", "RN-08": "Generador de reportes EPM (conexión con nombre)", "RN-09": "Generador de reportes EPM",
    "RN-10": "Configurador (mapeo) y generador de reportes", "RN-11": "Generadores BPC (reglas de conversión)", "RN-12": "Configurador (módulos) y orquestador",
    "RN-13": "Generadores BPC (modelo de propiedad)", "RN-14": "Generadores BPC (propiedad por periodo)", "RN-15": "Generadores BPC (journals con balanceo obligatorio)",
    "RN-16": "Paquete Z (validación de cuadre) y cargador no SAP", "RN-17": "Generadores BPC (work status)", "RN-18": "Generadores BPC (cuenta de diferencia en conversión)",
    "RN-19": "Escáner y orquestador (rechazo de la arquitectura B)"}
T([["RN", "Regla", "Módulo con prueba unitaria", "Marco"]] +
  [[r["codigo"], r["regla"], RN_MOD[r["codigo"]], "ABAP Unit y xUnit" if "Paquete Z" in RN_MOD[r["codigo"]] or "paquete Z" in RN_MOD[r["codigo"]] else "xUnit"] for r in DAF["rn"]],
  [800, 4238, 2600, 1200])
Hd(2, "2.2 Automatización por defecto")
P(f"Todo caso se automatiza salvo justificación explícita. De {TOT} casos, {TOT - len(MAN)} son automatizados ({PCT_AUTO} %) y {len(MAN)} son manuales, cada uno con su justificación en la tabla del caso. Las herramientas son:")
T([["Capa", "Herramienta"],
   ["Configurador (UX-01 a UX-10)", "Playwright para .NET, con axe-core para accesibilidad."],
   ["Verificaciones en BW y BPC", "xUnit con SAP NCo 3.1, leyendo objetos y datos por las funciones de inventario del paquete Z."],
   ["Reportes y plantillas EPM (UX-11 a UX-15)", "Excel COM con la API de automatización de EPM add-in, en un agente Windows con Excel."],
   ["Resiliencia", "Dobles de prueba del gateway RFC (retardo y error configurables) y proxy de red de pruebas."],
   ["Seguridad", "OWASP ZAP (DAST), SAST, SCA y escaneo de secretos del pipeline."]], [3000, 5838])
Hd(2, "2.3 Suite de humo")
P(f"La suite de humo recorre el flujo crítico de extremo a extremo y corre en cada despliegue a QA y UAT. Tiene {len(SMK)} casos:")
T([["CP", "HU", "Caso"]] + [[c["codigo"], c["hu"], c["titulo"]] for c in SMK], [1000, 1000, 6838])
P("La regresión completa corre por release; los casos de BW/4HANA corren cuando el ambiente QA está encendido.")

# 3
Hd(1, "3. Criterios de entrada y salida de QA")
T([["Criterio", "Condición"],
   ["Entrada", "Historia con su DoD cumplida (incluidas las pruebas unitarias), pipeline en verde, build desplegado en QA y datos de prueba cargados."],
   ["Salida (gate F9)", "100 % de los casos ejecutados, 0 defectos críticos o altos abiertos, prueba de frescura de la carga delta dentro del umbral, DAST sin hallazgos altos y ASVS nivel 2 sin hallazgos altos abiertos."]], [2000, 6838])

# 4
Hd(1, "4. Ambientes y datos de prueba")
Hd(2, "4.1 Ambientes")
T([["Ambiente", "Infraestructura (Arquitectura v1.0)", "Uso en las pruebas"],
   ["DEV", "Equipo del desarrollador y sandbox SAP de arquitectura A (BW 7.5, BPC 10.1)", "Pruebas unitarias y de integración; ejecución de QA de la variante BW 7.5 en una ventana reservada del sandbox."],
   ["QA", "BW/4HANA con BPC 2021 modelo estándar en SAP CAL sobre Azure, encendido bajo demanda", "Certificación de la variante BW/4HANA, regresión por release y pruebas no funcionales."],
   ["UAT", "Sistema de calidad del primer cliente piloto", "Suite de humo y UAT del cliente (F9)."],
   ["Agente de pruebas", "VM Windows del pipeline con Excel y EPM add-in", "Casos de reportes y plantillas EPM."]], [1200, 3638, 4000])
Hd(2, "4.2 Datos de prueba")
P("Todos los datos de prueba son sintéticos: no se usan datos reales de clientes en DEV ni en QA. En UAT los datos son del cliente y no salen de su sistema; los archivos no SAP se borran del equipo después de cargarlos (Arquitectura, dominio Cumplimiento). Un script del repositorio genera el Grupo Demo en el sandbox y en el ambiente QA:")
T([["Sociedad", "Nombre", "En SAP", "Moneda local", "Método", "% propiedad"]] + [list(g) for g in GRUPO], [1100, 2238, 1000, 1400, 2000, 1100])
UL(["Planes de cuentas CO01 (D100, D300, D400, D900) y US01 (D200), con CeCo, CeBe y jerarquía sintéticos.",
    "Movimientos del periodo 2026.06 con transacciones intercompañía entre D100, D200 y D300; periodos 2026.07 y 2026.08 para los cambios de participación.",
    "Tasas de cierre, promedio e histórica COP/USD de 2026.06 a 2026.08.",
    "Archivos no SAP de D400: válido, con errores, descuadrado y de volumen (100.000 filas).",
    "Presupuesto sintético 2027 en el ERP para las versiones anual, 3+9 y 6+6.",
    f"Tolerancia de cuadre {T_CUADRE} y umbral de conciliación {U_CONC} en moneda local: valores provisionales hasta cerrar la pregunta 14 del DAF; los casos de límite se ajustan a los definitivos."])

# 5
Hd(1, "5. Casos de prueba funcionales")
P(f"Cada historia lista sus casos: uno por criterio de aceptación (funcional), los negativos que derivan de las excepciones y de los estados de las pantallas, y los de límite de las reglas con umbral. El resultado esperado de un caso funcional es el criterio de aceptación. Los casos no funcionales y de resiliencia de cada historia se detallan en la sección 6. Total de la sección: {POR_CAT['Funcional'] + POR_CAT['Negativo'] + POR_CAT['Límite']} casos.")
for k, (ep, nom) in enumerate(EPICAS.items(), 1):
    hs = [h for h in BACKLOG["hus"] if h["epica"] == ep]
    ce = [c for c in casos if c["epica"] == ep and c["categoria"] in ("Funcional", "Negativo", "Límite")]
    B.append({"type": "page_break"})
    Hd(2, f"5.{k} {ep} — {nom}")
    P(f"{len(hs)} historias · {len(ce)} casos · precondición general: {PRE_EPICA[ep]}")
    for h in hs:
        ch = [c for c in ce if c["hu"] == h["codigo"]]
        Hd(3, f"{h['codigo']} — {h['titulo']}")
        if h["codigo"] in PRE_HU:
            P([{"text": "Precondición: ", "bold": True}, {"text": PRE_HU[h["codigo"]]}])
        rows = [["CP", "Caso y origen", "Pasos", "Resultado esperado", "Tipo"]]
        for c in ch:
            tipo = c["tipo"] + (" · Humo" if c["smoke"] else "")
            caso = [{"text": c["titulo"], "bold": True}, {"text": f" ({c['origen']} · {c['categoria']})"}]
            if c["tipo"] == "Manual":
                caso.append({"text": f" Manual: {c['justificacion_manual']}", "italic": True})
            rows.append([{"text": c["codigo"], "bold": True}, caso,
                         " ".join(f"{i}) {p}." if not p.endswith(".") else f"{i}) {p}" for i, p in enumerate(c["pasos"], 1)),
                         c["resultado"], {"text": tipo, "fill": FILL_T[c["tipo"]], "align": "center"}])
        T(rows, [800, 2100, 2900, 2238, 800])

# 6
B.append({"type": "page_break"})
Hd(1, "6. Pruebas no funcionales y de resiliencia")
P("Cada RNF del DAF tiene su prueba con el umbral numérico del DAF, y cada integración tiene casos de caso feliz, timeout, error del tercero y reintento.")


def tabla_nf(filtro):
    rows = [["CP", "RNF", "Prueba", "Pasos", "Criterio de paso", "Tipo"]]
    for c in casos:
        if c["categoria"] == "No funcional" and filtro(c):
            rows.append([{"text": c["codigo"], "bold": True}, c["rnf"], [{"text": c["titulo"], "bold": True}, {"text": f" ({c['hu']} · {c['herramienta']})"}] +
                         ([{"text": f" Manual: {c['justificacion_manual']}", "italic": True}] if c["tipo"] == "Manual" else []),
                         " ".join(f"{i}) {p}." for i, p in enumerate(c["pasos"], 1)), c["resultado"],
                         {"text": c["tipo"], "fill": FILL_T[c["tipo"]], "align": "center"}])
    T(rows, [800, 1100, 1900, 2338, 1900, 800])


Hd(2, "6.1 Carga y rendimiento")
P("El único RNF de rendimiento del DAF es la frescura de los datos (RNF-Rend-01): no hay usuarios concurrentes que modelar, porque el configurador es local y los reportes se actualizan desde EPM. La prueba mide la ventana nocturna con el volumen diario. La duración máxima de la ventana es la pregunta abierta 7 del DAF; la prueba registra la duración para cerrarla.")
tabla_nf(lambda c: c["area"] == "Rendimiento")
Hd(2, "6.2 Seguridad")
P("Nivel OWASP ASVS 2 heredado del DAF (sección 9). El configurador escucha solo en 127.0.0.1 con token de sesión (ADR-05); el DAST se ejecuta contra esa dirección en el ambiente de QA antes de cada release y antes del Go-Live.")
tabla_nf(lambda c: c["area"] == "Seguridad")
Hd(2, "6.3 Compatibilidad, accesibilidad y auditoría")
tabla_nf(lambda c: c["area"] in ("Compatibilidad", "Usabilidad", "Auditoría"))
Hd(2, "6.4 Resiliencia de las integraciones")
P("Estrategia de errores de la arquitectura (sección 8.3): timeout de 120 s por llamada RFC; un reintento automático solo en errores de comunicación, nunca en errores de negocio; cada paso es idempotente y un paso fallido detiene el plan y se reanuda desde ese paso.")
rows = [["CP", "INT", "Escenario", "Pasos", "Resultado esperado"]]
for c in casos:
    if c["categoria"] == "Resiliencia":
        rows.append([{"text": c["codigo"], "bold": True}, c["int"], [{"text": c["titulo"], "bold": True}, {"text": f" ({c['origen'].split(' / ')[1]} · {c['hu']})"}],
                     " ".join(f"{i}) {p}." for i, p in enumerate(c["pasos"], 1)), c["resultado"]])
T(rows, [800, 900, 2100, 2638, 2400])

# 7
B.append({"type": "page_break"})
Hd(1, "7. Matriz de trazabilidad CP ↔ HU ↔ RF")
P("Gate F7: 100 % de las historias con casos y cada criterio de aceptación cubierto.")
rows = [["HU", "Criterios", "Casos funcionales (criterio → CP)", "Otros casos", "RF / RNF / INT de origen"]]
for h in BACKLOG["hus"]:
    ch = [c for c in casos if c["hu"] == h["codigo"]]
    func = ", ".join(f"{c['criterio']}→{c['codigo'][3:]}" for c in ch if c["categoria"] == "Funcional")
    otros = ", ".join(c["codigo"][3:] for c in ch if c["categoria"] != "Funcional") or "—"
    rows.append([{"text": h["codigo"], "bold": True}, str(len(h["criterios"])), func, otros, ", ".join(h["trazabilidad"])])
T(rows, [800, 900, 3138, 1800, 2200])
Hd(2, "7.1 Estados de pantalla")
P("Cada estado vacío, carga y error de las pantallas tiene un caso. Cuando un criterio de aceptación ya lo verifica, se cita ese caso.")
cp_de = {(c["hu"], c["criterio"]): c["codigo"] for c in casos if c["categoria"] == "Funcional"}
rows = [["Pantalla", "Vacío", "Carga", "Error"]]
for p in UXI["pantallas"]:
    fila = [f"{p['codigo']} {p['nombre']}"]
    for est in ("vacío", "carga", "error"):
        if (p["codigo"], est) in UX_CUBIERTO:
            fila.append(cp_de[UX_CUBIERTO[(p["codigo"], est)]])
        else:
            fila.append(next(c["codigo"] for c in casos if c["origen"] == f"{p['codigo']} / estado {est}"))
    rows.append(fila)
T(rows, [3238, 1866, 1867, 1867])
Hd(2, "7.2 RNF, integraciones y reglas con umbral")
rows = [["Elemento", "Métrica o estrategia", "Casos"]]
for r in DAF["rnf"]:
    rows.append([r["codigo"], r["metrica"], ", ".join(c["codigo"] for c in casos if c.get("rnf") == r["codigo"])])
for i in DAF["int"]:
    rows.append([i["codigo"], f"{i['sistema']} · {i['mecanismo']}", ", ".join(c["codigo"] for c in casos if c.get("int") == i["codigo"])])
for rn, hu in RN_LIMITE.items():
    regla = next(r["regla"] for r in DAF["rn"] if r["codigo"] == rn)
    rows.append([rn, regla, ", ".join(c["codigo"] for c in casos if c["categoria"] == "Límite" and c["origen"].startswith(rn))])
T(rows, [1300, 5238, 2300])

# 8
Hd(1, "8. Gestión de defectos")
T([["Severidad", "Definición"],
   ["Crítica", "Crea, modifica o borra objetos o datos en el SAP del cliente de forma no esperada, expone credenciales o datos, o deja cifras consolidadas incorrectas sin aviso."],
   ["Alta", "Un paso del plan, una regla de consolidación o un reporte no funciona y no hay alternativa."],
   ["Media", "La función falla con una alternativa disponible, o un mensaje o estado no corresponde al diseño."],
   ["Baja", "Defecto cosmético o de redacción que no afecta el uso."]], [1500, 7338])
UL(["Flujo: reporte en Azure DevOps como Bug vinculado al caso y a la historia → triage diario → corrección → reprueba del caso y de la suite de humo.",
    "Rechazo de la entrega a UAT: 1 defecto crítico o 3 altos abiertos.",
    "Cada bug lleva pasos, evidencia (captura o registro de ejecuciones) y el ambiente donde ocurrió."])

# 9
Hd(1, "9. Métricas")
T([["Métrica", "Cálculo", "Uso"],
   ["Defect escape rate", "Defectos hallados en UAT o en producción ÷ total de defectos", "Eficacia de QA por release"],
   ["% casos automatizados", f"Casos automatizados ÷ total (línea base {PCT_AUTO} %)", "Mantener por encima de 70 %"],
   ["% aprobación en primera ejecución", "Casos aprobados al primer intento ÷ casos ejecutados", "Calidad de lo que llega a QA"],
   ["Densidad de defectos por épica", "Defectos ÷ story points de la épica", "Dónde reforzar revisión y pruebas unitarias"]], [2600, 3638, 2600])

# 10
Hd(1, "10. Ciclos y cronograma")
UL(["Ciclo por sprint: los casos de las historias terminadas en el sprint, más la suite de humo.",
    "Regresión completa por release, en los puntos de demostración del backlog: fin de E2, fin de E4, consolidación con la ola 1 (HU-44) y cierre mensual completo.",
    "Certificación BW/4HANA (HU-58 y RNF-Comp-05) en una ventana del ambiente QA encendido bajo demanda.",
    "Frescura de la carga delta, DAST, SAST, SCA y verificación ASVS antes de entregar a UAT.",
    "La fecha de cada ciclo la fija el cronograma de sprints."])

# 11
Hd(1, "11. Aprobación interna")
P("Este plan lo aprueban internamente QA, como autor, y el líder técnico de CM Consulting. No requiere firma del cliente.")
T([["Rol", "Nombre", "Fecha", "Aprobación"], ["QA (autor)", "", "", ""], ["Líder técnico CM", "", "", ""]], [2400, 2638, 1800, 2000])
P("El guion de UAT del cliente se genera en la fase F9 a partir de los casos de prioridad Alta de este plan.")

(D / "bloques_pruebas.json").write_text(json.dumps({"blocks": B}, ensure_ascii=False, indent=1), encoding="utf-8")
print("casos", TOT, POR_CAT, "manuales", len(MAN), "auto %", PCT_AUTO, "humo", len(SMK))
