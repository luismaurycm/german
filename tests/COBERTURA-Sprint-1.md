# Cobertura de criterios de aceptación · Sprint 1

Cada criterio de HU-01 a HU-06, HU-08 y HU-57 tiene al menos una prueba en `tests/Acelerador.Tests`.
Las pruebas se nombran con la HU y el criterio; `dotnet test --filter "HU=HU-03"` corre las de una historia.
Todas corren contra `SimulatedSapGateway`, sin SAP real (Regla 6).

| HU | CA | Prueba (clase · método) | Tipo |
|---|---|---|---|
| HU-01 | CA1 Sin SAP_ALL, usuario técnico | `EntregablesSpikeTests.Guion`, `EntregablesSpikeTests.RolTecnico` | Documento. **La ejecución real queda pendiente de SAP.** |
| HU-01 | CA2 Objeto BW y miembro BPC por RFC | `EntregablesSpikeTests.FirmasAbap` (firmas `Z_ACC_CREAR_OBJETO`, `Z_ACC_ACTIVAR`, `Z_ACC_BPC_CREAR_MIEMBRO`), `ConectorNcoTests.*` | Esqueleto. **Pendiente de SAP.** |
| HU-01 | CA3 Respaldo y restauración UJBR | `EntregablesSpikeTests.FirmasAbap` (`Z_ACC_BPC_UJBR`), pasos P6 y P10 del guion | Esqueleto. **Pendiente de SAP.** |
| HU-01 | CA4 Nota técnica con permisos y recomendación | `EntregablesSpikeTests.Guion` (sección 7, plantilla) | Documento. Se completa tras la prueba. |
| HU-01 | CA5 Duración de un sprint y escalamiento | `EntregablesSpikeTests.Guion` (sección 6) | Documento. |
| HU-02 | CA1 Lista con arquitectura, paso, estado, última ejecución y usuario | `PantallasTests.UX01Lista`, `ServicioClientesTests.ListaConDatos` | Web, servicio |
| HU-02 | CA2 Nombre obligatorio y no repetido | `ReglasClienteTests.NombreObligatorio`, `.NombreNoRepetido`, `PantallasTests.UX01NombreInvalido`, `ServicioClientesTests.NoRepetido` | Regla, web |
| HU-02 | CA3 Al crear lleva a UX-02 | `PantallasTests.UX01CrearLlevaAConexiones`, `ServicioClientesTests.NuevoEmpiezaEnConexiones` | Web |
| HU-02 | CA4 Estado vacío | `PantallasTests.UX01Vacio` | Web |
| HU-02 | CA5 Búsqueda por nombre | `ReglasClienteTests.Buscar`, `ServicioClientesTests.Busqueda`, `PantallasTests.UX01Busqueda` | Regla, web |
| HU-02 | CA6 Equipo del consultor, sin servidores | `ServidorLocalTests.SoloLoopback`, `RegistroEjecucionesTests.RutaEnPerfil` | Integración (Kestrel real) |
| HU-03 | CA1 Tarjeta por sistema, campos obligatorios | `ReglasConexionTests.CadaCampoObligatorio`, `PantallasTests.UX02Tarjetas`, `ServicioConexionesTests.DatosIncompletos` | Regla, web |
| HU-03 | CA2 «Conexión exitosa» o «Fallida» con el mensaje de SAP | `ServicioConexionesTests.ConexionExitosa`, `.ConexionFallida`, `PantallasTests.UX02Probar` | Servicio, web |
| HU-03 | CA3 Continuar solo con conexiones exitosas | `ReglaAvanceTests.SinConexiones`, `ServicioConexionesTests.ContinuarSoloConExito`, `PantallasTests.UX02ContinuarDeshabilitado` | Regla, servicio, web |
| HU-03 | CA4 Cada prueba en el registro | `ServicioConexionesTests.CadaPruebaQuedaRegistrada`, `PantallasTests.UX02Registro` | Servicio, web |
| HU-04 | CA1 Credencial enmascarada, Mostrar | `PantallasTests.UX02CredencialEnmascarada` (más verificación manual en navegador) | Web |
| HU-04 | CA2 Sin credencial legible en configuración ni registros | `ServicioConexionesTests.CredencialEnAlmacen`, `PantallasTests.UX02SinCredencialLegible`, `AlmacenCredencialesTests.Windows` (solo Windows) | Servicio, web |
| HU-04 | CA3 Rol técnico con permisos mínimos, sin SAP_ALL | `EntregablesSpikeTests.RolTecnico` | Documento. Valores finales pendientes de HU-01. |
| HU-04 | CA4 Credencial inválida sin escribirla | `SanitizadorMensajesTests.*`, `ServicioConexionesTests.ErrorSinCredencial`, `PantallasTests.UX02CredencialInvalida` | Regla, servicio, web |
| HU-05 | CA1 Botón solo con ERP y BW separados (RN-01) | `ReglaRn01Tests.*`, `ServicioConexionesTests.ValidacionNoAplicaEnB`, `PantallasTests.UX02ValidacionVisibleEnA`, `.UX02ValidacionOcultaEnMismoSistema` | Regla, web |
| HU-05 | CA2 Resultado con fecha, usuario y mensaje | `ServicioConexionesTests.ValidacionRegistrada`, `PantallasTests.UX02Validacion` | Servicio, web |
| HU-05 | CA3 Falla: mensaje y Continuar deshabilitado | `ReglaAvanceTests.FaltaValidacion`, `ServicioConexionesTests.ValidacionFallida`, `PantallasTests.UX02Validacion` | Regla, servicio, web |
| HU-06 | CA1 Insignia A o B y versiones | `PantallasTests.UX02ArquitecturaA`, `.UX02ArquitecturaB`, `ClasificadorArquitecturaTests.TextoArquitectura` | Web, regla |
| HU-06 | CA2 A reconoce BW 7.5 y BW/4HANA con BPC estándar | `ClasificadorArquitecturaTests.Bw75`, `.Bw4Hana`, `ServicioConexionesTests.DeteccionGuardada` | Regla, servicio |
| HU-06 | CA3 «Versión no soportada aún» y no continúa | `ClasificadorArquitecturaTests.SinVariante`, `ReglaAvanceTests.VersionNoSoportada`, `ServicioConexionesTests.VersionNoSoportada`, `PantallasTests.UX02VersionNoSoportada` | Regla, servicio, web |
| HU-06 | CA4 Guardado en el cliente y visible en UX-01 | `ServicioConexionesTests.DeteccionGuardada`, `RegistroEjecucionesTests.ClienteCompleto`, `PantallasTests.UX01Lista` | Servicio, web |
| HU-57 | CA1 «Arquitectura B no soportada en la versión 1» | `ClasificadorArquitecturaTests.ArquitecturaB`, `PantallasTests.UX02ArquitecturaB` | Regla, web |
| HU-57 | CA2 Continuar deshabilitado, nada creado | `ReglaAvanceTests.ArquitecturaB`, `.PuedeAbrir`, `ServicioConexionesTests.ArquitecturaB`, `PantallasTests.UX02ArquitecturaB` | Regla, servicio, web |
| HU-57 | CA3 «No soportado» con motivo en UX-01 | `ServicioConexionesTests.ArquitecturaB`, `PantallasTests.UX02ArquitecturaB` | Servicio, web |
| HU-57 | CA4 Detección en el registro | `ServicioConexionesTests.ArquitecturaB` | Servicio |
| HU-08 | CA1 Tarjeta con casilla y descripción | `ReglasModulosTests.Catalogo`, `PantallasTests.UX04Tarjetas` | Regla, web |
| HU-08 | CA2 Resumen de módulos y reportes | `ReglasModulosTests.Resumen`, `PantallasTests.UX04ResumenYContinuar` (más verificación manual en navegador) | Regla, web |
| HU-08 | CA3 Continuar deshabilitado sin módulos | `ReglasModulosTests.SinModulos`, `ServicioModulosTests.ContinuarSinModulos`, `PantallasTests.UX04ResumenYContinuar` | Regla, servicio, web |
| HU-08 | CA4 Activos fijos y deterioros se quedan en BW (RN-12) | `ReglasModulosTests.SoloBw`, `PantallasTests.UX04Tarjetas` | Regla, web |

Transversales: `SeguridadWebTests` (token por sesión, cookie HttpOnly y SameSite=Strict, CSP, host ajeno,
antiforgery), `RegistroEjecucionesTests` (RNF-Aud-01), `ClasificadorArquitecturaTests.AgregarVarianteNoTocaExistentes`
(RNF-Comp-04) y `PantallasTests.Accesibilidad` (WCAG 2.1 AA básico).

## Verificación manual hecha

Recorrido UX-01 → UX-02 → UX-03 → UX-04 → UX-05 en el navegador con el simulador (perfil ABw75): crear
cliente, probar las tres conexiones (también un fallo con host `falla`), Mostrar y Ocultar la credencial,
validar ERP–BW, ver la arquitectura detectada, marcar módulos con el resumen en vivo y volver a UX-01.
