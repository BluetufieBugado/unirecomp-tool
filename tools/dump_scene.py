#!/usr/bin/env python3
"""Spike asset pipeline: inventario das cenas do build via UnityPy (ground truth).

Carrega a pasta *_Data INTEIRA num env so (resolve refs cross-file: os
MonoScripts moram no globalgamemanagers). Extrai ordem das cenas, hierarquia,
behaviours->script e campos. Vira a especificacao do nosso parser.

Uso:
  python tools/dump_scene.py --data "Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data" --out docs/scenes_stardust.json
"""
import argparse
import json
from collections import Counter
from pathlib import Path

import UnityPy


def clean(v, depth=0):
    if depth > 6:
        return "..."
    if isinstance(v, dict):
        return {str(k): clean(x, depth + 1) for k, x in v.items()}
    if isinstance(v, (list, tuple)):
        return [clean(x, depth + 1) for x in v]
    if isinstance(v, (bytes, bytearray)):
        return f"<{len(v)} bytes>"
    if hasattr(v, "path_id") or hasattr(v, "m_PathID"):
        pid = getattr(v, "path_id", getattr(v, "m_PathID", "?"))
        fid = getattr(v, "file_id", getattr(v, "m_FileID", 0))
        return {"fileID": fid, "pathID": pid}
    if isinstance(v, (str, int, float, bool)) or v is None:
        return v
    return str(v)[:160]


