# Cobertura de criterios de aceptación · Sprint 6

HU-27, HU-28, HU-29, HU-30, HU-31 y HU-36, contra el simulador con el motor de referencia de consolidación
(`Acelerador.Core/Consolidacion`). Resultado al cierre: **414 aprobadas y 1 omitida** (DPAPI, solo Windows; el CI la exige).

| HU | CA | Prueba (clase · método) | Tipo |
|---|---|---|---|
| HU-27 | CA1 Solo arquitectura A (RN-19) | `ConsolidacionTests.SoloArquitecturaA` | Servicio |
| HU-27 | CA2 Paquete de Data Manager por sociedad y periodo | `ConsolidacionTests.ImportacionPorSociedad`, `.ImportarDosVecesNoDuplica` | Servicio |
| HU-27 | CA3 Solo las sociedades que cuadran | `ConsolidacionTests.SociedadDescuadrada`, `PantallasSprint6Tests.UX09SociedadRechazada` | Servicio, web |
| HU-27 | CA4 «Importación a BPC» con la cantidad de registros | `ConsolidacionTests.ImportacionPorSociedad`, `PantallasSprint6Tests.UX09Consolidacion` | Servicio, web |
| HU-28 | CA1 Variante de modelo estándar (RN-02); la embebida no | `ConsolidacionTests.Scripts` | Servicio |
| HU-28 | CA2 Códigos del cliente sin edición manual | `ConsolidacionTests.Scripts` | Servicio |
| HU-28 | CA3 UX-08 muestra la variante | `PantallasSprint6Tests.UX08ScriptsYCuentaCta` | Web |
| HU-28 | CA4 Respaldo UJBR antes de reemplazar un script | `ConsolidacionTests.RespaldoAntesDeReemplazar` | Servicio |
| HU-29 | CA1 Solo sociedades del grupo (RN-07) | `ConsolidacionTests.EliminacionIntercompania` | Servicio |
| HU-29 | CA2 Se eliminan las transacciones intercompañía | `ConsolidacionTests.EliminacionIntercompania`, `.DiferenciaNoConciliada` | Servicio, regla |
| HU-29 | CA3 Resultado o causa y Reintentar | `ConsolidacionTests.ConsolidacionFalla`, `PantallasSprint6Tests.UX09Consolidacion` | Servicio, web |
| HU-29 | CA4 Consultable en R05 | `ConsolidacionTests.ConsultableParaR05` | Servicio. **El reporte R05 llega con HU-45.** |
| HU-30 | CA1 Cierre, promedio e histórica (RN-11) | `ConsolidacionTests.Conversion` | Servicio |
| HU-30 | CA2 Disponible en las dos monedas (RN-06) | `ConsolidacionTests.Conversion` | Servicio |
| HU-30 | CA3 Resumen «Consolidación disponible en moneda local y en dólares» | `PantallasSprint6Tests.UX09Consolidacion` | Web |
| HU-30 | CA4 Sin tasa del periodo: falla e indica la moneda | `ConsolidacionTests.FaltaTasa`, `.FaltaTasaEnBpc`, `DatosMaestrosTests.TasaFaltante` | Regla, servicio |
| HU-31 | CA1 Diferencia en la cuenta de patrimonio, no en resultados (RN-18) | `ConsolidacionTests.DiferenciaEnConversion` | Servicio |
| HU-31 | CA2 El balance convertido suma cero | `ConsolidacionTests.DiferenciaEnConversion` | Servicio |
| HU-31 | CA3 UX-08 muestra la cuenta y UX-09 el paso | `PantallasSprint6Tests.UX08ScriptsYCuentaCta`, `.UX09Consolidacion` | Web |
| HU-36 | CA1 Activo = pasivo + patrimonio con tolerancia (RN-16) | `ReglaCuadreTests.ToleranciaDeBalance` (0,99 · 1,00 · 1,01), `ConsolidacionTests.SociedadDescuadrada`, `.NoSapValidada` | Regla, servicio |
| HU-36 | CA2 Utilidad = resultado del ejercicio | `ReglaCuadreTests.UtilidadIgualAlResultado` | Regla |
| HU-36 | CA3 Cuentas con saldo mapeadas | `ReglaCuadreTests.CuentasMapeadas`, `ConsolidacionTests.SinMapeoCompleto` | Regla, servicio |
| HU-36 | CA4 No se importa y queda en C03 con la diferencia | `ConsolidacionTests.SociedadDescuadrada`, `.ConsultableParaR05` | Servicio. **La plantilla C03 llega con HU-37.** |
| HU-36 | CA5 UX-09 muestra las sociedades rechazadas | `ConsolidacionTests.SociedadDescuadrada`, `PantallasSprint6Tests.UX09SociedadRechazada` | Servicio, web |

## Casos del plan F7

CP-154 a CP-169 y CP-203 a CP-210. CP-208 a CP-210 (tolerancia 0,99, 1,00 y 1,01) en `ReglaCuadreTests.ToleranciaDeBalance`;
CP-161 (repetir sin duplicar) en `ConsolidacionTests.ImportarDosVecesNoDuplica`. HU-25 CA5 (la carga no SAP pasa por la
validación de cuadre) en `ConsolidacionTests.NoSapValidada`. Quedan para el sandbox: CP-158 y CP-159 (importación encadenada
a la delta y bloqueo del modelo durante el paquete).

## Decisiones

- **Orden de los pasos:** la conversión va antes de la consolidación. BPC elimina en cada moneda de presentación, así que
  primero convierte (secuencia estándar de BPC: conversión, eliminación intercompañía, eliminación de inversión). El
  prototipo listaba la conversión al final; se documenta en el PR.
- **Motor de referencia:** `MotorConsolidacion` y `LogicaBpcReferencia` especifican lo que deben producir el paquete de
  importación y los scripts desplegados; el simulador los usa para tener datos consolidados. En SAP real lo calcula BPC.
- **Cuentas técnicas:** `ZACC_CTA` (diferencia en conversión, rubro F-00-303) y `ZACC_DIF_IC` (diferencias intercompañía,
  F-00-106). Rubros nuevos: F-00-106, F-00-302 (resultado del ejercicio) y F-00-303.
- Tolerancia de cuadre 1,00 provisional (pregunta 14 del DAF).
