# ADR-03 — .NET 8 con SAP .NET Connector 3.1

**Estado:** aceptado · **Fecha:** 16-Oct-26 · **RNF:** RNF-Seg-01, RNF-Comp-01 · **Costo relativo:** Bajo

## Contexto
El acelerador necesita RFC hacia BW y BPC (INT-02, INT-03) y generar archivos Excel.

## Decisión
.NET 8 para todo el producto, con SAP NCo 3.1 para RFC y Open XML SDK para los Excel. Un solo runtime más ABAP en el cliente.

## Alternativa más simple descartada
Python con PyRFC.

## Razón de descarte
SAP descontinuó PyRFC y no publica parches de seguridad; una dependencia sin soporte incumple RNF-Seg-01 (ASVS nivel 2, componentes con soporte vigente).

## Consecuencias
Requiere aceptar la licencia de SAP NCo y distribuirlo con el instalador. La compatibilidad de NCo 3.1 con .NET 8 se verifica en HU-01.
