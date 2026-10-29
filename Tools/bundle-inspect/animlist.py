# usage: python animlist.py
# For every live timeline (cutt/cutt_son*/son*_camera): the stage objects its animationList plays, with key
# count, clip ids and wrap modes used. animationList is the only record of which stage animations a live drives;
# master.mdb has none and the game's AnimationObjectController component has no fields.
import collections
import UnityPy
import umameta

rows = [r for r in umameta.read_meta("n LIKE 'cutt/cutt_son%/son%_camera'")]
total = 0
lives = 0
names = collections.Counter()
for n, h, k in sorted(rows):
    try:
        env = UnityPy.load(umameta.decrypt(h, k))
        w = next(o for o in env.objects if o.type.name == "MonoBehaviour").read_typetree()
    except Exception as e:
        print(n, "unreadable:", e)
        continue
    total += 1
    entries = []
    for e in w.get("animationList") or []:
        ks = e["keys"]["thisList"]
        ids = sorted({x["animationID"] for x in ks})
        wraps = sorted({x["wrapMode"] for x in ks})
        entries.append(f"{e['name']}({len(ks)} keys, clips {ids}, wrap {wraps})")
        kind = e["name"].split("_", 3)[-1] if e["name"].startswith("pfb_env_") else e["name"]
        names[kind] += 1
    if entries:
        lives += 1
        print(n.split("/")[-1], len(entries), "; ".join(entries))
print(f"{total} timelines read, {lives} with animationList")
print("object kinds:", names.most_common(40))
