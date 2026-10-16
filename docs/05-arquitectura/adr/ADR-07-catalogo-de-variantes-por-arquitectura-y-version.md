# ADR-07 — Catálogo de variantes por arquitectura y versión

**Estado:** aceptado · **Fecha:** 16-Oct-26 · **RNF:** RNF-Comp-01, RNF-Comp-04, RNF-Comp-05 · **Costo relativo:** Bajo

## Contexto
La versión 1 soporta la arquitectura A con BW 7.5 o BW/4HANA y BPC modelo estándar; la B queda fuera (RN-19). Agregar una versión no debe tocar las existentes (RNF-Comp-04).

## Decisión
Cada generador define una interfaz y una implementación por variante (BW 7.5 con InfoCubo, BW/4HANA con aDSO, BPC 10.1 y BPC 2021 modelo estándar). La variante se elige según la detección de RF-04. Las plantillas de cada variante viven en el catálogo versionado. Una variante de BPC embebido se agrega después sin tocar estas (D-04).

## Alternativa más simple descartada
Un solo generador con condicionales por versión.

## Razón de descarte
Cada versión nueva obligaría a modificar el código de las existentes, contra RNF-Comp-04 (0 cambios en variantes existentes).

## Consecuencias
Más archivos por variante, pero cada una se prueba sola. Agregar BW/4HANA (RNF-Comp-05) es una variante más.
