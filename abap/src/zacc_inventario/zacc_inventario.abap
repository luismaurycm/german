*&---------------------------------------------------------------------*
*& Grupo de funciones ZACC_INVENTARIO · Paquete ZACC
*& ESQUELETO DEL SPRINT 1 · PENDIENTE DE LA PRUEBA TÉCNICA HU-01 (R-02)
*& Inventario de lo existente (RF-03, HU-07). Solo lectura.
*&---------------------------------------------------------------------*

FUNCTION z_acc_inventario.
*"----------------------------------------------------------------------
*"*"Interfaz local:
*"  IMPORTING
*"     VALUE(IV_TIPO) TYPE  ZACC_DE_TIPO_INVENTARIO
*"     VALUE(IV_FILTRO) TYPE  STRING OPTIONAL
*"     VALUE(IV_MAXIMO) TYPE  I DEFAULT 5000
*"  EXPORTING
*"     VALUE(EV_JSON) TYPE  STRING
*"     VALUE(EV_TRUNCADO) TYPE  ABAP_BOOL
*"     VALUE(ET_MENSAJES) TYPE  BAPIRET2_T
*"----------------------------------------------------------------------
* Devuelve en JSON la lista de objetos de un tipo. IV_TIPO:
*   EXTRACTORES    fuentes de datos activas (ROOSOURCE en ERP / RSDS en BW)
*   INFOPROVIDERS  InfoCubos (RSDCUBE, BW 7.5) o aDSO (RSOADSO, BW/4HANA)
*   MODELOS_BPC    ambientes y modelos de BPC (tablas UJA_*, por confirmar)
*   CAMPOS_Z       campos Z/ZZ en BSEG/ACDOCA (DD03L) para RF-09
*   BODEGA         indicador de bodega existente (RN-03)
*
* Diseño:
*   - AUTHORITY-CHECK Z_ACC_PASO = LEER y la autorización de visualización
*     propia de cada área (S_RS_ADMWB 03, S_TABU_NAM sobre las tablas).
*   - Solo metadatos: nunca lee datos financieros ni de terceros
*     (Ley 1581/2012, arquitectura sección 9).
*   - IV_MAXIMO limita la respuesta; si hay más, EV_TRUNCADO = abap_true.
*   - El formato JSON es el contrato con el escáner (HU-07): cada elemento
*     lleva nombre técnico, descripción, estado y tipo.
*
  " PENDIENTE HU-01: implementar.
  APPEND VALUE #( type = 'E' id = 'ZACC' number = '000'
                  message = 'Z_ACC_INVENTARIO pendiente de la prueba técnica HU-01' ) TO et_mensajes.
ENDFUNCTION.
