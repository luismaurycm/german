# Cobertura de criterios de aceptación · Sprint 7

HU-32, HU-33, HU-34 y HU-35, contra el simulador con el motor de referencia de consolidación. Resultado al cierre:
**438 aprobadas y 1 omitida** (DPAPI, solo Windows; el CI la exige).

| HU | CA | Prueba (clase · método) | Tipo |
|---|---|---|---|
| HU-32 | CA1 Modelo de propiedad con la estructura de HU-10 como primer periodo | `PropiedadTests.ModeloConEstructuraInicial` | Servicio |
| HU-32 | CA2 Plantilla C02 conectada al modelo de propiedad | `PropiedadTests.PlantillaC02` | Servicio (xlsx) |
| HU-32 | CA3 UX-08 lista el modelo y C02 | `PropiedadTests.ModeloConEstructuraInicial`, `PantallasSprint7Tests.UX08Propiedad` | Servicio, web |
| HU-32 | CA4 C02 en el catálogo de UX-10 | **Llega con UX-10 (HU-42, Sprint 9).** Hoy C02 se abre desde la navegación de UX-09 | — |
| HU-33 | CA1 Solo integración global (RN-13) | `PropiedadTests.EliminacionDeInversion`, `.FueraDelPerimetro` | Servicio, motor |
| HU-33 | CA2 Porcentajes del periodo (RN-14) | `PropiedadTests.PorcentajesDelPeriodo` | Motor |
| HU-33 | CA3 Interés minoritario separado en el patrimonio | `PropiedadTests.EliminacionDeInversion`, `.ConsolidadoCuadra` | Servicio. **R03 llega con HU-45.** |
| HU-33 | CA4 UX-09 muestra «Eliminación de inversión e interés minoritario» | `PropiedadTests.EliminacionDeInversion`, `PantallasSprint7Tests.UX09Inversion`, `.UX08Propiedad` | Servicio, web |
| HU-33 | CA5 Diferencia en una cuenta identificable | `PropiedadTests.EliminacionDeInversion` (`ZACC_DIF_INV`) | Servicio |
| HU-34 | CA1 La coligada no se integra línea a línea | `PropiedadTests.MetodoDeParticipacion` | Servicio |
| HU-34 | CA2 Se reconoce el % del resultado del periodo | `PropiedadTests.MetodoDeParticipacion` | Servicio |
| HU-34 | CA3 Las transacciones con coligadas no se eliminan | `PropiedadTests.MetodoDeParticipacion` | Servicio |
| HU-34 | CA4 Fuera del perímetro no participa | `PropiedadTests.FueraDelPerimetro` | Motor |
| HU-35 | CA1 Editar % y método por periodo | `ParticipacionesC02Tests.GuardarPeriodo`, `PantallasSprint7Tests.UX13Guardar` | Servicio, web |
| HU-35 | CA2 Copiar del periodo anterior | `ParticipacionesC02Tests.CopiarAnterior`, `PantallasSprint7Tests.UX13Copiar` | Servicio, web |
| HU-35 | CA3 0 a 100 y método obligatorio; la celda se marca y la fila no se guarda | `ParticipacionesC02Tests.ValidacionPorCelda` (150 · −0,01 · 100,01 · sin método), `PantallasSprint7Tests.UX13Error` | Servicio, web |
| HU-35 | CA4 El cambio aplica desde el periodo elegido (RN-14) | `ParticipacionesC02Tests.GuardarPeriodo`, `PropiedadTests.PorcentajesDelPeriodo` | Servicio, motor |
| HU-35 | CA5 Auditoría con usuario, fecha, valor anterior y nuevo | `ParticipacionesC02Tests.Auditoria`, `PantallasSprint7Tests.UX13Guardar` | Servicio, web |
| HU-35 | Estados vacío, carga y error de UX-13 | `ParticipacionesC02Tests.Vacio`, `PantallasSprint7Tests.UX13Copiar`, `.UX13CargaYNavegacion`, `.UX13ErrorConexion` | Servicio, web |

## Casos del plan F7

CP-177 a CP-201. CP-197 (−0,01) y CP-198 (100,01) en `ParticipacionesC02Tests.ValidacionPorCelda`; CP-199 a CP-201
(80 % en el periodo anterior, 90 % desde el periodo del cambio y heredado al siguiente) en `PropiedadTests.PorcentajesDelPeriodo`.
CP-180 queda para HU-42. CP-202 (retención de 12 meses de la auditoría de BPC) es configuración del sandbox.

## Decisiones

- **Cuentas técnicas:** `ZACC_IM` (interés minoritario, rubro F-00-304, patrimonio), `ZACC_DIF_INV` (diferencia entre
  inversión y patrimonio eliminado, F-00-108), `ZACC_RES_IM` (resultado del minoritario), `ZACC_RES_PART` (participación en resultados de
  asociadas, F-00-402) y `ZACC_RES_PART_EJ` (su efecto en el resultado del ejercicio, F-00-302).
- **Coligadas:** el % del resultado se reconoce contra la cuenta de inversión de la matriz (o `ZACC_DIF_INV` si no hay).
- **Periodos de C02:** los doce meses del año del periodo inicial; el modelo de propiedad usa el último periodo registrado
  hasta el consultado (RN-14), así que no hace falta copiar los meses sin cambios.
- **Auditoría:** la guarda BPC (auditoría estándar del modelo); UX-13 la consulta. La base local no guarda importes ni
  porcentajes (RNF-Seg-04); el registro de ejecuciones anota solo qué sociedades se guardaron.

## Corrección

- UX-09 dejaba de consultar el estado cuando el último paso terminaba, un momento antes de que el cliente avanzara a
  Reportes; en Windows la prueba de UX-13 llegaba antes del avance. Ahora la ejecución se considera en curso hasta que el
  cliente avanza (`VistaEjecucion.EnCurso`, prueba `EjecucionTests.EnCursoHastaQueAvanzaElCliente`, HU-14).
