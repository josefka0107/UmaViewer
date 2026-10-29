# usage: python fielddiff.py <viewer Scripts dir> <music id>
# For every timeline list the viewer loads, the key fields the game's data has (with non-default values) that the
# viewer's key classes do not declare, so Unity drops them. Also LiveTimelineData top-level fields.
import collections
import os
import re
import sys
import UnityPy

src_dir, music = sys.argv[1], sys.argv[2]

# --- viewer classes: name -> (base, fields) -----------------------------------------------------------------
classes = {}
for root, _, files in os.walk(src_dir):
    for f in files:
        if not f.endswith(".cs"):
            continue
        text = open(os.path.join(root, f), encoding="utf-8", errors="ignore").read()
        text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
        text = "\n".join(l.split("//")[0] for l in text.splitlines())
        for m in re.finditer(r"class\s+(\w+)(?:<[^>]*>)?\s*(?::\s*([\w.<>, ]+))?\s*\{", text):
            name, base = m.group(1), (m.group(2) or "").split(",")[0].strip()
            base = re.sub(r"<.*", "", base).split(".")[-1]
            # class body by brace matching
            i, depth = m.end(), 1
            while depth and i < len(text):
                depth += {"{": 1, "}": -1}.get(text[i], 0)
                i += 1
            body = text[m.end():i]
            # only fields at depth 0 of the body
            flat, d = [], 0
            for ch in body:
                if ch == "{": d += 1
                elif ch == "}": d -= 1
                elif d == 0: flat.append(ch)
            fields = set(re.findall(r"(?:public|\[SerializeField\][^;]*?)\s+[\w<>\[\],. ]+?\s+(\w+)\s*(?:=[^;]*)?;", "".join(flat)))
            classes.setdefault(name, (base, set()))[1].update(fields)


def all_fields(cls, seen=None):
    seen = seen or set()
    if cls not in classes or cls in seen:
        return set()
    seen.add(cls)
    base, fields = classes[cls]
    return fields | all_fields(base, seen)


def field_type(cls, field):
    """Element type name of a field declared in cls (List<T>/T[]/T)."""
    for root, _, files in os.walk(src_dir):
        pass
    return None


def nondefault(v):
    if isinstance(v, (int, float)):
        return v != 0
    if isinstance(v, str):
        return v != ""
    if isinstance(v, dict):
        if set(v) == {"m_FileID", "m_PathID"}:
            return v["m_PathID"] != 0
        if set(v) <= set("xyzw") or set(v) <= set("rgba"):
            return any(nondefault(x) for x in v.values())
        return any(nondefault(x) for x in v.values())
    if isinstance(v, list):
        return len(v) > 0
    return False


# Key list element classes per worksheet field, read from the worksheet source
ws_src = open(os.path.join(src_dir, "umamusume/Gallop/Live/Cutt/LiveTimelineWorkSheet.cs"), encoding="utf-8").read()
ws_src = re.sub(r"/\*.*?\*/", "", ws_src, flags=re.S)
list_types = dict((n, t) for t, n in re.findall(r"public\s+List<(\w+)>\s+(\w+)", ws_src))
single_types = dict((n, t) for t, n in re.findall(r"public\s+(LiveTimelineKey\w+DataList)\s+(\w+)", ws_src))


def key_class_of(group_cls):
    """Group class -> its keys field type -> template argument (key class)."""
    for root, _, files in os.walk(src_dir):
        for f in files:
            if f.endswith(".cs"):
                text = open(os.path.join(root, f), encoding="utf-8", errors="ignore").read()
                m = re.search(r"class\s+%s\b[^{]*\{(.*?)\n\s*\}" % re.escape(group_cls), text, flags=re.S)
                if m:
                    km = re.search(r"public\s+(\w+)\s+keys\s*;", m.group(1))
                    if km:
                        return km.group(1)
    return None


def template_arg(list_cls):
    for root, _, files in os.walk(src_dir):
        for f in files:
            if f.endswith(".cs"):
                text = open(os.path.join(root, f), encoding="utf-8", errors="ignore").read()
                m = re.search(r"class\s+%s\s*:\s*LiveTimelineKeyDataListTemplate<(\w+)>" % re.escape(list_cls), text)
                if m:
                    return m.group(1)
    return None


load = lambda p: next(o for o in UnityPy.load(p).objects if o.type.name == "MonoBehaviour").read_typetree()
base = os.path.join(os.path.dirname(os.path.abspath(__file__)), "bundles")
w = load(os.path.join(base, f"cutt_cutt_son{music}_son{music}_camera"))
d = load(os.path.join(base, f"cutt_cutt_son{music}_data"))

report = []
for name, value in w.items():
    entries = value if isinstance(value, list) else [value] if isinstance(value, dict) and "thisList" in value else None
    if not entries:
        continue
    if name in list_types:
        key_list_cls = key_class_of(list_types[name])
        group_fields = all_fields(list_types[name])
        missing_group = collections.Counter(k for e in entries for k, v in e.items() if k not in group_fields | {"keys"} and nondefault(v))
    elif name in single_types:
        key_list_cls, missing_group = single_types[name], collections.Counter()
        entries = [{"keys": value}]
    else:
        continue
    key_cls = template_arg(key_list_cls) if key_list_cls else None
    if not key_cls:
        report.append((name, "?", {}, {}))
        continue
    have = all_fields(key_cls) | {"frame", "attribute"}
    missing = collections.Counter()
    for e in entries:
        for k in e.get("keys", {}).get("thisList", []):
            for f, v in k.items():
                if f not in have and nondefault(v):
                    missing[f] += 1
    if missing or missing_group:
        report.append((name, key_cls, dict(missing.most_common(12)), dict(missing_group)))

print(f"== son{music}: key fields with data that the viewer's classes drop")
for name, cls, missing, mg in report:
    print(f"{name} [{cls}]: keys {missing}" + (f" | group {mg}" if mg else ""))

have_d = all_fields("LiveTimelineData")
print("\n== LiveTimelineData top-level fields with data the viewer drops:")
print([k for k, v in d.items() if k not in have_d and not k.startswith("m_") and nondefault(v)])
cs = d.get("characterSettings", {})
have_cs = all_fields("LiveTimelineCharacterSettings")
print("characterSettings dropped:", [k for k, v in cs.items() if k not in have_cs and nondefault(v)])
