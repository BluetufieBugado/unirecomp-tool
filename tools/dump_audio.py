#!/usr/bin/env python3
"""Manifesto de áudio offline (fatia A1): clipes + AudioSources por level.

Não decodifica (FSB5/Vorbis vira fatia A2). Gera docs/audio_stardust.json:
  clips: {"asset|path": {name, resource, offset, size, freq, ch, length}}
  levels: {file: {externals: [...], sources: [{src, clip:(file,path)|None,
            volume, pitch, loop, mute, playOnAwake}]}}

Uso:
  python tools/dump_audio.py --data "..._Data" --out docs/audio_stardust.json
"""
import argparse
import json
from pathlib import Path

import UnityPy

LEVELS = ["level0", "level1", "level2", "level3"]


def clean(v, depth=0):
    if depth > 5:
        return "..."
    if isinstance(v, dict):
        return {str(k): clean(x, depth + 1) for k, x in v.items()}
    if isinstance(v, (list, tuple)):
        return [clean(x, depth + 1) for x in v]
    if isinstance(v, (bytes, bytearray)):
        return "<%dB>" % len(v)
    if isinstance(v, (str, int, float, bool)) or v is None:
        return v
    return str(v)[:100]


def pid(p):
    if isinstance(p, dict):
        return p.get("m_FileID", 0), p.get("m_PathID", 0)
    return 0, 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", required=True)
    ap.add_argument("--out", default="docs/audio_stardust.json")
    args = ap.parse_args()
    env = UnityPy.load(str(Path(args.data)))
    byfile = {}
    for o in env.objects:
        try:
            fn = o.assets_file.name
        except Exception:
            fn = "?"
        byfile.setdefault(fn, []).append(o)

    clips = {}
    for o in env.objects:
        if o.type.name != "AudioClip":
            continue
        try:
            fn = o.assets_file.name
        except Exception:
            fn = "?"
        try:
            t = clean(dict(o.read_typetree()))
        except Exception as e:
            print("clip %s typetree fail: %s" % (o.path_id, str(e)[:100]))
            continue
        r = t.get("m_Resource", {}) or {}
        clips["%s|%d" % (fn, o.path_id)] = {
            "name": t.get("m_Name", "?"),
            "resource": r.get("m_Source", ""),
            "offset": r.get("m_Offset", 0),
            "size": r.get("m_Size", 0),
            "freq": t.get("m_Frequency", 44100),
            "ch": t.get("m_Channels", 2),
            "length": t.get("m_Length", 0.0),
            "fmt": t.get("m_CompressionFormat", 1),
        }

    levels = {}
    for lv in LEVELS:
        objs = byfile.get(lv, [])
        # externals na ordem do UnityPy (igual ao sfindex: índice fileID-1)
        exts = []
        for o in objs:
            pass
        env_assets = None
        for o in objs:
            try:
                env_assets = o.assets_file
                break
            except Exception:
                continue
        if env_assets is not None:
            try:
                exts = [str(getattr(e, "path", "?")) for e in env_assets.externals]
            except Exception as e:
                print(lv, "externals fail:", str(e)[:100])
        sources = []
        for o in objs:
            if o.type.name != "AudioSource":
                continue
            try:
                t = clean(dict(o.read_typetree()))
            except Exception as e:
                print(lv, "source %s fail: %s" % (o.path_id, str(e)[:100]))
                continue
            cf, cp = pid(t.get("m_AudioClip", {}) or {})
            if (cf, cp) == (0, 0):
                for k, v in t.items():
                    if k.lower() == "m_audioclip" and isinstance(v, dict):
                        cf, cp = pid(v)
                        break
            g = t.get("m_GameObject", {}) or {}
            sources.append({
                "src": o.path_id,
                "go": g.get("m_PathID", g.get("pathID", 0)),
                "clipFile": cf,
                "clipPath": cp,
                "volume": float(t.get("m_Volume", 1.0)),
                "pitch": float(t.get("m_Pitch", 1.0)),
                "loop": bool(t.get("Loop", False)),
                "mute": bool(t.get("Mute", False)),
                "playOnAwake": bool(t.get("m_PlayOnAwake", True)),
            })
        levels[lv] = {"externals": exts, "sources": sources}

    print("clips=%d levels=%s" % (
        len(clips), {k: len(v["sources"]) for k, v in levels.items()}))
    Path(args.out).write_text(json.dumps(
        {"clips": clips, "levels": levels}, indent=1), encoding="utf-8")
    print("OK ->", args.out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
