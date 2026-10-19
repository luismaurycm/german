# Cobertura de criterios de aceptación · Sprint 5

HU-21, HU-22, HU-23, HU-24 y HU-26, contra el simulador (Regla 6). Resultado al cierre: **387 aprobadas y 1 omitida**
(DPAPI, solo Windows; el CI la exige).

| HU | CA | Prueba (clase · método) | Tipo |
|---|---|---|---|
| HU-21 | CA1 Plan de cuentas de cada sociedad (RN-05) | `DatosMaestrosTests.PlanesPorSociedad` | Servicio |
| HU-21 | CA2 «Planes de cuentas» en UX-09 con la cantidad | `DatosMaestrosTests.Cantidades`, `PantallasSprint5Tests.UX09Cantidades` | Servicio, web |
| HU-21 | CA3 Sociedad sin plan en el ERP: error con su código | `DatosMaestrosTests.PlanInexistente` | Servicio |
| HU-22 | CA1 CeCo y CeBe de todas las sociedades | `DatosMaestrosTests.CentrosPorSociedad` | Servicio |
| HU-22 | CA2 Cantidades en UX-09 | `DatosMaestrosTests.Cantidades`, `PantallasSprint5Tests.UX09Cantidades` | Servicio, web |
| HU-22 | CA3 Error con la sociedad y el mensaje del sistema | `DatosMaestrosTests.CentrosPorSociedad` | Servicio |
| HU-23 | CA1 Moneda local tal como la maneja SAP (RN-06) | `DatosMaestrosTests.MonedasYTasas` (T001), `ReglasSociedadesTests.MonedaValida` (moneda igual a la del ERP) | Servicio, regla |
| HU-23 | CA2 Tasas de cierre, promedio e histórica por moneda y periodo (RN-11) | `DatosMaestrosTests.MonedasYTasas`, `.PeriodosDeTasas` | Servicio, regla |
| HU-23 | CA3 Moneda sin tasa: error del paso | `DatosMaestrosTests.TasaFaltante`, `PantallasSprint5Tests.UX09SinTasa` | Servicio, web |
| HU-23 | CA4 UX-08 con moneda local y tipos de tasa | `PantallasSprint5Tests.UX08ModeloYMaestros`, `PantallasSprint3Tests.UX08Grupos` | Web |
| HU-24 | CA1 Jerarquía de cuentas desde el ERP | `DatosMaestrosTests.Jerarquia` | Servicio |
| HU-24 | CA2 Cuenta de la jerarquía fuera del plan: se reporta | `DatosMaestrosTests.Jerarquia`, `PantallasSprint5Tests.UX09AvisoJerarquia` | Servicio, web |
| HU-24 | CA3 Cantidad de nodos en UX-09 | `DatosMaestrosTests.Cantidades`, `PantallasSprint5Tests.UX09Cantidades` | Servicio, web |
| HU-26 | CA1 Dimensiones: cuentas, entidades, intercompañía, terceros y demás | `ModeloBpcTests.Dimensiones` | Servicio |
| HU-26 | CA2 Miembros desde los datos maestros | `ModeloBpcTests.Miembros` | Servicio |
| HU-26 | CA3 UX-08 lista el modelo y UX-09 muestra el paso | `ModeloBpcTests.EjecutaModelo`, `PantallasSprint5Tests.UX08ModeloYMaestros`, `.UX09Cantidades` | Servicio, web |
| HU-26 | CA4 Miembro rechazado por BPC con su código | `ModeloBpcTests.MiembroRechazado` | Servicio |

## Casos del plan F7

CP-122 a CP-134 (HU-21 a HU-24) y CP-146 a CP-153 (HU-26): CP-151 (tiempo agotado contra BPC) usa el mismo mecanismo de
`EjecucionTests.TiempoAgotado`; CP-152 (error de negocio de BPC sin reintento, con el respaldo UJBR previo) y CP-153
(reintento sin duplicar) en `ModeloBpcTests.ErrorYReintento`. Las comparaciones contra SKA1, CSKS, CEPC, TCURR y el
ambiente BPC quedan para el sandbox con SAP real (HU-01).

## Decisiones

- Dimensiones del modelo de consolidación (la arquitectura deja la lista abierta): ACCOUNT, ENTITY, INTCO, TERCERO, TIME,
  CATEGORY, RPTCURRENCY, FLOW, AUDITTRAIL y GROUP. En ACCOUNT las cuentas mapeadas cuelgan de su rubro, así el rubro suma
  sus cuentas en BPC (RN-10). Se documenta en el PR para confirmarla con arquitectura.
- Tipos de tasa de TCURR: cierre B, promedio M, histórica H, por confirmar con el cliente (pregunta 3 del DAF).
- El paso BPC empieza con un respaldo UJBR del ambiente (ADR-06).
