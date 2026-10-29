# Reads the game's encrypted meta (read only) and decrypts asset bundles into an output folder.
# usage: python umameta.py list <substring>
#        python umameta.py dump <bundle name> <out dir>
import ctypes, os, sys

GAME = r"D:\Users\jose.rangel\Umamusume\umamusume_Data\Persistent"
DLL = r"D:\dev\UmaViewer-Fork\Assets\Plugins\sqlite3mc_x64.dll"
DB_BASE_KEY = bytes([0xF1, 0x70, 0xCE, 0xA4, 0xDF, 0xCE, 0xA3, 0xE1, 0xA5, 0xD8, 0xC7, 0x0B, 0xD1])
DB_KEY = bytes([0x6D, 0x5B, 0x65, 0x33, 0x63, 0x36, 0x63, 0x25, 0x54, 0x71, 0x2D, 0x73, 0x50, 0x53, 0x63, 0x38,
                0x6D, 0x34, 0x37, 0x7B, 0x35, 0x63, 0x70, 0x23, 0x37, 0x34, 0x53, 0x29, 0x73, 0x43, 0x36, 0x33])
AB_KEY = bytes([0x53, 0x2B, 0x46, 0x31, 0xE4, 0xA7, 0xB9, 0x47, 0x3E, 0x7C, 0xFB])

lib = ctypes.CDLL(DLL)
P = ctypes.c_void_p
lib.sqlite3_open_v2.argtypes = [ctypes.c_char_p, ctypes.POINTER(P), ctypes.c_int, P]
lib.sqlite3mc_config.argtypes = [P, ctypes.c_char_p, ctypes.c_int]
lib.sqlite3_key.argtypes = [P, ctypes.c_char_p, ctypes.c_int]
lib.sqlite3_prepare_v2.argtypes = [P, ctypes.c_char_p, ctypes.c_int, ctypes.POINTER(P), P]
lib.sqlite3_step.argtypes = [P]
lib.sqlite3_column_text.argtypes = [P, ctypes.c_int]
lib.sqlite3_column_text.restype = ctypes.c_char_p
lib.sqlite3_column_int64.argtypes = [P, ctypes.c_int]
lib.sqlite3_column_int64.restype = ctypes.c_int64
lib.sqlite3_errmsg.argtypes = [P]
lib.sqlite3_errmsg.restype = ctypes.c_char_p


def read_meta(where):
    db = P()
    assert lib.sqlite3_open_v2(os.path.join(GAME, "meta").encode(), ctypes.byref(db), 1, None) == 0
    lib.sqlite3mc_config(db, b"cipher", 3)
    key = bytes(k ^ DB_BASE_KEY[i % 13] for i, k in enumerate(DB_KEY))
    lib.sqlite3_key(db, key, len(key))
    stmt = P()
    rc = lib.sqlite3_prepare_v2(db, f"SELECT n,h,e FROM a WHERE {where}".encode(), -1, ctypes.byref(stmt), None)
    if rc != 0:
        raise RuntimeError(lib.sqlite3_errmsg(db))
    rows = []
    while lib.sqlite3_step(stmt) == 100:
        rows.append((lib.sqlite3_column_text(stmt, 0).decode(), lib.sqlite3_column_text(stmt, 1).decode(),
                     lib.sqlite3_column_int64(stmt, 2)))
    return rows


def decrypt(h, e):
    data = bytearray(open(os.path.join(GAME, "dat", h[:2], h), "rb").read())
    if e == 0:
        return bytes(data)
    kb = e.to_bytes(8, "little", signed=True)
    keys = bytes(b ^ kb[j] for b in AB_KEY for j in range(8))
    for i in range(256, len(data)):
        data[i] ^= keys[i % len(keys)]
    return bytes(data)


if __name__ == "__main__":
    if sys.argv[1] == "list":
        for n, h, e in read_meta(f"n LIKE '%{sys.argv[2]}%'"):
            print(n, h, e)
    elif sys.argv[1] == "dump":
        (n, h, e), = read_meta(f"n = '{sys.argv[2]}'")
        out = os.path.join(sys.argv[3], n.replace("/", "_"))
        os.makedirs(sys.argv[3], exist_ok=True)
        open(out, "wb").write(decrypt(h, e))
        print(out)
