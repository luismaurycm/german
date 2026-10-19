# Cobertura de criterios de aceptación · Sprint 2

Cada criterio de HU-07, HU-09, HU-10, HU-11, HU-12 y HU-54 tiene al menos una prueba en `tests/Acelerador.Tests`.
Las pruebas se nombran con la HU y el criterio; `dotnet test --filter "HU=HU-11"` corre las de una historia.
Todas corren contra `SimulatedSapGateway`, sin SAP real (Regla 6). Resultado al cierre del sprint:
**286 aprobadas y 1 omitida** (la prueba DPAPI de HU-04, que solo corre en Windows y que el CI exige en `windows-latest`).

| HU | CA | Prueba (clase · método) | Tipo |
|---|---|---|---|
| HU-07 | CA1 Pestañas: extractores, InfoProviders (InfoCubos o aDSO según la versión), modelos BPC y campos Z | `ReglasInventarioTests.CuatroPestanas`, `.PestanaSegunVersion`, `ServicioDiagnosticoTests.EscaneaCuatroListas`, `.Bw4HanaAdso`, `PantallasSprint2Tests.UX03Pestanas`, `.UX03Adso` | Regla, servicio, web |
| HU-07 | CA2 Bodega existente; si existe no se recrea (RN-03) | `ReglasInventarioTests.BodegaExistente`, `ServicioDiagnosticoTests.BodegaExistente`, `PantallasSprint2Tests.UX03Bodega`, `.UX03Vacio` | Regla, servicio, web. **Que UX-08 no recree la bodega se prueba cuando exista el plan (HU-13).** |
| HU-07 | CA3 Volver a escanear repite y actualiza la fecha | `ServicioDiagnosticoTests.VolverAEscanear`, `PantallasSprint2Tests.UX03VolverAEscanear` | Servicio, web |
| HU-07 | CA4 Si falla, el paso que falló y el mensaje del sistema | `ServicioDiagnosticoTests.FalloConPaso`, `.FalloConservaAnterior`, `.FalloSinCredencial`, `.SinConexion`, `PantallasSprint2Tests.UX03Error` | Servicio, web |
| HU-09 | CA1 Agregar y editar con código, nombre, en SAP, moneda, matriz y plan | `ReglasSociedadesTests.FilasValidas`, `.AgregarDesdeErp`, `ServicioSociedadesTests.GuardaYEdita`, `PantallasSprint2Tests.UX05AgregarYEditar` | Regla, servicio, web |
| HU-09 | CA2 Solo sociedades del grupo (RN-07) | `ReglasSociedadesTests.SoloDelGrupo`, `ServicioSociedadesTests.ErpNoResponde`, `PantallasSprint2Tests.UX05SoloGrupo`, `.UX05AgregarYEditar` (el selector solo ofrece las del ERP) | Regla, servicio, web |
| HU-09 | CA3 Plan de cuentas propio por sociedad (RN-05) | `ReglasSociedadesTests.PlanPropio`, `PantallasSprint2Tests.UX05SinMoneda` | Regla, web |
| HU-09 | CA4 «La sociedad X no tiene moneda local» y no se guarda | `ReglasSociedadesTests.MonedaObligatoria`, `.MonedaValida`, `ServicioSociedadesTests.SinMonedaNoGuarda`, `PantallasSprint2Tests.UX05SinMoneda` | Regla, servicio, web |
| HU-09 | CA5 Una sociedad no SAP habilita UX-07 | `ServicioSociedadesTests.NoSapHabilitaUx07`, `PantallasSprint2Tests.UX05NoSapHabilitaUx07`, `.UX06Continuar` | Servicio, web |
| HU-10 | CA1 % de propiedad y método (RN-13) | `ReglasSociedadesTests.PorcentajeYMetodo`, `PantallasSprint2Tests.UX05PorcentajeYMetodo` | Regla, web |
| HU-10 | CA2 Una sola matriz | `ReglasSociedadesTests.UnaSolaMatriz`, `.SociedadMatriz`, `PantallasSprint2Tests.UX05DosMatrices` | Regla, web |
| HU-10 | CA3 Fuera de 0 a 100 se marca en la fila y no se guarda | `ReglasSociedadesTests.PorcentajeFueraDeRango` (120, −0,01, 100,01…), `.PorcentajeEnRango` (0 y 100), `PantallasSprint2Tests.UX05PorcentajeFueraDeRango`, `.UX05PorcentajeLimite` | Regla (límites), web |
| HU-10 | CA4 Primer periodo del modelo de propiedad (HU-32) | `ReglasSociedadesTests.EstructuraInicial`, `ServicioSociedadesTests.EstructuraGuardada` | Regla, servicio. **El consumo por el modelo de propiedad llega con HU-32.** |
| HU-11 | CA1 Rubro y cuentas marcadas, filtradas por el plan de la sociedad (RN-05) | `ServicioMapeoTests.CuentasDelErpPorPlan`, `.AsignarYSumar`, `.ValidaCuentas`, `PantallasSprint2Tests.UX06Asignar`, `.UX06OtraSociedad` | Servicio, web |
| HU-11 | CA2 Vista previa con la suma de las cuentas (RN-10) | `ReglasMapeoTests.ValorRubro`, `ServicioMapeoTests.AsignarYSumar`, `.Quitar`, `PantallasSprint2Tests.UX06Asignar` | Regla, servicio, web |
| HU-11 | CA3 Contador de sin mapear y filtro «Solo sin mapear» | `ReglasMapeoTests.SinMapear`, `ServicioMapeoTests.ContadorYFiltro`, `PantallasSprint2Tests.UX06Vacio`, `.UX06Asignar` | Regla, servicio, web |
| HU-11 | CA4 Cuentas leídas del ERP por la conexión de HU-03 | `ServicioMapeoTests.CuentasDelErpPorPlan`, `.CuentaNuevaDelErp`, `.ErpFalla`, `.PlanDistinto`, `PantallasSprint2Tests.UX06ErrorErp` | Servicio, web. **Lectura real pendiente de HU-01.** |
| HU-11 | CA5 Si el guardado falla, cuántas asignaciones no se guardaron | `ReglasMapeoTests.MensajeNoGuardadas`, `ServicioMapeoTests.GuardadoFalla`, `PantallasSprint2Tests.UX06ErrorAlGuardar` | Regla, servicio, web |
| HU-12 | CA1 Con Presupuesto se muestran las versiones y se agregan, renombran o quitan | `VersionesTests.Propuestas_`, `.AgregarRenombrarQuitar`, `PantallasSprint2Tests.UX04VersionesVisibles`, `.UX04EditarVersiones` | Regla, servicio, web |
| HU-12 | CA2 Debe existir la versión de presupuesto anual | `VersionesTests.AnualObligatoria`, `PantallasSprint2Tests.UX04SinAnual` | Regla, servicio, web |
| HU-12 | CA3 Códigos sin repetir | `VersionesTests.CodigosUnicos`, `.CodigoYNombre`, `PantallasSprint2Tests.UX04CodigoRepetido` | Regla, web |
| HU-12 | CA4 Las versiones pasan al plan (UX-08) | `VersionesTests.PasanAlPlan` (`ReglasVersiones.ParaPlan`) | Regla, servicio. **UX-08 las mostrará con HU-13.** |
| HU-54 | CA1 Fecha y hora, usuario, sistema, paso, objetos y resultado | `RegistroTests.Columnas`, `PantallasSprint2Tests.UX09Registro`, `.UX09Filtro` | Regla, web |
| HU-54 | CA2 100 % registrado y retención de 12 meses (RNF-Aud-01) | `RegistroTests.TodoRegistrado`, `.Retencion` (11 m 29 d, 12 m y 12 m 1 d), `PantallasSprint2Tests.UX09DepuraAlArrancar`, `RegistroEjecucionesTests.SoloAgrega` | Servicio, integración |
| HU-54 | CA3 Descargar registro con los mismos campos | `RegistroTests.Descarga`, `.CsvSeguro`, `PantallasSprint2Tests.UX09Descargar` | Regla, web |
| HU-54 | CA4 Sin credenciales (RNF-Seg-02) | `RegistroTests.SinCredenciales`, `PantallasSprint2Tests.UX09SinCredencial` (clave centinela en pantalla, descarga y registro de la aplicación) | Servicio, web |

