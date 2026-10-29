# usage: python bundleinfo.py <decrypted bundle>
# Lists objects, whether the serialized files carry type trees, and the MonoScript class names.
import sys
import UnityPy

env = UnityPy.load(sys.argv[1])
for name, f in env.files.items():
    for sf in getattr(f, "files", {f: f}).values() if hasattr(f, "files") else [f]:
        if hasattr(sf, "_enable_type_tree"):
            print("serialized file", getattr(sf, "name", name), "type tree:", sf._enable_type_tree, "unity", sf.unity_version)
for obj in env.objects:
    line = f"{obj.type.name} {obj.path_id}"
    if obj.type.name in ("MonoBehaviour", "MonoScript", "TextAsset", "GameObject"):
        try:
            d = obj.read()
            line += f" name={getattr(d, 'm_Name', '')}"
            if obj.type.name == "MonoScript":
                line += f" class={d.m_Namespace}.{d.m_ClassName}"
            if obj.type.name == "MonoBehaviour":
                script = d.m_Script.read() if d.m_Script else None
                line += f" script={script.m_Namespace + '.' + script.m_ClassName if script else '?'}"
        except Exception as e:
            line += f" ({e})"
    print(line)
