# Certificación de la variante BW/4HANA con BPC 2021 · Acelerador de consolidación

HU-58 (RNF-Comp-05, RNF-Comp-04). Variante `A-BW4H-BPC2021-STD`: BW/4HANA y BPC 2021 modelo estándar, con aDSO y ODP.

## Contra el simulador (en cada build)

`CertificacionBw4HanaTests.RecorridoCompleto` ejecuta con el perfil `ABw4Hana` y los cuatro módulos todos los RF de la
arquitectura A, y comprueba lo siguiente:
- La ejecución termina en todos sus pasos: bodega con aDSO y ODP, datos maestros, activos fijos, deterioros, modelo BPC,
  propiedad, scripts, cuadre, importación, presupuesto, conversión, consolidación e inversión.
- No se crea ningún InfoCubo (CA2).
- Los reportes de UX-11 se calculan.
- El .zip trae los 34 reportes y plantillas.
- C04 queda completo para todas las sociedades.

`CertificacionBw4HanaTests.ClientesExistentesSinCambios`, `ArquitecturaTests` (RNF-Comp-04) y `GeneracionSprint4Tests`
(HU-16 CA1) comprueban que agregar la variante no cambia ni la variante ni el plan de los clientes BW 7.5 (CA3).

| RF | Paso o pantalla | Resultado en el simulador |
|---|---|---|
| RF-01 a RF-04 | Conexiones, validación ERP–BW, detección de la variante | Cumple |
| RF-05 a RF-07, RF-39 | Módulos, versiones, sociedades, mapeo | Cumple |
| RF-08 a RF-10 | Bodega financiera en aDSO, campos Z, cadenas full y delta | Cumple, sin InfoCubos |
| RF-11 a RF-15 | Datos maestros y sociedades no SAP | Cumple |
| RF-16 a RF-19, RF-26, RF-31 a RF-33, RF-36 | Modelo BPC, propiedad, scripts, cuadre, importación, conversión, eliminaciones | Cumple |
| RF-23, RF-28 | Modelo de planeación y captura P05 | Cumple |
| RF-24, RF-25, RF-29, RF-30 | Activos fijos y deterioros en BW y sus reportes | Cumple |
| RF-20 a RF-22, RF-27, RF-34 a RF-38 | Reportes, controles C01 a C04, asientos y bloqueo | Cumple |

## Pendiente en el ambiente real (dependencia de HU-01)

La certificación final se ejecuta en un ambiente BW/4HANA con BPC 2021 modelo estándar (DAF, pregunta 18), con el paquete
Z transportado y el usuario técnico mínimo.
1. Ejecutar el plan completo con los cuatro módulos y guardar el registro de ejecuciones (HU-54) como evidencia.
2. Confirmar en RSA1 que no se creó ningún InfoCubo en el espacio `ZACC_*`.
3. Abrir y actualizar los 34 Excel con el EPM add-in (CP-266).
4. Repetir la consolidación de un mes y comparar R01 con el balance consolidado de BPC.
