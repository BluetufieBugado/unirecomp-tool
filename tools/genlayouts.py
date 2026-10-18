#!/usr/bin/env python3
"""Extrai layouts de fields serializados dos scripts (.cs -> JSON).

Regras Unity: serializa fields de instância públicos + privados com
[SerializeField]. Pula static/const/properties/métodos/[NonSerialized].
Serve de especificação para o SceneLoader (C#) desserializar MonoBehaviours
de builds stripped (sem type trees).

Uso:
  python tools/genlayouts.py --scripts "Stardust Raiders/Assets/Scripts" --out docs/layouts_stardust.json
"""
import argparse
import json
import re
from pathlib import Path

PRIMS = {"bool", "byte", "sbyte", "char", "short", "ushort", "int", "uint",
         "float", "double", "long", "ulong", "string"}
ALIAS = {"Boolean": "bool", "Byte": "byte", "SByte": "sbyte", "Char": "char",
         "Int16": "short", "UInt16": "ushort", "Int32": "int", "UInt32": "uint",
         "Single": "float", "Double": "double", "Int64": "long", "UInt64": "ulong",
         "String": "string"}
UNITY_STRUCTS = {"Vector2", "Vector3", "Vector4", "Quaternion", "Color", "Color32",
                 "Rect", "Bounds", "Matrix4x4", "Ray", "RaycastHit", "Keyframe",
                 "ContactPoint", "LayerMask"}
UNITY_ENUMS = {"KeyCode", "Space", "WrapMode", "TouchPhase", "RenderMode", "TextAnchor",
               "ForceMode", "RuntimePlatform", "LoadSceneMode", "HideFlags", "PrimitiveType",
                 "FontStyle", "HorizontalWrapMode", "VerticalWrapMode", "TextAlignment",
                 "TextAnchor"}
UNITY_OBJECTS = {"Object", "GameObject", "Component", "Behaviour", "MonoBehaviour",
                 "Transform", "RectTransform", "Rigidbody", "Rigidbody2D", "Collider",
                 "Collider2D", "BoxCollider", "BoxCollider2D", "SphereCollider",
                 "CapsuleCollider", "MeshCollider", "CharacterController",
                 "MeshFilter", "MeshRenderer", "SkinnedMeshRenderer", "Renderer",
                 "Camera", "Light", "AudioSource", "AudioListener", "AudioClip",
                 "Animation", "Animator", "Material", "Shader", "Texture", "Texture2D",
                 "Sprite", "Font", "Mesh", "PhysicMaterial", "PhysicMaterial2D",
                 "TextAsset", "AnimationClip", "RuntimeAnimatorController",
                 "ParticleSystem", "ParticleSystemRenderer", "TrailRenderer",
                 "LineRenderer", "SpriteRenderer", "Canvas", "CanvasRenderer",
                 "Graphic", "MaskableGraphic", "Text", "Image", "RawImage", "Button",
                 "Slider", "Scrollbar", "ScrollRect", "Dropdown", "Toggle", "InputField",
                 "EventSystem", "EventTrigger", "CanvasScaler", "CanvasGroup",
                 "RectTransformUtility", "AnimationCurve", "Gradient",
                 "AudioMixer", "AudioMixerGroup", "AudioMixerSnapshot"}
# AnimationCurve/Gradient são classes serializadas INLINE (não PPtr) — mas o
# leitor 2b ainda não as implementa: marcadas como unsupported abaixo.
OPAQUE = {"AnimationCurve", "Gradient", "GradientColorKey", "GradientAlphaKey",
          "RectOffset", "Vector2Int", "Vector3Int", "RangeInt", "ColorUsage",
          "ExposedReference", "SceneAsset", "DefaultAsset"}
BEHAVIOUR_BASES = {"MonoBehaviour", "Component", "Behaviour", "ScriptableObject",
                   "UIBehaviour", "Selectable", "MaskableGraphic", "Graphic",
                   "BaseRaycaster", "BaseInput", "BaseInputModule", "TMP_Text",
                   "TMP_InputField", "TMP_Dropdown"}

FIELD_RE = re.compile(
    r"^(public|private|protected|internal)\s+"
    r"((?:static|const|readonly|volatile|new|sealed|override|extern)\s+)*"
    r"([\w\.<>,\[\]\s\?]+?)\s+(\w+)\s*(=[^;]*)?;\s*$")
CLASS_RE = re.compile(
    r"^\s*(public\s+|private\s+|protected\s+|internal\s+)?"
    r"(sealed\s+|abstract\s+|partial\s+|static\s+)*"
    r"(class|struct|interface|enum)\s+(\w+)\s*(?::\s*([^{]+))?(\{)?\s*$")
ATTR_RE = re.compile(r"^\s*\[(\w+)")


