# ADR-11 — Distribución firmada desde Azure DevOps

**Estado:** aceptado · **Fecha:** 16-Oct-26 · **RNF:** RNF-Seg-01 · **Costo relativo:** Bajo

## Contexto
El ejecutable se instala en equipos de consultores y escribe en sistemas de clientes; debe pasar SAST, SCA y escaneo de secretos (RNF-Seg-01).

## Decisión
Pipeline de Azure DevOps con build, pruebas unitarias, SAST, SCA y escaneo de secretos en cada PR. El .exe autocontenido se firma con certificado de firma de código y se publica en Azure Artifacts junto al transporte del paquete Z.

## Alternativa más simple descartada
Es la opción simple.

## Razón de descarte
—

## Consecuencias
Costo del certificado de firma de código (sección 15).
