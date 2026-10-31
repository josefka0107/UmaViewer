# Exportación de modelos (PMX)

Movido desde `CLAUDE.md` (2026-10-31), sin cambios en el texto.

- **Exporting models** — hecho en la rama `fix/duplicate-files-pr224` (commits `3336775`, `bac4bf4`), verificado en Blender 5.2 + MMD Tools con Vodka (1008), el escenario `pfb_env_cutin1004_00_00` y el mob 8626. Cubre issues upstream #125, #186, #230:
  - Una malla legible por renderer (`GetExportMesh`), compartida por vértices/materiales/morphs; `MakeReadableMeshCopy` copia todos los streams y submeshes.
  - Se ignoran renderers sin malla (partículas, líneas) y submeshes sin material (`GetExportSubMeshCount`: los mobs esconden un cuerpo entero ahí).
  - `PMXWriter` calcula el tamaño de índices según los conteos (escenas >65535 vértices / >127 texturas).
  - PMX se escribe a `.tmp` y se mueve al final (`WritePMX`), con errores mostrados en la UI.
  - Colores de mobs: `TextureExporter.BakeMaskColor` hornea `USE_MASK_COLOR` en `_MainTex` (archivo `mask<hash>_<tex>.png`). Fórmula **aproximada** (no hay fuente del shader): canal de `_MaskColorTex` neutro en 0.5, hacia 1 tiñe con color `X2`, hacia 0 con `X1`, multiplicado sobre el diff.
  - Comprobación sin Blender: scripts node en el scratchpad (`pmxcheck.js` valida estructura, `pmxmats.js` lista material→textura).
- **Pendiente en export:**
  - Número del dorsal (`_ZekkenNumberTex`, glifo 64×64 en alfa, número aleatorio 1–17 en `UmaContainerCharacter.cs:1411`): la posición la calcula el shader; la malla no tiene UV para ello. Única vía fiable: medirlo en runtime (render con/sin número + render de UVs). Aparcado por coste.
  - `UnassignedReferenceException: m_AtlasTextures of TMP_FontAsset` al mostrar mensajes con texto japonés (`UmaViewerUI.ShowMessage`): fuente de fallback TMP sin atlas. No afecta al export; sin investigar si es previo a nuestros cambios.