## Estados de pantalla (normal, vacío, carga y error)

| Pantalla | Vacío | Carga | Error |
|---|---|---|---|
| UX-03 | `UX03SinEscaneo`, `UX03Vacio` | `UX03Carga` (barra «Escaneando BW y BPC…») | `UX03Error` |
| UX-04 versiones | `UX04VersionesVisibles` (sección oculta sin Presupuesto) | botón «Guardando…» del Sprint 1 | `UX04SinAnual`, `UX04CodigoRepetido` |
| UX-05 | `UX05Vacio` | `UX05Carga` («Cargando sociedades…») | `UX05ErrorAlGuardar`, `UX05ErrorErp` |
| UX-06 | `UX06Vacio` | `UX06Carga` («Cargando cuentas…») | `UX06ErrorErp`, `UX06ErrorAlGuardar` |
| UX-09 registro | `UX09Vacio` | filtro con «Cargando registro…» | `UX09Error` |

La carga es una mejora progresiva (`wwwroot/js/acelerador.js`, `mostrarCarga`): mientras el servidor lee del sistema del
cliente, el contenido se reemplaza por un esqueleto o una barra con texto. Sin JavaScript todo funciona igual.

## Casos del plan de pruebas F7 (docs/06-plan-pruebas)

| Caso | Cubierto por |
|---|---|
| CP-035 a CP-038 | HU-07 CA1 a CA4 (tabla de arriba) |
| CP-039 Bodega completa si no existe | `ServicioDiagnosticoTests.BodegaExistente`, `PantallasSprint2Tests.UX03Vacio`. La parte de UX-08 queda para HU-13. |
| CP-040 a CP-041 Vacío y carga de UX-03 | `UX03Vacio`, `UX03Carga` |
| CP-049 a CP-053 | HU-09 CA1 a CA5 |
| CP-054 a CP-056 Vacío, carga y error de UX-05 | `UX05Vacio`, `UX05Carga`, `UX05ErrorAlGuardar` |
| CP-057 a CP-060 | HU-10 CA1 a CA4 |
| CP-061 a CP-064 Límites −0,01, 0, 100 y 100,01 | `ReglasSociedadesTests.PorcentajeFueraDeRango`, `.PorcentajeEnRango`, `PantallasSprint2Tests.UX05PorcentajeFueraDeRango`, `.UX05PorcentajeLimite` |
| CP-065 a CP-069 | HU-11 CA1 a CA5 |
| CP-070 a CP-071 Vacío y carga de UX-06 | `UX06Vacio`, `UX06Carga` |
| CP-072 a CP-076 | HU-12 CA1 a CA4; CP-076 (sin Presupuesto no hay versiones) en `UX04VersionesVisibles` y `VersionesTests.PasanAlPlan` |
| CP-293 a CP-297 | HU-54 CA1 a CA4; CP-297 (depuración a 11 m 29 d, 12 m y 12 m 1 d) en `RegistroTests.Retencion` |

