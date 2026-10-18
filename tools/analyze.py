#!/usr/bin/env python3
"""unirecomp analyze - perfil de jogo Unity Mono (MVP, stdlib only)."""
import argparse
import json
import re
import sys
from pathlib import Path

API_PATTERNS = [
    "Transform", "Destroy", r"Instantiate", r"DontDestroyOnLoad",
    r"Vector3", r"Quaternion", r"Input\.", r"KeyCode",
    r"Rigidbody", r"Collider", r"Collision", r"Trigger", r"Physics\.",
    r"AudioSource", r"AudioClip", r"ParticleSystem", r"Animator",
    r"Camera", r"Gizmos", r"Resources\.Load", r"SceneManager",
    r"PlayerPrefs", r"Canvas", r"\bButton\b", r"\bImage\b", r"\bText\b",
    r"TextMeshPro", r"Application\.", r"Time\.",
]
API_RE = re.compile(r"(" + "|".join(API_PATTERNS) + r")")
NET_RE = re.compile(r"NetworkBehaviour|NetworkManager|Photon|Mirror|UNet|Steamworks")
RISK_RE = re.compile(r"DllImport|System\.IO|System\.Net|Sockets|UnityWebRequest|\bWWW\b|FMOD")
EDITOR_RE = re.compile(r"using UnityEditor|from UnityEditor")
VERSION_RE = re.compile(r"\d+\.\d+\.\d+[fabp]\d+")
EDITOR_VERSION_RE = re.compile(r"m_EditorVersion\s*:\s*(\S+)")


def read_editor_version(project: Path):
    f = project / "ProjectSettings" / "ProjectVersion.txt"
    if not f.is_file():
        return None
    m = EDITOR_VERSION_RE.search(f.read_text(encoding="utf-8", errors="replace"))
    return m.group(1) if m else None


def read_build_version(data_dir: Path):
    for name in ("globalgamemanagers", "globalgamemanagers.assets"):
        f = data_dir / name
        if f.is_file():
            raw = f.read_bytes().decode("ascii", errors="ignore")
            found = sorted(set(VERSION_RE.findall(raw)))
            # prefere a versao de editor (2018.x) se houver varias
            pref = [v for v in found if v.startswith("2018.") or v.startswith("2017.") or v.startswith("5.")]
            return (pref or found or [None])[0], found
    return None, []


def scan_scripts(project: Path):
    scripts_dir = project / "Assets" / "Scripts"
    roots = [scripts_dir if scripts_dir.is_dir() else project / "Assets"]
    files = []
    for root in roots:
        files += sorted(root.rglob("*.cs"))
    # dedup (caso Assets contenha Scripts)
    files = sorted(set(files))
    api_counts: dict[str, int] = {}
    editor_files: list[str] = []
    net_hits: list[str] = []
    risk_hits: list[dict] = []
    for cs in files:
        try:
            text = cs.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        rel = cs.relative_to(project).as_posix()
        for m in API_RE.finditer(text):
            api_counts[m.group(1)] = api_counts.get(m.group(1), 0) + 1
        if EDITOR_RE.search(text):
            editor_files.append(rel)
        if NET_RE.search(text):
            net_hits.append(rel)
        for i, line in enumerate(text.splitlines(), 1):
            if RISK_RE.search(line):
                risk_hits.append({"file": rel, "line": i, "text": line.strip()[:120]})
                if len(risk_hits) >= 20:
                    break
    return files, api_counts, editor_files, net_hits, risk_hits


def scan_assets(project: Path):
    assets = project / "Assets"
    shaders = sorted(p.relative_to(project).as_posix() for p in assets.rglob("*.shader")) if assets.is_dir() else []
    scenes_all = sorted(p.relative_to(project).as_posix() for p in assets.rglob("*.unity")) if assets.is_dir() else []
    # separa cenas de exemplo de terceiros (TextMeshPro) das cenas reais do jogo
    scenes_game = [s for s in scenes_all if "TextMesh Pro" not in s and "Examples" not in s]
    return shaders, scenes_all, scenes_game


def scan_build(build: Path):
    # aceita tanto a raiz do build (com *_Data) quanto a pasta *_Data direto
    data_dir = build
    if not (build / "Managed").is_dir():
        cands = [d for d in build.iterdir() if d.is_dir() and d.name.endswith("_Data")] if build.is_dir() else []
        if len(cands) == 1:
            data_dir = cands[0]
    info: dict = {"data_dir": str(data_dir), "managed": [], "native_plugins": [],
                  "levels": [], "asset_files": [], "is_mono": False, "is_il2cpp": False}
    managed = data_dir / "Managed"
    if managed.is_dir():
        for dll in sorted(managed.glob("*.dll")):
            info["managed"].append({"name": dll.name, "kb": round(dll.stat().st_size / 1024)})
        names = {m["name"] for m in info["managed"]}
        info["is_mono"] = "Assembly-CSharp.dll" in names
        info["is_il2cpp"] = "GameAssembly.dll" in names or (data_dir.parent / "GameAssembly.dll").is_file()
    if data_dir.is_dir():
        for dll in sorted(data_dir.rglob("*.dll")):
            if "Managed" not in dll.parts:
                try:
                    rel = dll.relative_to(data_dir).as_posix()
                except ValueError:
                    rel = dll.name
                info["native_plugins"].append(rel)
        for lv in sorted(data_dir.glob("level*")):
            info["levels"].append(lv.name)
        for a in sorted(data_dir.glob("*.assets")) + sorted(data_dir.glob("sharedassets*")):
            if a.is_file():
                info["asset_files"].append({"name": a.name, "mb": round(a.stat().st_size / 1024 / 1024, 2)})
        info["asset_files"] = sorted(info["asset_files"], key=lambda x: -x["mb"])[:10]
    ver, all_vers = read_build_version(data_dir)
    info["build_version"] = ver
    info["build_versions_all"] = all_vers
    return info


