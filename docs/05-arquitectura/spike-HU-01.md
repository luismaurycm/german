# Spike HU-01: prueba técnica de inyección en la arquitectura A

**Estado:** guion listo, prueba sin ejecutar (no hay acceso a SAP todavía) · **Riesgo:** R-02, aceptado hasta el resultado
**Decisiones relacionadas:** ADR-03 (.NET 8 con SAP NCo 3.1), ADR-05 (usuario técnico mínimo), ADR-06 (RFC con paquete Z, transportes y UJBR)
**Duración máxima:** un sprint. Si el mecanismo falla, la decisión se escala antes de iniciar la épica E3.

## 1. Qué se quiere confirmar

| # | Pregunta | Criterio de HU-01 |
|---|---|---|
| Q1 | ¿SAP NCo 3.1 funciona desde .NET 8 (x64) en el equipo del consultor? | ADR-03 |
| Q2 | ¿Se puede crear y activar por RFC un objeto BW con el paquete Z y el usuario técnico, sin SAP_ALL? | CA1, CA2 |
| Q3 | ¿Se puede crear por RFC un miembro de dimensión en BPC modelo estándar? | CA2 |
| Q4 | ¿Se puede generar y restaurar un respaldo UJBR del ambiente BPC de prueba? | CA3 |
| Q5 | ¿Cuáles son los permisos mínimos reales de cada paso? | CA1, CA4, HU-04 CA3 |
| Q6 | ¿Qué componentes y releases reporta SAP para BW y BPC? (para la detección de HU-06) | HU-06 |

## 2. Ambiente

- **SAP:** sandbox de arquitectura A del ambiente DEV de la arquitectura: ECC 6.0 (o S/4HANA) + BW 7.5 con BPC 10.1 modelo estándar, en sistemas separados.
- **BPC:** un ambiente de prueba copiado del de ejemplo (p. ej. `ZACC_PRUEBA`), nunca el ambiente productivo ni uno con datos del cliente.
- **Equipo del consultor:** Windows 10/11 x64, .NET 8, SAP GUI, el acelerador compilado con `Sap:Modo = Nco`.
- **Red:** acceso a los puertos del gateway y del dispatcher de los sistemas (33NN y 32NN) o por SAProuter.
- **Personas:** Administrador SAP del cliente (transporte, rol, usuario), consultor BW/BPC de CM y líder técnico de CM.

## 3. Preparación (Administrador SAP)

1. Crear en el sandbox el paquete `ZACC`, los objetos de [`abap/ddic.md`](../../abap/ddic.md) y los grupos de funciones de [`abap/src/`](../../abap/src), con los módulos marcados como RFC habilitados.
2. Crear la tabla `ZACC_CONFIG` con el prefijo `ZACC` y el directorio lógico `ZACC_UJBR`.
3. Crear el objeto de autorización `Z_ACC_PASO` y el rol `ZACC_TECNICO` según [`abap/rol-ZACC_TECNICO.md`](../../abap/rol-ZACC_TECNICO.md).
4. Crear el usuario técnico `RFC_ACEL` (tipo Sistema) con solo ese rol. Verificar en SUIM que no tiene SAP_ALL ni SAP_NEW.
5. Crear una orden de transporte para los objetos de prueba.

## 4. Permisos mínimos

Los de [`abap/rol-ZACC_TECNICO.md`](../../abap/rol-ZACC_TECNICO.md). Durante toda la prueba se deja activo STAUTHTRACE
para el usuario `RFC_ACEL`: cada chequeo fallido se anota en la tabla de la sección 7 y se agrega al rol con el valor mínimo.

## 5. Pasos

Cada paso tiene su evidencia: captura de SAP GUI, salida del acelerador o exportación de STAUTHTRACE.