def pptr_id(p):
    try:
        if isinstance(p, dict):
            return p.get("m_FileID", 0), p.get("m_PathID", 0)
        return getattr(p, "file_id", 0), getattr(p, "path_id", 0)
    except Exception:
        return 0, 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", required=True, help="pasta *_Data do build")
    ap.add_argument("--out", default="docs/scenes_stardust.json")
    args = ap.parse_args()
    data = Path(args.data)

    print("carregando _Data inteira (resolve cross-file)...")
    env = UnityPy.load(str(data))
    files = {}
    for obj in env.objects:
        try:
            fname = obj.assets_file.name
        except Exception:
            fname = "?"
        files.setdefault(fname, []).append(obj)
    print(f"{sum(len(v) for v in files.values())} objetos em {len(files)} arquivos")

    gm_key = next((k for k, v in files.items()
                   if any(o.type.name == "BuildSettings" for o in v)), None)
    print("globalgamemanagers:", gm_key)
    build_order = []
    if gm_key:
        for obj in files[gm_key]:
            if obj.type.name == "BuildSettings":
                for how in ("read_typetree", "read"):
                    try:
                        t = getattr(obj, how)()
                        d = dict(t) if not isinstance(t, dict) else t
                        build_order = [s for s in d.get("m_Scenes", []) if isinstance(s, str)]
                        if build_order:
                            break
                    except Exception as e:
                        print(f"  BuildSettings via {how}: {str(e)[:100]}")
    print(f"cenas no BuildSettings ({len(build_order)}):")
    for i, s in enumerate(build_order):
        print(f"  level{i} <- {s}")

    # inventario de scripts (para o parser: pathID -> classe)
    scripts, game_scripts = {}, {}
    for obj in env.objects:
        if obj.type.name != "MonoScript":
            continue
        try:
            st = obj.read_typetree()
            cls, asm = st.get("m_ClassName", "?"), st.get("m_AssemblyName", "?")
            scripts.setdefault(f"{asm}::{cls}", []).append(
                {"file": getattr(obj.assets_file, "name", "?"), "pathID": obj.path_id})
            if asm == "Assembly-CSharp.dll" and not (st.get("m_Namespace", "") or "").startswith("TMPro"):
                game_scripts[cls] = obj.path_id
        except Exception:
            pass
    print(f"MonoScripts: {len(scripts)} distintos, {len(game_scripts)} do jogo")

    level_names = sorted([k for k in files if k.split(".")[0] in
                          (f"level{i}" for i in range(50)) or k.startswith("level")])
    result = {"build_order": build_order, "game_scripts": dict(sorted(game_scripts.items())),
              "levels": []}
    data_out = {}
    for idx, lname in enumerate(level_names):
        by_type = Counter()
        gos, transforms, behaviours = {}, {}, []
        for obj in files[lname]:
            tn = obj.type.name
            by_type[tn] += 1
            try:
                if tn == "GameObject":
                    t = obj.read_typetree()
                    gos[obj.path_id] = {"name": t.get("m_Name"), "active": t.get("m_IsActive", 1),
                                        "tag": t.get("m_Tag", 0), "layer": t.get("m_Layer", 0)}
                elif tn in ("Transform", "RectTransform"):
                    t = obj.read_typetree()
                    _, father = pptr_id(t.get("m_Father"))
                    kids = [pptr_id(k)[1] for k in (t.get("m_Children") or [])]
                    _, goid = pptr_id(t.get("m_GameObject"))
                    transforms[obj.path_id] = {"kind": tn, "go": goid, "father": father, "children": kids,
                                               "pos": clean(t.get("m_LocalPosition")),
                                               "rot": clean(t.get("m_LocalRotation")),
                                               "scale": clean(t.get("m_LocalScale"))}
                elif tn == "MonoBehaviour":
                    goid, tree, err = None, None, None
                    try:
                        t = obj.read_typetree()
                        _, goid = pptr_id(t.get("m_GameObject"))
                        tree = clean(t)
                    except Exception as e:
                        err = str(e)[:90]
                    script, asm, ns = "?", "?", ""
                    try:
                        st = obj.read().m_Script.read_typetree()
                        script, asm = st.get("m_ClassName", "?"), st.get("m_AssemblyName", "?")
                        ns = st.get("m_Namespace", "")
                        if goid is None:
                            try:
                                _, goid = pptr_id(obj.read().m_GameObject)
                            except Exception:
                                pass
                    except Exception:
                        pass
                    behaviours.append({"go": goid, "script": script, "assembly": asm,
                                       "namespace": ns, "pathID": obj.path_id,
                                       "tree": tree, "stripped_error": err})
            except Exception as e:
                print(f"  [{lname}] obj {obj.path_id} ({tn}): {str(e)[:100]}")
        child_gos = set()
        tr_by_id = transforms
        for tr in tr_by_id.values():
            for k in tr["children"]:
                if k in tr_by_id:
                    child_gos.add(tr_by_id[k]["go"])
        go_name = {pid: g["name"] for pid, g in gos.items()}
        roots = sorted({go_name[pid] for pid in go_name if pid not in child_gos})
        named = sum(1 for b in behaviours if b["script"] != "?")
        with_fields = sum(1 for b in behaviours if b["tree"])
        entry = {"file": lname, "build_index": idx,
                 "scene_path": build_order[idx] if idx < len(build_order) else "?",
                 "n_objects": sum(by_type.values()), "by_type": dict(sorted(by_type.items())),
                 "n_gameobjects": len(gos), "n_behaviours": len(behaviours),
                 "scripts_resolved": named, "with_fields": with_fields,
                 "roots": roots,
                 "behaviours": sorted(behaviours, key=lambda b: str(b["script"]))}
        result["levels"].append(entry)
        data_out[str(lname)] = {"gos": {str(k): v for k, v in gos.items()},
                                "transforms": {str(k): v for k, v in transforms.items()}}
        print(f"{lname}: {entry['n_objects']} objs, {len(gos)} GOs, {len(behaviours)} beh "
              f"({named} c/ script, {with_fields} c/ fields), {len(roots)} raizes")

    Path(args.out).write_text(json.dumps(result, indent=1, ensure_ascii=False), encoding="utf-8")
    print("salvo em", args.out)
    Path("docs/scene_data.json").write_text(json.dumps(data_out, indent=1, ensure_ascii=False), encoding="utf-8")
    print("dados por objeto salvos em docs/scene_data.json")


if __name__ == "__main__":
    raise SystemExit(main())
