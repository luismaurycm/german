# Verificación OWASP ASVS 4.0.3 nivel 2 · Acelerador de consolidación

HU-55 (RNF-Seg-01). Verificación del código del configurador al cierre del Sprint 11, contra el simulador de SAP.
**Resultado: sin hallazgos altos abiertos.** Los controles marcados «Prueba» se comprueban en cada build del CI
(`ubuntu-latest` y `windows-latest`).

## Contexto que define el alcance

- Aplicación web local en el equipo del consultor (ADR-02): Kestrel escucha solo en `127.0.0.1`, sin usuarios ni roles
  propios. La identidad es la sesión de Windows y un token aleatorio por arranque (ADR-05).
- Los datos financieros quedan en BW y BPC; la base local SQLite guarda configuración, metadatos y el registro (ADR-04).
- La comunicación con SAP es por RFC con SAP .NET Connector 3.1 y SNC; se comprueba en la prueba técnica HU-01.

## Controles

| Capítulo | Requisito (resumen) | Cómo se cumple | Evidencia |
|---|---|---|---|
| V1 Arquitectura | 1.1, 1.4 Componentes y fronteras de confianza documentados | Monolito modular, Core sin dependencias de SAP, puertos para SAP y datos | ADR-01 a ADR-11; `src/README.md` |
| V2 Autenticación | 2.1-2.10 Credenciales y secretos | Sin contraseñas propias. Token de 256 bits por arranque, comparado en tiempo constante | `SesionLocal`; prueba `SeguridadWebTests` (ADR-05) |
| V2.10 Secretos de servicio | Credenciales de SAP fuera del código y de la base | Almacén de credenciales de Windows (DPAPI); en el simulador, en memoria | `WindowsCredentialStore`; prueba DPAPI en Windows (CI la exige) |
| V3 Sesión | 3.2-3.4 Token aleatorio, cookie HttpOnly, SameSite=Strict, sin token en la URL | El token del enlace se cambia por la cookie y se redirige sin él | Prueba `SeguridadWebTests` «el token válido se cambia por una cookie…» |
| V4 Control de acceso | 4.1-4.3 Acceso denegado por defecto | Toda ruta exige la sesión (401 sin ella); cada paso exige el avance del cliente (`ReglaAvance`) | Pruebas «sin token de sesión, toda página responde 401» y de navegación por paso |
| V4.2.2 CSRF | Antiforgery en todo POST | `AutoValidateAntiforgeryToken` global; cookie SameSite=Strict | Prueba «un POST sin token antiforgery se rechaza» |
| V5 Validación | 5.1 Validación del lado del servidor de formularios y archivos | Reglas en Core para cada formulario; el archivo no SAP solo como CSV de texto | `SeguridadAsvsTests.SoloCsv`, `.SubirNoCsv`, `.FormularioManipulado` y las pruebas de cada HU |
| V5.3 Codificación de salida | Razor codifica todo; el JS usa `textContent`; CSV protegido contra fórmulas | Sin `Html.Raw` con datos; descarga del registro con celdas protegidas | Pruebas de HU-54 |
| V5.3.4 Inyección SQL | Consultas parametrizadas | Todos los repositorios usan parámetros | `Acelerador.Infra/Datos/Repositorios.cs` |
| V7 Errores y registro | 7.1 Sin secretos en el registro; 7.4 errores genéricos | `SanitizadorMensajes` quita credenciales; página de error sin detalles técnicos | Prueba RNF-Seg-02; `Pages/Error.cshtml` |
| V8 Protección de datos | 8.1-8.3 Sin caché de páginas, minimización de datos | `Cache-Control: no-store`; la base local no guarda importes ni terceros | `SeguridadAsvsTests.TercerosNoQuedanEnLaBaseLocal` |
| V9 Comunicaciones | 9.2 Canal cifrado hacia los servicios | RFC con SNC hacia SAP; la interfaz web es solo loopback | **Pendiente de HU-01 (sandbox).** |
| V10 Código malicioso | 10.3 Integridad de la distribución | Paquete firmado desde Azure DevOps | ADR-11 |
| V12 Archivos | 12.1.1 Límite de tamaño; 12.2 tipo de archivo; 12.3 sin rutas del usuario | 4 MB por petición, 20 MB la carga no SAP; solo CSV; el archivo se procesa en memoria | `SeguridadAsvsTests.LimiteDePeticion`, `.SoloCsv` |
| V13 API | 13.1.3 Límite de peticiones; 13.2 métodos | El único JSON es el estado de la ejecución (GET de solo lectura) | `Ejecucion.cshtml.cs` |
| V14 Configuración | 14.2 Dependencias sin vulnerabilidades conocidas | El CI falla si `dotnet list package --vulnerable --include-transitive` encuentra alguna | `SeguridadAsvsTests.DependenciasSinVulnerabilidades`; `ci.yml` |
| V14.4 Encabezados | CSP sin recursos externos, nosniff, DENY, no-referrer, COOP, Permissions-Policy, sin `Server` | `EncabezadosSeguridad`; `AddServerHeader = false` | Prueba «…envía encabezados de endurecimiento» |
| V14.5 Hosts | Rechazo de hosts ajenos (DNS rebinding) | `AllowedHosts: 127.0.0.1;localhost` | Prueba «un nombre de host distinto… se rechaza» |

## Hallazgos de esta verificación

| Id | Severidad | Hallazgo | Estado |
|---|---|---|---|
| ASVS-01 | Media | No había un límite global del tamaño de las peticiones (Kestrel admite 30 MB por defecto). | Resuelto: 4 MB por petición y 20 MB solo para la carga no SAP (`UseLimitePeticiones`). |
| ASVS-02 | Media | Con un límite global, la validación antiforgery leía la carga no SAP antes de que `[RequestSizeLimit]` subiera su límite, así que una carga legítima de más de 4 MB se habría rechazado. | Resuelto: el middleware fija el límite de la carga antes de leer el cuerpo. |
| ASVS-03 | Baja | La carga no SAP aceptaba cualquier extensión; un binario solo fallaba como CSV mal formado. | Resuelto: solo `.csv` o `.txt` de texto, rechazo explícito. |
| ASVS-04 | Baja | HTTP sin TLS y cookie sin `Secure`. | Aceptado: solo loopback (ADR-02); el tráfico no sale del equipo. |
| ASVS-05 | Baja | La sesión dura lo que el proceso. | Aceptado: uso local de un solo usuario; cerrar el acelerador invalida el token (ADR-05). |

## Ley 1581 de 2012 (datos de terceros)

Los archivos de sociedades no SAP pueden traer el tercero de cada partida. El acelerador los valida en memoria, los
carga en la tabla de staging de BW y no los guarda en la base local ni en el registro de ejecuciones. La validación
pendiente vence a los 30 minutos (HU-25). El tratamiento de esos datos en SAP sigue las políticas del cliente
(DAF, sección 9).
