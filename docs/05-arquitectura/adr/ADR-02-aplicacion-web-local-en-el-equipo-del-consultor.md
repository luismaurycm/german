# ADR-02 — Aplicación web local en el equipo del consultor

**Estado:** aceptado · **Fecha:** 16-Oct-26 · **RNF:** RNF-Comp-03 · **Costo relativo:** Bajo

## Contexto
La pregunta 4 del DAF pide definir si el configurador corre en el equipo del consultor o en un servidor de CM. RNF-Comp-03 exige cero servidores adicionales para el prototipo.

## Decisión
El configurador corre en el equipo del Consultor implementador como ejecutable autocontenido que abre el navegador en http://127.0.0.1. No hay servidor de CM. Cierra la pregunta 4 del DAF para la versión 1.

## Alternativa más simple descartada
Es la opción simple.

## Razón de descarte
—

## Consecuencias
Sin costo de hosting ni identidad central. La configuración de cada cliente vive en el equipo del consultor: para compartirla se exporta e importa (decisión diferida D-01).
