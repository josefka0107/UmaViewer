# usage: python worksheet.py <decrypted cutt camera bundle> [list name to dump]
import json, sys
import UnityPy

env = UnityPy.load(sys.argv[1])
obj = next(o for o in env.objects if o.type.name == "MonoBehaviour")
w = obj.read_typetree()


def count_keys(lst):
    total = 0
    for entry in lst:
        if isinstance(entry, dict):
            keys = entry.get("keys")
            if isinstance(keys, dict):
                total += len(keys.get("thisList", []))
    return total


if len(sys.argv) < 3:
    for k, v in w.items():
        if isinstance(v, list):
            mark = " <== PROPS" if "prop" in k.lower() else ""
            print(f"{k}: entries={len(v)} keys={count_keys(v)}{mark}")
else:
    lst = w[sys.argv[2]]
    for i, entry in enumerate(lst):
        e = dict(entry)
        keys = e.pop("keys", {})
        print(f"[{i}]", json.dumps(e, ensure_ascii=False, default=str)[:400])
        for key in keys.get("thisList", [])[:int(sys.argv[3]) if len(sys.argv) > 3 else 6]:
            print("    ", json.dumps(key, ensure_ascii=False, default=str)[:500])
        print("     total keys:", len(keys.get("thisList", [])))