def main() -> int:
    ap = argparse.ArgumentParser(description="Analisa jogo Unity Mono para o UniRecomp")
    ap.add_argument("--project", required=True, help="pasta do projeto Unity aberto (com Assets/ e ProjectSettings/)")
    ap.add_argument("--build", required=True, help="raiz do build Windows OU pasta *_Data")
    ap.add_argument("--out", default="profile.json", help="arquivo JSON de saida")
    args = ap.parse_args()

    project = Path(args.project)
    build = Path(args.build)
    if not project.is_dir():
        print(f"ERRO: projeto nao encontrado: {project}", file=sys.stderr)
        return 1
    if not build.is_dir():
        print(f"ERRO: build nao encontrado: {build}", file=sys.stderr)
        return 1

    editor_version = read_editor_version(project)
    files, api_counts, editor_files, net_hits, risk_hits = scan_scripts(project)
    shaders, scenes_all, scenes_game = scan_assets(project)
    build_info = scan_build(build)
    versions_match = bool(editor_version and build_info.get("build_version")
                          and editor_version == build_info["build_version"])

    asm_csharp_kb = next((m["kb"] for m in build_info["managed"] if m["name"] == "Assembly-CSharp.dll"), None)

    profile = {
        "project": str(project),
        "editor_version": editor_version,
        "build_version": build_info.get("build_version"),
        "versions_match": versions_match,
        "is_mono": build_info["is_mono"],
        "is_il2cpp": build_info["is_il2cpp"],
        "assembly_csharp_kb": asm_csharp_kb,
        "scripts_count": len(files),
        "scripts_editor_only": editor_files,
        "net_scripts": net_hits,
        "risk_hits": risk_hits,
        "api_counts": dict(sorted(api_counts.items(), key=lambda kv: -kv[1])[:30]),
        "shaders_custom": shaders,
        "scenes_game": scenes_game,
        "scenes_third_party_count": len(scenes_all) - len(scenes_game),
        "build": build_info,
    }
    Path(args.out).write_text(json.dumps(profile, indent=2, ensure_ascii=False), encoding="utf-8")

    # relatorio humano
    print(f"Editor : {editor_version}")
    print(f"Build  : {build_info.get('build_version')}  (match={versions_match})")
    print(f"Mono={build_info['is_mono']} IL2CPP={build_info['is_il2cpp']}  Assembly-CSharp={asm_csharp_kb}KB  scripts={len(files)}")
    print(f"Plugins nativos fora de Managed: {len(build_info['native_plugins'])}")
    for p in build_info["native_plugins"][:10]:
        print(f"  - {p}")
    print(f"Levels: {', '.join(build_info['levels'])}")
    print(f"Cenas do jogo ({len(scenes_game)}):")
    for s in scenes_game:
        print(f"  - {s}")
    print(f"Shaders custom ({len(shaders)}):")
    for s in shaders:
        print(f"  - {s}")
    print("Top APIs:")
    for k, v in sorted(api_counts.items(), key=lambda kv: -kv[1])[:15]:
        print(f"  {v:5d}  {k}")
    print(f"Scripts com UnityEditor ({len(editor_files)}): {', '.join(editor_files) or '-'}")
    print(f"Scripts com rede ({len(net_hits)}): {', '.join(net_hits) or '-'}")
    print(f"Riscos DllImport/IO/Net ({len(risk_hits)}):")
    for r in risk_hits[:10]:
        print(f"  {r['file']}:{r['line']}: {r['text']}")
    print(f"\nprofile salvo em: {args.out}")

    # veredito MVP
    problems = []
    if not build_info["is_mono"]:
        problems.append("nao e Mono")
    if build_info["is_il2cpp"]:
        problems.append("e IL2CPP")
    if build_info["native_plugins"]:
        problems.append(f"{len(build_info['native_plugins'])} plugin(s) nativo(s)")
    if net_hits:
        problems.append("usa rede")
    if risk_hits:
        problems.append(f"{len(risk_hits)} uso(s) de risco (IO/DllImport)")
    if problems:
        print("VEREDITO: requer atencao -> " + "; ".join(problems))
    else:
        print("VEREDITO: bom candidato para MVP")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
