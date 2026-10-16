# Brief de diseño — Acelerador de consolidación financiera SAP BW/BPC

Fuente: Documento de Análisis Funcional v1.0 (16-Oct-26). Cada pantalla cita los RF que la originan y las reglas (RN) que definen sus validaciones. No agregues pantallas ni campos que no estén aquí.

---

## 1. Prompt maestro

**Proyecto.** Aplicación web interna de CM Consulting que guía a un consultor SAP en la implementación de consolidación financiera para un cliente. El consultor se conecta al SAP del cliente, revisa lo que ya existe, elige los módulos y las sociedades, mapea las cuentas y la herramienta genera la bodega en SAP BW, el modelo en SAP BPC y los reportes de Excel. Hoy ese trabajo es manual y se repite en cada cliente.

**Hay dos superficies que prototipar:**
1. **Configurador web** (UX-01 a UX-10). Lo usa el **Consultor implementador**.
2. **Plantillas de Excel con el complemento EPM** (UX-11 y UX-12). Las usa el **Usuario financiero** del cliente. Dibújalas como una hoja de Excel con la cinta del complemento "EPM" arriba; no son pantallas web.

**Actores (usa estos nombres exactos):**
- **Consultor implementador:** consultor de CM que usa el configurador web.
- **Usuario financiero:** persona del área financiera del cliente que abre y actualiza los reportes en Excel y captura el presupuesto.
- Administrador SAP del cliente: no opera pantallas; solo se menciona en mensajes de ayuda (crea el usuario técnico e importa transportes).

**Marca y sistema visual.** Producto interno de CM Consulting:
- Primario azul **#144E8C**, acento amarillo **#F2B707** (solo para resaltar; nunca texto sobre blanco), títulos en azul **#365F91**.
- Tipografía **Ubuntu** (respaldo: Segoe UI, sans-serif).
- Neutros grises con leve tinte azul. Estética limpia y corporativa, densidad media. Es una herramienta técnica: prioriza tablas legibles y estados claros sobre decoración.
- Logo "CMConsulting" en la esquina superior izquierda.

**Plataforma.** Web responsive. Uso principal en escritorio (1440 px); debe ser usable en 360 px, donde las tablas pasan a tarjetas y la navegación lateral a un menú.

**Accesibilidad (RNF-Usa-01): WCAG 2.1 nivel AA.**
- Contraste mínimo de 4,5:1.
- Todo operable con teclado y con foco visible.
- Etiquetas visibles en todos los campos.
- El estado nunca se comunica solo con color: siempre va con ícono y texto.

**Componentes globales del configurador:**
- **Barra superior:** logo, nombre del cliente activo con un selector para volver a UX-01, y una insignia con la arquitectura detectada:
  - "Arquitectura A · BW 7.5 + BPC estándar"
  - "Arquitectura B · S/4HANA con BW y BPC embebidos"
- **Navegación lateral por pasos**, en este orden: Conexiones, Diagnóstico, Módulos, Sociedades, Mapeo de cuentas, Sociedades no SAP, Plan de generación, Ejecución, Reportes.
  - Cada paso muestra un estado: pendiente, completo o con error.
  - "Sociedades no SAP" solo aparece si hay alguna sociedad marcada como no SAP en UX-05.
- **Notificaciones tipo toast** para acciones completadas y errores.
- **Mensajes de error** que dicen qué falló y cómo corregirlo. Nunca códigos crudos solos: si SAP devuelve un código, se muestra junto a la explicación.
- **Credenciales siempre enmascaradas** (RNF-Seg-02).

**Tono.** Español de Colombia, profesional y directo. Los botones dicen exactamente lo que hacen ("Probar conexión", "Generar", "Descargar reportes").

**Datos de ejemplo.** Usa valores ilustrativos, marcados como ejemplo, nunca empresas reales:
- Sociedades: 1000 Matriz (COP), 2000 Filial Colombia (COP), 3000 Filial Panamá (USD), 9000 Filial no SAP (COP).
- Rubro: F-00-101 Efectivo y equivalentes de efectivo.

---

## 2. Especificación por pantalla

### UX-01 — Clientes
- **Usuario:** Consultor implementador
- **Propósito:** ver las implementaciones en curso y empezar una nueva.
- **RF que soporta:** RF-01, RF-04
- **Datos visibles:**
  - Tabla con: cliente, arquitectura (A/B y versión), paso actual, estado, fecha y hora de la última ejecución, usuario de la última ejecución.
