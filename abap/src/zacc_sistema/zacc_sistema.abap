*&---------------------------------------------------------------------*
*& Grupo de funciones ZACC_SISTEMA · Paquete ZACC
*& ESQUELETO DEL SPRINT 1 · PENDIENTE DE LA PRUEBA TÉCNICA HU-01 (R-02)
*& Lectura del sistema para HU-05 (RN-01) y HU-06 (RF-04). Solo lectura.
*&---------------------------------------------------------------------*

FUNCTION z_acc_sistema_info.
*"----------------------------------------------------------------------
*"*"Interfaz local:
*"  EXPORTING
*"     VALUE(ES_SISTEMA) TYPE  ZACC_S_SISTEMA
*"     VALUE(ET_COMPONENTES) TYPE  ZACC_T_COMPONENTE
*"     VALUE(ET_MENSAJES) TYPE  BAPIRET2_T
*"----------------------------------------------------------------------
* Identifica el sistema que responde: SID, mandante, release de SAP_BASIS
* y los componentes de software instalados. El acelerador interpreta los
* componentes con InterpretadorComponentes (src/Acelerador.Sap/Nco):
*   SAP_BW 750  → BW 7.5        DW4CORE 200 → BW/4HANA 2.0
*   CPMBPC 810  → BPC 10.1 (por confirmar)   BPC 2021 → por confirmar
* Con el SID de ERP y de BW el acelerador decide la arquitectura: el mismo
* SID es arquitectura B (RN-19).
*
* Diseño:
*   - AUTHORITY-CHECK Z_ACC_PASO = LEER.
*   - ES_SISTEMA-SID = sy-sysid, ES_SISTEMA-MANDANTE = sy-mandt.
*   - ET_COMPONENTES: SELECT component, release, extrelease FROM cvers.
*     Se lee dentro del módulo (no con RFC_READ_TABLE) para no pedir al
*     cliente S_TABU_DIS amplio: basta S_TABU_NAM sobre CVERS.
*
  " PENDIENTE HU-01: implementar.
  APPEND VALUE #( type = 'E' id = 'ZACC' number = '000'
                  message = 'Z_ACC_SISTEMA_INFO pendiente de la prueba técnica HU-01' ) TO et_mensajes.
ENDFUNCTION.


FUNCTION z_acc_bw_validar_fuente.
*"----------------------------------------------------------------------
*"*"Interfaz local:
*"  IMPORTING
*"     VALUE(IV_LOGSYS) TYPE  RSSLOGSYS
*"  EXPORTING
*"     VALUE(EV_ACTIVA) TYPE  ABAP_BOOL
*"     VALUE(ET_MENSAJES) TYPE  BAPIRET2_T
*"----------------------------------------------------------------------
* HU-05 · RN-01. Se llama en BW. Confirma que el sistema fuente del ERP
* (sistema lógico, p. ej. ECPCLNT300) existe, está activo y que su destino
* RFC responde, que es lo que hace «Verificar» en RSA1 → Sistemas fuente.
*
* Diseño:
*   - AUTHORITY-CHECK Z_ACC_PASO = LEER y S_RS_ADMWB (03) sobre SOURCESYS.
*   - Lee RSBASIDOC / RSLOGSYSDEST para el destino del sistema lógico.
*   - Prueba el destino con RFC_PING (CALL FUNCTION ... DESTINATION).
*   - Comprueba que el extractor 0FI_GL_14 (ECC) o los de ACDOCA (S/4)
*     estén replicados (ROOSOURCE en el ERP vía el destino, por confirmar).
*   - EV_ACTIVA = abap_true solo si todo lo anterior es correcto; si no,
*     ET_MENSAJES lleva el texto del sistema, que el acelerador muestra.
*
  " PENDIENTE HU-01: implementar.
  APPEND VALUE #( type = 'E' id = 'ZACC' number = '000'
                  message = 'Z_ACC_BW_VALIDAR_FUENTE pendiente de la prueba técnica HU-01' ) TO et_mensajes.
ENDFUNCTION.
