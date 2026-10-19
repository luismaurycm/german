# Acelerador de consolidación · guía de desarrollo

Monolito modular .NET 8 que corre en el equipo del consultor (ADR-01, ADR-02). SAP se simula hasta la prueba técnica HU-01.

| Sprint | HU | Pantallas |
|---|---|---|
| 1 | HU-01 a HU-06, HU-08 (y el rechazo de la arquitectura B de HU-57) | UX-01 Clientes, UX-02 Conexiones, UX-04 Módulos |
| 2 | HU-07, HU-09, HU-10, HU-11, HU-12, HU-54 | UX-03 Diagnóstico e inventario, versiones de presupuesto en UX-04, UX-05 Sociedades, UX-06 Mapeo de cuentas, registro de UX-09 |
| 3 | HU-13, HU-14, HU-15, HU-17 | UX-08 Plan de generación, UX-09 Ejecución (pasos, reintento), bodega BW 7.5 y campos Z |
| 4 | HU-16, HU-18, HU-19, HU-20, HU-25 | Variante BW/4HANA (aDSO, ODP), cadenas full y delta, activos fijos y deterioros en BW, UX-07 Sociedades no SAP |
| 5 | HU-21, HU-22, HU-23, HU-24, HU-26 | Datos maestros (planes, CeCo y CeBe, monedas y tasas, jerarquía) y modelo de consolidación en BPC |

Coberturas por sprint: `tests/COBERTURA-Sprint-N.md`.

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
| `Simulador:BodegaExistente` | false | BW ya tiene la bodega financiera (RN-03). |
| `Simulador:InventarioVacio` | false | El cliente no tiene extractores, InfoProviders, modelos BPC ni campos Z. |
| `Simulador:FallaInventario` | vacío | Paso del escaneo que falla: `Extractores`, `InfoProviders`, `ModelosBpc` o `CamposZ`. |
| `Simulador:CamposZAdicionales` | [] | Campos Z que aparecen al volver a escanear (p. ej. `ZZNUEVO`). |
| `Simulador:FallaSociedades` | false | La lectura de sociedades del ERP falla. |
| `Simulador:FallaCuentas` | false | La lectura de cuentas del ERP falla. |
| `Simulador:CuentasAdicionales` | [] | Cuentas nuevas en el ERP (`Plan`, `Numero`, `Descripcion`, `Nodo`, `Saldo`). |

### Recorrer los estados con el simulador

Además del perfil, el simulador reacciona a lo que se escribe en UX-02:

- host que contiene `falla` → «Fallida: Partner '…:sapgw00' not reached»;
- credencial `invalida` → «Fallida: Name or password is incorrect»;
- host de BW que contiene `sinfuente` → la validación ERP–BW falla;
- host de BW que contiene `bodega` → el escaneo marca «Bodega existente: Sí» (UX-03, RN-03);
- host de BPC que contiene `sinpermiso` → el escaneo falla en el paso 3, modelos BPC (UX-03);
- host del ERP que contiene `vacio` → sin extractores ni campos Z.

Datos del ERP de ejemplo: sociedades 1000 Matriz (COP, plan PCCO), 2000 Filial Colombia (COP, PCCO) y 3000 Filial
Panamá (USD, PCPA). La 9000 del prototipo es la sociedad no SAP: se agrega con «Sociedad no SAP». El plan PCCO tiene
25 cuentas y el PCPA 9, con saldos a 2026-09 para la vista previa de RN-10. El tercero T901 sirve para probar RN-07.

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

## Motor de generación

`Acelerador.Core/Generacion`: cada área tiene un generador (`IGeneradorObjetos`) que produce objetos del plan con la operación
del paquete Z que los crea (`OperacionSap`, definición JSON). `GeneradorPlan` los agrupa en los pasos de UX-09
(`PasosGeneracion`) y marca lo que ya existe (RN-03). `ServicioEjecucion` corre los pasos en orden en una cola en segundo
plano (`ColaEjecuciones`), registra cada objeto, se detiene en el primer error y reanuda desde ese paso. Cada llamada RFC
tiene 120 s de espera y un solo reintento en errores de comunicación. Agregar una variante o un área es agregar un
generador a `GeneradorPlan.VersionUno` (ADR-07, RNF-Comp-04).

