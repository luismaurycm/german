# ADR-05 — Identidad: sesión de Windows y usuario técnico mínimo

**Estado:** aceptado · **Fecha:** 16-Oct-26 · **RNF:** RNF-Seg-01, RNF-Seg-03 · **Costo relativo:** Bajo

## Contexto
Hay un único usuario local por instalación. En SAP, el acelerador actúa con el usuario técnico que crea el Administrador SAP del cliente.

## Decisión
No hay pantalla de inicio de sesión: la aplicación escucha solo en 127.0.0.1 y exige un token aleatorio por sesión, generado al iniciar, en una cookie HttpOnly. En SAP se usa un rol Z por paso, sin SAP_ALL, y SNC si el cliente lo exige.

## Alternativa más simple descartada
Es la opción simple.

## Razón de descarte
—

## Consecuencias
Si el configurador pasa a un servidor (D-01), se agrega Microsoft Entra ID. El rol técnico se documenta en el paquete Z.
