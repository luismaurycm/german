# Cobertura de criterios de aceptación · Sprint 8

HU-37, HU-38, HU-39, HU-40 y HU-41, contra el simulador con el motor de referencia de consolidación. Resultado al cierre:
**472 aprobadas y 1 omitida** (DPAPI, solo Windows; el CI la exige).

| HU | CA | Prueba (clase · método) | Tipo |
|---|---|---|---|
| HU-37 | CA1 C03 por sociedad: diferencia de balance, utilidad − resultado, cuentas mapeadas, importada | `CierreTests.C03Normal`, `PantallasSprint8Tests.UX11C03` | Servicio, web |
| HU-37 | CA2 «No importada» con texto e ícono | `CierreTests.C03NoImportada`, `PantallasSprint8Tests.UX11C03` | Servicio, web |
| HU-37 | CA3 «Aún no hay importaciones en el periodo elegido» | `CierreTests.C03VacioYError`, `PantallasSprint8Tests.UX11C03VacioYError` | Servicio, web |
| HU-37 | CA4 Sin BPC, el error de conexión de EPM | `CierreTests.C03VacioYError`, `PantallasSprint8Tests.UX11C03VacioYError` | Servicio, web |
| HU-38 | CA1 C01 por pareja y cuentas: lo que registra cada parte y la diferencia | `CierreTests.C01Parejas`, `PantallasSprint8Tests.UX11C01` | Servicio, web |
| HU-38 | CA2 «Supera el umbral» con texto e ícono | `CierreTests.C01SuperaUmbral`, `.UmbralDeConciliacion` (999,99 · 1.000,00 · 1.000,01), `PantallasSprint8Tests.UX11C01` | Servicio, regla, web |
| HU-38 | CA3 Solo sociedades del grupo (RN-07) | `CierreTests.C01SoloGrupo` (tercero T9001 y sociedad fuera del perímetro) | Regla, servicio |
| HU-38 | CA4 Se ejecuta antes de la consolidación | `CierreTests.C01AntesDeConsolidar` | Servicio |
| HU-39 | CA1 Grupo, periodo, moneda y líneas completas | `CierreTests.GuardarAsiento`, `PantallasSprint8Tests.UX14` | Servicio, web |
| HU-39 | CA2 Guardar solo si cuadra; si no, «Descuadrado» con la diferencia (RN-15) | `CierreTests.SoloAsientosCuadrados` (0,01 de menos · 0,01 de más · 100.000 · cuadrado), `PantallasSprint8Tests.UX14`, `.UX14Errores` | Servicio, web, JS |
| HU-39 | CA3 No se aceptan líneas de una sociedad bloqueada (RN-17) | `CierreTests.AsientoConSociedadBloqueada`, `PantallasSprint8Tests.UX14Errores` | Servicio, web |
| HU-39 | CA4 Número, usuario y fecha en la lista del periodo | `CierreTests.GuardarAsiento`, `PantallasSprint8Tests.UX14` | Servicio, web |
| HU-39 | Estados vacío, carga y error de UX-14 | `CierreTests.AsientoVacio`, `PantallasSprint8Tests.UX14` (vacío y «Guardando…»), `.UX14Errores` | Regla, web |
| HU-40 | CA1 Bloquear o Desbloquear por sociedad | `CierreTests.BloqueoAuditado`, `PantallasSprint8Tests.UX15` | Servicio, web |
| HU-40 | CA2 Sin cargas, asientos ni reprocesos (RN-17) | `CierreTests.SinCargas`, `.AsientoConSociedadBloqueada`, `.SinReprocesos` | Servicio |
| HU-40 | CA3 No bloquea a las demás | `CierreTests.AsientoConSociedadBloqueada`, `.BloqueoAuditado`, `PantallasSprint8Tests.UX15` | Servicio, web |
| HU-40 | CA4 Bloqueo y desbloqueo con usuario y fecha | `CierreTests.BloqueoAuditado` | Servicio |
| HU-41 | CA1 Matriz en orden: carga, cuadre, conciliación, conversión, consolidación, reportes | `CierreTests.MatrizDelCierre`, `PantallasSprint8Tests.UX15` | Servicio, web |
| HU-41 | CA2 Un paso con error enlaza al reporte que explica la causa | `CierreTests.ErrorConCausa` (C03, C01), `.ConsolidacionConError` (ejecución), `PantallasSprint8Tests.UX15Error` | Servicio, web |
| HU-41 | CA3 Acceso a C02 y Asientos | `PantallasSprint8Tests.UX15`, `.NavegacionYPlan` | Web |
| HU-41 | CA4 «El cierre de … aún no ha iniciado» | `CierreTests.NoIniciado`, `PantallasSprint8Tests.UX15NoIniciado` | Servicio, web |
| HU-41 | Estados carga y error de UX-15 | `PantallasSprint8Tests.UX15` («Consultando el estado del cierre…»), `.UX15Error` | Web |
| HU-37 a HU-41 | UX-08 lista los controles del cierre y el plan sube C01, C03, C04 y Asientos | `CierreTests.PlantillasDelCierre`, `PantallasSprint8Tests.NavegacionYPlan` | Servicio (xlsx), web |

## Casos del plan F7

CP-211 a CP-240. CP-219 a CP-221 (umbral 1.000,00 − 0,01, igual y + 0,01) en `CierreTests.UmbralDeConciliacion`; CP-228 a
CP-230 (créditos 0,01 por debajo, iguales y 0,01 por encima) en `CierreTests.SoloAsientosCuadrados`. CP-227 y CP-239 (retardo
de 5 s) se cubren con el texto de carga en la página; el retardo real lo prueba el sandbox.

## Decisiones

- **Umbral de C01:** 1.000,00 en la moneda del reporte, provisional (pregunta 14 del DAF), el del plan de pruebas F7. Solo
  se marca lo que lo supera. C01 lee los datos importados (y convertidos), excluye la inversión en subsidiarias, que se
  elimina contra el patrimonio, y separa las parejas de balance y de resultados.
- **Estados de trabajo (RN-17):** Abierta o Bloqueada por sociedad y periodo en el modelo de consolidación. BPC rechaza
  asientos con líneas de la sociedad bloqueada; la validación, la importación y la conversión la saltan con un aviso y
  conservan sus datos; las cargas no SAP se rechazan en UX-07 una vez configurado BPC.
- **Asientos:** número `AJ-nnnn`, origen `AJUSTE` a nivel de grupo, en la moneda elegida (local del grupo o USD). Cada
  línea lleva un débito o un crédito. «Descuadrado» muestra la diferencia en valor absoluto.
- **C04 Reportes:** queda Pendiente hasta que UX-10 genere los reportes (HU-42 y HU-48, Sprint 9).
- **Corrección:** el conteo de sociedades consolidadas ya no descuenta las bloqueadas (conservan sus datos).
