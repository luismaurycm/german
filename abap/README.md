# Paquete Z del acelerador (ZACC)

**Estado: esqueleto del Sprint 1, pendiente de la prueba técnica HU-01 (riesgo R-02).**
Contiene las firmas y el diseño de los módulos de función RFC; los cuerpos devuelven
«pendiente de la prueba técnica HU-01». No se ha importado en ningún sistema SAP.

Lo usa el acelerador por RFC con SAP NCo 3.1 (ADR-03, ADR-06). El Administrador SAP del
cliente lo importa una vez por transporte; el acelerador nunca escribe código ABAP en el cliente.

## Contenido

| Grupo de funciones | Módulo de función | Para qué | HU | Escribe en el cliente |
|---|---|---|---|---|
| ZACC_SISTEMA | `Z_ACC_SISTEMA_INFO` | SID, mandante y componentes (CVERS) | HU-06 | No |
| ZACC_SISTEMA | `Z_ACC_BW_VALIDAR_FUENTE` | Validación de la conexión ERP–BW (RN-01) | HU-05 | No |
| ZACC_INVENTARIO | `Z_ACC_INVENTARIO` | Inventario de extractores, InfoProviders, modelos BPC y campos Z | HU-07 | No |
| ZACC_BW | `Z_ACC_CREAR_OBJETO` | Crear un objeto BW desde una definición JSON | HU-15 en adelante | Sí |
| ZACC_BW | `Z_ACC_ACTIVAR` | Activar un objeto BW | HU-15 en adelante | Sí |
| ZACC_BPC | `Z_ACC_BPC_INFO` | Versión de BPC y tipo de modelo de cada ambiente | HU-06 | No |
| ZACC_BPC | `Z_ACC_BPC_CREAR_MIEMBRO` | Crear miembros de dimensión en BPC estándar | HU-01, HU-26 en adelante | Sí |
| ZACC_BPC | `Z_ACC_BPC_UJBR` | Respaldo y restauración UJBR del ambiente | HU-01, ADR-06 | Sí (archivo) |
| ZACC_COMUN | `Z_ACC_JOB_ESTADO` | Estado de un job en fondo del acelerador | HU-01 | No |

Las reglas comunes (autorización por paso, espacio de nombres, idempotencia, simulación,
registro SLG1) están al inicio de [`src/zacc_comun/zacc_comun.abap`](src/zacc_comun/zacc_comun.abap).

- Tipos del diccionario: [`ddic.md`](ddic.md).
- Rol técnico con permisos mínimos por paso, sin SAP_ALL (HU-04 CA3, RNF-Seg-03): [`rol-ZACC_TECNICO.md`](rol-ZACC_TECNICO.md).
- Guion de la prueba técnica: [`../docs/05-arquitectura/spike-HU-01.md`](../docs/05-arquitectura/spike-HU-01.md).

## Formato

Los archivos `.abap` son el código fuente como se ve en SE37, uno por grupo de funciones.
Cuando la prueba técnica confirme el mecanismo, el paquete se crea en el sandbox, se serializa
con abapGit en esta carpeta y se reemplaza este esqueleto.

## Lo que la prueba HU-01 debe confirmar

Todo lo marcado «por confirmar» en los comentarios: las APIs de creación de aDSO y de miembros
de BPC, el programa de UJBR para ejecutar en fondo, los componentes de software de BPC 2021 y
los objetos de autorización exactos del rol (con STAUTHTRACE).
