# Cobertura de criterios de aceptación · Sprint 11

HU-55, HU-56 y HU-58. Resultado al cierre: **536 aprobadas y 1 omitida** (DPAPI, solo Windows; el CI la exige).

| HU | CA | Prueba (clase · método) o documento | Tipo |
|---|---|---|---|
| HU-55 | CA1 Verificación ASVS nivel 2 sin hallazgos altos abiertos | `docs/07-verificacion/verificacion-asvs-l2.md`; `SeguridadAsvsTests.LimiteDePeticion`; `SeguridadWebTests` (sesión, cookie, encabezados, hosts, antiforgery) | Documento, web |
| HU-55 | CA2 Archivos y formularios validados en el servidor | `SeguridadAsvsTests.SoloCsv` (.xlsx, .exe), `.SubirNoCsv`, `.FormularioManipulado`, y las validaciones de cada HU | Regla, servicio, web |
| HU-55 | CA3 Dependencias sin vulnerabilidades altas conocidas | `SeguridadAsvsTests.DependenciasSinVulnerabilidades` (el CI falla con cualquier vulnerabilidad conocida) | CI |
| HU-55 | CA4 Datos de terceros según la Ley 1581/2012 | `SeguridadAsvsTests.TercerosNoQuedanEnLaBaseLocal` | Servicio |
| HU-56 | CA1 UX-01 a UX-10 con 0 incumplimientos A y AA en revisión automática | `AccesibilidadTests.SinIncumplimientos` (UX-01 a UX-15), `.NoSap`, `.LaRevisionDetecta`, `.Contraste` | Web |
| HU-56 | CA2 Foco visible y teclado | `AccesibilidadTests.FocoVisible`; controles nativos y pestañas WAI-ARIA (`docs/07-verificacion/accesibilidad-wcag.md`) | CSS, documento |
| HU-56 | CA3 Estados con texto e ícono | `AccesibilidadTests.Contraste`; pruebas de UX-09 a UX-15 que verifican el texto de cada estado | Web |
| HU-58 | CA1 Todos los RF de la arquitectura A en BW/4HANA | `CertificacionBw4HanaTests.RecorridoCompleto`. **Ambiente real: sandbox (HU-01).** | Servicio |
| HU-58 | CA2 Ningún InfoCubo | `CertificacionBw4HanaTests.RecorridoCompleto`, `ArquitecturaTests` (HU-06 CA2) | Servicio |
| HU-58 | CA3 La variante no cambia clientes existentes | `CertificacionBw4HanaTests.ClientesExistentesSinCambios`, `ArquitecturaTests` (RNF-Comp-04), `GeneracionSprint4Tests` (HU-16 CA1) | Servicio |

## Correcciones de la verificación

- ASVS-01 y ASVS-02: límite global de 4 MB por petición y 20 MB para la carga no SAP, aplicado antes de la validación
  antiforgery.
- ASVS-03: la carga no SAP solo acepta CSV de texto.
- UX-12: el resultado de la grilla se anuncia con `aria-live`.
