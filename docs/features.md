# Props en el visor y versión web (aparcada)

Movido desde `CLAUDE.md` (2026-10-31), sin cambios en el texto.

## Features pendientes (README: "/" incompleto)

- **Viewing Props, Scenery, Live scenes** — props (`pfb_chr_prop*`) y escenarios (`3d/env/.../pfb_*`, `cutt/cutt_son*`) se cargan instanciando el prefab tal cual (`UmaContainerProp.LoadProp`), sin ajuste de shaders/iluminación; `LoadAssetPath` reporta scripts faltantes del juego.
  - Hecho (commits `13ce473`, `34c35cb`): props aparecen junto a la uma con gizmo magenta (`UIHandleProp`, en Pose Mode): Reset, Attach a mano derecha/izquierda (`Hand_Attach_R/L`, los props ya están modelados para ese hueso, sin offset), Detach, Link/Unlink a la animación actual. Vínculos en `PropAnimationLinks.json` (raíz del proyecto en el Editor, ignorado por git); se aplican en `UmaContainerCharacter.LoadAnimation`. Props pegados a la mano se excluyen del export de la uma.
  - `master.mdb` no tiene relación animación↔prop (`character_prop_animation` solo describe animaciones propias de 6 props); probablemente está en los timelines de historia.
  - Nombres de props (commit `f849bf8`, `PropNames.cs`): la lista muestra el nombre del personaje si el número coincide (`prop1001_*` → 1001) y nombres propios vía "Rename" en el gizmo, guardados en `PropNames.json` (ignorado por git). El campo de texto es una copia del buscador de Props (la UI no tiene diálogo de texto).

## Idea aparcada: versión web

Evaluado y pospuesto. Conclusiones:
- La BD es lo fácil: `master.mdb` funciona con `sql.js`; `meta` necesitaría un build WASM de SQLite3MultipleCiphers.
- Bloqueos reales: acceso a `dat/` (muchos GB, local) → usar File System Access API para que cada usuario elija su carpeta `Persistent` (no alojar assets en servidor: sería redistribuir material de Cygames); plugins nativos (`sqlite3*.dll`, LAME, StandaloneFileBrowser, CySpringPlugin) no corren en WebGL; bundles cifrados requieren `LoadFromMemory`; límite de memoria WebGL (~4 GB); uso de `Thread`/`Task.Run` en 6 archivos (incl. `CySpringController.cs`).
- Primer paso propuesto si se retoma: prueba de concepto web que lea `master.mdb` con `sql.js` y liste personajes/trajes/canciones.
