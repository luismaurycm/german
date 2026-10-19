# Cobertura de criterios de aceptación · Sprint 9

HU-42, HU-43, HU-44, HU-45 y HU-48, contra el simulador con el motor de referencia de consolidación. Resultado al cierre:
**493 aprobadas y 1 omitida** (DPAPI, solo Windows; el CI la exige).

| HU | CA | Prueba (clase · método) | Tipo |
|---|---|---|---|
| HU-42 | CA1 Sociedad, Incluir filiales, Moneda y Periodo en celdas con lista | `PantallasSprint9Tests.UX11R01`, `ReportesTests.PaqueteZip` (listas en el Excel) | Web, xlsx |
| HU-42 | CA2 Incluir filiales: sola o con filiales y columna Total (RN-09) | `ReportesTests.IncluirFiliales`, `PantallasSprint9Tests.UX11R01` | Servicio, web |
| HU-42 | CA3 Solo moneda local o dólares (RN-06) | `ReportesTests.SoloMonedaLocalODolares`, `PantallasSprint9Tests.UX11R01` | Servicio, web |
| HU-42 | CA4 Sin BPC, error de conexión de EPM; sin datos, «Sin datos para la sociedad y el periodo elegidos» | `ReportesTests.SinDatosYSinConexion`, `PantallasSprint9Tests.UX11Estados` | Servicio, web |
| HU-43 | CA1 Cada rubro es la suma de las cuentas mapeadas (RN-10) | `ReportesTests.RubroEsSumaDeCuentas`, `.R01CuadraConElConsolidado` | Servicio |
| HU-43 | CA2 Un cambio de mapeo se refleja al actualizar | `ReportesTests.CambioDeMapeo` | Servicio |
| HU-43 | CA3 El detalle de un rubro muestra sus cuentas | `ReportesTests.DetalleDelRubro`, `PantallasSprint9Tests.UX11Detalle` | Servicio, web |
| HU-44 | CA1 R01, R05 y R07 como Excel con EPM conectados a BPC | `ReportesTests.PaqueteZip`, `PantallasSprint9Tests.UX10Descargar` | Servicio (xlsx), web |
| HU-44 | CA2 R01 cuadra con el balance consolidado | `ReportesTests.R01CuadraConElConsolidado` (COP y USD, rubro por rubro) | Servicio |
| HU-44 | CA3 R05 por pareja de sociedades | `ReportesTests.R05PorPareja`, `PantallasSprint9Tests.UX11OlasUnoYDos` | Servicio, web |
| HU-44 | CA4 Abre y actualiza con EPM add-in (RNF-Comp-02) | `ReportesTests.PaqueteZip` (fórmulas `EPMRetrieveData` y nombre `EPM_CONEXION`). **Abrir con EPM real: sandbox.** | xlsx |
| HU-45 | CA1 R02, R03, R04 y R06 con la estructura común | `ReportesTests.OlaDos`, `PantallasSprint9Tests.UX11OlasUnoYDos` | Servicio, web |
| HU-45 | CA2 R03 con interés minoritario y diferencia en conversión por separado | `ReportesTests.R03Separado`, `PantallasSprint9Tests.UX11OlasUnoYDos` | Servicio, web |
| HU-45 | CA3 R04 clasifica; un rubro sin clasificar es error en UX-10 | `ReportesTests.R04SinClasificar`, `.OlaDos` (el efectivo al cierre cuadra con F-00-101) | Servicio |
| HU-48 | CA1 Catálogo con ID, reporte, módulo, fuente, ola y estado, filtrable por módulo | `ReportesTests.Catalogo`, `PantallasSprint9Tests.UX10Catalogo` | Servicio, web |
| HU-48 | CA2 C01 a C04 con vista previa en UX-11, UX-13 o UX-15 | `ReportesTests.Catalogo`, `PantallasSprint9Tests.UX10Catalogo` | Servicio, web |
| HU-48 | CA3 .zip con los Excel de los módulos elegidos | `ReportesTests.PaqueteZip`, `PantallasSprint9Tests.UX10Descargar` | Servicio, web |
| HU-48 | CA4 Un reporte con error muestra la causa | `ReportesTests.R04SinClasificar` | Servicio |
| HU-48 | Estados vacío y carga de UX-10 | `PantallasSprint9Tests.UX10Vacio`, `.UX10Catalogo` («Generando…») | Web |
| HU-32 | CA4 (pendiente del Sprint 7) C02 en el catálogo de UX-10 | `ReportesTests.Catalogo`, `PantallasSprint9Tests.UX10Catalogo` | Servicio, web |
| HU-41 | CA1 (columna Reportes de C04) | `ReportesTests.ReportesEnC04` | Servicio |

## Casos del plan F7

CP-241 a CP-259 y CP-267 a CP-272. CP-245 y CP-272 (carga) se cubren con los textos de carga de la página. CP-247 a CP-249
(cortar la red o perder el permiso durante la actualización en EPM) y CP-256 (abrir con el add-in) quedan para el sandbox:
el acelerador entrega el Excel conectado; la actualización la hace EPM.

## Decisiones

- **Columnas con filiales.** La sociedad con sus filiales integradas (integración global), las eliminaciones y ajustes
  del grupo y el total consolidado. Una sub-holding con filiales suma sus sociedades sin eliminaciones, porque el grupo de
  consolidación es el de la matriz.
- **Signo de presentación.** Activos y gastos con su saldo; pasivos, patrimonio e ingresos con el signo invertido.
- **R04 (método indirecto).** Las variaciones se miden frente al cierre del periodo anterior; en el primer periodo el saldo
  anterior es cero. F-00-104 Otros activos no tiene actividad de flujo por defecto: si se usa, R04 queda con error hasta que
  se clasifique (pregunta para el DAF).
- **R05 y R06** son reportes dinámicos: en el Excel, EPM expande las filas al actualizar.
- **Descripciones de cuentas.** Se usan las de los miembros de BPC («Cuenta 11050501 del plan PCCO»), sin leer el ERP
  (RN-08).
- **Estado de generación.** La base local guarda solo el estado de generación de cada reporte (versión 5 del esquema, sin
  importes); el .zip se arma en memoria.
