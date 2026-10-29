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
- Compilar sin Unity (p. ej. en un worktree sin `Library/`, que es enorme y no se puede copiar): generar un `.rsp` desde `umamusume.csproj` / `RuntimeGizmo.csproj` del checkout principal (HintPaths relativos a él, `ProjectReference` → `Library/ScriptAssemblies/*.dll`, fuentes del worktree) y ejecutar `Editor\Data\NetCoreRuntime\dotnet.exe Editor\Data\DotNetSdkRoslyn\csc.dll -noconfig @archivo.rsp`. Solo valida C#, no la importación de assets.
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
  - Nombres de props (commit `f849bf8`, `PropNames.cs`): la lista muestra el nombre del personaje si el número coincide (`prop1001_*` → 1001) y nombres propios vía "Rename" en el gizmo, guardados en `PropNames.json` (ignorado por git). El campo de texto es una copia del buscador de Props (la UI no tiene diálogo de texto).

## Pendientes (estado al 2026-10-28)

En la rama `fix/duplicate-files-pr224`, todo commiteado. Siguiente sesión:

1. **Lives** (necesitan prueba del usuario en el Editor con capturas; leer `Editor.log`):
   - #226 lives de grupo (うまぴょい伝説) muestran solo una uma. Ver `UmaViewerBuilder.LoadLive`: expande personajes según `worksheetList[0].charaMotSeqList.Count`.
   - Modo 0 ("Main Members", un clip `son<id>_*` de 4 partes por miembro) crasheaba con `ArgumentOutOfRangeException` en `LiveTimelineMotionSequence.Initialize` (visto con 1181 トレセンラーメン列伝): la expansión de PR #224 en `LoadLive` añadía más personajes que clips. Arreglado (2026-10-29): solo se expande en modo 1 y se protege el índice. Falta prueba del usuario.
   - Umas compatibles (2026-10-29): `live_permission_data` (music_id, chara_id) = umas con voz propia; solo aplica si `live_data.song_chara_type = 2` (47 de 50 lives; tipo 1 = todos cantan). `live_recommend_formation` = formación sugerida por el juego (36 canciones, chara + dress por posición). El visor ya permitía cualquier uma (sin voz → voz aleatoria en `Director.InitializeMusic`). Añadido en el panel de live, creado en runtime (`UmaViewerUI.SetupLiveSelectExtras`): toggle "Only Voiced" (filtra `CharactersList` al elegir hueco) y botón "Recommended" (rellena huecos; sin formación → umas con voz, primer traje propio). Falta prueba del usuario (posición de los controles).
   - **Props de personaje en lives: implementados** (2026-10-29, `Gallop/Live/LivePropsController.cs`, creado en `Director.InitializeTimeline` y actualizado en `ApplyTimelineLateUpdate`; datos en `Cutt/LiveTimelineDataList/LiveTimelinePropsData.cs`). Falta prueba del usuario con 1181 en modo 1 (8 posiciones). Comprobación sin Unity: `Tools/bundle-inspect/propsim.py <music_id> [variación]` repite las reglas sobre cualquier live (probado con 1181 y 1025). Analizado con `Tools/bundle-inspect`; el TypeTree de los bundles da los nombres reales:
     - `LiveTimelineData.propsSettings.propsDataGroup[]`: `isCharaProps`, `charaPropsMajorId/MinorId` → `3d/chara/prop/prop<major>_<minor:00>/pfb_chr_prop...`, `attachJointNames` (`Hand_Attach_L/R`, `Mic_Attach_00`), condiciones `{Type: 1=CharaPosition (0-based), 2=CharaId, 3=DressId, Value}`. Campos que nos faltan: `IsToonProp`, `IsRichProp`, `AttachPropIdArray`, `isInfluenceOfCharaHeight`, `isUseGenderDiffPropsId`…
     - `LiveTimelineWorkSheet.propsList` (keys `propsID`, `settingFlags` = máscara de posiciones, `rendererEnable`, `IsApplyAnimation` + `AnimationClip`/`StartAnimationTime`) y `propsAttachList` (keys `_attachJointName`, `_settingFlags`, `_propsId`, `_offsetPosition`, `OffsetRotate`, `OffsetScale`, `_attachType`). **`propsID`/`_propsId` es el índice entre los props de esa posición** (los grupos cuyas condiciones cumple, en orden), no el índice global: así encaja todo en 1181. **`-1` = todos los props de esas posiciones** (1025 solo usa `-1`), limitado a los que las listas activas nombran para la posición, para no mostrar los de otras variaciones. Entradas con `_applyVariation/_variationId` (0..2) = juegos alternativos (cuenco 1183_06/07/05 + utensilio 7018/7019/7020); cómo elige el juego es desconocido, se usa la 0.
     - Animaciones de prop: la key de props referencia el clip (`3d/motion/live/prop/son<id>/anm_liv_son<id>_<clip cuerpo>_prop<major>_<minor>`); son dependencias del bundle `son<id>_camera`, así que se resuelven al precargar el cutt. Los prefabs `pfb_chr_prop*` no lo son y se cargan aparte.
     - `Mic_Attach_00` no existe en ningún asset (umas, props, escenario `live10151`; buscado con `Tools/bundle-inspect/findnode.py`): lo crea el juego. Sus keys llevan offsets de escenario (cuenco en la barra a (4.11, 0.75, 1.47)), así que se usa la raíz del escenario (`Director._stageController`). Suposición: comprobar en pantalla.
     - `prop7018/7019/7020` son los cuencos de ramen (nodos `Soup0`, `seaweed`, `Acc*`); `1183_05/06/07` lo que la uma sujeta en la mano derecha al comer.
     - El log escribe `[LiveProps] <prop> at position N shown at frame F on <nodo>` la primera vez que se ve cada prop.
     - La 1181 tiene 8 posiciones (`motionSequenceIndices`): 0–5 cantan, 6 come (`eat01..06`, cuencos prop1183) y 7 cocina (`cook`). Con 6 umas, los props de 6/7 no tienen dueño.
   - `objectList` con `AttachTarget = Character (2)`: `CharacterPosition` es **1-based** (1181 usa 7 y 8 con 8 posiciones). Arreglado en `StageController.UpdateObject` (2026-10-29); antes daba `ArgumentOutOfRangeException` y pegaba al personaje equivocado.
   - REA no sirve para el código del juego: es IL2CPP sin `global-metadata.dat` en disco y Ghidra no está instalado.
   - #227 live "Gift": umas en pose T.
   - #229 faltan luces del escenario en lives.
   - #239 `pfb_env_cutin1007_00_00` (Gold Ship) no aparece en Scenes; filtro en `UmaViewerUI.LoadMapPanel` (`3d/env` + `pfb_`). Pedir al usuario buscar `cutin` en Scenes.
2. ~~`PropNames.json` quedó `{}`~~ y ~~atajos activos al escribir~~: arreglado en la rama `fix/rename-and-hotkeys` (2026-10-29). Causa probable: el campo guardaba en `onEndEdit`, que UGUI también dispara al hacer clic fuera / Escape / desactivarse → `Set(prop, "")`. Ahora solo guarda con Enter (`onSubmit`) y si el nombre cambió. `UmaViewerUI.IsTyping` silencia H, Espacio, atajos del gizmo y teclas de `FreeCam`. **Falta prueba del usuario** en el Editor.
3. Error TMP `m_AtlasTextures` al mostrar mensajes con japonés (fallback de fuente sin atlas).
4. Dorsal de mobs (`_ZekkenNumberTex`), aparcado: requiere medir en runtime.
5. Avisar upstream (katboi01): proyecto no compila tras PR #224 (duplicados) y Defender marca `CySpringPlugin.dll`; nuestros commits `6311308` y export pueden servir como PR.
6. Vínculos de `PropAnimationLinks.json`: el archivo conserva 4 vínculos del 2026-10-28 tras cerrar el Editor; falta confirmar que se reaplican al reabrir Play.
7. Idea aparcada: versión web (ver sección arriba).
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
