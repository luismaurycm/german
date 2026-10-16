# Rol técnico ZACC_TECNICO: permisos mínimos por paso

HU-04 CA3 · RNF-Seg-03 · ADR-05. Lo crea el Administrador SAP del cliente (PFCG) y lo asigna al
usuario técnico de tipo Sistema o Comunicación que usa el acelerador. Hay un rol por sistema
(ERP, BW con BPC) y el cliente aprueba qué pasos habilita.

**Prohibido: SAP_ALL, SAP_NEW, S_RFC con `*` en RFC_NAME, S_DEVELOP de cambio y S_TABU_DIS sobre todos los grupos.**
El acelerador no los necesita y la prueba HU-01 lo verifica con SUIM.

Los valores marcados «por confirmar» se cierran en la prueba técnica HU-01 con un rastreo de
autorizaciones (STAUTHTRACE) de cada paso.

## Por paso

| Paso del acelerador | Sistema | Objeto | Campos y valores | HU |
|---|---|---|---|---|
| Probar conexión | ERP, BW | S_RFC | RFC_TYPE = FUGR; RFC_NAME = SRFC (RFC_PING, RFC_SYSTEM_INFO); ACTVT = 16 | HU-03 |
| Identificar arquitectura y versión | ERP, BW | S_RFC | RFC_TYPE = FUGR; RFC_NAME = ZACC_SISTEMA, ZACC_BPC; ACTVT = 16 | HU-06 |
| | ERP, BW | Z_ACC_PASO | ZACC_PASO = LEER; ACTVT = 16 | HU-06 |
| | ERP, BW | S_TABU_NAM | ACTVT = 03; TABLE = CVERS | HU-06 |
| | BW | Tarea de BPC | Ver ambientes (perfil de tareas de BPC, por confirmar) | HU-06 |
| Validar conexión ERP–BW | BW | S_RFC | RFC_NAME = ZACC_SISTEMA; ACTVT = 16 | HU-05 |
| | BW | S_RS_ADMWB | ACTVT = 03; RSADMWBOBJ = SOURCESYS | HU-05 |
| | BW | Z_ACC_PASO | ZACC_PASO = LEER | HU-05 |
| Inventariar | ERP, BW | S_RFC | RFC_NAME = ZACC_INVENTARIO; ACTVT = 16 | HU-07 |
| | ERP, BW | Z_ACC_PASO | ZACC_PASO = LEER | HU-07 |
| | BW | S_RS_ADMWB | ACTVT = 03; RSADMWBOBJ = INFOOBJECT, INFOCUBE, DATASOURCE (por confirmar) | HU-07 |
| | ERP | S_TABU_NAM | ACTVT = 03; TABLE = ROOSOURCE, DD03L | HU-07 |
| Crear objetos BW | BW | S_RFC | RFC_NAME = ZACC_BW; ACTVT = 16 | HU-15 en adelante |
| | BW | Z_ACC_PASO | ZACC_PASO = CREAR | |
| | BW | S_RS_IOBJ, S_RS_ICUBE (BW 7.5), S_RS_ADSO (BW/4HANA), S_RS_TR, S_RS_DTP, S_RS_PC | ACTVT = 01, 02; nombres limitados al prefijo del acelerador (por confirmar) | |
| | BW | S_TRANSPRT | ACTVT = 01, 02; TTYPE = TASK (graba en la orden del cliente) | |
| Activar objetos BW | BW | Z_ACC_PASO | ZACC_PASO = ACTIVAR | HU-15 en adelante |
| | BW | Mismos objetos S_RS_* | ACTVT = 07 | |
| Respaldar BPC (UJBR) | BW | S_RFC | RFC_NAME = ZACC_BPC, ZACC_COMUN; ACTVT = 16 | HU-01 |
| | BW | Z_ACC_PASO | ZACC_PASO = RESPALDAR | |
| | BW | S_BTCH_JOB | JOBACTION = RELE; JOBGROUP = ' ' (solo jobs propios ZACC_*) | |
| | BW | S_DATASET | ACTVT = 34; FILENAME = ruta del directorio lógico ZACC_UJBR | |
| | BW | Tarea de BPC | Respaldo y restauración del ambiente (por confirmar) | |
| Restaurar BPC (UJBR) | BW | Z_ACC_PASO | ZACC_PASO = RESTAURAR (solo si el cliente lo aprueba) | HU-01 |
| | BW | S_DATASET | ACTVT = 33; misma ruta | |
| Crear miembros BPC | BW | Z_ACC_PASO | ZACC_PASO = CREAR | HU-01, HU-26 en adelante |
| | BW | Tarea de BPC | Administrar dimensiones del ambiente (por confirmar) | |

## Cómo se verifica

1. SUIM → usuarios por autorizaciones complejas: el usuario técnico no tiene SAP_ALL ni SAP_NEW.
2. STAUTHTRACE durante cada paso de la prueba HU-01: todo chequeo fallido se documenta y se
   agrega aquí con el valor mínimo; nada se agrega «por si acaso».
3. El resultado queda en la nota técnica de HU-01 (sección 7 del guion).