- **Componentes:**
  - Tabla con búsqueda por nombre.
  - Botón primario "Nuevo cliente", que abre un diálogo con el campo "Nombre del cliente".
  - Fila clicable que lleva a UX-02.
- **Estados:**
  - Normal.
  - Vacío: "Aún no hay clientes. Crea el primero para empezar." con el botón.
  - Carga: filas esqueleto.
  - Error: "No se pudo cargar la lista de clientes" con "Reintentar".
- **Validaciones visibles:**
  - El nombre del cliente es obligatorio y no puede repetirse.
- **Interacciones:**
  - Crear cliente lleva a UX-02 con el formulario vacío.
- **Responsive (360 px):** cada cliente es una tarjeta con nombre, insignia de arquitectura y estado.

### UX-02 — Conexiones
- **Usuario:** Consultor implementador
- **Propósito:** registrar y probar la conexión a los tres sistemas SAP del cliente.
- **RF que soporta:** RF-01, RF-02, RF-04
- **Datos visibles:**
  - Un bloque por sistema (SAP ERP, SAP BW, SAP BPC), cada uno con: sistema, host, mandante, usuario técnico y credencial (entidad Conexión).
  - Resultado de cada prueba: exitosa, o fallida con el mensaje que devolvió el sistema.
  - Arquitectura detectada y versiones de BW y BPC.
- **Componentes:**
  - Tres tarjetas con formulario.
  - Botón "Probar conexión" en cada tarjeta.
  - Botón "Validar conexión ERP–BW".
  - Panel "Arquitectura detectada" con la insignia A o B.
  - Botón "Continuar al diagnóstico".
- **Estados:**
  - Normal.
  - Vacío: formulario sin datos, con ayuda "Pide al Administrador SAP del cliente el usuario técnico".
  - Carga: spinner dentro del botón mientras prueba.
  - Error: la tarjeta en rojo con el mensaje devuelto y la sugerencia de revisar host, mandante o credencial.
- **Validaciones visibles:**
  - Host, mandante, usuario técnico y credencial son obligatorios.
  - La credencial nunca se muestra en claro; hay un botón para revelarla mientras se mantiene presionado (RNF-Seg-02).
  - "Validar conexión ERP–BW" solo está activo cuando ERP y BW son sistemas separados (RN-01). En la arquitectura B se oculta y aparece la nota "ERP y BW están integrados; no requiere validación".
  - "Continuar" queda deshabilitado hasta que las conexiones requeridas hayan pasado la prueba.
  - Si la versión detectada no tiene variante disponible, se muestra el aviso "Versión no soportada aún" y no se puede continuar (RNF-Comp-04).
- **Interacciones:**
  - Al probar las conexiones el sistema detecta la arquitectura (RF-04) y actualiza la insignia de la barra superior.
  - "Continuar al diagnóstico" lleva a UX-03.
- **Responsive (360 px):** las tarjetas se apilan una debajo de otra.

### UX-03 — Diagnóstico e inventario
- **Usuario:** Consultor implementador
- **Propósito:** ver qué existe ya en el sistema del cliente antes de generar nada.
- **RF que soporta:** RF-03, RF-04, RF-09
- **Datos visibles:**
  - Pestañas: Extractores activos, InfoProviders (InfoCubos o aDSO), Modelos BPC, Campos Z.
  - Para cada objeto: nombre técnico, descripción y estado (activo o inactivo).
  - Indicador "Bodega existente: sí/no".
- **Componentes:**
  - Pestañas con tablas.
  - Casillas en la pestaña Campos Z para elegir cuáles incorporar a la bodega.
  - Botón "Volver a escanear".
  - Botón "Continuar a módulos".
- **Estados:**
  - Normal.
  - Vacío por pestaña: "No se encontraron campos Z" o "El cliente no tiene bodega en BW: se generará completa".
  - Carga: barra de progreso "Escaneando BW y BPC…".
  - Error: "El escaneo no terminó" con el paso que falló y "Reintentar".
- **Validaciones visibles:**
  - Si la bodega existe, aparece el aviso "Se usará la bodega existente; no se recrea" (RN-03).
- **Interacciones:**
  - Los campos Z que se marcan aquí aparecen después en el plan de generación (UX-08).