Convenciones del simulador para los datos maestros: `Simulador:PlanesSinDatos`, `Simulador:FallaCentrosSociedades`,
`Simulador:TasasFaltantes` («COP:2026-06») y `Simulador:CuentasHuerfanasJerarquia` («PCCO:8888»). BPC rechaza miembros con
caracteres fuera de letras, números, guion, punto y guion bajo (hasta 32).

Convenciones del simulador para la ejecución: `Simulador:FallaEscritura` (sistemas sin permiso de escritura),
`Simulador:FallaObjetos` (objetos que fallan), `Simulador:LatenciaEscrituraMs` y el usuario técnico `rfc_lectura`, que
no tiene permiso de escritura.

## Sociedades no SAP (UX-07)

El archivo de cada sociedad no SAP (formato provisional, pregunta 5 del DAF: CSV UTF-8 con `sociedad;cuenta;periodo;tercero;ceco;cebe;importe`)
se valida en memoria y, al confirmar, se carga a la tabla de staging `ZACC_NOSAP` de BW con `Z_ACC_CARGAR_ARCHIVO`; una
carga nueva del periodo reemplaza la anterior. La base local guarda solo los metadatos de la carga. UX-07 tiene «Descargar
plantilla» con el formato.

## Registro de ejecuciones

Tabla `ejecuciones` en SQLite (RNF-Aud-01, HU-54): cada prueba de conexión, validación ERP–BW, detección de
arquitectura y paso del escaneo de inventario deja fecha, usuario de Windows, cliente, sistema, paso, objetos y
resultado. Los servicios solo agregan filas (`IRegistroEjecuciones`); la depuración (`IDepuracionRegistro`) corre al
arrancar y quita solo las entradas de más de 12 meses. Las lecturas que llenan una pantalla (sociedades y cuentas
del ERP) no son ejecuciones y no se registran.

En pantalla: enlace «Registro de ejecuciones» al pie de la navegación (`/clientes/{id}/registro`), con filtro por
resultado y «Descargar registro» (CSV con punto y coma, BOM UTF-8 y celdas protegidas contra fórmulas de Excel).

## Base local

El esquema va en la versión 2: `BaseLocal.InicializarAsync` crea las tablas `inventarios`, `sociedades` y
`mapeo_cuentas` y agrega a `clientes` las columnas `versiones` y `sociedades_no_sap`. Una base del Sprint 1 se migra
sola al arrancar.

## Integración continua

`.github/workflows/ci.yml` (ADR-11) corre en cada PR y en cada push a `main`, en `ubuntu-latest` y `windows-latest`:
restaurar, compilar en Release, probar; en Windows exige que la prueba DPAPI de HU-04 se ejecute y apruebe; y falla si
`dotnet list package --vulnerable --include-transitive` encuentra un paquete vulnerable. La firma del ejecutable y la
publicación en Azure Artifacts quedan para el sprint de distribución.

## Pendiente de SAP real (HU-01)

Ver `docs/05-arquitectura/spike-HU-01.md` y `abap/`. `NcoSapGateway` y el paquete Z son esqueletos hasta la prueba técnica.
Del Sprint 2 quedan pendientes las lecturas reales de inventario (`Z_ACC_INVENTARIO`), sociedades
(`BAPI_COMPANYCODE_GETLIST` y `BAPI_COMPANYCODE_GETDETAIL`) y cuentas (`BAPI_GL_ACC_GETLIST`, nodo de la estructura de
balance y `BAPI_GL_GETGLACCPERIODBALANCES`).
