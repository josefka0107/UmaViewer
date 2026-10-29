# usage: python shaderprops.py <shader name substring>...
# Lists the properties each matching shader in the game's "shader" bundle declares (what HasProperty sees).
import sys
import UnityPy
import umameta

(n, h, e), = umameta.read_meta("n = 'shader'")
env = UnityPy.load(umameta.decrypt(h, e))
wanted = sys.argv[1:]
for o in env.objects:
    if o.type.name != "Shader":
        continue
    try:
        t = o.read_typetree()
    except Exception as ex:
        continue
    pf = t.get("m_ParsedForm", {})
    name = pf.get("m_Name", "")
    if not any(w in name for w in wanted):
        continue
    props = [(p.get("m_Name"), p.get("m_Type")) for p in pf.get("m_PropInfo", {}).get("m_Props", [])]
    print(name, "->", props)
    # Global (non-property) uniforms used by the first pass, which bgColor lists may drive instead
    names = set()
    for sub in pf.get("m_SubShaders", []):
        for ps in sub.get("m_Passes", []):
            for k, v in ps.get("m_NameIndices", []):
                names.add(k)
    print("   uniforms:", sorted(x for x in names if "olor" in x or "Shadow" in x or "Mul" in x)[:40])
