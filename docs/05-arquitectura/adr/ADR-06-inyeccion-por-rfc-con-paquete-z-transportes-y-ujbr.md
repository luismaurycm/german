# ADR-06 — Inyección por RFC con paquete Z, transportes y UJBR

**Estado:** aceptado · **Fecha:** 16-Oct-26 · **RNF:** RNF-Seg-03, RNF-Aud-01, RNF-Comp-01 · **Costo relativo:** Medio

## Contexto
El DAF fija RFC con paquete Z como mecanismo (INT-02, INT-03). Se debe inventariar en línea, crear objetos paso a paso y poder reintentar.

## Decisión
El Administrador SAP importa una vez por transporte el paquete Z con módulos de función RFC. El acelerador los llama para inventariar, crear y activar objetos. Antes de cambiar BPC se toma un respaldo UJBR.

## Alternativa más simple descartada
Generar archivos y transportes para que el Administrador SAP los importe a mano.

## Razón de descarte
Sin lectura en línea no hay inventario (RF-03) ni reintento por paso con registro (RF-08, RNF-Aud-01).

## Consecuencias
Depende de que el cliente permita código Z (supuesto del DAF) y de la prueba técnica HU-01 (riesgo R-02).
