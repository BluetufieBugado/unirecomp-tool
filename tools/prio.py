#!/usr/bin/env python3
"""unirecomp prio - sugere a ordem de implementacao do runtime.

Cruza docs/profile_*.json (grep no fonte) + docs/apicheck_*.json
(TypeRef/MemberRef do CIL) e agrupa as classes em tiers por
dependencia: Tier 0 nao depende de nada, Tier 3 e polimento.

Uso:
  python tools/prio.py --profile docs/profile_stardust.json --apicheck docs/apicheck_stardust.json --out docs/prio_stardust.json
"""
import argparse
import json
from pathlib import Path

# (tier, titulo, motivo)
TIERS = {
    0: ("Nucleo (object model + math + time)",
        "Sem isso nada funciona: hierarquia GameObject/Transform, tipos matematicos, Time, Debug. Enums e atributos sao gratuitos (so declarar)."),
    1: ("Gameplay (input + fisica + audio + cenas)",
        "O loop do jogo: ler input, mover/colidir, tocar audio, trocar de cena, corotinas. Depende do Tier 0."),
    2: ("Apresentacao (render + UI)",
        "Tudo que desenha na tela: meshes/materiais, uGUI, TextMeshPro, Animator. Depende dos Tiers 0+1."),
    3: ("Sistema/polimento",
        "Resolucao e quality settings. Por ultimo, nao afeta gameplay."),
}

# mapeamento exato tipo -> tier. Chave: nome do tipo (sem namespace) ou "Assembly::Tipo".
_TIER_MAP: dict[str, int] = {}
for _t in ("Object", "GameObject", "Component", "Transform", "MonoBehaviour", "Behaviour",
           "Vector2", "Vector3", "Vector4", "Quaternion", "Mathf", "Matrix4x4", "Random",
           "Color", "Color32", "Bounds", "Ray", "Time", "Debug",
           "SerializeField", "HeaderAttribute", "HideInInspector", "RangeAttribute",
           "SpaceAttribute", "TextAreaAttribute", "RequireComponent", "ExecuteInEditMode",
           "KeyCode", "RuntimePlatform", "Space", "WrapMode", "TouchPhase",
           "LoadSceneMode", "RenderMode", "TextAnchor", "ForceMode"):
    _TIER_MAP[_t] = 0
for _t in ("Input", "Touch", "Physics", "Collider", "Rigidbody", "RaycastHit",
           "AudioSource", "AudioClip", "AudioMixer", "AudioMixerGroup", "AudioMixerSnapshot",
           "Camera", "Resources", "SceneManager", "Scene", "AsyncOperation", "Application",
           "Coroutine", "WaitForSeconds", "WaitForEndOfFrame", "AnimationCurve", "Keyframe"):
    _TIER_MAP[_t] = 1
for _t in ("Renderer", "MeshRenderer", "Mesh", "Material", "Light",
           "RectTransform", "Rect", "Canvas", "RectTransformUtility",
           "Animator", "AnimatorStateInfo", "TextMesh", "Font",
           "Graphic", "Image", "Text", "Button", "Slider", "Scrollbar", "ScrollRect",
           "Dropdown", "Toggle", "Mask", "CanvasGroup",
           "EventSystem", "PointerEventData",
           "UnityAction`1", "UnityAction`2", "UnityAction`3",
           "UnityEvent`1", "UnityEvent`2", "UnityEvent`3",
           "TextMeshPro", "TextMeshProUGUI", "TMP_Text", "TMP_InputField", "TMP_Dropdown",
           "TMP_FontAsset", "TMP_TextInfo", "TMP_InputValidator"):
    _TIER_MAP[_t] = 2
for _t in ("Screen", "Resolution", "QualitySettings", "Gizmos"):
    _TIER_MAP[_t] = 3


def short_name(fq: str) -> str:
    return fq.split(".")[-1]


def tier_of(assembly: str, fq: str) -> int:
    short = short_name(fq)
    if short in _TIER_MAP:
        return _TIER_MAP[short]
    if assembly.startswith("TextMeshPro") or "TMPro" in fq or "TextMeshPro" in fq:
        return 2
    if "EventSystems" in fq or fq.startswith("UnityEngine.UI"):
        return 2
    if "UnityEngine.Events" in fq:
        return 2  # callbacks de UI (onClick) — sem isso botoes nao funcionam
    return 3  # nao classificado: revisar manualmente, comeca pelo fim


def main() -> int:
    ap = argparse.ArgumentParser(description="Prioriza implementacao do runtime em tiers")
    ap.add_argument("--profile", required=True)
    ap.add_argument("--apicheck", required=True)
    ap.add_argument("--out", default="prio.json")
    args = ap.parse_args()

    profile = json.loads(Path(args.profile).read_text(encoding="utf-8"))
    apicheck = json.loads(Path(args.apicheck).read_text(encoding="utf-8"))
    mentions = profile.get("api_counts", {})

    tiers: dict[int, list[dict]] = {0: [], 1: [], 2: [], 3: []}
    for mod, classes in apicheck["by_module"].items():
        for fq, members in classes.items():
            t = tier_of(mod, fq)
            tiers[t].append({"type": fq, "assembly": mod, "members": members,
                             "n_members": len(members)})
    for t in tiers:
        tiers[t].sort(key=lambda c: (-c["n_members"], c["type"]))

    out = {"tiers": [], "unmatched_note": "Tier 3 inclui nao-classificados: revisar manualmente."}
    for tid in (0, 1, 2, 3):
        title, why = TIERS[tid]
        cls = tiers[tid]
        out["tiers"].append({
            "tier": tid, "title": title, "why": why,
            "classes": len(cls), "members": sum(c["n_members"] for c in cls),
            "items": cls,
        })
    Path(args.out).write_text(json.dumps(out, indent=2, ensure_ascii=False), encoding="utf-8")

    for entry in out["tiers"]:
        print(f"\n===== TIER {entry['tier']} — {entry['title']} =====")
        print(f"({entry['why']})")
        print(f"-- {entry['classes']} classes, {entry['members']} membros --")
        for c in entry["items"]:
            tag = " [tipo-only]" if c["n_members"] == 0 else ""
            print(f"  [ ] {c['type']} ({c['n_members']}){tag}")
    print(f"\npriorizacao salva em: {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
