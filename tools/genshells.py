#!/usr/bin/env python3
"""Gera Shells.cs por modulo Unity a partir do apicheck.

Cria apenas os tipos referenciados pelo jogo que AINDA NAO tem implementacao
real no shim (ver IMPLEMENTED). Cascas existem para o assembly do jogo
carregar (Fase 1); membros reais vêm nos Tiers 1-2. Bases/kinds sao
best-effort a partir do conhecimento da API Unity — corrigir na implementacao.

Uso: python tools/genshells.py  (lê docs/apicheck_stardust.json)
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
APICHECK = ROOT / "docs" / "apicheck_stardust.json"
SHIM = ROOT / "runtime" / "shim"

MODULE_DIR = {
    "UnityEngine.CoreModule": "CoreModule",
    "UnityEngine.AnimationModule": "AnimationModule",
    "UnityEngine.AudioModule": "AudioModule",
    "UnityEngine.PhysicsModule": "PhysicsModule",
    "UnityEngine.TextRenderingModule": "TextRenderingModule",
    "UnityEngine.UI": "UI",
    "UnityEngine.UIModule": "UIModule",
    "UnityEngine.ParticleSystemModule": "ParticleSystem",
    "TextMeshPro-2017.3-Runtime": "TextMeshPro",
}

# Tipos com implementacao real em runtime/shim/CoreModule/ (nao gerar casca).
IMPLEMENTED = {
    "Vector2", "Vector3", "Vector4", "Quaternion", "Matrix4x4", "Mathf",
    "Color", "Color32", "Bounds", "Ray", "Keyframe", "Random",
    "Object", "Component", "Behaviour", "MonoBehaviour", "Coroutine",
    "GameObject", "Transform", "Time", "Debug", "Application",
    "WaitForSeconds", "WaitForEndOfFrame",
    "SerializeField", "HeaderAttribute", "HideInInspector", "RangeAttribute",
    "SpaceAttribute", "TextAreaAttribute", "RequireComponent", "ExecuteInEditMode",
    "KeyCode", "Space", "WrapMode", "TouchPhase", "RenderMode", "TextAnchor",
    "ForceMode", "RuntimePlatform", "LoadSceneMode",
    "AsyncOperation", "Scene", "SceneManager",  # reais em CoreModule/Loading.cs
}

# short -> (kind, base, namespace). kind: class|static|struct|enum|interface|
# delegate1|delegate2|delegate3|generic1|generic2|generic3 ; base None = object.
META = {
    "UnityEngine.CoreModule": {
        "AnimationCurve": ("class", "Object", "UnityEngine"),
        "AsyncOperation": ("class", None, "UnityEngine"),
        "Camera": ("class", "Behaviour", "UnityEngine"),
        "Input": ("static", None, "UnityEngine"),
        "Light": ("class", "Behaviour", "UnityEngine"),
        "Material": ("class", "Object", "UnityEngine"),
        "Mesh": ("class", "Object", "UnityEngine"),
        "MeshRenderer": ("class", "Renderer", "UnityEngine"),
        "Renderer": ("class", "Component", "UnityEngine"),
        "RectTransform": ("class", "Transform", "UnityEngine"),
        "Rect": ("struct", None, "UnityEngine"),
        "QualitySettings": ("static", None, "UnityEngine"),
        "Resolution": ("struct", None, "UnityEngine"),
        "Resources": ("static", None, "UnityEngine"),
        "Scene": ("struct", None, "UnityEngine.SceneManagement"),
        "SceneManager": ("static", None, "UnityEngine.SceneManagement"),
        "Screen": ("static", None, "UnityEngine"),
        "Touch": ("struct", None, "UnityEngine"),
        # Descobertos nas cenas (by_type), nao no codigo (gap: type args de
        # genericos como GetComponent<BoxCollider> nao aparecem no apicheck).
        "MeshFilter": ("class", "Component", "UnityEngine"),
        "SpriteRenderer": ("class", "Renderer", "UnityEngine"),
        "TrailRenderer": ("class", "Renderer", "UnityEngine"),
        "FlareLayer": ("class", "Behaviour", "UnityEngine"),
        "LightmapSettings": ("static", None, "UnityEngine"),
        "RenderSettings": ("static", None, "UnityEngine"),
        "UnityAction": ("delegates", None, "UnityEngine.Events"),
        "UnityEvent": ("generics", None, "UnityEngine.Events"),
    },
    "UnityEngine.AnimationModule": {
        "Animator": ("class", "Behaviour", "UnityEngine"),
        "AnimatorStateInfo": ("struct", None, "UnityEngine"),
    },
    "UnityEngine.AudioModule": {
        "AudioMixer": ("class", "Object", "UnityEngine.Audio"),
        "AudioMixerGroup": ("class", "Object", "UnityEngine.Audio"),
        "AudioMixerSnapshot": ("class", "Object", "UnityEngine.Audio"),
        "AudioClip": ("class", "Object", "UnityEngine"),
        "AudioSource": ("class", "Behaviour", "UnityEngine"),
        "AudioListener": ("class", "Behaviour", "UnityEngine"),
    },
    "UnityEngine.PhysicsModule": {
        "Collider": ("class", "Component", "UnityEngine"),
        "Physics": ("static", None, "UnityEngine"),
        "RaycastHit": ("struct", None, "UnityEngine"),
        "Rigidbody": ("class", "Component", "UnityEngine"),
        # Descobertos nas cenas (by_type), nao no codigo:
        "BoxCollider": ("class", "Collider", "UnityEngine"),
        "MeshCollider": ("class", "Collider", "UnityEngine"),
        "SphereCollider": ("class", "Collider", "UnityEngine"),
        "CapsuleCollider": ("class", "Collider", "UnityEngine"),
    },
    "UnityEngine.TextRenderingModule": {
        "Font": ("class", "Object", "UnityEngine"),
        "TextMesh": ("class", "Component", "UnityEngine"),
    },
    "UnityEngine.UI": {
        "EventSystem": ("class", "Behaviour", "UnityEngine.EventSystems"),
        "IEventSystemHandler": ("interface", None, "UnityEngine.EventSystems"),
        "IPointerClickHandler": ("interface", "IEventSystemHandler", "UnityEngine.EventSystems"),
        "IPointerEnterHandler": ("interface", "IEventSystemHandler", "UnityEngine.EventSystems"),
        "IPointerExitHandler": ("interface", "IEventSystemHandler", "UnityEngine.EventSystems"),
        "IPointerUpHandler": ("interface", "IEventSystemHandler", "UnityEngine.EventSystems"),
        "PointerEventData": ("class", None, "UnityEngine.EventSystems"),
        "StandaloneInputModule": ("class", "Behaviour", "UnityEngine.EventSystems"),
        "Dropdown": ("class", "Behaviour", "UnityEngine.UI"),
        "Graphic": ("class", "Behaviour", "UnityEngine.UI"),
        "Image": ("class", "Graphic", "UnityEngine.UI"),
        "ScrollRect": ("class", "Behaviour", "UnityEngine.UI"),
        "Scrollbar": ("class", "Behaviour", "UnityEngine.UI"),
        "Slider": ("class", "Behaviour", "UnityEngine.UI"),
        "Text": ("class", "Graphic", "UnityEngine.UI"),
        "Toggle": ("class", "Behaviour", "UnityEngine.UI"),
        "Button": ("class", "Behaviour", "UnityEngine.UI"),
        "CanvasScaler": ("class", "Behaviour", "UnityEngine.UI"),
        "EventTrigger": ("class", "Behaviour", "UnityEngine.UI"),
        "GraphicRaycaster": ("class", "Behaviour", "UnityEngine.UI"),
        "Mask": ("class", "Behaviour", "UnityEngine.UI"),
        "Outline": ("class", "Behaviour", "UnityEngine.UI"),
        "Shadow": ("class", "Behaviour", "UnityEngine.UI"),
    },
    "UnityEngine.UIModule": {
        "Canvas": ("class", "Behaviour", "UnityEngine"),
        "CanvasRenderer": ("class", "Component", "UnityEngine"),
        "RectTransformUtility": ("static", None, "UnityEngine"),
    },
    "UnityEngine.ParticleSystemModule": {
        "ParticleSystem": ("class", "Component", "UnityEngine"),
        "ParticleSystemRenderer": ("class", "Renderer", "UnityEngine"),
    },
    "TextMeshPro-2017.3-Runtime": {
        "TMP_Asset": ("class", "Object", "TMPro"),
        "TMP_FontAsset": ("class", "TMP_Asset", "TMPro"),
        "TMP_Text": ("class", "Behaviour", "TMPro"),
        "TextMeshPro": ("class", "TMP_Text", "TMPro"),
        "TextMeshProUGUI": ("class", "TMP_Text", "TMPro"),
        "TMP_InputField": ("class", "Behaviour", "TMPro"),
        "TMP_InputField+SubmitEvent": ("nested", "TMP_InputField", "TMPro"),
        "TMP_Dropdown": ("class", "Behaviour", "TMPro"),
        "TMP_InputValidator": ("class", None, "TMPro"),
        "TextContainer": ("class", "Behaviour", "TMPro"),
        "TMP_TextInfo": ("class", None, "TMPro"),
        "TMP_CharacterInfo": ("struct", None, "TMPro"),
        "TMP_LineInfo": ("struct", None, "TMPro"),
        "TMP_LinkInfo": ("struct", None, "TMPro"),
        "TMP_MeshInfo": ("struct", None, "TMPro"),
        "TMP_WordInfo": ("struct", None, "TMPro"),
        "FastAction": ("delegates", None, "TMPro"),
        "ShaderUtilities": ("static", None, "TMPro"),
        "TMPro_EventManager": ("static", None, "TMPro"),
        "TMPro_ExtensionMethods": ("static", None, "TMPro"),
        "TMP_TextUtilities": ("static", None, "TMPro"),
        "TextAlignmentOptions": ("enum", None, "TMPro"),
        "TextContainerAnchors": ("enum", None, "TMPro"),
        "TMP_TextElementType": ("enum", None, "TMPro"),
        "TMP_VertexDataUpdateFlags": ("enum", None, "TMPro"),
    },
}

HEADER = ("// GERADO por tools/genshells.py a partir de docs/apicheck_stardust.json — nao editar a mao.\n"
          "// Cascas para o assembly do jogo carregar (Fase 1). Membros reais vêm nos Tiers 1-2.\n"
          "using UnityEngine;\n")


def short_of(fq):
    return fq.split(".")[-1]


def emit(kind, name, base, arity=1):
    # partial de propósito: membros reais vivem em arquivos de mão
    # (ex. CoreModule/Input.cs) sem brigar com o gerador.
    if kind == "class":
        return f"public partial class {name}" + (f" : {base}" if base else "") + " { }"
    if kind == "static":
        return f"public static partial class {name} {{ }}"
    if kind == "struct":
        return f"public partial struct {name} {{ }}"
    if kind == "enum":
        return f"public enum {name} {{ }}"
    if kind == "interface":
        return f"public partial interface {name}" + (f" : {base}" if base else "") + " { }"
    if kind == "enum":
        return f"public enum {name} {{ }}"
    if kind == "interface":
        return f"public interface {name}" + (f" : {base}" if base else "") + " { }"
    if kind == "delegates":
        ts = ", ".join(f"T{i}" for i in range(arity))
        ps = ", ".join(f"T{i} arg{i}" for i in range(arity))
        return f"public delegate void {name}<{ts}>({ps});"
    if kind == "generics":
        ts = ", ".join(f"T{i}" for i in range(arity))
        return f"public class {name}<{ts}> {{ }}"
    raise ValueError(kind)


def clean(name):
    return name.split("`")[0].split("+")[-1]


def main():
    apicheck = json.loads(APICHECK.read_text(encoding="utf-8"))
    referenced = {}  # (module, fq) -> True
    for mod, classes in apicheck["by_module"].items():
        for fq in classes:
            referenced[(mod, fq)] = True

    total, warned = 0, []
    for mod, folder in MODULE_DIR.items():
        table = META.get(mod, {})
        by_ns, arities = {}, {}
        for (m, fq), _ in referenced.items():
            if m != mod:
                continue
            short = short_of(fq)  # ex: UnityAction`2 | TMP_InputField+SubmitEvent
            if short in IMPLEMENTED or ("+" in short and short.split("+")[-1] in IMPLEMENTED):
                continue
            key = short if short in table else clean(short)
            if key not in table:
                warned.append(f"{mod}::{fq}")
                kind, base, ns = "class", None, fq.rpartition(".")[0] or "UnityEngine"
            else:
                kind, base, ns = table[key]
            if "`" in short:
                arities.setdefault((ns, clean(short)), set()).add(int(short.split("`")[1]))
            if kind == "nested":
                by_ns.setdefault(ns, {}).setdefault("__nested__", []).append((base, short.split("+")[-1]))
            else:
                by_ns.setdefault(ns, {})[clean(short)] = (kind, base)
        # Segunda passada: entradas da tabela descobertas nas cenas (by_type),
        # nao no codigo — o loop acima so ve o apicheck.
        emitted = {(ns, n) for ns, ms in by_ns.items() for n in ms if n != "__nested__"}
        for key, (kind, base, ns) in table.items():
            if "+" in key or "`" in key or kind in ("nested", "delegates", "generics"):
                continue
            if key in IMPLEMENTED or (ns, key) in emitted:
                continue
            by_ns.setdefault(ns, {})[key] = (kind, base)

        lines = [HEADER.rstrip("\n"), ""]
        for ns in sorted(by_ns):
            lines.append(f"namespace {ns}")
            lines.append("{")
            nested_map = {}
            for parent, child in by_ns[ns].pop("__nested__", []):
                nested_map.setdefault(parent, []).append(child)
            for name in sorted(by_ns[ns]):
                kind, base = by_ns[ns][name]
                if kind in ("delegates", "generics"):
                    for a in sorted(arities.get((ns, name), {1})):
                        lines.append(f"    {emit(kind, name, base, a)}")
                        total += 1
                elif name in nested_map:
                    lines.append(f"    public partial class {name}" + (f" : {base}" if kind == "class" and base else ""))
                    lines.append("    {")
                    for child in nested_map[name]:
                        lines.append(f"        public class {child} {{ }}")
                        total += 1
                    lines.append("    }")
                    total += 1
                else:
                    lines.append(f"    {emit(kind, name, base)}")
                    total += 1
            lines.append("}")
            lines.append("")
        out = SHIM / folder / "Shells.cs"
        out.write_text("\n".join(lines), encoding="utf-8")
        print(f"{folder}/Shells.cs")

    if warned:
        print("ATENCAO — tipos sem metadados (emitidos como class/plain):")
        for w in warned:
            print("  -", w)
    print(f"cascas geradas: {total}")


if __name__ == "__main__":
    raise SystemExit(main())
