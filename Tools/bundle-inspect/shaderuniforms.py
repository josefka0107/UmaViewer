# usage: python shaderuniforms.py <shader name substring> [filter regex]
# All uniform/property names the matching shaders' passes reference (m_NameIndices), optionally filtered.
import re
import sys
import UnityPy
import umameta

(n, h, e), = umameta.read_meta("n = 'shader'")
env = UnityPy.load(umameta.decrypt(h, e))
flt = re.compile(sys.argv[2], re.I) if len(sys.argv) > 2 else None
for o in env.objects:
    if o.type.name != "Shader":
        continue
    try:
        t = o.read_typetree()
    except Exception:
        continue
    pf = t.get("m_ParsedForm", {})
    if sys.argv[1] not in pf.get("m_Name", ""):
        continue
    names = set()
    for sub in pf.get("m_SubShaders", []):
        for ps in sub.get("m_Passes", []):
            for k, v in ps.get("m_NameIndices", []):
                names.add(k)
    kw = set()
    for sub in pf.get("m_SubShaders", []):
        for ps in sub.get("m_Passes", []):
            for k in ps.get("m_LocalKeywordMask", []) or []:
                pass
    shown = sorted(x for x in names if not flt or flt.search(x))
    print(pf["m_Name"], f"({len(names)} names):", shown[:80])
    print("   keywords:", sorted(t.get("m_KeywordNames", []) or pf.get("m_KeywordNames", []))[:60])
