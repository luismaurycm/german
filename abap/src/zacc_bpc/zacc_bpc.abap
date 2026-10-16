*&---------------------------------------------------------------------*
*& Grupo de funciones ZACC_BPC · Paquete ZACC
*& ESQUELETO DEL SPRINT 1 · PENDIENTE DE LA PRUEBA TÉCNICA HU-01 (R-02)
*& BPC modelo estándar (INT-03, ADR-06). Se instala en el sistema BW
*& donde corre BPC.
*&---------------------------------------------------------------------*

FUNCTION z_acc_bpc_info.
*"----------------------------------------------------------------------
*"*"Interfaz local:
*"  EXPORTING
*"     VALUE(ET_AMBIENTES) TYPE  ZACC_T_AMBIENTE_BPC
*"     VALUE(ET_COMPONENTES) TYPE  ZACC_T_COMPONENTE
*"     VALUE(ET_MENSAJES) TYPE  BAPIRET2_T
*"----------------------------------------------------------------------
* HU-06. Versión de BPC y tipo de modelo de cada ambiente.
* ET_AMBIENTES: id del ambiente, descripción, tipo de modelo
* (ESTANDAR | EMBEBIDO) y estado. La versión 1 solo soporta modelo
* estándar; un ambiente embebido da «Versión no soportada aún» (D-04).
*
* Diseño:
*   - AUTHORITY-CHECK Z_ACC_PASO = LEER.
*   - Componentes desde CVERS (como Z_ACC_SISTEMA_INFO).
*   - Ambientes estándar: tablas UJA_* (UJA_APPSET_INFO, por confirmar);
*     embebidos: tablas RSBPC* (por confirmar en HU-01).
*
  " PENDIENTE HU-01: implementar.
  APPEND VALUE #( type = 'E' id = 'ZACC' number = '000'
                  message = 'Z_ACC_BPC_INFO pendiente de la prueba técnica HU-01' ) TO et_mensajes.
ENDFUNCTION.


FUNCTION z_acc_bpc_crear_miembro.
*"----------------------------------------------------------------------
*"*"Interfaz local:
*"  IMPORTING
*"     VALUE(IV_AMBIENTE) TYPE  UJ_APPSET_ID
*"     VALUE(IV_DIMENSION) TYPE  UJ_DIM_NAME
*"     VALUE(IV_DEFINICION) TYPE  STRING
*"     VALUE(IV_SIMULAR) TYPE  ABAP_BOOL DEFAULT ABAP_FALSE
*"  EXPORTING
*"     VALUE(EV_ESTADO) TYPE  ZACC_DE_ESTADO
*"     VALUE(ET_MENSAJES) TYPE  BAPIRET2_T
*"----------------------------------------------------------------------
* Crea o actualiza miembros de una dimensión de BPC modelo estándar y
* procesa la dimensión. Es el objeto BPC de la prueba HU-01 (CA2).
* IV_DEFINICION: JSON con ID, descripción, padre y propiedades.
*
* Diseño:
*   - AUTHORITY-CHECK Z_ACC_PASO = CREAR y que el usuario técnico tenga en
*     BPC la tarea de administración de dimensiones del ambiente.
*   - Exige que exista un respaldo UJBR reciente del ambiente (ADR-06):
*     sin respaldo de las últimas 24 h, responde FALLIDO.
*   - API candidata, a confirmar en HU-01: administración de dimensiones de
*     BPC (clases CL_UJA_*) o paquete de Data Manager de carga de miembros.
*
  " PENDIENTE HU-01: implementar.
  ev_estado = 'FALLIDO'.
  APPEND VALUE #( type = 'E' id = 'ZACC' number = '000'
                  message = 'Z_ACC_BPC_CREAR_MIEMBRO pendiente de la prueba técnica HU-01' ) TO et_mensajes.
ENDFUNCTION.


FUNCTION z_acc_bpc_ujbr.
*"----------------------------------------------------------------------
*"*"Interfaz local:
*"  IMPORTING
*"     VALUE(IV_AMBIENTE) TYPE  UJ_APPSET_ID
*"     VALUE(IV_ACCION) TYPE  ZACC_DE_ACCION_UJBR
*"     VALUE(IV_ARCHIVO) TYPE  STRING OPTIONAL
*"  EXPORTING
*"     VALUE(EV_ARCHIVO) TYPE  STRING
*"     VALUE(EV_JOBNAME) TYPE  BTCJOB
*"     VALUE(EV_JOBCOUNT) TYPE  BTCJOBCNT
*"     VALUE(ET_MENSAJES) TYPE  BAPIRET2_T
*"----------------------------------------------------------------------
* Respaldo y restauración del ambiente BPC con UJBR (ADR-06): antes de
* cambiar BPC, el acelerador siempre pide un RESPALDAR.
* IV_ACCION: RESPALDAR | RESTAURAR. RESTAURAR exige IV_ARCHIVO.
* Lanza un job en fondo (nombre ZACC_UJBR_<ambiente>) y devuelve su número;
* el acelerador sigue el avance con Z_ACC_JOB_ESTADO.
*
* Diseño:
*   - AUTHORITY-CHECK Z_ACC_PASO = RESPALDAR (o RESTAURAR, más restringido).
*   - El archivo se escribe solo en el directorio lógico ZACC_UJBR (FILE),
*     validado con FILE_VALIDATE_NAME: S_DATASET acotado a esa ruta.
*   - Programa estándar de UJBR a ejecutar en fondo: por confirmar en
*     HU-01 (el que usa la transacción UJBR) con su variante.
*   - Nunca restaura sobre un ambiente distinto del respaldado.
*
  " PENDIENTE HU-01: implementar.
  APPEND VALUE #( type = 'E' id = 'ZACC' number = '000'
                  message = 'Z_ACC_BPC_UJBR pendiente de la prueba técnica HU-01' ) TO et_mensajes.
ENDFUNCTION.
