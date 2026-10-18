*&---------------------------------------------------------------------*
*& Grupo de funciones ZACC_COMUN · Paquete ZACC (Acelerador de consolidación)
*& ESQUELETO DEL SPRINT 1 · PENDIENTE DE LA PRUEBA TÉCNICA HU-01 (R-02)
*&---------------------------------------------------------------------*
*& Reglas comunes a todos los módulos de función Z_ACC_* (ADR-06):
*&
*& 1. Todos son RFC habilitados, con parámetros por valor y tipos del
*&    diccionario (ver abap/ddic.md). Los mensajes vuelven en ET_MENSAJES
*&    (BAPIRET2_T): nunca se lanza una excepción no clasificada al llamador.
*& 2. Cada módulo hace AUTHORITY-CHECK del objeto Z_ACC_PASO con el paso
*&    que ejecuta (LEER, CREAR, ACTIVAR, RESPALDAR) antes de tocar nada.
*&    El rol ZACC_TECNICO da solo los pasos que el cliente aprueba
*&    (abap/rol-ZACC_TECNICO.md). Nunca SAP_ALL (RNF-Seg-03).
*& 3. Solo se crean objetos en el espacio de nombres configurado para el
*&    cliente (tabla ZACC_CONFIG, campo PREFIJO; por defecto ZACC). Un
*&    nombre fuera del espacio se rechaza con mensaje E (hardening).
*& 4. Las escrituras son idempotentes: si el objeto ya existe con la misma
*&    definición se devuelve EV_ESTADO = 'EXISTE', no un error (reintento
*&    por paso, RF-08).
*& 5. Toda escritura admite IV_SIMULAR = abap_true: valida sin grabar.
*& 6. Cada llamada deja una entrada en el registro de aplicación (SLG1,
*&    objeto ZACC, subobjeto = paso) con usuario, objeto y resultado.
*&    El acelerador guarda además su propio registro (RNF-Aud-01).
*& 7. Las definiciones viajan como JSON (STRING) y se leen con /UI2/CL_JSON.
*&---------------------------------------------------------------------*

FUNCTION z_acc_job_estado.
*"----------------------------------------------------------------------
*"*"Interfaz local:
*"  IMPORTING
*"     VALUE(IV_JOBNAME) TYPE  BTCJOB
*"     VALUE(IV_JOBCOUNT) TYPE  BTCJOBCNT
*"  EXPORTING
*"     VALUE(EV_ESTADO) TYPE  ZACC_DE_ESTADO
*"     VALUE(ET_MENSAJES) TYPE  BAPIRET2_T
*"----------------------------------------------------------------------
* Estado de un job en fondo lanzado por Z_ACC_BPC_UJBR (u otros pasos
* largos). El acelerador consulta cada pocos segundos hasta que termina.
*
* Diseño:
*   - AUTHORITY-CHECK Z_ACC_PASO = LEER.
*   - Solo responde por jobs cuyo nombre empieza con ZACC_ (no expone
*     otros jobs del sistema).
*   - Lee el estado con BP_JOB_STATUS_GET y el log del job con
*     BP_JOBLOG_READ; traduce: P/S/R → EN_CURSO, F → TERMINADO,
*     A → FALLIDO (con el log en ET_MENSAJES).
*
  " PENDIENTE HU-01: implementar.
  APPEND VALUE #( type = 'E' id = 'ZACC' number = '000'
                  message = 'Z_ACC_JOB_ESTADO pendiente de la prueba técnica HU-01' ) TO et_mensajes.
ENDFUNCTION.
