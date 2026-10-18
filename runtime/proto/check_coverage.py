"""Garante que todo membro Tier 0 da checklist existe no prototipo.

Uso: python runtime/proto/check_coverage.py  (a partir da raiz do projeto)
Falha (exit 1) listando o que falta implementar.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
sys.path.insert(0, str(Path(__file__).resolve().parent))
import tier0

OPMAP = {
    "op_Addition": "__add__", "op_Subtraction": "__sub__", "op_Multiply": "__mul__",
    "op_Division": "__truediv__", "op_UnaryNegation": "__neg__",
    "op_Equality": "__eq__", "op_Inequality": "__ne__", "op_Implicit": ("__bool__", "__implicit__"),
}

STATIC = {"Mathf", "Time", "Debug", "Random", "Application"}


def sample(cls_name):
    """Instancia de teste ou None (checar na classe)."""
    cls = getattr(tier0, cls_name)
    if cls_name in STATIC:
        return cls
    try:
        if cls_name == "GameObject":
            return cls("_probe_")
        return cls()
    except TypeError:
        return cls  # .ctor com args (atributos): checa na classe


def exists(obj, member):
    base = member[:-3] if member.endswith("<T>") else member
    if base == ".ctor":
        return callable(obj if isinstance(obj, type) else type(obj))
    if base in OPMAP:
        want = OPMAP[base]
        want = (want,) if isinstance(want, str) else want
        return any(hasattr(obj, w) for w in want)
    if base.startswith("get_") or base.startswith("set_"):
        return hasattr(obj, base[4:])
    return hasattr(obj, base)


def short(fq):
    return fq.split(".")[-1]


def main():
    prio = json.loads((ROOT / "docs" / "prio_stardust.json").read_text(encoding="utf-8"))
    tier0_data = next(t for t in prio["tiers"] if t["tier"] == 0)
    missing, total = [], 0
    for item in tier0_data["items"]:
        cls_name = short(item["type"])
        if not hasattr(tier0, cls_name):
            missing.append(f"{item['type']} (classe ausente)")
            total += len(item["members"])
            continue
        obj = sample(cls_name)
        for m in item["members"]:
            total += 1
            if not exists(obj, m):
                missing.append(f"{item['type']}.{m}")
    print(f"Tier 0: {total - len(missing)}/{total} membros cobertos")
    if missing:
        print("FALTANDO:")
        for m in missing:
            print("  -", m)
        return 1
    print("cobertura Tier 0: 100%")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
