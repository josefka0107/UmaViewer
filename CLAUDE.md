# UmaViewer (fork)

Visor en Unity de los assets de Uma Musume: Pretty Derby. Fork de `josefka0107/UmaViewer` (origen: `katboi01/UmaViewer`).

El fork es **solo de consulta**: el visor se rehace desde cero en `D:\dev\UmaViewer_2\UmaViewer2` (Unity 6). Aquí valen los hallazgos y `Tools/bundle-inspect`.

## Proyecto

- Unity **2022.3.62f3**. Escena principal: `Assets/Scenes/Version2.unity`; lives en `Assets/Scenes/LiveScene.unity`.
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
- `Tools/bundle-inspect` (Python + UnityPy, venv aparte): descifra bundles vía `meta` y lee el TypeTree (nombres reales de campos de clases del juego), busca nodos (`findnode.py`) y simula props de lives (`propsim.py`). Es la vía para entender objetos del juego; REA no sirve (IL2CPP sin metadata en disco).
- Referencia revisada (2026-10-29): [UmamusumeExplorer](https://github.com/MarshmallowAndroid/UmamusumeExplorer) (C#, AssetsTools.NET). Solo audio (CRI HCA/ACB, mezcla de voces por posición en `Audio/Live/SongMixer.cs`, `PartTrigger.cs`), tablas de `master.mdb` y extractor; mismo descifrado que `UmaAssetBundleStream`. Sin clases 3D (cutt, props, luces). Útil si se trabaja el audio de lives.

## Notas detalladas (leer cuando haga falta)

- `docs/lives.md`: todo lo averiguado de las lives (timeline, props, efectos, luces, escenario, posproceso, monitores) y los pendientes de 2026-10-29/30. Antes de tocar la orientación de los pases de posproceso, leer su apartado "Orientación": se rompe al cambiar el signo de `_MainTex_TexelSize.y` y solo se ve grabando.
- `docs/export.md`: exportación PMX (lo hecho y lo pendiente).
- `docs/features.md`: props junto a la uma (gizmo, `PropAnimationLinks.json`, `PropNames.json`) y la idea aparcada de versión web.
