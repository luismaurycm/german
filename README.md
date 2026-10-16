# Acelerador de consolidación financiera SAP BW/BPC

Producto interno de CM Consulting: una herramienta que se conecta al SAP del cliente, revisa lo que ya existe y genera lo que falta para implementar consolidación financiera:
- la bodega en SAP BW;
- el modelo y los scripts en SAP BPC;
- los reportes de Excel con EPM add-in.

Cubre los módulos de consolidación, presupuesto, activos fijos y deterioros, sobre dos arquitecturas:
- **A:** BW 7.5 con BPC modelo estándar.
- **B:** ERP, BW y BPC integrados, con BPC modelo embebido.

## Estado según el Playbook CM

| Fase | Entregable | Estado |
|---|---|---|
| F3 · Análisis funcional | [DAF v1.0](docs/02-analisis-funcional/Analisis_Funcional_Acelerador_Consolidacion_BPC_CM_Consulting.docx): 30 RF, 10 RNF, 12 RN, 6 INT, ASVS nivel 2 | Listo para aprobación |
| F4 · Diseño UX | [Brief de diseño](docs/03-diseno-ux/Brief_Design_Acelerador_Consolidacion_BPC_CM_Consulting.md), [prototipo navegable](docs/03-diseno-ux/prototipo-acelerador-bpc.html) y [Documento UX v1.0](docs/03-diseno-ux/Documento_UX_Acelerador_Consolidacion_BPC_CM_Consulting.docx): 12 pantallas, gate F4 cumplido | Listo para aprobación |
| F5 · Historias de usuario | — | Pendiente |
| F6 · Arquitectura | — | Pendiente |

## Contenido

```
docs/
  00-insumos/transcripciones/   Transcripción automática de la sesión técnica (16-Oct-26)
  01-solucion/                  Borrador de solución con diagrama (página HTML)
  02-analisis-funcional/        Documento de Análisis Funcional (Word)
  03-diseno-ux/                 Brief, prototipo HTML, Documento UX, capturas y diagramas
fuentes/
  transcripcion/                Script de transcripción (faster-whisper)
  daf/                          Generador del DAF y su JSON de requerimientos
  ux/                           Generador del Documento UX, inventario de pantallas y capturas
```

## Prototipo

Abre `docs/03-diseno-ux/prototipo-acelerador-bpc.html` en el navegador.
- Arriba se elige la pantalla, el estado (normal, vacío, carga o error) y la arquitectura (A o B).
- Cada pantalla se puede abrir directo con su código, por ejemplo `#UX-08`.
- Los datos que muestra son de ejemplo.

## Regenerar los documentos

Los generadores producen el JSON de bloques que consume la skill base `documentos-cm-consulting`, la cual aplica la plantilla corporativa:

```bash
python fuentes/daf/build_daf.py fuentes/daf
python fuentes/ux/build_ux.py
```

Después:
1. Valida con `validar_requerimientos.py` (skill de análisis funcional) y con `validar_ux.py` (skill de diseño UX).
2. Genera el Word con `build_cm_doc.py <bloques.json> <salida.docx>`. Necesita Pillow para insertar las imágenes.

## Preguntas abiertas principales

Ninguna es crítica. Las de mayor impacto en arquitectura (F6) son:
- Dónde se aloja el configurador web (DAF, pregunta 4).
- Versiones exactas de cada arquitectura (DAF, pregunta 2).
- Cómo leen BW con EPM los reportes de activos fijos y deterioros (DAF, pregunta 12).
