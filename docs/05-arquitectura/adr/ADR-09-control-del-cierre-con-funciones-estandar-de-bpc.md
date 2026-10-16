# ADR-09 — Control del cierre con funciones estándar de BPC

**Estado:** aceptado · **Fecha:** 16-Oct-26 · **RNF:** RNF-Aud-02, RNF-Comp-01 · **Costo relativo:** Bajo

## Contexto
RF-31 a RF-38 piden propiedad, eliminación de inversión, asientos, validación de cuadre, bloqueo y flujo de cierre, con registro de cambios (RNF-Aud-02).

## Decisión
Se configuran las funciones estándar de BPC: modelo de propiedad, reglas de negocio de eliminación y conversión, journals, work status por sociedad, controles y BPF. Solo la validación de cuadre previa a la importación usa script logic o un BAdI del paquete Z. La auditoría usa la auditoría estándar de BPC.

## Alternativa más simple descartada
Es la opción simple.

## Razón de descarte
—

## Consecuencias
Todas son funciones de BPC modelo estándar, disponibles en BPC 10.1 y 2021. Su disponibilidad en BPC embebido se evalúa con la arquitectura B (D-04).
