# Cobertura de criterios de aceptación · Sprint 3

HU-13, HU-14, HU-15 y HU-17. Todas las pruebas corren contra `SimulatedSapGateway` (Regla 6); `dotnet test --filter "HU=HU-14"`
corre las de una historia. Resultado al cierre: **335 aprobadas y 1 omitida** (DPAPI, solo Windows; el CI la exige).

| HU | CA | Prueba (clase · método) | Tipo |
|---|---|---|---|
| HU-13 | CA1 Plan en SAP BW, SAP BPC y Monedas y tasas; cada objeto Nuevo o Existente | `PlanGeneracionTests.AgrupaYMarca`, `PantallasSprint3Tests.UX08Grupos` | Servicio, web |
| HU-13 | CA2 Los existentes no se recrean ni se modifican (RN-03) | `PlanGeneracionTests.BodegaExistente`, `EjecucionTests.BodegaExistenteNoCrea`, `.ReintentarSinRepetir`, `PantallasSprint3Tests.UX08Existentes` | Servicio, web |
| HU-13 | CA3 Total de objetos y confirmación con ese número | `PlanGeneracionTests.AgrupaYMarca`, `PantallasSprint3Tests.UX08Confirmacion` | Servicio, web |
| HU-13 | CA4 Paso anterior incompleto con enlace | `PlanGeneracionTests.Faltantes`, `.FaltantesEnOrden`, `.NoSoportado`, `PantallasSprint3Tests.UX08Faltante`, `.UX08ErrorPasoPrevio` | Regla, servicio, web |
| HU-14 | CA1 Pasos completo, en curso, pendiente o con error, con duración | `EjecucionTests.EjecutaEnOrden`, `PantallasSprint3Tests.UX09Completa`, `.UX09Carga` | Servicio, web |
| HU-14 | CA2 Causa, enlace a la pantalla que corrige y Reintentar | `EjecucionTests.PasoConError`, `.PantallaQueCorrige`, `.SinCredencial`, `PantallasSprint3Tests.UX09ErrorYReintento` | Regla, servicio, web |
| HU-14 | CA3 Reintentar no repite los pasos completos | `EjecucionTests.ReintentarSinRepetir`, `.ReintentoDesdeElPasoFallido`, `.Interrumpida`, `PantallasSprint3Tests.UX09ErrorYReintento` | Regla, servicio, web |
| HU-14 | CA4 Resumen y Ver reportes | `PantallasSprint3Tests.UX09Completa` | Web. **UX-10 llega con HU-48.** |
| HU-15 | CA1 Sin bodega: 0FI_GL_14 y los InfoProviders de consolidación | `PlanGeneracionTests.BodegaNueva`, `EjecucionTests.EjecutaEnOrden` | Servicio. **Creación real pendiente de HU-01.** |
| HU-15 | CA2 Con bodega: paso Existente, sin objetos (RN-03) | `PlanGeneracionTests.BodegaExistente`, `EjecucionTests.BodegaExistenteNoCrea`, `PantallasSprint3Tests.UX09BodegaExistente` | Servicio, web |
| HU-15 | CA3 Cada objeto en el registro con su nombre técnico | `EjecucionTests.RegistroPorObjeto` | Servicio |
| HU-15 | CA4 UX-09 muestra el número de objetos creados | `PantallasSprint3Tests.UX09Completa` | Web |
| HU-17 | CA1 Marcar campos Z en UX-03 | `PantallasSprint2Tests.UX03CamposZ`, `PantallasSprint3Tests.UX03CampoIncompatible` | Web |
| HU-17 | CA2 Los marcados aparecen como Nuevo en UX-08 | `PlanGeneracionTests.CamposZEnElPlan`, `PantallasSprint3Tests.UX08CampoZ` | Servicio, web |
| HU-17 | CA3 Tipo incompatible marcado con el motivo y no incorporado | `ReglasCamposZTests.Compatibilidad`, `.Motivo`, `PlanGeneracionTests.CamposZEnElPlan`, `.NoIncorporaIncompatible`, `PantallasSprint3Tests.UX03CampoIncompatible` | Regla, servicio, web |

## Estados de pantalla

| Pantalla | Vacío | Carga | Error |
|---|---|---|---|
| UX-08 | `UX08Faltante` («Completa los pasos anteriores para ver el plan») | `UX08Carga` («Calculando el plan…») | `UX08ErrorPasoPrevio` |
| UX-09 | `UX09Vacio` («Aún no hay ejecuciones») | `UX09Carga` (paso en curso, consulta del estado) | `UX09ErrorYReintento` |

## Casos del plan F7

CP-077 a CP-082 (HU-13), CP-083 a CP-088 (HU-14), CP-089 a CP-096 (HU-15) y CP-102 a CP-104 (HU-17). CP-094 (tiempo de espera
de 120 s con un solo reintento) y CP-095 (error de negocio sin reintento) en `EjecucionTests.TiempoAgotado` y
`.ErrorDeNegocioSinReintento`; CP-096 (reintento sin duplicar, el paquete Z responde EXISTE) en `.ReintentarSinRepetir`.
Las verificaciones en RSA1 y RSA6 se reemplazan por los objetos que registra el simulador; **en SAP real quedan para HU-01**.

## Transversales

- `PlanGeneracionTests.GeneradoresExtensibles` (RNF-Comp-04): un generador nuevo se agrega al catálogo sin tocar los existentes.
- `PantallasSprint3Tests.Accesibilidad`: secciones plegables con `<details>` (teclado), tablas con título, sin estilos en línea.
- La cola en segundo plano (`ColaEjecuciones`) corre en las pruebas web reales; al arrancar, una ejecución a medias queda
  con error y se puede reintentar (`EjecucionTests.Interrumpida`).
