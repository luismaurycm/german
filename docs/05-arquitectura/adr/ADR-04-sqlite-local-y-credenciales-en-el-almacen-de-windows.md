# ADR-04 — SQLite local y credenciales en el almacén de Windows

**Estado:** aceptado · **Fecha:** 16-Oct-26 · **RNF:** RNF-Seg-02, RNF-Aud-01 · **Costo relativo:** Bajo

## Contexto
Se guardan configuración por cliente y el registro de ejecuciones con retención de 12 meses (RNF-Aud-01); las credenciales no pueden quedar legibles (RNF-Seg-02).

## Decisión
SQLite en el perfil del usuario para configuración y registro. Las credenciales se guardan en el Administrador de credenciales de Windows (DPAPI) y la base solo guarda una referencia. El registro conserva 12 meses y se puede exportar.

## Alternativa más simple descartada
Es la opción simple.

## Razón de descarte
—

## Consecuencias
Cero servicios de base de datos. El cifrado en reposo del equipo lo da BitLocker, obligatorio en los equipos de CM.
