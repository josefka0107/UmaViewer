# usage: python cuttdetail.py <decrypted cutt data bundle>
import json, sys
import UnityPy

env = UnityPy.load(sys.argv[1])
obj = next(o for o in env.objects if o.type.name == "MonoBehaviour")
t = obj.read_typetree()
print("top-level:", [k for k in t.keys()])
for i, g in enumerate(t["propsSettings"]["propsDataGroup"]):
    g = dict(g)
    conds = [[(c["Type"], c["Value"]) for c in grp["propsConditionData"]] + [("all" if grp["satisfiesAllConditions"] else "any")]
             for grp in g.pop("propsConditionGroup")]
    print(i, json.dumps(g, ensure_ascii=False), "conditions:", conds)
ws = t["worksheetList"]
print("worksheets:", len(ws))
w = ws[0]
lists = {k: len(v) for k, v in w.items() if isinstance(v, list)}
print("worksheet[0] lists:", lists)
print("worksheet[0] scalars:", {k: v for k, v in w.items() if not isinstance(v, (list, dict))})
