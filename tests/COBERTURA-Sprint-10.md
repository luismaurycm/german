# Cobertura de criterios de aceptación · Sprint 10

HU-46, HU-47, HU-49, HU-50, HU-51, HU-52 y HU-53, contra el simulador. Resultado al cierre: **510 aprobadas y 1 omitida**
(DPAPI, solo Windows; el CI la exige).

| HU | CA | Prueba (clase · método) | Tipo |
|---|---|---|---|
| HU-46 | CA1 R08 a R14 con la estructura común | `NotasYPresupuestoTests.NotasCuadranConR01` | Servicio |
| HU-46 | CA2 R08, R10 y R11 con los movimientos de activos fijos y deterioros de BW (RF-30) | `NotasYPresupuestoTests.MovimientosDeBw`, `PantallasSprint10Tests.UX11Notas` | Servicio, web |
| HU-46 | CA3 Cada nota cuadra con su rubro de R01 | `NotasYPresupuestoTests.NotasCuadranConR01` | Servicio |
| HU-47 | CA1 R15 a R20 con la estructura común | `NotasYPresupuestoTests.OlaTresYJunta` | Servicio |
| HU-47 | CA2 R20 en moneda local y en dólares | `NotasYPresupuestoTests.OlaTresYJunta`, `PantallasSprint10Tests.UX11Notas` | Servicio, web |
| HU-47 | CA3 Los 20 reportes abren con EPM | `NotasYPresupuestoTests.CatalogoCompleto` (34 Excel con conexión EPM). **Abrir con el add-in: sandbox.** | xlsx |
| HU-49 | CA1 Solo con Presupuesto marcado | `NotasYPresupuestoTests.ModeloDePresupuesto`, `.SoloConElModulo` | Servicio |
| HU-49 | CA2 Modelo de planeación independiente | `NotasYPresupuestoTests.SoloConElModulo` (guardar en planeación no cambia la consolidación; la conversión no toca la planeación) | Servicio |
| HU-49 | CA3 UX-08 lista el modelo y UX-09 el paso «Modelo de presupuesto» | `NotasYPresupuestoTests.ModeloDePresupuesto`, `PantallasSprint10Tests.PlanYEjecucion` | Servicio, web |
| HU-49 | CA4 El presupuesto del ERP queda en el modelo | `NotasYPresupuestoTests.ModeloDePresupuesto` | Servicio |
| HU-50 | CA1 Sociedad, versión y año; celdas amarillas y totales | `NotasYPresupuestoTests.CapturaYP01`, `PantallasSprint10Tests.UX12` | Servicio, web |
| HU-50 | CA2 Solo números; la celda inválida en rojo y sin guardar | `NotasYPresupuestoTests.SoloNumerosYVacio`, `PantallasSprint10Tests.UX12Guardar` | Servicio, web |
| HU-50 | CA3 Guardar datos escribe en el modelo de planeación (RN-08) | `NotasYPresupuestoTests.CapturaYP01`, `PantallasSprint10Tests.UX12Guardar` | Servicio, web |
| HU-50 | CA4 «Sin presupuesto para esta versión: ingresa los valores» | `NotasYPresupuestoTests.SoloNumerosYVacio`, `PantallasSprint10Tests.UX12Guardar` | Servicio, web |
| HU-51 | CA1 Versión de la lista de HU-12 en UX-12 y P01 | `NotasYPresupuestoTests.Versiones`, `PantallasSprint10Tests.UX12`, `.UX11P01` | Servicio, web |
| HU-51 | CA2 Una versión no cambia las demás | `NotasYPresupuestoTests.Versiones` | Servicio |
| HU-51 | CA3 La versión en el encabezado de P01 a P05 | `PantallasSprint10Tests.UX11P01`, `.UX12`; parámetro Versión en los Excel P01 a P05 | Web, xlsx |
| HU-52 | CA1 P01 a P04 conectados al modelo de planeación | `NotasYPresupuestoTests.CatalogoCompleto`, `.ReportesDePresupuesto` | Servicio, xlsx |
| HU-52 | CA2 P01 con presupuesto, ejecución, variación y % | `NotasYPresupuestoTests.ReportesDePresupuesto`, `PantallasSprint10Tests.UX11P01` | Servicio, web |
| HU-52 | CA3 Lo guardado en P05 se refleja en P01 | `NotasYPresupuestoTests.CapturaYP01` | Servicio |
| HU-53 | CA1 A01 a A03 y D01, D02 leen BW (RN-12) | `NotasYPresupuestoTests.ReportesDeBw` (llamadas a BW, ninguna a BPC) | Servicio |
| HU-53 | CA2 Fuente BW en UX-10 | `NotasYPresupuestoTests.CatalogoCompleto`, `PantallasSprint10Tests.UX11Bw` | Servicio, web |
| HU-53 | CA3 A01 y D01 con saldo inicial, movimientos y saldo final | `NotasYPresupuestoTests.ReportesDeBw`, `PantallasSprint10Tests.UX11Bw` | Servicio, web |

## Casos del plan F7

CP-260 a CP-266 y CP-273 a CP-292. CP-265 y CP-266 (abrir los Excel con el add-in), CP-282 (retardo de 5 s) y CP-283 (quitar
el permiso de escritura en BPC) quedan para el sandbox; el estado de error de UX-12 se prueba con BPC sin respuesta.

## Decisiones

- **Rubros nuevos** para las notas: F-00-105 Carteras colectivas, F-00-109 Intangibles, F-00-204 Provisiones y
  contingencias, F-00-205 Impuestos por pagar, F-00-206 Dividendos por pagar, F-00-403 Ingresos por dividendos y F-00-504
  Impuesto a las ganancias, con su actividad en R04. Una nota sin cuentas mapeadas a su rubro muestra el estado vacío.
- **Movimientos de BW** de las notas y de A01/D01: de la sociedad elegida, en su moneda local; las clases de activo
  intangibles (software, licencias, marcas) van a R11 y el resto a R10.
- **Modelo de planeación** `PLANEACION` (ACCOUNT, ENTITY, CATEGORY, COSTCENTER, TIME, RPTCURRENCY) con ACTUAL y las versiones
  de HU-12. La carga inicial lleva el presupuesto del ERP a la versión anual y la ejecución a ACTUAL; las proyecciones
  empiezan vacías. P01 compara el presupuesto del año con la ejecución acumulada hasta el último mes cerrado.
- **Captura (P05):** ingresos y gastos en positivo; el total de la grilla es el resultado presupuestado.
- **Corrección:** la lógica de referencia de la consolidación (conversión, eliminaciones, importación) solo lee y escribe
  el modelo de consolidación; antes convertía también los registros de planeación del periodo.
