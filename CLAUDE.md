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
- Unity puede reescribir `ProjectSettings/ShaderGraphSettings.asset` solo con finales de línea; revertir con `git checkout`.
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
- **Exporting models** — ver issues upstream katboi01/UmaViewer #125, #186, #177, #230. Problemas detectados en `ModelExporter.cs`:
  - `MakeReadableMeshCopy` solo copia el vertex buffer stream 0; mallas con varios streams pierden UVs/normales.
  - Ruta `MeshRenderer` usa `meshfilter.mesh` (instancia copia) en vez de `sharedMesh`, e indexa `normals[i]`/`uv[i]` sin comprobar que existan.
  - `ExportModel(UmaContainer, ...)` crea el `FileStream` antes de construir el modelo y no tiene try/finally → excepción deja un `.pmx` de 0 bytes (#186).
  - Mobs: texturas de pelo/ojos/cola compartidas no se exportan y los colores de `mob_*_color_set` no se aplican (#177, #230).
