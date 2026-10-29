# Replays LivePropsController's rules on a live's timeline: when each character prop is shown and where.
# usage: python propsim.py <music id> [variation] [positions]
# The cutt bundles are decrypted with umameta.py into ./bundles if missing.
import os
import sys
import UnityPy
import umameta

MUSIC = sys.argv[1] if len(sys.argv) > 1 else "1181"
VARIATION = int(sys.argv[2]) if len(sys.argv) > 2 else 0
BUNDLES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "bundles")


def load(name):
    path = os.path.join(BUNDLES, name.replace("/", "_"))
    if not os.path.exists(path):
        (n, h, e), = umameta.read_meta(f"n = '{name}'")
        os.makedirs(BUNDLES, exist_ok=True)
        open(path, "wb").write(umameta.decrypt(h, e))
    return next(o for o in UnityPy.load(path).objects if o.type.name == "MonoBehaviour").read_typetree()


d = load(f"cutt/cutt_son{MUSIC}/data")
w = load(f"cutt/cutt_son{MUSIC}/son{MUSIC}_camera")
POSITIONS = int(sys.argv[3]) if len(sys.argv) > 3 else len(w["charaMotSeqList"])
groups = d["propsSettings"]["propsDataGroup"]


def matches(g, pos):
    cgs = [c for c in g["propsConditionGroup"] if not c["IsInvalid"] and c["propsConditionData"]]
    if not cgs:
        return True
    def m(c):
        return c["Value"] == pos if c["Type"] == 1 else True
    return any((all if c["satisfiesAllConditions"] else any)(m(x) for x in c["propsConditionData"]) for c in cgs)


props = []  # (group index, position, local index)
for pos in range(POSITIONS):
    local = 0
    for i, g in enumerate(groups):
        if not matches(g, pos):
            continue
        props.append((i, pos, local))
        local += 1
plists = [l for l in w["propsList"] if not l["_applyVariation"] or l["_variationId"] == VARIATION]
alists = [l for l in w["propsAttachList"] if not l["_applyVariation"] or l["_variationId"] == VARIATION]

# Props each position's active lists name explicitly; propsID -1 targets those (or all when none are named)
named = {}
for lists, flags, pid in ((plists, "settingFlags", "propsID"), (alists, "_settingFlags", "_propsId")):
    for l in lists:
        for k in l["keys"]["thisList"]:
            if k[pid] >= 0:
                for pos in range(POSITIONS):
                    if (k[flags] >> pos) & 1:
                        named.setdefault(pos, set()).add(k[pid])


def targets(key_id, pos, li):
    return key_id == li or (key_id < 0 and (pos not in named or li in named[pos]))


def cur(keys, f):
    c = None
    for k in keys:
        if k["frame"] > f:
            break
        c = k
    return c


def state(i, pos, f, li):
    vis = None
    for l in plists:
        k = cur(l["keys"]["thisList"], f)
        if k and (k["settingFlags"] >> pos) & 1 and targets(k["propsID"], pos, li):
            if vis is None or k["frame"] >= vis["frame"]:
                vis = k
    att = None
    for l in alists:
        k = cur(l["keys"]["thisList"], f)
        if k and (k["_settingFlags"] >> pos) & 1 and targets(k["_propsId"], pos, li) and k["_attachJointName"]:
            if att is None or k["frame"] >= att["frame"]:
                att = k
    visible = bool(vis and vis["rendererEnable"])
    anim = bool(visible and vis["IsApplyAnimation"] and vis["AnimationClip"]["m_PathID"])
    joint = att["_attachJointName"] if att else groups[i]["attachJointNames"][0]
    return visible, joint, anim


print(f"son{MUSIC} variation {VARIATION}, {POSITIONS} positions, named: {named}")
for i, pos, li in props:
    g = groups[i]
    name = (f"prop{g['charaPropsMajorId']}_{g['charaPropsMinorId']:02}" if g["isCharaProps"] else f"cmn_prop{g['propsName']}") + f"@{pos}"
    prev, spans = None, []
    for f in range(0, int(d["timeLength"] * 60), 10):
        s = state(i, pos, f, li)
        if s != prev:
            spans.append((f, s))
            prev = s
    print(name, [(f, "ON" if v else "off", j, "anim" if a else "") for f, (v, j, a) in spans][:12])