- **Responsive (360 px):** las pestañas se convierten en un selector desplegable y las tablas en tarjetas.

### UX-04 — Módulos
- **Usuario:** Consultor implementador
- **Propósito:** elegir qué módulos se implementan.
- **RF que soporta:** RF-05, RF-23, RF-24, RF-25
- **Datos visibles:** cuatro tarjetas con nombre del módulo, qué genera y casilla "seleccionado".

  | Módulo | Qué genera |
  |---|---|
  | Consolidación | Bodega BW + modelo BPC + 20 reportes |
  | Presupuesto | Carga desde 0FI_GL_14 + modelo de planeación BPC + 5 reportes |
  | Activos fijos | Carga en BW + 3 reportes |
  | Deterioros | Carga en BW + 2 reportes |

- **Componentes:**
  - Tarjetas seleccionables con casilla.
  - Resumen inferior: "N módulos · N reportes".
  - Botón "Continuar".
- **Estados:**
  - Normal.
  - Vacío: ninguno marcado, con mensaje "Elige al menos un módulo".
  - Carga: tarjetas esqueleto.
  - Error: "No se pudo guardar la selección".
- **Validaciones visibles:**
  - Las tarjetas de Activos fijos y Deterioros muestran la nota "Se queda en BW; no crea modelo en BPC" (RN-12).
  - "Continuar" queda deshabilitado si no hay ningún módulo marcado.
- **Interacciones:**
  - Marcar o desmarcar actualiza el resumen y el plan (UX-08).
- **Responsive (360 px):** tarjetas en una sola columna.

### UX-05 — Sociedades del grupo
- **Usuario:** Consultor implementador
- **Propósito:** definir qué sociedades entran en la consolidación.
- **RF que soporta:** RF-06, RF-13
- **Datos visibles:**
  - Tabla con: código, nombre, en SAP (sí/no), moneda local, sociedad matriz y plan de cuentas.
  - Indicador del total de sociedades y de cuántas son no SAP.
- **Componentes:**
  - Tabla editable con botón "Agregar sociedad".
  - Interruptor "en SAP" en cada fila.
  - Selector de sociedad matriz en cada fila.
- **Estados:**
  - Normal.
  - Vacío: "Agrega las sociedades del grupo que entran en la consolidación".
  - Carga: filas esqueleto.
  - Error: "No se pudieron guardar las sociedades".
- **Validaciones visibles:**
  - Solo se agregan sociedades del grupo. La ayuda dice "Clientes y proveedores no son entidades de consolidación" (RN-07).
  - Cada sociedad muestra su propio plan de cuentas, que puede ser distinto entre sociedades (RN-05).
  - Nota fija: "La consolidación se presenta en moneda local y en dólares" (RN-06).
- **Interacciones:**
  - Marcar una sociedad como no SAP habilita el paso UX-07 en la navegación.
- **Responsive (360 px):** cada sociedad es una tarjeta con el código y el nombre como título.

### UX-06 — Mapeo de cuentas
- **Usuario:** Consultor implementador
- **Propósito:** asignar las cuentas contables de cada sociedad a los rubros de los reportes.
- **RF que soporta:** RF-07
- **Datos visibles:**
  - A la izquierda, el árbol de rubros con código, nombre y cantidad de cuentas mapeadas (p. ej. "F-00-101 Efectivo y equivalentes de efectivo · 12 cuentas").
  - A la derecha, la lista de cuentas con código, plan de cuentas, descripción y nodo de jerarquía.
  - Contador de cuentas sin mapear.
- **Componentes:**
  - Árbol de rubros.
  - Tabla de cuentas con filtro por sociedad y por plan de cuentas.
  - Asignación con casillas y botón "Asignar al rubro", o arrastrando cuentas al rubro.
  - Filtro "solo sin mapear".
- **Estados:**
  - Normal.
  - Vacío: "Aún no hay cuentas mapeadas. Elige un rubro y asígnale cuentas".
  - Carga: esqueleto en el árbol y en la tabla.
  - Error: "No se pudo guardar el mapeo".
- **Validaciones visibles:**
  - El rubro muestra la suma de las cuentas mapeadas como vista previa: "El valor del rubro es la suma de sus cuentas" (RN-10).
  - Las cuentas se filtran por el plan de cuentas de la sociedad elegida (RN-05).
- **Interacciones:**
  - Al seleccionar un rubro se resaltan sus cuentas.
  - Desde aquí se puede quitar una cuenta de un rubro.