Los datos de los casos (Grupo Demo D100 a D500, planes CO01 y US01, tercero T9001) se reemplazan por los del paisaje
del simulador (sociedades 1000, 2000, 3000 y 9000; planes PCCO y PCPA; tercero T901).

## Transversales

- `PantallasSprint2Tests.RecorridoCompleto`: UX-01 → UX-06 con el simulador.
- `PantallasSprint2Tests.Accesibilidad` (WCAG 2.1 AA básico en UX-03, UX-04, UX-05, UX-06 y el registro): cada control
  con nombre accesible, tablas con título, un solo `h1`, sin estilos en línea (CSP). Las pestañas de UX-03 siguen el
  patrón de WAI-ARIA (flechas, Inicio y Fin); los errores de fila llevan `aria-invalid` y `aria-describedby`.
- `EsquemaSprint2Tests`: una base del Sprint 1 se migra al esquema 2; inventario, sociedades y mapeo persisten.
- `SimuladorSprint2Tests`: convenciones del simulador y `NcoSapGateway` «pendiente de HU-01».
- CI (`.github/workflows/ci.yml`, ADR-11): build y pruebas en `ubuntu-latest` y `windows-latest`; en Windows la prueba
  DPAPI de HU-04 debe ejecutarse y aprobar; `dotnet list package --vulnerable --include-transitive` falla con cualquier
  paquete vulnerable.

## Pendiente de SAP real (HU-01)

- `NcoSapGateway.LeerInventarioAsync` (Z_ACC_INVENTARIO), `LeerSociedadesAsync` (BAPI_COMPANYCODE_GETLIST y GETDETAIL)
  y `LeerCuentasAsync` (BAPI_GL_ACC_GETLIST, nodo de la estructura de balance y BAPI_GL_GETGLACCPERIODBALANCES)
  devuelven «pendiente de HU-01».
- La marca de bodega financiera (qué InfoProviders son «la bodega») se confirma con el contrato JSON de Z_ACC_INVENTARIO.
- El catálogo de rubros (`CatalogoRubros`) es el inicial de la versión 1; se ajusta con los reportes R01 a R20.
