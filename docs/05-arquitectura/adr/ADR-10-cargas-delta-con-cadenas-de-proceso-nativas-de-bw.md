# ADR-10 — Cargas delta con cadenas de proceso nativas de BW

**Estado:** aceptado · **Fecha:** 16-Oct-26 · **RNF:** RNF-Rend-01 · **Costo relativo:** Bajo

## Contexto
Los datos del día anterior deben estar en los reportes (RNF-Rend-01: una ejecución cada 24 horas).

## Decisión
Cadenas de proceso de BW programadas cada noche, sin borrado de peticiones solapadas; la importación a BPC encadenada al final de la delta.

## Alternativa más simple descartada
Es la opción simple.

## Razón de descarte
—

## Consecuencias
La duración de la ventana se confirma con el volumen real (pregunta 7 del DAF).
