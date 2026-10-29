# Replays LivePropsController's rules on the 1181 data: which prop is visible where, frame by frame.
import UnityPy

d = next(o for o in UnityPy.load('bundles/cutt_cutt_son1181_data').objects if o.type.name == 'MonoBehaviour').read_typetree()
w = next(o for o in UnityPy.load('bundles/cutt_cutt_son1181_son1181_camera').objects if o.type.name == 'MonoBehaviour').read_typetree()
import sys
VARIATION, POSITIONS = int(sys.argv[1]) if len(sys.argv) > 1 else 0, 8
groups = d['propsSettings']['propsDataGroup']


def matches(g, pos):
    cgs = [c for c in g['propsConditionGroup'] if not c['IsInvalid'] and c['propsConditionData']]
    if not cgs:
        return True
    def m(c):
        return c['Value'] == pos if c['Type'] == 1 else True
    return any((all if c['satisfiesAllConditions'] else any)(m(x) for x in c['propsConditionData']) for c in cgs)


props = []  # (group index, position, local index)
for pos in range(POSITIONS):
    local = 0
    for i, g in enumerate(groups):
        if not matches(g, pos):
            continue
        if g['isCharaProps']:
            props.append((i, pos, local))
        local += 1
plists = [l for l in w['propsList'] if not l['_applyVariation'] or l['_variationId'] == VARIATION]
alists = [l for l in w['propsAttachList'] if not l['_applyVariation'] or l['_variationId'] == VARIATION]


def cur(keys, f):
    c = None
    for k in keys:
        if k['frame'] > f:
            break
        c = k
    return c


def state(i, pos, f, li):
    vis = None
    for l in plists:
        k = cur(l['keys']['thisList'], f)
        if not k or not (k['settingFlags'] >> pos) & 1:
            continue
        if k['propsID'] != li and not (k['propsID'] < 0 and not k['rendererEnable']):
            continue
        if vis is None or k['frame'] >= vis['frame']:
            vis = k
    att = None
    for l in alists:
        k = cur(l['keys']['thisList'], f)
        if not k or k['_propsId'] != li or not (k['_settingFlags'] >> pos) & 1:
            continue
        if att is None or k['frame'] >= att['frame']:
            att = k
    visible = bool(vis and vis['propsID'] == li and vis['rendererEnable'])
    anim = bool(visible and vis['IsApplyAnimation'] and vis['AnimationClip']['m_PathID'])
    joint = att['_attachJointName'] if att else groups[i]['attachJointNames'][0]
    return visible, joint, anim


print('instances:', [(f"prop{groups[i]['charaPropsMajorId']}_{groups[i]['charaPropsMinorId']:02}", pos) for i, pos, li in props])
for i, pos, li in props:
    name = f"prop{groups[i]['charaPropsMajorId']}_{groups[i]['charaPropsMinorId']:02}@{pos}"
    prev, spans = None, []
    for f in range(0, int(d['timeLength'] * 60), 10):
        s = state(i, pos, f, li)
        if s != prev:
            spans.append((f, s))
            prev = s
    print(name, [(f, 'ON' if v else 'off', j, 'anim' if a else '') for f, (v, j, a) in spans if v or f == 0][:10])
