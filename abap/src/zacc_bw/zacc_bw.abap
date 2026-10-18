*&---------------------------------------------------------------------*
*& Grupo de funciones ZACC_BW · Paquete ZACC
*& ESQUELETO DEL SPRINT 1 · PENDIENTE DE LA PRUEBA TÉCNICA HU-01 (R-02)
*& Creación y activación de objetos BW (INT-02, ADR-06, ADR-07).
*&---------------------------------------------------------------------*

FUNCTION z_acc_crear_objeto.
*"----------------------------------------------------------------------
*"*"Interfaz local:
*"  IMPORTING
*"     VALUE(IV_TIPO_OBJETO) TYPE  ZACC_DE_TIPO_OBJETO
*"     VALUE(IV_NOMBRE) TYPE  ZACC_DE_NOMBRE_OBJETO
*"     VALUE(IV_DEFINICION) TYPE  STRING
*"     VALUE(IV_PAQUETE) TYPE  DEVCLASS
*"     VALUE(IV_ORDEN) TYPE  TRKORR
*"     VALUE(IV_SIMULAR) TYPE  ABAP_BOOL DEFAULT ABAP_FALSE
*"  EXPORTING
*"     VALUE(EV_ESTADO) TYPE  ZACC_DE_ESTADO
*"     VALUE(ET_MENSAJES) TYPE  BAPIRET2_T
*"----------------------------------------------------------------------
* Crea (sin activar) un objeto BW desde su definición JSON, que genera el
* acelerador con la plantilla de la variante (ADR-07).
* IV_TIPO_OBJETO: IOBJ (InfoObjeto), CUBE (InfoCubo, solo BW 7.5),
*                 ADSO (solo BW/4HANA), TRFN, DTP, CHAIN.
* EV_ESTADO: CREADO | EXISTE | SIMULADO | FALLIDO.
*
* Diseño:
*   - AUTHORITY-CHECK Z_ACC_PASO = CREAR, más la autorización BW estándar
*     del tipo (S_RS_IOBJ, S_RS_ICUBE, S_RS_ADSO, S_RS_TR, S_RS_DTP,
*     S_RS_PC) con actividad 01/02: el rol técnico solo las tiene en el
*     espacio de nombres del acelerador.
*   - Rechaza IV_NOMBRE fuera del prefijo de ZACC_CONFIG (regla común 3).
*   - Rechaza CUBE en BW/4HANA y ADSO en BW 7.5 (la variante decide).
*   - Si existe con la misma definición → EXISTE (regla común 4).
*   - Graba el objeto en IV_ORDEN (orden de transporte del cliente) y en
*     IV_PAQUETE; nunca en $TMP salvo en la prueba técnica.
*   - APIs candidatas, a confirmar en HU-01:
*       IOBJ  BAPI_IOBJ_CREATE
*       CUBE  BAPI_CUBE_CREATE
*       ADSO  API de aDSO de BW/4HANA (clase CL_RSO_ADSO_API, por confirmar)
*       TRFN/DTP/CHAIN  clases de BW (CL_RSTRAN_*, CL_RSBK_DTP, CL_RSPC_*)
*
  " PENDIENTE HU-01: implementar.
  ev_estado = 'FALLIDO'.
  APPEND VALUE #( type = 'E' id = 'ZACC' number = '000'
                  message = 'Z_ACC_CREAR_OBJETO pendiente de la prueba técnica HU-01' ) TO et_mensajes.
ENDFUNCTION.


FUNCTION z_acc_activar.
*"----------------------------------------------------------------------
*"*"Interfaz local:
*"  IMPORTING
*"     VALUE(IV_TIPO_OBJETO) TYPE  ZACC_DE_TIPO_OBJETO
*"     VALUE(IV_NOMBRE) TYPE  ZACC_DE_NOMBRE_OBJETO
*"     VALUE(IV_ORDEN) TYPE  TRKORR
*"  EXPORTING
*"     VALUE(EV_ESTADO) TYPE  ZACC_DE_ESTADO
*"     VALUE(ET_MENSAJES) TYPE  BAPIRET2_T
*"----------------------------------------------------------------------
* Activa un objeto creado por Z_ACC_CREAR_OBJETO.
* EV_ESTADO: ACTIVO | YA_ACTIVO | FALLIDO (con el log de activación).
*
* Diseño:
*   - AUTHORITY-CHECK Z_ACC_PASO = ACTIVAR y la autorización BW del tipo
*     con actividad 07 (activar).
*   - Solo activa objetos del espacio de nombres del acelerador.
*   - APIs candidatas, a confirmar en HU-01: BAPI_IOBJ_ACTIVATE_MULTIPLE,
*     BAPI_CUBE_ACTIVATE, activación de aDSO por su API.
*   - Devuelve el log de activación completo en ET_MENSAJES para que el
*     acelerador lo muestre en el paso fallido (RF-08, reintento).
*
  " PENDIENTE HU-01: implementar.
  ev_estado = 'FALLIDO'.
  APPEND VALUE #( type = 'E' id = 'ZACC' number = '000'
                  message = 'Z_ACC_ACTIVAR pendiente de la prueba técnica HU-01' ) TO et_mensajes.
ENDFUNCTION.
