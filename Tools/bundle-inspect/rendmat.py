# usage: python rendmat.py <bundle meta name> <renderer GameObject name>...
# Loads the bundle with its meta dependencies so external materials resolve, then prints each named renderer's
# materials: shader name and saved color/float properties.
import ctypes
import os
import sys
import UnityPy
import umameta

lib, P = umameta.lib, ctypes.c_void_p
db = P()
lib.sqlite3_open_v2(os.path.join(umameta.GAME, "meta").encode(), ctypes.byref(db), 1, None)
lib.sqlite3mc_config(db, b"cipher", 3)
key = bytes(k ^ umameta.DB_BASE_KEY[i % 13] for i, k in enumerate(umameta.DB_KEY))
lib.sqlite3_key(db, key, len(key))
st = P()
lib.sqlite3_prepare_v2(db, f"SELECT d FROM a WHERE n = '{sys.argv[1]}'".encode(), -1, ctypes.byref(st), None)
lib.sqlite3_step(st)
deps = [d for d in (lib.sqlite3_column_text(st, 0) or b"").decode().split(";") if d and not d.startswith("3d/motion")]

env = UnityPy.Environment()
for name in [sys.argv[1]] + deps:
    rows = umameta.read_meta(f"n = '{name}'")
    if rows:
        n, h, e = rows[0]
        env.load_file(umameta.decrypt(h, e), name=name)

wanted = set(sys.argv[2:])
for o in env.objects:
    if o.type.name not in ("MeshRenderer", "SkinnedMeshRenderer"):
        continue
    r = o.read()
    go = r.m_GameObject.read()
    if go.m_Name not in wanted:
        continue
    for mp in r.m_Materials:
        try:
            m = mp.read()
        except Exception as ex:
            print(go.m_Name, "material unresolved", ex)
            continue
        try:
            shader = m.m_Shader.read().m_ParsedForm.m_Name
        except Exception:
            try:
                shader = m.m_Shader.read().m_Name
            except Exception as ex:
                shader = f"? ({ex})"
        props = m.m_SavedProperties
        colors = {c[0] if isinstance(c, tuple) else c.first: c[1] if isinstance(c, tuple) else c.second for c in props.m_Colors}
        floats = {c[0] if isinstance(c, tuple) else c.first: c[1] if isinstance(c, tuple) else c.second for c in props.m_Floats}
        texs = [t[0] if isinstance(t, tuple) else t.first for t in props.m_TexEnvs]
        print(f"{go.m_Name}: material {m.m_Name} shader {shader}")
        print("   colors:", {k: tuple(round(getattr(v, a), 2) for a in "rgba") for k, v in colors.items()})
        print("   floats:", {k: round(v, 3) for k, v in list(floats.items())[:25]})
        print("   textures:", texs, "keywords:", getattr(m, "m_ValidKeywords", None) or getattr(m, "m_ShaderKeywords", None))
