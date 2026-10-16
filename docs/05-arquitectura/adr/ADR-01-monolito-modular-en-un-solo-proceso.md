# ADR-01 — Monolito modular en un solo proceso

**Estado:** aceptado · **Fecha:** 16-Oct-26 · **RNF:** RNF-Comp-03 · **Costo relativo:** Bajo

## Contexto
El acelerador lo usa un Consultor implementador a la vez, sobre un cliente a la vez; el equipo de construcción es de 4 a 5 personas.

## Decisión
Una sola aplicación .NET 8 con módulos separados por interfaz: configurador, orquestador, escáner, generadores BW, generadores BPC, cargador de datos maestros, reportes y registro.

## Alternativa más simple descartada
Es la opción simple.

## Razón de descarte
—

## Consecuencias
Despliegue de un solo ejecutable. Los generadores se prueban de forma aislada contra la interfaz ISapGateway, sin SAP real (Regla 6).
