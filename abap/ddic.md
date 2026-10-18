# Diccionario del paquete ZACC

Esqueleto del Sprint 1; se crea en SE11 durante la prueba HU-01.

## Elementos de datos

| Elemento | Tipo | Valores |
|---|---|---|
| `ZACC_DE_ESTADO` | CHAR 12 | CREADO, EXISTE, SIMULADO, ACTIVO, YA_ACTIVO, EN_CURSO, TERMINADO, FALLIDO |
| `ZACC_DE_TIPO_INVENTARIO` | CHAR 20 | EXTRACTORES, INFOPROVIDERS, MODELOS_BPC, CAMPOS_Z, BODEGA |
| `ZACC_DE_TIPO_OBJETO` | CHAR 10 | IOBJ, CUBE, ADSO, TRFN, DTP, CHAIN |
| `ZACC_DE_NOMBRE_OBJETO` | CHAR 40 | Nombre técnico; debe empezar con el prefijo de `ZACC_CONFIG` |
| `ZACC_DE_ACCION_UJBR` | CHAR 10 | RESPALDAR, RESTAURAR |
| `ZACC_DE_PASO` | CHAR 10 | LEER, CREAR, ACTIVAR, RESPALDAR, RESTAURAR (campo del objeto de autorización) |

## Estructuras y tipos de tabla

| Estructura | Campos | Tabla |
|---|---|---|
| `ZACC_S_SISTEMA` | `SID` (SYSYSID), `MANDANTE` (MANDT), `RELEASE_BASIS` (CHAR 10), `HOST` (CHAR 64) | — |
| `ZACC_S_COMPONENTE` | `COMPONENTE` (DLVUNIT), `RELEASE` (SAPRELEASE), `EXTRELEASE` (SAPPATCHLV) | `ZACC_T_COMPONENTE` |
| `ZACC_S_AMBIENTE_BPC` | `AMBIENTE` (UJ_APPSET_ID), `DESCRIPCION` (CHAR 60), `MODELO` (CHAR 10: ESTANDAR, EMBEBIDO), `ESTADO` (CHAR 10) | `ZACC_T_AMBIENTE_BPC` |

## Tabla de configuración

`ZACC_CONFIG` (cliente, mantenimiento con SM30 por el Administrador SAP):
`PREFIJO` (espacio de nombres permitido, por defecto `ZACC`), `PAQUETE` (paquete destino),
`DIR_UJBR` (directorio lógico de respaldos).

## Objeto de autorización

`Z_ACC_PASO` con los campos `ZACC_PASO` (`ZACC_DE_PASO`) y `ACTVT` (16 = ejecutar).
Cada módulo de función lo verifica con el paso que ejecuta.

## Clase de mensajes

`ZACC` · mensaje 000 = «& & & &» (texto libre devuelto en BAPIRET2).
