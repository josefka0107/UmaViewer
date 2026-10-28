# UmaViewer (fork)

Visor en Unity de los assets de Uma Musume: Pretty Derby. Fork de `josefka0107/UmaViewer` (origen: `katboi01/UmaViewer`).

## Proyecto

- Unity **2022.3.62f3**. Escena principal: `Assets/Scenes/Version2.unity`; lives en `Assets/Scenes/LiveScene.unity`.
- Scripts principales en `Assets/Scripts/`:
  - `UmaViewerBuilder.cs` — carga personajes, props, lives, audio (`LoadUma`, `LoadProp`, `LoadLive`, `LoadAssetPath`).
  - `UmaViewerUI.cs` — paneles (`LoadPropPanel`, `LoadMapPanel`, `LoadLivePanels`).
  - `UmaAssetManager.cs` — carga de AssetBundles (cifrados vía `UmaAssetBundleStream` + `LoadFromStream`).
  - `UmaDatabase/UmaDatabaseController.cs` — consultas a `master.mdb` y `meta`.
  - `Exporters/ModelExporter.cs` — exportación PMX (LibMMD); `Settings/UISettingsModel.cs:ExportModel` es el punto de entrada desde la UI.
  - `umamusume/Gallop/` — reimplementaciones de clases del juego (Live, Cutt, CySpring, Face...).
- Unity 2022.3.62f3 instalado en `C:\Program Files\Unity\Hub\Editor\2022.3.62f3\` (también hay un 6000.6.7f1: **no** abrir el proyecto con él, lo migraría).
- Compilar en batch (~1–5 min; 0 errores = OK):
  `& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode -nographics -quit -projectPath "D:\dev\UmaViewer-Fork" -logFile <scratchpad>\unity.log`
  y buscar `error CS` / `conflicts with` en el log. Las pruebas visuales las hace el usuario en el Editor (escena `Version2`).
- Si el Editor está abierto con el proyecto, el batch no puede correr: Unity recompila al volver a su ventana. Leer el resultado en `C:\Users\jose.rangel\AppData\Local\Unity\Editor\Editor.log` (`grep "error CS"` tras la última compilación).
- Cuidado: en archivos con `using Gallop;` existe `Gallop.Math`; usar `Mathf` o `System.Math` explícito.
- Unity reescribe solo `ProjectSettings/ShaderGraphSettings.asset`, `ProjectSettings/ProjectSettings.asset` y `Assets/RT/RT_Screen.renderTexture` al usar el Editor; no incluirlos en commits salvo cambio intencionado.
- `Assets/Plugins/CySpringPlugin.dll` (3,8 MB, sin firmar, añadido en PR #224 upstream) lo detecta Defender como `Trojan:Win32/Kepavll!rfn`. El usuario decidió excluir solo ese archivo en Defender (2026-10-28). No es el DLL del juego (`umamusume_Data/Plugins/x86_64/CySpringPlugin.dll`, 676 KB), aunque ambos exportan `NativeClothUpdate`, `NativeClothSkirtUpdate`, `NativeSkirtUpdate`.
- Upstream (katboi01, commit 07f82e9 / PR #224) **no compila**: copió ~25 archivos a carpetas nuevas sin borrar los viejos. Arreglado en la rama `fix/duplicate-files-pr224` (borrar copias viejas con mismo GUID, quitar `Assets/Plugins/StandaloneFileBrowser` y su referencia en `umamusume.asmdef`, calificar `Gallop.CySpringParamData*Element` en `UmaContainerCharacter.cs`).
- Hay dos juegos de clases CySpring: `Gallop.*` (`Gallop/Cyspring/`, el que usan los personajes) y global (`Gallop/StageScript/CySpring/`, `Gallop/DereScript/StageScript/CySpring/`). Dentro de clases sin namespace el nombre sin calificar resuelve al global: calificar con `Gallop.`.

## Datos del juego

- Instalación local: `D:\Users\jose.rangel\Umamusume\umamusume_Data\Persistent\` (`dat/`, `meta`, `master/master.mdb`).
- `master.mdb`: SQLite **sin cifrar**, ~44 MB, 622 tablas. `meta` sí está cifrado (sqlite3mc).
- Tablas que usa UmaViewer: `text_data` (categorías 6=personajes, 14=trajes, 16=canciones, 59=mobs), `chara_data`, `mob_data`, `dress_data`, `live_data`, `jukebox_music_data`, `face_type_data`, `chara_dress_color_set`, `mob_dress_color_set`, `mob_hair_color_set`.
- No modificar los archivos originales del juego; trabajar sobre copias.

## Herramientas

- REA (`rea-agents` 6.8.0, CLI global bajo Node 26.13.0 vía fnm). Ej.: `rea inspect-sqlite-database <copia.mdb> --table chara_data --schema-scope selected_table --row-limit 5 --format json`.
- En Windows con fnm, usar `npm.cmd` / `rea.cmd` dentro de `fnm exec`.

## Idea aparcada: versión web

Evaluado y pospuesto. Conclusiones:
- La BD es lo fácil: `master.mdb` funciona con `sql.js`; `meta` necesitaría un build WASM de SQLite3MultipleCiphers.
- Bloqueos reales: acceso a `dat/` (muchos GB, local) → usar File System Access API para que cada usuario elija su carpeta `Persistent` (no alojar assets en servidor: sería redistribuir material de Cygames); plugins nativos (`sqlite3*.dll`, LAME, StandaloneFileBrowser, CySpringPlugin) no corren en WebGL; bundles cifrados requieren `LoadFromMemory`; límite de memoria WebGL (~4 GB); uso de `Thread`/`Task.Run` en 6 archivos (incl. `CySpringController.cs`).
- Primer paso propuesto si se retoma: prueba de concepto web que lea `master.mdb` con `sql.js` y liste personajes/trajes/canciones.

## Features pendientes (README: "/" incompleto)

- **Viewing Props, Scenery, Live scenes** — props (`pfb_chr_prop*`) y escenarios (`3d/env/.../pfb_*`, `cutt/cutt_son*`) se cargan instanciando el prefab tal cual (`UmaContainerProp.LoadProp`), sin ajuste de shaders/iluminación; `LoadAssetPath` reporta scripts faltantes del juego.
  - Hecho (commits `13ce473`, `34c35cb`): props aparecen junto a la uma con gizmo magenta (`UIHandleProp`, en Pose Mode): Reset, Attach a mano derecha/izquierda (`Hand_Attach_R/L`, los props ya están modelados para ese hueso, sin offset), Detach, Link/Unlink a la animación actual. Vínculos en `PropAnimationLinks.json` (raíz del proyecto en el Editor, ignorado por git); se aplican en `UmaContainerCharacter.LoadAnimation`. Props pegados a la mano se excluyen del export de la uma.
  - `master.mdb` no tiene relación animación↔prop (`character_prop_animation` solo describe animaciones propias de 6 props); probablemente está en los timelines de historia.
  - Pendiente: nombres legibles de props (la lista muestra nombres de archivo); issues upstream de lives #226 (solo una uma en lives de grupo), #227 ("Gift" en pose T), #229 (faltan luces), #239 (cutin 1007 no aparece en Scenes).
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
