# ADR-08 — Reportes EPM generados desde plantillas Open XML

**Estado:** aceptado · **Fecha:** 16-Oct-26 · **RNF:** RNF-Comp-02 · **Costo relativo:** Bajo

## Contexto
Se entregan 34 reportes y plantillas que el cliente conserva (RF-20, RNF-Comp-02).

## Decisión
Plantillas .xlsx con fórmulas EPM y conexión con nombre; el generador reemplaza parámetros (modelo, sociedades, rubros) con Open XML SDK. Los reportes de activos fijos y deterioros usan una conexión local de EPM add-in a consultas BW (cierra la pregunta 12 del DAF, sujeto a HU-01).

## Alternativa más simple descartada
Es la opción simple.

## Razón de descarte
—

## Consecuencias
No requiere Excel en el equipo de generación. Si la conexión local a BW no funciona, se abre control de cambios sobre RN-12 (riesgo R-03).