def strip_line(s):
    out, i, n = [], 0, len(s)
    while i < n:
        if s[i] == '"':
            i += 1
            while i < n and s[i] != '"':
                i += 2 if s[i] == "\\" else 1
            i += 1
        elif s[i] == "/" and i + 1 < n and s[i + 1] == "/":
            break
        else:
            out.append(s[i])
            i += 1
    return "".join(out)


def short(t):
    return t.split(".")[-1].strip()


def split_generic_args(s):
    depth, cur, parts = 0, "", []
    for ch in s:
        if ch == "<":
            depth += 1
            cur += ch
        elif ch == ">":
            depth -= 1
            cur += ch
        elif ch == "," and depth == 0:
            parts.append(cur.strip())
            cur = ""
        else:
            cur += ch
    if cur.strip():
        parts.append(cur.strip())
    return parts


class Layouter:
    def __init__(self):
        self.classes = {}   # nome -> {base, serializable, struct, file, line}
        self.enums = set()
        self.scripts = {}
        self.nested = {}
        self.skipped = []
        self.unknown_types = set()

    # ---- passada 1: classes, structs, enums ----
    def scan_types(self, root):
        for cs in sorted(root.rglob("*.cs")):
            if "Editor" in cs.parts:
                continue
            try:
                text = cs.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            pending_serial = False
            for i, raw in enumerate(text.splitlines(), 1):
                line = strip_line(raw).strip()
                if not line:
                    continue
                am = ATTR_RE.match(line)
                if am and am.group(1) in ("Serializable", "SerializeField", "System"):
                    if am.group(1) in ("Serializable", "System") and "Serializable" in line.split("]")[0]:
                        pending_serial = True
                    continue
                cm = CLASS_RE.match(line)
                if cm:
                    kind, name, bases = cm.group(3), cm.group(4), (cm.group(5) or "")
                    if kind == "enum":
                        self.enums.add(name)
                    else:
                        base = short(split_generic_args(bases)[0]) if bases else ""
                        if name in self.classes:
                            self.classes[name].setdefault("dup", []).append(str(cs))
                        else:
                            self.classes[name] = {"base": base, "serializable": pending_serial,
                                                  "struct": kind == "struct",
                                                  "file": cs.as_posix(), "line": i}
                        pending_serial = False
                    continue
                if line and not line.startswith(("[", "#", "/")):
                    pending_serial = False

    def kind_of(self, t):
        t = ALIAS.get(t, t)
        if t in PRIMS:
            return {"kind": t}
        if t in self.enums or t in UNITY_ENUMS:
            return {"kind": "enum", "type": t}
        if t in UNITY_STRUCTS:
            return {"kind": t.lower().replace("vector", "vector")}
        if t in OPAQUE:
            return {"kind": "unsupported", "why": "opaque-unity-type"}
        if t in UNITY_OBJECTS or t in BEHAVIOUR_BASES:
            return {"kind": "pptr", "target": t}
        if t in self.classes:
            c = self.classes[t]
            # MonoBehaviour com [Serializable] (ex. Sector) continua PPtr: a
            # Unity serializa referência de MB como PPtr, nunca inline.
            if self.is_behaviour(t):
                return {"kind": "pptr", "target": t}
            if c["struct"] or c["serializable"]:
                return {"kind": "nested", "type": t}
            self.unknown_types.add(f"class:{t}(base {c['base']})")
            return {"kind": "unsupported", "why": f"plain-class {t}"}
        # genéricos / arrays
        if t.endswith("[]"):
            inner = self.kind_of(short(t[:-2]))
            if inner.get("kind") == "unsupported":
                return inner
            return {"kind": "array", "of": inner}
        if "<" in t and t.endswith(">"):
            outer, argstr = t.split("<", 1)
            outer, args = short(outer), split_generic_args(argstr[:-1])
            if outer == "List" and len(args) == 1:
                inner = self.kind_of(short(args[0]))
                if inner.get("kind") == "unsupported":
                    return inner
                return {"kind": "array", "of": inner}
            return {"kind": "unsupported", "why": f"generic {t}"}
        self.unknown_types.add(t)
        return {"kind": "unsupported", "why": f"unknown {t}"}

    # ---- passada 2: fields ----
    def scan_fields(self, root):
        for cs in sorted(root.rglob("*.cs")):
            if "Editor" in cs.parts:
                continue
            try:
                text = cs.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            pending_attrs, stack = [], []
            depth = 0
            for i, raw in enumerate(text.splitlines(), 1):
                line = strip_line(raw)
                s = line.strip()
                am = ATTR_RE.match(s)
                if am:
                    pending_attrs.append(am.group(1))
                    depth += s.count("{") - s.count("}")
                    continue
                cm = CLASS_RE.match(s)
                if cm and cm.group(3) in ("class", "struct"):
                    stack.append({"name": cm.group(4), "depth": depth,
                                  "opened": "{" in s,
                                  "fields": [], "unsupported": [],
                                  "file": cs.as_posix(), "line": i,
                                  "base": (self.classes.get(cm.group(4), {}) or {}).get("base", "")})
                fm = FIELD_RE.match(s)
                if fm and stack:
                    vis, mods = fm.group(1), fm.group(2) or ""
                    typ_raw, name0 = fm.group(3), fm.group(4)
                    names = [name0]
                    typ = typ_raw
                    if "," in typ_raw and "<" not in typ_raw:
                        # declaração múltipla: public bool x, y, z; (vírgula pode
                        # grudar no tipo via backtracking: 'bool x, y,')
                        core = typ_raw.strip().rstrip(",").strip()
                        chunks = [c.strip() for c in (core + "," + name0).split(",")]
                        words = chunks[0].split()
                        typ = " ".join(words[:-1])
                        names = [words[-1]] + [c for c in chunks[1:] if c]
                    typ = short(typ)
                    is_static = "static" in mods.split() or "const" in mods.split()
                    is_readonly = "readonly" in mods.split()
                    has_sf = "SerializeField" in pending_attrs
                    nonser = "NonSerialized" in pending_attrs
                    cur = stack[-1]
                    if is_static or is_readonly or nonser:
                        for name in names:
                            self.skipped.append({"script": cur["name"], "field": name,
                                                 "reason": "static/const/readonly" if (is_static or is_readonly) else "NonSerialized",
                                                 "line": i, "public": vis == "public"})
                    elif vis == "public" or has_sf:
                        for name in names:
                            k = self.kind_of(typ)
                            entry = {"name": name, "type": typ, "line": i}
                            entry.update(k)
                            if k.get("kind") == "unsupported":
                                cur["unsupported"].append(entry)
                            else:
                                cur["fields"].append(entry)
                    else:
                        for name in names:
                            self.skipped.append({"script": cur["name"], "field": name,
                                                 "reason": "private", "line": i, "public": False})
                if s and not s.startswith("#"):
                    pending_attrs = []
                if stack and not stack[-1].get("opened") and "{" in s:
                    stack[-1]["opened"] = True  # o { da própria classe (pode vir na linha seguinte)
                depth += s.count("{") - s.count("}")
                while stack and stack[-1].get("opened") and depth <= stack[-1]["depth"]:
                    done = stack.pop()
                    self.store_class(done)

    def is_behaviour(self, name):
        """MonoBehaviour direto ou via cadeia (BossController : EnemyController)."""
        seen, cur = set(), name
        while cur and cur not in seen:
            seen.add(cur)
            if cur in BEHAVIOUR_BASES:
                return True
            nxt = (self.classes.get(cur, {}) or {}).get("base", "")
            nxt = short(nxt.split(",")[0]).split("<")[0].strip()
            if not nxt or nxt in UNITY_STRUCTS or nxt in self.enums:
                return False
            if nxt in UNITY_OBJECTS:
                return nxt in ("MonoBehaviour", "Component", "Behaviour", "Transform", "RectTransform")
            cur = nxt
        return False

    def store_class(self, done):
        rec = {"file": done["file"], "line": done["line"],
               "base": done.get("base") or (self.classes.get(done["name"], {}) or {}).get("base", ""),
               "fields": done["fields"], "unsupported": done["unsupported"]}
        if self.is_behaviour(done["name"]):
            self.scripts[done["name"]] = rec
        else:
            key = done["name"]
            if key in self.nested and self.nested[key]["file"] != done["file"]:
                key = f"{done['file'].split('/')[-1]}::{done['name']}"
            self.nested[key] = rec


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--scripts", required=True)
    ap.add_argument("--out", default="docs/layouts_stardust.json")
    args = ap.parse_args()
    root = Path(args.scripts)
    lay = Layouter()
    lay.scan_types(root)
    lay.scan_fields(root)
    full = [s for s, r in lay.scripts.items() if not r["unsupported"]]
    partial = {s: r["unsupported"] for s, r in lay.scripts.items() if r["unsupported"]}
    pub_skipped = [s for s in lay.skipped if s["public"] and s["reason"] != "static/const/readonly"]
    report = {"scripts_total": len(lay.scripts), "scripts_full": len(full),
              "scripts_partial": sorted(partial), "nested": len(lay.nested),
              "enums": len(lay.enums),
              "public_skipped_suspicious": pub_skipped,
              "unknown_types": sorted(lay.unknown_types)}
    Path(args.out).write_text(json.dumps({"scripts": lay.scripts, "nested": lay.nested,
                                          "enums": sorted(lay.enums), "skipped": lay.skipped,
                                          "report": report}, indent=1, ensure_ascii=False),
                              encoding="utf-8")
    print(f"scripts: {len(lay.scripts)} (full: {len(full)}, partial: {len(partial)}), "
          f"nested: {len(lay.nested)}, enums: {len(lay.enums)}")
    print("partial:", sorted(partial) or "-")
    print("public skipped (revisar!):", pub_skipped or "-")
    print("unknown types:", sorted(lay.unknown_types) or "-")
    print("salvo em", args.out)


if __name__ == "__main__":
    raise SystemExit(main())
