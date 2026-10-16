# Acelerador de consolidación · guía de desarrollo

Monolito modular .NET 8 que corre en el equipo del consultor (ADR-01, ADR-02). El Sprint 1 cubre
HU-01 a HU-06 y HU-08 (con el rechazo de la arquitectura B de HU-57): clientes (UX-01),
conexiones (UX-02) y módulos (UX-04). SAP se simula hasta la prueba técnica HU-01.

## Requisitos

- SDK de .NET 8 (el repositorio fija 8.0.413 en `global.json`; acepta parches 8.0.4xx).
- Nada más: sin servidores ni base de datos instalada (RNF-Comp-03). SQLite va dentro de la aplicación.

## Compilar, probar y correr

Desde la raíz del repositorio:

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet run --project src/Acelerador.Web
```

Al arrancar, la consola imprime un enlace `http://127.0.0.1:5080/?t=…` y abre el navegador con él.
El token cambia en cada arranque: sin él, la aplicación responde 401 (ADR-05).
Para no abrir el navegador: `--Acelerador:AbrirNavegador=false`.

## Proyectos

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `Acelerador.Sap` | Contrato `ISapGateway` y sus DTO; `SimulatedSapGateway`; `NcoSapGateway` (pendiente de HU-01) | — |
| `Acelerador.Core` | Dominio, reglas de negocio (`Reglas/`), servicios que orquestan cada pantalla (`Servicios/`) y puertos (`Puertos/`) | Sap |
| `Acelerador.Infra` | SQLite (`Datos/`), almacén de credenciales de Windows y en memoria (`Credenciales/`) | Core |
| `Acelerador.Web` | Razor Pages, sesión local con token, composición | Core, Infra, Sap |
| `tests/Acelerador.Tests` | xUnit: reglas, servicios contra el simulador, SQLite, pantallas completas y seguridad | todos |

Las reglas de negocio son funciones puras en `Acelerador.Core/Reglas` y se prueban contra el
simulador, sin SAP (Regla 6). Cada prueba lleva en su nombre la HU y el criterio que cubre:
`dotnet test --filter "HU=HU-06"` corre solo las de una historia.

## Configuración

`src/Acelerador.Web/appsettings.json`, o por línea de comandos (`--Seccion:Clave=valor`) o variables
de entorno (`Seccion__Clave`).

| Clave | Por defecto | Qué hace |
|---|---|---|
| `Acelerador:Puerto` | 5080 | Puerto en 127.0.0.1 (0 = cualquiera libre). Nunca escucha en otra interfaz. |
| `Acelerador:BaseDatos` | vacío | Ruta del SQLite. Vacío = `%LOCALAPPDATA%\CMConsulting\Acelerador\acelerador.db`. |
| `Acelerador:AlmacenCredenciales` | Auto | `Auto`: Administrador de credenciales en Windows, memoria en otros sistemas. `Windows` o `Memoria` para forzarlo. |
| `Sap:Modo` | Simulado | `Simulado` o `Nco` (el conector real devuelve «pendiente de HU-01»). |
| `Simulador:Perfil` | ABw75 | `ABw75`, `ABw4Hana`, `AVersionNoSoportada`, `ABpcEmbebido` o `B`. |
| `Simulador:SistemasConFallo` | [] | Sistemas cuya prueba de conexión falla (`Erp`, `Bw`, `Bpc`). |
| `Simulador:FallaValidacionErpBw` | false | La validación ERP–BW falla. |
| `Simulador:FallaDeteccion` | false | La detección de arquitectura falla. |
| `Simulador:LatenciaMs` | 600 | Demora de cada llamada, para ver el estado de carga. |

### Recorrer los estados con el simulador

Además del perfil, el simulador reacciona a lo que se escribe en UX-02:

- host que contiene `falla` → «Fallida: Partner '…:sapgw00' not reached»;
- credencial `invalida` → «Fallida: Name or password is incorrect»;
- host de BW que contiene `sinfuente` → la validación ERP–BW falla.

Paisaje del prototipo en arquitectura A: ERP `erp-ejemplo.local` mandante 300; BW y BPC
`bw-ejemplo.local` mandante 100. Para la arquitectura B, los tres en `s4-ejemplo.local` 300 con:

```bash
dotnet run --project src/Acelerador.Web -- --Simulador:Perfil=B
```

## Seguridad

- Kestrel escucha solo en `127.0.0.1`; `AllowedHosts` acepta solo `127.0.0.1` y `localhost`.
- Token aleatorio por arranque, cambiado por la cookie `acelerador_sesion` (HttpOnly, SameSite=Strict); formularios con antiforgery.
- CSP `default-src 'self'`, sin recursos externos.
- Las credenciales de SAP van al Administrador de credenciales de Windows (DPAPI). SQLite guarda solo la referencia
  `CMConsulting.Acelerador/<cliente>/<sistema>`. Las credenciales nunca vuelven al navegador ni llegan a un registro:
  los mensajes de SAP se sanitizan y las pruebas buscan la clave en la base, los registros y la configuración.
- Fuera de Windows (desarrollo en macOS o Linux) el almacén es en memoria y la aplicación lo avisa en pantalla.

## Registro de ejecuciones

Tabla `ejecuciones` en SQLite (RNF-Aud-01, base de HU-54): cada prueba de conexión, validación ERP–BW y
detección de arquitectura deja fecha, usuario de Windows, cliente, sistema, paso, objetos y resultado.
Solo se agregan filas. La consulta y descarga en pantalla llegan con HU-54 (UX-09).

## Pendiente de SAP real (HU-01)

Ver `docs/05-arquitectura/spike-HU-01.md` y `abap/`. `NcoSapGateway` y el paquete Z son esqueletos hasta la prueba técnica.
