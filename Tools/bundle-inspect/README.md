# bundle-inspect

Read-only scripts to look inside the game's asset bundles without Unity, mainly to learn the
serialized field names of the game's classes (the bundles keep their type trees).

Setup (once): `python -m venv venv && venv/Scripts/python -m pip install UnityPy`

- `umameta.py list <text>`: meta entries whose name contains `<text>` (name, hash, key).
- `umameta.py dump <name> <dir>`: decrypts that bundle into `<dir>` (original files are never modified).
- `bundleinfo.py <bundle>`: objects, MonoBehaviour classes and whether the type tree is present.
- `cuttdetail.py <cutt data bundle>`: `LiveTimelineData` top-level fields and `propsSettings`.
- `worksheet.py <cutt camera bundle> [list] [n]`: `LiveTimelineWorkSheet` lists with key counts, or the keys of one list.

A live's timeline is split in `cutt/cutt_son<id>/data` (`LiveTimelineData`) and
`cutt/cutt_son<id>/son<id>_camera` (`LiveTimelineWorkSheet`).
`umameta.py` reads `meta` through `Assets/Plugins/sqlite3mc_x64.dll` with the keys from `Config.cs` (Jp region).
