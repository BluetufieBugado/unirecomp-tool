#!/usr/bin/env python3
"""Indice SerializedFile v17 (2018.1.5f1) com cross-check contra UnityPy.

Le header/metadata/types(objects)/externals com struct e compara 100% com o
UnityPy (mesmo arquivo). Vira o oracle (docs/sfindex_*.json) do port C#.

Uso: python tools/sfindex.py --data "..._Data" --file level0
"""
import argparse
import json
import struct
from pathlib import Path

import UnityPy


class R:
    def __init__(self, buf):
        self.b = buf
        self.p = 0

    def u32(self):
        v = struct.unpack_from("<I", self.b, self.p)[0]
        self.p += 4
        return v

    def i32(self):
        v = struct.unpack_from("<i", self.b, self.p)[0]
        self.p += 4
        return v

    def i64(self):
        v = struct.unpack_from("<q", self.b, self.p)[0]
        self.p += 8
        return v

    def u8(self):
        v = self.b[self.p]
        self.p += 1
        return v

    def blob(self, n):
        v = self.b[self.p:self.p + n]
        self.p += n
        return v

    def align4(self):
        self.p = (self.p + 3) & ~3

    def ustr_null(self):
        end = self.b.index(b"\x00", self.p)
        s = self.b[self.p:end].decode("utf-8", errors="replace")
        self.p = end + 1
        return s


def parse(path):
    buf = Path(path).read_bytes()
    ms, fs, ver, doff = struct.unpack_from(">IIII", buf, 0)
    header = {"metadata_size": ms, "file_size": fs, "version": ver,
              "data_offset": doff, "endian_flag": buf[16], "reserved": list(buf[17:20])}
    assert header["version"] == 17, header
    r = R(buf)
    r.p = 20
    meta_start = r.p
    unity_version = r.ustr_null()
    target_platform = r.u8()
    enable_tree = r.u8()
    r.align4()
    n_types = r.i32()
    types = []
    for _ in range(n_types):
        class_id = r.i32()
        stripped = r.u8()
        script_idx = struct.unpack_from("<h", r.b, r.p)[0]
        r.p += 2
        script_id = None
        if script_idx >= 0:
            script_id = r.blob(16).hex()
        old_hash = r.blob(16).hex()
        types.append({"class_id": class_id, "stripped": stripped,
                      "script_idx": script_idx, "script_id": script_id,
                      "old_hash": old_hash})
    n_obj = r.i32()
    r.align4()  # entradas alinhadas a 4 (pad 0-3 conforme o arquivo)
    objects = []
    for _ in range(n_obj):
        path_id = r.i64()
        start = r.u32()
        size = r.u32()
        type_id = r.i32()
        objects.append({"path_id": path_id, "start": start, "size": size, "type_id": type_id})
    # scriptTypes: count + PPtr p/ MonoScript (tamanho = max(script_idx)+1)
    n_st = r.i32()
    script_refs = []
    for _ in range(n_st):
        script_refs.append((r.i32(), r.i64()))
    # externals: count U32 + entradas {tempEmpty cstring, guid 16B, type u32} = 21B
    ext_pos = r.p
    n_ext = r.i32()
    externals, ext_ok = [], True
    try:
        for _ in range(n_ext):
            e0 = r.p
            temp = r.ustr_null()
            guid = r.blob(16).hex()
            typ = r.i32()
            path = r.ustr_null()  # packed, sem align
            if temp != "" or typ != 0:
                print(f"  ext incomum: temp={temp!r} type={typ}")
            externals.append({"temp": temp, "type": typ, "path": path, "span": [e0, r.p]})
        if r.p < header["data_offset"]:
            tail = r.b[r.p:header["data_offset"]]
            assert all(b == 0 for b in tail), f"trailer nao-zero ({len(tail)}B)"
            r.p = header["data_offset"]
        assert r.p == header["data_offset"], f"offset {r.p} != data {header['data_offset']}"
    except Exception as e:
        ext_ok = False
        externals.append({"parse_error": str(e)[:120], "at": r.p})
    return header, unity_version, target_platform, types, objects, script_refs, externals, ext_ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", required=True)
    ap.add_argument("--file", default="level0")
    ap.add_argument("--out", default=None)
    args = ap.parse_args()
    data = Path(args.data)
    header, uver, plat, types, objects, script_refs, externals, ext_ok = parse(data / args.file)
    print("header:", header, "| unity:", uver, "| platform:", plat)
    print(f"types={len(types)} objects={len(objects)} scriptRefs={len(script_refs)}")

    env = UnityPy.load(str(data / args.file))
    sf = [a for a in env.assets if getattr(a, "name", "") == args.file][0]
    utypes = [(t.class_id, getattr(t, "script_type_index", -1)) for t in sf.types]
    assert len(types) == len(utypes), (len(types), len(utypes))
    bad_t = [(i, (t["class_id"], t["script_idx"]), utypes[i])
             for i, t in enumerate(types)
             if (t["class_id"], t["script_idx"]) != (utypes[i][0], utypes[i][1] if utypes[i][1] is not None else -1)]
    print(f"types iguais: {len(types) - len(bad_t)}/{len(types)}")
    for b in bad_t[:5]:
        print("  diff tipo:", b)
    uobjs = {}
    for o in env.objects:
        uobjs[o.path_id] = (o.byte_start, o.byte_size, getattr(o.type, "class_id", "?"))
    assert len(objects) == len(uobjs), (len(objects), len(uobjs))
    doff = header["data_offset"]
    myclass = [t["class_id"] for t in types]
    for hyp, fn in (("raw", lambda o: o["start"]), ("raw+data", lambda o: o["start"] + doff)):
        bad, badc = [], []
        for o in objects:
            u = uobjs.get(o["path_id"])
            if u is None or (fn(o), o["size"]) != u[:2]:
                bad.append((o["path_id"], (fn(o), o["size"]), u[:2] if u else None))
            elif u[2] != "?" and u[2] != (myclass[o["type_id"]] if 0 <= o["type_id"] < len(myclass) else None):
                badc.append((o["path_id"], myclass[o["type_id"]], u[2]))
        print(f"objects ({hyp}): start/size {len(objects) - len(bad)}/{len(objects)}, classe {len(objects) - len(badc)}/{len(objects)}")
        for b in (bad + badc)[:6]:
            print("  diff:", b)
    uext = [str(getattr(e, "path", "?")) for e in sf.externals]
    mext = [e.get("path") for e in externals if "path" in e]
    print("externals unity:", uext)
    print("externals meus:", mext, "| parse ok:", ext_ok)
    ok = not bad_t and ext_ok and mext == uext and not bad and not badc  # bad/badc finais = hipótese raw+data
    # hipótese vencedora: start relativo ao data_offset (provado acima)
    if ok:
        out = args.out or f"docs/sfindex_{args.file}.json"
        Path(out).write_text(json.dumps({"file": args.file, "header": header,
                                         "unity_version": uver, "target_platform": plat,
                                         "types": types, "script_refs": script_refs,
                                         "externals": mext, "objects": objects}, indent=1),
                             encoding="utf-8")
        print("OK 100% ->", out)
        return 0
    print("FALHA — ver diffs acima")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
