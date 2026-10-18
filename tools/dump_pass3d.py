#!/usr/bin/env python3
"""Pass 3D offline: malhas + links MeshFilter->Mesh + câmeras de um level.

Gera um JSON que o host aplica após o SceneBuilder (sem parsear o formato
Mesh 2018.1 em C# nesta fatia).

Uso:
  python tools/dump_pass3d.py --data "..._Data" --file level0 --out docs/pass3d_level0.json
"""
import argparse
import json
import struct
from pathlib import Path

import UnityPy


def extract_mesh(o):
    d = o.read()
    vd = d.m_VertexData
    n = int(vd.m_VertexCount)
    raw = bytes(vd.m_DataSize)
    channels = list(vd.m_Channels)
    pos_off = int(channels[0].offset)
    # canal 1 = normal (dim 3, float32); ausente em algumas malhas
    nrm_off = None
    if len(channels) > 1 and int(channels[1].dimension) == 3:
        nrm_off = int(channels[1].offset)
    stride = 0
    for ch in channels:
        if int(ch.dimension) == 0:
            continue
        stride = max(stride, int(ch.offset) + int(ch.dimension) * 4)
    if n <= 0 or stride <= 0 or len(raw) < n * stride:
        return None
    verts = []
    normals = []
    for i in range(n):
        x, y, z = struct.unpack_from("<3f", raw, i * stride + pos_off)
        verts += [float(x), float(y), float(z)]
        if nrm_off is not None:
            nx, ny, nz = struct.unpack_from("<3f", raw, i * stride + nrm_off)
            normals += [float(nx), float(ny), float(nz)]
    ib = bytes(d.m_IndexBuffer) if d.m_IndexBuffer else b""
    if int(d.m_IndexFormat) == 0:
        idx = list(struct.unpack(f"<{len(ib)//2}H", ib)) if ib else []
    else:
        idx = list(struct.unpack(f"<{len(ib)//4}I", ib)) if ib else []
    return {"verts": verts, "normals": normals, "tris": idx}