- **Responsive (360 px):** primero la lista de rubros; al tocar un rubro se abre la lista de sus cuentas.

### UX-07 — Carga de sociedades no SAP
- **Usuario:** Consultor implementador
- **Propósito:** subir la información contable de las sociedades que no están en SAP.
- **RF que soporta:** RF-15
- **Datos visibles:**
  - Lista de sociedades no SAP (traídas de UX-05) con su estado de carga: pendiente, cargada o con errores.
  - Vista previa de las primeras filas del archivo: sociedad, cuenta, periodo, tercero, centro de costo, centro de beneficio e importe en moneda local.
- **Componentes:**
  - Zona de carga con arrastrar y soltar y un botón "Elegir archivo", una por sociedad.
  - Tabla de vista previa.
  - Lista de errores por fila.
  - Botón "Confirmar carga".
- **Estados:**
  - Normal.
  - Vacío: "Ninguna sociedad está marcada como no SAP"; en ese caso el paso no aparece en la navegación.
  - Carga: barra de progreso de subida.
  - Error: tabla de errores por fila, con número de fila, columna y motivo.
- **Validaciones visibles:**
  - El archivo debe corresponder a una sociedad del grupo registrada en UX-05 (RN-07).
  - Las cuentas deben existir en el mapeo; las que no, se listan como "cuenta sin mapear".
  - Muestra el formato con la etiqueta "por definir" (DAF, pregunta 5).
- **Interacciones:**
  - "Confirmar carga" marca la sociedad como cargada.
- **Responsive (360 px):** zonas de carga apiladas; la vista previa se desplaza horizontalmente dentro de su contenedor.

### UX-08 — Plan de generación
- **Usuario:** Consultor implementador
- **Propósito:** revisar todo lo que se va a crear antes de ejecutar.
- **RF que soporta:** RF-08, RF-09, RF-10, RF-16, RF-18, RF-23, RF-24, RF-25, RF-26
- **Datos visibles:** resumen por capa.
  - **BW:** extractores a activar (0FI_GL_14 y los de activos fijos y deterioros), InfoProviders a crear, campos Z elegidos, cadenas de proceso con "Carga full inicial" y "Delta todas las noches".
  - **BPC:**
    - Modelo de consolidación: dimensiones y scripts, con la etiqueta de la variante de su arquitectura.
    - Modelo de planeación de presupuesto, si el módulo está elegido.
  - **Monedas:** moneda local de cada sociedad, conversión a dólares y tipos de tasa (cierre, promedio, histórica).
  - Total de objetos a crear.
- **Componentes:**
  - Secciones plegables por capa.
  - Lista de objetos con ícono de "nuevo" o "existente, no se modifica".
  - Botón primario "Generar".
- **Estados:**
  - Normal.
  - Vacío: "Completa los pasos anteriores para ver el plan".
  - Carga: "Calculando el plan…".
  - Error: el paso previo incompleto, con un enlace para ir a corregirlo.
- **Validaciones visibles:**
  - Si la bodega existe, los objetos existentes aparecen como "existente, no se modifica" (RN-03).
  - Los scripts muestran "Variante para modelo estándar" o "Variante para modelo embebido" (RN-02).
  - Las cadenas de proceso explican "Después de la carga full inicial, las siguientes cargas son delta cada noche" (RN-04).
  - Las tasas muestran la regla: cierre para balance, promedio para resultados, histórica para patrimonio (RN-11).
  - Activos fijos y deterioros aparecen solo en la sección de BW (RN-12).
  - Antes de generar hay un diálogo de confirmación: "Se crearán N objetos en el sistema del cliente. ¿Generar?".
- **Interacciones:**
  - "Generar" lleva a UX-09 e inicia la ejecución.
- **Responsive (360 px):** las secciones se apilan y el botón "Generar" queda fijo abajo.

### UX-09 — Ejecución y registro
- **Usuario:** Consultor implementador
- **Propósito:** seguir la ejecución paso a paso y revisar el registro.
- **RF que soporta:** RF-11, RF-12, RF-13, RF-14, RF-17, RF-19, RF-26
- **Datos visibles:**
  - Lista de pasos con estado (pendiente, en curso, completo, con error) y duración. Los pasos son:
    1. Planes de cuentas
    2. Centros de costo y de beneficio
    3. Monedas y tasas
    4. Jerarquía de cuentas
    5. Objetos BW
    6. Importación a BPC (solo en el modelo estándar)
    7. Scripts
    8. Consolidación
    9. Conversión a dólares
  - Registro de cada paso (entidad Ejecución): usuario, fecha y hora, sistema, paso, objetos creados o modificados y resultado.
