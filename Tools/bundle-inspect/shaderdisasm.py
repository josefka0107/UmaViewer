# usage: python shaderdisasm.py <exact shader name> [max programs]
# Disassembles the distinct DX11 programs (DXBC) of a shader in the game's "shader" bundle with Windows'
# d3dcompiler_47.dll (D3DDisassemble), to read what a game shader really does with its uniforms. Registers are
# unnamed: cbN[i] are constant buffer slots, tN textures in declaration order. Windows only.
import ctypes
import struct
import sys
import lz4.block
import UnityPy
import umameta

name = sys.argv[1]
limit = int(sys.argv[2]) if len(sys.argv) > 2 else 50

d3d = ctypes.WinDLL("d3dcompiler_47.dll")
d3d.D3DDisassemble.argtypes = [ctypes.c_char_p, ctypes.c_size_t, ctypes.c_uint, ctypes.c_char_p,
                               ctypes.POINTER(ctypes.c_void_p)]


def disasm(code):
    blob = ctypes.c_void_p()
    if d3d.D3DDisassemble(code, len(code), 0, None, ctypes.byref(blob)) != 0:
        return ""
    vt = ctypes.cast(ctypes.cast(blob, ctypes.POINTER(ctypes.c_void_p))[0], ctypes.POINTER(ctypes.c_void_p))
    get_ptr = ctypes.WINFUNCTYPE(ctypes.c_void_p, ctypes.c_void_p)(vt[3])
    get_size = ctypes.WINFUNCTYPE(ctypes.c_size_t, ctypes.c_void_p)(vt[4])
    return ctypes.string_at(get_ptr(blob), get_size(blob)).decode(errors="replace")


def flat(x):
    out = []
    for v in x:
        out.extend(flat(v) if isinstance(v, (list, tuple)) else [v])
    return out


(n, h, e), = umameta.read_meta("n = 'shader'")
env = UnityPy.load(umameta.decrypt(h, e))
for o in env.objects:
    if o.type.name != "Shader":
        continue
    s = o.read()
    if s.m_ParsedForm.m_Name != name:
        continue
    for ss in s.m_ParsedForm.m_SubShaders:
        print("passes:", [p.m_State.m_Name for p in ss.m_Passes])
    blob = bytes(s.compressedBlob)
    raw = b""
    for a, b, c in zip(flat(s.offsets), flat(s.compressedLengths), flat(s.decompressedLengths)):
        raw += lz4.block.decompress(blob[a:a + b], uncompressed_size=c)
    seen, i = set(), 0
    while len(seen) < limit:
        i = raw.find(b"DXBC", i)
        if i < 0:
            break
        size = struct.unpack_from("<I", raw, i + 24)[0]
        text = disasm(raw[i:i + size])
        i += 4
        body = "\n".join(l for l in text.split("\n") if l.strip() and not l.startswith("//"))
        if body and body not in seen:
            seen.add(body)
            print(f"----- program {len(seen)}\n{body}")
