# Acelerador de consolidación financiera SAP BW/BPC

Producto interno de CM Consulting: una herramienta que se conecta al SAP del cliente, revisa lo que ya existe y genera lo que falta para implementar consolidación financiera:
- la bodega en SAP BW;
- el modelo y los scripts en SAP BPC;
- los reportes de Excel con EPM add-in.

Cubre los módulos de consolidación, presupuesto, activos fijos y deterioros, sobre dos arquitecturas:
- **A:** BW 7.5 o BW/4HANA con BPC modelo estándar (BPC 10.1 o 2021).
- **B:** ERP, BW y BPC integrados, con BPC modelo embebido. Fuera de la versión 1: se detecta y se rechaza.

## Estado según el Playbook CM

| Fase | Entregable | Estado |
|---|---|---|
| F3 · Análisis funcional | [DAF v1.2](docs/02-analisis-funcional/Analisis_Funcional_Acelerador_Consolidacion_BPC_CM_Consulting.docx): 39 RF, 12 RNF, 19 RN, 6 INT, ASVS nivel 2. La v1.1 agrega reglas de consolidación y control del cierre (RF-31 a RF-39) y la variante BW/4HANA; la v1.2 deja la arquitectura B fuera de la versión 1 | Listo para aprobación |
| F4 · Diseño UX | [Brief de diseño](docs/03-diseno-ux/Brief_Design_Acelerador_Consolidacion_BPC_CM_Consulting.md), [prototipo navegable](docs/03-diseno-ux/prototipo-acelerador-bpc.html) y [Documento UX v1.2](docs/03-diseno-ux/Documento_UX_Acelerador_Consolidacion_BPC_CM_Consulting.docx): 15 pantallas, gate F4 cumplido; UX-01 y UX-02 rechazan la arquitectura B | Listo para aprobación |
| F5 · Historias de usuario | [Historias de usuario v1.1](docs/04-historias-usuario/Historias_Usuario_Acelerador_Consolidacion_BPC_CM_Consulting.docx): 9 épicas, 58 HU, 327 SP (unos 11 sprints de 30 SP) | Listo para revisión |
| F6 · Arquitectura | [Documento de arquitectura v1.0](docs/05-arquitectura/Arquitectura_Acelerador_Consolidacion_BPC_CM_Consulting.docx) y [ADRs](docs/05-arquitectura/adr): app .NET 8 local, RFC con paquete Z, 11 ADRs; gate F6 cumplido | Aprobado internamente |

## Contenido

```
docs/
  00-insumos/transcripciones/   Transcripción automática de la sesión técnica (16-Oct-26)
  01-solucion/                  Borrador de solución con diagrama (página HTML)
  02-analisis-funcional/        Documento de Análisis Funcional (Word)
  03-diseno-ux/                 Brief, prototipo HTML, Documento UX, capturas y diagramas
  04-historias-usuario/         Documento de historias de usuario (Word)
  05-arquitectura/              Documento de arquitectura, diagramas y ADRs en Markdown
fuentes/
  transcripcion/                Script de transcripción (faster-whisper)
  daf/                          Generador del DAF y su JSON de requerimientos
  ux/                           Generador del Documento UX, inventario de pantallas y capturas
  hu/                           Generador de las historias de usuario y su backlog.json
  arq/                          Generador de arquitectura, arquitectura.json y render de diagramas
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
python fuentes/hu/build_hu.py
python fuentes/arq/build_arq.py
```

Después:
1. Valida con `validar_requerimientos.py` (skill de análisis funcional), `validar_ux.py` (skill de diseño UX) y `validar_backlog.py` (skill de historias de usuario).
2. Genera el Word con `build_cm_doc.py <bloques.json> <salida.docx>`. Necesita Pillow para insertar las imágenes.
3. Las capturas del prototipo se regeneran con `python fuentes/ux/capturar.py docs/03-diseno-ux/prototipo-acelerador-bpc.html` (Edge en Windows, Chrome en macOS).

## Preguntas abiertas principales

Ninguna es crítica. Las de mayor impacto en arquitectura (F6) son:
- Dónde se aloja el configurador web (DAF, pregunta 4).
- Versiones exactas de cada arquitectura (DAF, pregunta 2).
- Cómo leen BW con EPM los reportes de activos fijos y deterioros (DAF, pregunta 12).
- Si asientos de ajuste, bloqueo por sociedad y flujo de cierre funcionan con BPC embebido (DAF, pregunta 17).
- Versiones y ambiente de prueba para la variante BW/4HANA con BPC 2021 (DAF, pregunta 18).