| Paso | Acción | Resultado esperado | Evidencia |
|---|---|---|---|
| P1 | Aplicación de consola .NET 8 x64 que carga `sapnco.dll` y `sapnco_utils.dll` (NCo 3.1) y llama `RFC_PING` con un destino en memoria. | Respuesta sin excepción; versión de NCo impresa. | Salida de consola. Responde Q1. |
| P2 | En el acelerador, con `Sap:Modo = Nco`, registrar ERP, BW y BPC en UX-02 y **Probar conexión**. | «Conexión exitosa» en los tres, con SID y producto. Una clave errada da «Fallida» con el mensaje de SAP y sin la clave. | Captura de UX-02 y del registro de ejecuciones. |
| P3 | `Z_ACC_SISTEMA_INFO` en ERP y BW, y `Z_ACC_BPC_INFO` en BW. | SID, componentes (SAP_BW 750, CPMBPC…) y ambientes con su tipo de modelo. | Salida de SE37 o del acelerador. Responde Q6: actualizar `InterpretadorComponentes`. |
| P4 | `Z_ACC_BW_VALIDAR_FUENTE` con el sistema lógico del ERP. | `EV_ACTIVA = X`. Con un sistema lógico inexistente, mensaje claro. | Salida de SE37. |
| P5 | `Z_ACC_INVENTARIO` con cada tipo. | JSON con extractores, InfoProviders, ambientes BPC y campos Z, sin datos financieros. | JSON guardado. |
| P6 | `Z_ACC_BPC_UJBR` con `RESPALDAR` sobre `ZACC_PRUEBA`; seguir con `Z_ACC_JOB_ESTADO`. | Job `ZACC_UJBR_ZACC_PRUEBA` terminado y archivo en el directorio lógico. | SM37 y AL11. Responde Q4 (respaldo). |
| P7 | `Z_ACC_CREAR_OBJETO` con `IOBJ` `ZACCT001` (CHAR 10, texto «Prueba acelerador»): primero `IV_SIMULAR = X`, luego real con la orden del paso 3.5. | `SIMULADO` y luego `CREADO`; el objeto queda en la orden. Repetir da `EXISTE`. | SE09 con la orden. |
| P8 | `Z_ACC_ACTIVAR` sobre `ZACCT001`. | `ACTIVO`. | RSD1 en SAP GUI muestra el InfoObjeto activo. Responde Q2. |
| P9 | `Z_ACC_BPC_CREAR_MIEMBRO` en `ZACC_PRUEBA`, dimensión de entidad, miembro `ZACCT01`. | `CREADO` y dimensión procesada. | Administración de BPC o EPM add-in muestra el miembro. Responde Q3. |
| P10 | `Z_ACC_BPC_UJBR` con `RESTAURAR` y el archivo del P6. | El ambiente vuelve al estado del P6: el miembro `ZACCT01` ya no existe. | Administración de BPC. Responde Q4 (restauración). |
| P11 | Intentar `Z_ACC_CREAR_OBJETO` con un nombre fuera del prefijo (`0TEST01`). | Rechazo con mensaje E; nada creado. | Salida de SE37. |
| P12 | Revisar STAUTHTRACE de todos los pasos. | Lista final de objetos y valores; ningún permiso amplio. | Exportación de STAUTHTRACE. Responde Q5. |
| P13 | Limpieza: borrar `ZACCT001` y su orden, y el ambiente `ZACC_PRUEBA` si se creó solo para la prueba. | Sandbox sin restos. | SE09 y BPC. |

## 6. Criterios de éxito y de escalamiento

**Éxito (seguir con el mecanismo de ADR-06):**

- Q1 a Q4 en «sí» con el usuario técnico y el rol `ZACC_TECNICO`, sin SAP_ALL (CA1, CA2, CA3).
- El rol final no tiene permisos amplios (Q5) y queda documentado en `abap/rol-ZACC_TECNICO.md` (CA4, HU-04 CA3).
- Las APIs «por confirmar» del paquete Z tienen respuesta y se anotan en el código ABAP.

**Escalamiento (antes de iniciar E3):**

| Si falla | Opción |
|---|---|
| Q1: NCo 3.1 no funciona con .NET 8 | Proceso auxiliar con la versión de NCo soportada que hable con el acelerador por línea de comandos; se revisa ADR-03. |
| Q2 o Q3: el cliente no permite crear por RFC | Alternativa descartada en ADR-06: generar transportes y archivos para que el Administrador SAP los importe. Pierde inventario en línea y reintento por paso: control de cambios. |
| Q3: no hay API estable para miembros de BPC | Carga de miembros con un paquete de Data Manager lanzado por el paquete Z. |
| Q4: UJBR no se puede lanzar en fondo | Respaldo manual del Administrador SAP antes de cada paso de BPC, con confirmación en el acelerador. |

La decisión la toman el líder técnico de CM y el Administrador SAP del cliente, y queda en la nota técnica.

## 7. Nota técnica de resultado (plantilla)

Entregable de HU-01 (CA4). Se completa al terminar la prueba.

- **Fecha, ambiente y personas.**
- **Resultado por pregunta (Q1 a Q6):** sí o no, con la evidencia del paso.
- **Permisos mínimos confirmados:** tabla por paso con objeto, campos y valores (reemplaza los «por confirmar» del rol).
- **Componentes y releases leídos:** para `InterpretadorComponentes` (BPC 10.1 y BPC 2021).
- **APIs confirmadas:** creación de InfoObjeto, aDSO o InfoCubo, miembros BPC y programa de UJBR.
- **Recomendación:** seguir con el mecanismo o cambiarlo (sección 6), y efecto en R-02.

## 8. Qué cambia en el código cuando la prueba termine

| Pieza | Hoy (Sprint 1) | Después de HU-01 |
|---|---|---|
| `src/Acelerador.Sap/Nco/NcoSapGateway.cs` | Devuelve «pendiente de la prueba técnica HU-01»; sin referencia a NCo. | Llama `RFC_PING`, `RFC_SYSTEM_INFO`, `Z_ACC_SISTEMA_INFO`, `Z_ACC_BW_VALIDAR_FUENTE` y `Z_ACC_BPC_INFO` con NCo 3.1. |
| `src/Acelerador.Sap/Nco/InterpretadorComponentes.cs` | BW confirmado; BPC 10.1 supuesto (CPMBPC 810); BPC 2021 sin reconocer. | Tabla de componentes de BPC confirmada. |
| `abap/` | Firmas y diseño. | Paquete real serializado con abapGit. |
| `abap/rol-ZACC_TECNICO.md` | Valores «por confirmar». | Valores del rastreo STAUTHTRACE. |

Las reglas de negocio no cambian: se prueban contra `SimulatedSapGateway` (Regla 6) y el conector real solo traduce llamadas.

## 9. Cómo correr el acelerador para la prueba

```bash
dotnet run --project src/Acelerador.Web -- --Sap:Modo=Nco
```

Con `Sap:Modo = Simulado` (por defecto) la aplicación usa el simulador; ver `src/README.md`.
