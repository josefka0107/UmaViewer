# usage: python schemadiff.py <worksheet .cs> <music id>...
# Worksheet lists that carry keys in the game's data but that the viewer's LiveTimelineWorkSheet does not declare
# (Unity drops them silently), plus declared lists for reference.
import re
import sys
import UnityPy

cs = open(sys.argv[1], encoding="utf-8").read()
cs = re.sub(r"/\*.*?\*/", "", cs, flags=re.S)
cs = "\n".join(l.split("//")[0] for l in cs.splitlines())
declared = set(re.findall(r"public\s+[\w<>\[\],\s.]+?\s+(\w+)\s*(?:=|;)", cs))

for music in sys.argv[2:]:
    path = f"bundles/cutt_cutt_son{music}_son{music}_camera"
    w = next(o for o in UnityPy.load(path).objects if o.type.name == "MonoBehaviour").read_typetree()
    missing, present = [], []
    for k, v in w.items():
        if not isinstance(v, list) or not v:
            continue
        keys = sum(len(e["keys"]["thisList"]) for e in v if isinstance(e, dict) and isinstance(e.get("keys"), dict))
        (present if k in declared else missing).append(f"{k}({len(v)}/{keys} keys)")
    print(f"son{music} DROPPED by the viewer: {', '.join(missing) or '-'}")
    print(f"son{music} loaded: {', '.join(present)}\n")