- **Componentes:**
  - Línea de pasos vertical.
  - Panel de registro con filtro por resultado.
  - Botones "Reintentar desde este paso" y "Descargar registro".
- **Estados:**
  - Normal: todos los pasos completos, con el resumen "Consolidación disponible en moneda local y dólares".
  - Vacío: "Aún no hay ejecuciones".
  - Carga: el paso en curso con su animación y los demás en espera.
  - Error: el paso fallido en rojo, con el mensaje y "Reintentar desde este paso".
- **Validaciones visibles:**
  - Al terminar la consolidación se muestra que solo participan las sociedades del grupo (RN-07).
  - El resumen final muestra los resultados en dos monedas (RN-06).
  - Cada ejecución queda en el registro (RNF-Aud-01).
- **Interacciones:**
  - Si un paso falla (por ejemplo, por falta de permiso de escritura en BPC), el mensaje enlaza a la pantalla donde se corrige (p. ej. UX-02).
  - Al terminar, el botón "Ver reportes" lleva a UX-10.
- **Responsive (360 px):** la línea de pasos y el registro se muestran uno debajo del otro.

### UX-10 — Reportes
- **Usuario:** Consultor implementador
- **Propósito:** generar y descargar los Excel que se entregan al cliente.
- **RF que soporta:** RF-20, RF-27, RF-29
- **Datos visibles:**
  - Catálogo de 30 reportes con ID, nombre, módulo (Consolidación, Presupuesto, Activos fijos, Deterioros), fuente de datos (BPC o BW), ola (1, 2 o 3, solo en consolidación) y estado (pendiente o generado).
  - Ejemplos: R07 Efectivo y equivalentes de efectivo; P01 Presupuesto frente a ejecución; A01 Movimiento de activos fijos por clase; D01 Movimiento del deterioro.
- **Componentes:**
  - Tabla con filtros por módulo y por estado.
  - Casillas para seleccionar reportes.
  - Botón "Generar seleccionados".
  - Botón "Descargar reportes (.zip)".
- **Estados:**
  - Normal.
  - Vacío: "Ejecuta la generación (UX-09) para habilitar los reportes".
  - Carga: progreso por reporte.
  - Error: el reporte con error y su motivo.
- **Validaciones visibles:**
  - Los reportes de consolidación y presupuesto muestran la fuente "BPC" (RN-08).
  - Los de activos fijos y deterioros muestran "BW" (RN-12).
  - Solo aparecen los reportes de los módulos elegidos en UX-04.
- **Interacciones:**
  - Al hacer clic en un reporte se abre su vista previa con la estructura de UX-11.
- **Responsive (360 px):** cada reporte es una tarjeta con su ID, nombre y una insignia de estado.

### UX-11 — Plantilla de reporte EPM (Excel)
- **Usuario:** Usuario financiero
- **Propósito:** consultar un reporte con datos al día y elegir qué ver.
- **RF que soporta:** RF-20, RF-21, RF-22, RF-27, RF-29
- **Superficie:** hoja de Excel con la pestaña de cinta "EPM". Prototipa el reporte R07 Efectivo y equivalentes de efectivo como modelo de todos los demás.
- **Datos visibles:**
  - Encabezado con el logo del cliente (marcador), nombre del reporte, ID y fecha de actualización.
  - Bloque de parámetros:
    - Sociedad: lista desplegable.
    - Incluir filiales: Sí/No.
    - Moneda: moneda local o dólares.
    - Periodo.
  - Cuerpo: rubros con código y nombre (p. ej. F-00-101), su valor y la apertura por sociedad cuando se incluyen filiales.
- **Componentes:**
  - Celdas de parámetros con listas desplegables.
  - Botón de cinta "Actualizar" (refresh de EPM).
  - Tabla con totales en negrita.
  - Notas al pie con la fuente "Modelo BPC" o "BW".
- **Estados:**
  - Normal.
  - Vacío: "Sin datos para la sociedad y el periodo elegidos".
  - Carga: indicador de EPM "Actualizando…".
  - Error: "No se pudo conectar a BPC; revisa la conexión de EPM".
