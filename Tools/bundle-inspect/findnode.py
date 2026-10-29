# usage: python findnode.py <node name> <meta name substring> [<substring> ...]
# Decrypts (in memory) every 3d/ bundle whose meta name contains one of the substrings and prints
# the transforms named <node name> with their parent chain.
import sys
import UnityPy
import umameta

node = sys.argv[1]
rows = {r[0]: r for s in sys.argv[2:] for r in umameta.read_meta(f"n LIKE '3d/%{s}%'")}
print(f"scanning {len(rows)} bundles")
for name, h, e in sorted(rows.values()):
    try:
        env = UnityPy.load(umameta.decrypt(h, e))
    except Exception as ex:
        print("  skip", name, ex)
        continue
    for obj in env.objects:
        if obj.type.name != "Transform":
            continue
        t = obj.read()
        if t.m_GameObject.read().m_Name != node:
            continue
        chain = []
        while t is not None:
            chain.append(t.m_GameObject.read().m_Name)
            t = t.m_Father.read() if t.m_Father and t.m_Father.path_id else None
        print(f"  {name}: {' <- '.join(chain)}")