def clean(v, depth=0):
    if depth > 5:
        return "..."
    if isinstance(v, dict):
        return {str(k): clean(x, depth + 1) for k, x in v.items()}
    if isinstance(v, (list, tuple)):
        return [clean(x, depth + 1) for x in v]
    if isinstance(v, (bytes, bytearray)):
        return f"<{len(v)}B>"
    if hasattr(v, "path_id"):
        return {"fileID": getattr(v, "file_id", 0), "pathID": v.path_id}
    if isinstance(v, (str, int, float, bool)) or v is None:
        return v
    return str(v)[:120]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", required=True)
    ap.add_argument("--file", default="level0")
    ap.add_argument("--out", default=None)
    ap.add_argument("--max-verts", type=int, default=60000)
    args = ap.parse_args()
    env = UnityPy.load(str(Path(args.data)))
    objs = [o for o in env.objects
            if getattr(getattr(o, "assets_file", None), "name", "?") == args.file]
    by_type = {}
    for o in objs:
        by_type.setdefault(o.type.name, []).append(o)

    meshes, skipped = {}, []
    for o in by_type.get("Mesh", []):
        m = extract_mesh(o)
        if m is None or len(m["verts"]) // 3 > args.max_verts:
            skipped.append(o.path_id)
            continue
        meshes[str(o.path_id)] = m

    links = []
    for o in by_type.get("MeshFilter", []):
        try:
            t = clean(o.read_typetree())
            mp = t.get("m_Mesh", {}) or {}
            links.append({"filter": o.path_id,
                          "mesh": mp.get("m_PathID", mp.get("pathID", 0)),
                          "meshFile": mp.get("m_FileID", mp.get("fileID", 0))})
        except Exception as e:
            links.append({"filter": o.path_id, "error": str(e)[:100]})

    # Submeshes por malha: faixas de índices em ELEMENTOS (host desenha cada
    # faixa com a cor do material correspondente).
    for o in by_type.get("Mesh", []):
        key = str(o.path_id)
        if key not in meshes:
            continue
        try:
            t = o.read_typetree()
            fmt = int(t.get("m_IndexFormat", 1))
            elem = 2 if fmt == 0 else 4
            ranges = []
            for sm in t.get("m_SubMeshes", []) or []:
                first = int(sm.get("firstByte", 0)) // elem
                count = int(sm.get("indexCount", 0)) // elem
                if count > 0:
                    ranges.append([first, count])
            if ranges:
                meshes[key]["ranges"] = ranges
        except Exception:
            pass

    # Materiais por MeshRenderer (chave: path do MeshFilter irmão): cores
    # _Color (fallback _Color1, ex. toon Prototype) resolvidas cross-file.
    level_file = objs[0].assets_file if objs else None
    externals = []
    try:
        externals = list(level_file.externals) if level_file is not None else []
    except Exception:
        pass

    def ext_name(i):
        # fileID 1-based sobre externals (0 = arquivo local)
        if 1 <= i <= len(externals):
            e = externals[i - 1]
            for attr in ("name", "asset_name", "assetName", "path"):
                v = getattr(e, attr, None)
                if v:
                    return str(v)
            s = str(e)
            return s
        return None

    def norm(n):
        n = (n or "").lower().replace("\\", "/").split("/")[-1]
        return n[:-7] if n.endswith(".assets") else n

    mat_by_file_path = {}
    for o in env.objects:
        if o.type.name != "Material":
            continue
        try:
            fn = getattr(getattr(o, "assets_file", None), "name", "?") or "?"
        except Exception:
            fn = "?"
        mat_by_file_path.setdefault(norm(fn), {})[o.path_id] = o

    def mat_color(file_id, path_id):
        white = [1.0, 1.0, 1.0, 1.0]
        try:
            if file_id == 0:
                cand_files = [norm(getattr(level_file, "name", ""))]
            else:
                cand_files = [norm(ext_name(file_id))]
            o = None
            for cf in cand_files:
                bucket = mat_by_file_path.get(cf, {})
                if path_id in bucket:
                    o = bucket[path_id]
                    break
            if o is None:  # fallback: qualquer arquivo com esse path
                for bucket in mat_by_file_path.values():
                    if path_id in bucket:
                        o = bucket[path_id]
                        break
            if o is None:
                return white
            t = o.read_typetree()
            cols = {}
            sp = t.get("m_SavedProperties", {}) or {}
            for entry in sp.get("m_Colors", []) or []:
                try:
                    cols[entry[0]] = entry[1]
                except Exception:
                    pass
            # Toon (ex. Prototype): _Color branco + rampa em _Color1 — usa a
            # base real quando _Color é só identidade.
            def whiteish(c):
                return c is not None and c.get("r") == 1 and c.get("g") == 1 and c.get("b") == 1
            c0, c1 = cols.get("_Color"), cols.get("_Color1")
            pick = c1 if (c1 is not None and not whiteish(c1) and whiteish(c0)) else (c0 if c0 is not None else c1)
            if pick is not None:
                return [float(pick.get("r", 1)), float(pick.get("g", 1)),
                        float(pick.get("b", 1)), float(pick.get("a", 1))]
            return white
        except Exception:
            return white

    go_of_filter = {}
    for o in by_type.get("MeshFilter", []):
        try:
            t = o.read_typetree()
            go_of_filter[o.path_id] = (t.get("m_GameObject") or {}).get("m_PathID")
        except Exception:
            pass
    renderer_links = []
    for o in by_type.get("MeshRenderer", []):
        try:
            t = o.read_typetree()
            go = (t.get("m_GameObject") or {}).get("m_PathID")
            filt = next((f for f, g in go_of_filter.items() if g == go), 0)
            colors = [mat_color(m.get("m_FileID", 0), m.get("m_PathID", 0))
                      for m in (t.get("m_Materials", []) or [])]
            renderer_links.append({"filter": filt, "go": go, "colors": colors})
        except Exception as e:
            renderer_links.append({"filter": 0, "error": str(e)[:100]})

    cameras = []
    for o in by_type.get("Camera", []):
        try:
            t = clean(o.read_typetree())
            go = t.get("m_GameObject", {}) or {}
            cameras.append({"path": o.path_id,
                            "go": go.get("m_PathID", go.get("pathID", 0)),
                            "fov": float(t.get("field of view", 60.0)),
                            "near": float(t.get("near clip plane", 0.3)),
                            "far": float(t.get("far clip plane", 1000.0)),
                            "depth": float(t.get("m_Depth", 0.0)),
                            "ortho": bool(t.get("orthographic", False)),
                            "bg": [t.get("m_BackGroundColor", {}).get("r", 0),
                                   t.get("m_BackGroundColor", {}).get("g", 0),
                                   t.get("m_BackGroundColor", {}).get("b", 0)]})
        except Exception as e:
            cameras.append({"path": o.path_id, "error": str(e)[:100]})

    print(f"meshes={len(meshes)} skip={len(skipped)} links={len(links)} cameras={len(cameras)} matlinks={len(renderer_links)}")
    out = args.out or f"docs/pass3d_{args.file}.json"
    Path(out).write_text(json.dumps(
        {"file": args.file, "meshes": meshes,
         "meshLinks": links, "cameras": cameras,
         "rendererLinks": renderer_links}, indent=1), encoding="utf-8")
    print("OK ->", out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
