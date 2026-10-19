# Cobertura de criterios de aceptación · Sprint 4

HU-16, HU-18, HU-19, HU-20 y HU-25, contra el simulador (Regla 6). Resultado al cierre: **368 aprobadas y 1 omitida**
(DPAPI, solo Windows; el CI la exige).

| HU | CA | Prueba (clase · método) | Tipo |
|---|---|---|---|
| HU-16 | CA1 La variante sale de la detección sin cambiar la configuración (RNF-Comp-04) | `VarianteBw4HanaTests.AdsoSinInfoCubos`, `.SinCambiarConfiguracion` | Servicio |
| HU-16 | CA2 aDSO, ningún InfoCubo (RNF-Comp-05) | `VarianteBw4HanaTests.AdsoSinInfoCubos`, `.SinCambiarConfiguracion`, `.Bw4BodegaExistente`, `ModulosBwTests.ModulosEnBw4` | Servicio |
| HU-16 | CA3 Extractores por ODP | `VarianteBw4HanaTests.Odp` | Servicio. **Contexto ODP real pendiente de HU-01.** |
| HU-16 | CA4 UX-08 muestra «aDSO» | `PantallasSprint4Tests.UX08Adso` | Web |
| HU-18 | CA1 Cadena full y delta cada 24 h (RN-04) | `CadenasTests.FullYDelta` | Servicio |
| HU-18 | CA2 Antigüedad de 24 h o menos (RNF-Rend-01) | `CadenasTests.SinBorradoSolapado` (programación ≤ 24 h) | Servicio. **La medición de 5 noches (CP-106, CP-109) es en el sandbox.** |
| HU-18 | CA3 Sin borrado de peticiones solapadas | `CadenasTests.SinBorradoSolapado` | Servicio |
| HU-18 | CA4 La carga full se ve como paso en UX-09 | `CadenasTests.CargaFullComoPaso` | Servicio |
| HU-19 | CA1 Solo con el módulo marcado | `ModulosBwTests.SoloConModulo` | Servicio |
| HU-19 | CA2 Extractor, InfoProviders y cargas en BW, sin BPC (RN-12) | `ModulosBwTests.ActivosFijos`, `.ModulosEnBw4` | Servicio |
| HU-19 | CA3 Paso «Activos fijos en BW» en UX-08 y UX-09 | `PantallasSprint4Tests.UX08yUX09Modulos` | Web |
| HU-20 | CA1 Solo con el módulo marcado | `ModulosBwTests.SoloConModulo` | Servicio |
| HU-20 | CA2 Extractor, InfoProviders y cargas en BW, sin BPC (RN-12) | `ModulosBwTests.Deterioros`, `.ModulosEnBw4` | Servicio. **Extractor de deterioros por confirmar en HU-01.** |
| HU-20 | CA3 Paso «Deterioros en BW» en UX-08 y UX-09 | `PantallasSprint4Tests.UX08yUX09Modulos` | Web |
| HU-25 | CA1 Un archivo por sociedad no SAP con vista previa | `LectorArchivoNoSapTests.Valido`, `ServicioNoSapTests.ConfirmarCarga`, `PantallasSprint4Tests.UX07VistaPrevia` | Regla, servicio, web |
| HU-25 | CA2 Errores por fila, columna y motivo; «cuenta sin mapear» | `LectorArchivoNoSapTests.ErroresPorFila`, `.FormatoInvalido`, `PantallasSprint4Tests.UX07Errores`, `.UX07Codificacion` | Regla, web |
| HU-25 | CA3 Confirmar solo sin errores | `ServicioNoSapTests.NoConfirmaConErrores`, `PantallasSprint4Tests.UX07Errores`, `.UX07Confirmar` | Servicio, web |
| HU-25 | CA4 Solo sociedades del grupo registradas (RN-07) | `LectorArchivoNoSapTests.SoloDelGrupo`, `PantallasSprint4Tests.UX07OtraSociedad` | Regla, web |
| HU-25 | CA5 Pasa por la validación de cuadre antes de BPC | `ServicioNoSapTests.PlanConNoSap` (paso «Sociedades no SAP» hacia la bodega) | Servicio. **La validación de cuadre llega con HU-36.** |

## Estados de UX-07

Vacío: `UX07Vacio`. Carga (barra de progreso de subida y arrastrar y soltar): `UX07Carga`. Error: `UX07Errores`,
`UX07Codificacion`, `ServicioNoSapTests.FallaSinParciales`.

## Casos del plan F7

CP-097 a CP-100 (HU-16), CP-105, CP-107 y CP-108 (HU-18), CP-114 a CP-121 (HU-19 y HU-20), CP-135 a CP-145 (HU-25):
CP-143 (sin datos parciales si se interrumpe) en `ServicioNoSapTests.FallaSinParciales`; CP-145 (la carga nueva reemplaza
la anterior) en `.Reemplaza`. Quedan para el sandbox con SAP real: CP-101 (certificación BW/4HANA, HU-58), CP-106, CP-109
a CP-113 (cadenas ejecutándose contra el ERP) y la lectura en RSA1, RSA6 y RSPC.

## Transversales

- Los importes de las sociedades no SAP no se guardan en la base local: la validación vive en memoria 30 minutos y solo
  se guardan los metadatos de la carga (`ServicioNoSapTests.PendienteVence`, arquitectura sección 7).
- `PantallasSprint4Tests.Accesibilidad`: UX-07 con controles nombrados, tablas con título y sin estilos en línea.