- **Validaciones visibles:**
  - "Incluir filiales" cambia la vista: una sola sociedad o la sociedad con todas sus filiales (RN-09).
  - La moneda solo admite moneda local o dólares (RN-06).
  - Cada valor es la suma de las cuentas mapeadas al rubro (RN-10). Un comentario en la celda lista esas cuentas.
- **Interacciones:**
  - Al cambiar un parámetro y presionar "Actualizar" se recalculan los datos.
- **Responsive:** no aplica (Excel de escritorio).

### UX-12 — Captura de presupuesto (Excel EPM)
- **Usuario:** Usuario financiero
- **Propósito:** ingresar y ajustar el presupuesto.
- **RF que soporta:** RF-28
- **Superficie:** hoja de Excel con la cinta EPM. Corresponde al formulario P05.
- **Datos visibles:**
  - Parámetros: sociedad, versión y periodo (año).
  - Grilla: filas por cuenta y centro de costo, columnas por periodo (mes), celdas de importe que se pueden editar.
  - Total por fila y por columna.
- **Componentes:**
  - Celdas editables resaltadas.
  - Botón de cinta "Guardar datos".
  - Botón de cinta "Actualizar".
- **Estados:**
  - Normal.
  - Vacío: "Sin presupuesto para esta versión: ingresa los valores".
  - Carga: "Guardando…".
  - Error: "No se guardaron N celdas", con las celdas marcadas.
- **Validaciones visibles:**
  - Solo se aceptan números.
  - Los totales no se pueden editar.
  - Los datos se guardan en el modelo de planeación de BPC, no en BW (RN-08).
- **Interacciones:**
  - Después de guardar, el reporte P01 Presupuesto frente a ejecución (UX-11) ya muestra los valores nuevos.
- **Responsive:** no aplica (Excel de escritorio).

**RF sin interfaz:** RF-30. La información de activos fijos y deterioros alimenta R08, R10 y R11 de forma automática; no tiene pantalla propia, pero se ve en esos reportes.

---

## 3. Flujos a prototipar

1. **Implementación de un cliente nuevo** (camino principal):
   - UX-01 Nuevo cliente → UX-02 Conexiones → UX-03 Diagnóstico → UX-04 Módulos → UX-05 Sociedades → UX-06 Mapeo de cuentas.
   - Si hay alguna sociedad no SAP, pasa por UX-07.
   - Sigue: UX-08 Plan de generación → UX-09 Ejecución → UX-10 Reportes.
2. **Reintento tras un paso fallido:** UX-09 con el paso "Consolidación" en error por falta de permiso de escritura en BPC → enlace a UX-02 → corregir → volver a UX-09 → "Reintentar desde este paso".
3. **Consulta de un reporte:** UX-11 con R07 → cambiar Sociedad a "1000 Matriz", Incluir filiales "Sí" y Moneda "Dólares" → Actualizar.
4. **Captura y seguimiento de presupuesto:** UX-12 → ingresar valores → Guardar datos → UX-11 con P01 Presupuesto frente a ejecución actualizado.

Prototipa también la variante del flujo 1 en **arquitectura B**:
- UX-02 sin el botón "Validar conexión ERP–BW".
- UX-09 sin el paso "Importación a BPC".

---

## 4. Instrucciones de iteración en Claude Design

1. Antes de fijar la dirección visual, pide **2 o 3 variantes** de las pantallas de prioridad Alta: UX-02, UX-06, UX-08, UX-09 y UX-11.
2. Revisa en cada pantalla los estados **vacío, carga y error**, no solo el normal.
3. Verifica las pantallas en **360 px** y la navegación completa con teclado.
4. Cuando el prototipo esté aprobado internamente, entrega a la fase siguiente (Modo 2 de esta skill):
   - Una **captura PNG por pantalla**, con el nombre `UX-XX.png` (12 en total).
   - El **link navegable** del prototipo.

**Preguntas de diseño abiertas (no críticas):**
- **Inicio de sesión del configurador:** depende de dónde se aloje (DAF, pregunta 4). Por ahora prototipa sin pantalla de login.
- **Volumen típico de sociedades y cuentas por cliente:** define la paginación de UX-05 y UX-06 (DAF, pregunta 7). Por ahora asume hasta 50 sociedades y 5.000 cuentas, con paginación de 50 filas.
