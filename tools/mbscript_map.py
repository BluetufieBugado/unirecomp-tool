#!/usr/bin/env python3
"""Mapeia MonoBehaviour stripped -> (GameObject, script) lendo bytes crus.

Layout base do MonoBehaviour 2018.1 (12B PPtr GO + enabled + 12B PPtr script):
valida cada parse cruzando com os pathIDs conhecidos (GOs do level +
MonoScripts do globalgamemanagers). Cross-check do futuro parser proprio.

Uso:
  python tools/mbscript_map.py --data "..._Data" --out docs/mbmap_stardust.json
"""
import argparse
import json
import struct
from pathlib import Path

import UnityPy


def parse_base(raw):
    """Tenta layout A. Retorna (gof, gop, enabled, scf, scp, name) ou None."""
    if len(raw) < 28:
        return None
    gof, gop = struct.unpack_from("<iq", raw, 0)
    enabled = raw[12]
    scf, scp = struct.unpack_from("<iq", raw, 16)
    name = ""
    if len(raw) >= 32:
        (n,) = struct.unpack_from("<i", raw, 28)
        if 0 <= n < 256 and 32 + n <= len(raw):
            try:
                name = raw[32:32 + n].decode("utf-8", errors="replace")
            except Exception:
                name = ""
    return gof, gop, enabled, scf, scp, name


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", required=True)
    ap.add_argument("--out", default="docs/mbmap_stardust.json")
    args = ap.parse_args()
    data = Path(args.data)

    env = UnityPy.load(str(data))
    by_file = {}
    for obj in env.objects:
        try:
            by_file.setdefault(obj.assets_file.name, []).append(obj)
        except Exception:
            pass
    gm_key = next(k for k in by_file if any(o.type.name == "BuildSettings" for o in by_file[k]))
    mono_ids, mono_name, mono_files = set(), {}, set()
    for k, objs in by_file.items():
        for o in objs:
            if o.type.name != "MonoScript":
                continue
            mono_ids.add((k, o.path_id))
            mono_files.add(k)
            try:
                st = o.read_typetree()
                mono_name[(k, o.path_id)] = f"{st.get('m_AssemblyName','?')}::{st.get('m_ClassName','?')}"
            except Exception:
                pass
    print(f"MonoScripts: {len(mono_ids)} em {sorted(mono_files)}")

    result, total_ok, total = {}, 0, 0
    for lname in sorted(k for k in by_file if k.startswith("level")):
        raw_file = (data / lname).read_bytes()
        gos = set()
        for o in by_file[lname]:
            if o.type.name == "GameObject":
                gos.add(o.path_id)
        try:
            import os
            af = by_file[lname][0].assets_file
            exts = [os.path.basename(str(getattr(e, "path", "?") or "?")) for e in af.externals]
        except Exception as e:
            exts = [f"<externals ilegivel: {e}>"]
        print(f"{lname} externals: {exts}")
        mapped, errors = [], []
        for o in by_file[lname]:
            if o.type.name != "MonoBehaviour":
                continue
            total += 1
            start = getattr(o, "byte_start", None)
            size = getattr(o, "byte_size", None)
            if start is None:
                errors.append({"pathID": o.path_id, "err": "sem byte_start"})
                continue
            raw = raw_file[start:start + (size or 64)]
            p = parse_base(raw)
            if p is None:
                errors.append({"pathID": o.path_id, "err": "curto"})
                continue
            gof, gop, enabled, scf, scp, _ = p
            sfile = exts[scf - 1] if 1 <= scf <= len(exts) else "?"
            script = mono_name.get((sfile, scp), "?")
            ok = (gof == 0 and gop in gos and script != "?")
            if ok:
                total_ok += 1
            mapped.append({"pathID": o.path_id, "go_pathID": gop, "enabled": enabled,
                           "script_file": sfile, "script_pathID": scp,
                           "script": script, "ok": ok})
        result[lname] = {"externals": exts, "mapped": mapped, "errors": errors}
        nok = sum(1 for m in mapped if m["ok"])
        print(f"{lname}: {nok}/{len(mapped)} validos")
    Path(args.out).write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(f"TOTAL: {total_ok}/{total} ({100.0 * total_ok / max(total, 1):.1f}%) -> {args.out}")


if __name__ == "__main__":
    raise SystemExit(main())
