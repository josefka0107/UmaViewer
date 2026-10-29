# usage: python clipnodes.py [meta prefix] [body bundle]
# Collects the transform paths animated by live body clips that the body skeleton does not have: nodes the game
# creates at runtime (Mic_Attach_00, Position/Mic_Attach_00_loc, ...) so the body motion can move them.
# Reading full type trees takes too long for ~1100 clips, so curve paths are taken from the raw serialized data:
# Unity stores them as length-prefixed strings, and a path is a string whose segments look like node names.
import re
import struct
import sys
from collections import defaultdict
import UnityPy
import umameta

prefix = sys.argv[1] if len(sys.argv) > 1 else "3d/motion/live/body/"
body = sys.argv[2] if len(sys.argv) > 2 else "3d/chara/body/bdy1001_00/pfb_bdy1001_00"

# Skeleton paths relative to the body root
(n, h, e), = umameta.read_meta(f"n = '{body}'")
env = UnityPy.load(umameta.decrypt(h, e))
skeleton = set()
for o in env.objects:
    if o.type.name != "Transform":
        continue
    t, parts = o.read(), []
    while t.m_Father and t.m_Father.path_id:
        parts.append(t.m_GameObject.read().m_Name)
        t = t.m_Father.read()
    skeleton.add("/".join(reversed(parts)))
roots = {p.split("/")[0] for p in skeleton if p}

PATH = re.compile(rb"^[A-Za-z0-9_]+(/[A-Za-z0-9_]+)*$")


def strings(data):
    """Length-prefixed (int32, 4-byte aligned) ASCII strings that look like transform paths."""
    for m in re.finditer(rb"[A-Za-z0-9_/]{3,200}", data):
        start = m.start()
        if start < 4:
            continue
        length = struct.unpack_from("<i", data, start - 4)[0]
        if length == len(m.group()) and PATH.match(m.group()):
            yield m.group().decode()


found = defaultdict(set)  # extra path -> songs using it
rows = umameta.read_meta(f"n LIKE '{prefix}%'")
print(f"{len(rows)} clips, {len(skeleton)} skeleton paths", file=sys.stderr, flush=True)
for i, (name, h, e) in enumerate(rows):
    try:
        clip_env = UnityPy.load(umameta.decrypt(h, e))
    except Exception:
        continue
    for o in clip_env.objects:
        if o.type.name != "AnimationClip":
            continue
        for s in set(strings(o.get_raw_data())):
            # Keep strings that start like a node path but are not in the skeleton
            if s not in skeleton and (s.split("/")[0] in roots or "Attach" in s or "_loc" in s or "Node" in s):
                found[s].add(name.split("/")[-2])
    if i % 100 == 0:
        print(f"  {i}/{len(rows)}", file=sys.stderr, flush=True)

for path, songs in sorted(found.items(), key=lambda kv: -len(kv[1])):
    print(f"{len(songs):4d} songs  {path}  e.g. {sorted(songs)[:6]}")
