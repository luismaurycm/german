# Accesibilidad WCAG 2.1 AA · Acelerador de consolidación

HU-56 (RNF-Usa-01). Revisión de las pantallas del configurador al cierre del Sprint 11. **Resultado: 0 incumplimientos A
y AA en la revisión automática.**

## Revisión automática (en cada build)

`tests/Acelerador.Tests/Soporte/RevisionAccesibilidad.cs` revisa el HTML que entrega el acelerador. La prueba
`AccesibilidadTests.SinIncumplimientos` recorre:
- UX-01 a UX-10, con un cliente consolidado y los cuatro módulos (UX-07 en `AccesibilidadTests.NoSap`).
- Las vistas de UX-11 a UX-15: R01 con detalle, R04, R10, R20, C01, C03, P01, A01, D02, C02, C04, Asientos y P05.
- Los estados vacío y de error.

| Criterio | Qué se revisa |
|---|---|
| 1.1.1 (A) | Íconos SVG ocultos (`aria-hidden`) o con texto alternativo; imágenes con `alt` |
| 1.3.1 (A) | Un `h1` por página; tablas con `caption`; `th` con `scope` |
| 1.4.3 (AA) | Contraste de 4,5:1 o más en texto, enlaces, botones y estados (`AccesibilidadTests.Contraste`) |
| 2.4.1 (A) | Enlace «Saltar al contenido» hacia `#contenido` |
| 2.4.2 (A) | Título de página |
| 2.4.3 (A) | Sin `tabindex` positivo: el orden del foco es el del documento |
| 2.4.4 (A) | Enlaces con texto o `aria-label` |
| 2.4.7 (AA) | Foco visible: contorno de 3 px y halo amarillo (`AccesibilidadTests.FocoVisible`) |
| 3.1.1 (A) | `lang="es"` |
| 3.3.2 (A) y 4.1.2 (A) | Todo control con etiqueta o `aria-label`; botones con nombre; `aria-invalid` válido |
| 4.1.1 (A) | Ids únicos |

`AccesibilidadTests.LaRevisionDetecta` comprueba que la revisión encuentra cada incumplimiento que cubre.

## Teclado y estados (CA2 y CA3)

- Todos los controles son nativos (enlaces, botones, campos, listas): se operan con Tab, Enter y Espacio. La zona de
  arrastre de UX-07 tiene su campo de archivo, que se elige con el teclado.
- Las pestañas de UX-03 siguen el patrón WAI-ARIA (flechas, Inicio y Fin).
- Cada estado combina texto e ícono: Completo, Pendiente, Con error, «Supera el umbral», «No importada», «Descuadrado».
  El color nunca es la única señal.
- Los mensajes usan `role="status"` o `role="alert"`; los totales de UX-12 y UX-14 se anuncian con `aria-live`.

## Pendiente con usuarios

La revisión con un lector de pantalla (NVDA) y con zoom al 200 % se hace en la prueba de aceptación, con el equipo del
consultor (plan de pruebas F7).
