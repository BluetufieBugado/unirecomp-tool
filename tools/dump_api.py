#!/usr/bin/env python3
"""unirecomp dump_api - lista o que Assembly-CSharp importa de UnityEngine*.

Equivalente Python do que faria com Mono.Cecil: percorre as tabelas
TypeRef e MemberRef do assembly do jogo (via dnfile) e agrupa por
modulo Unity -> classe -> membro. Vira a checklist do runtime.

Uso:
  pip install dnfile
  python tools/dump_api.py --dll "<jogo>_Data/Managed/Assembly-CSharp.dll" --out docs/apicheck.json
"""
import argparse
import json
import sys
from collections import defaultdict
from pathlib import Path

try:
    import dnfile
except ImportError:
    print("ERRO: falta dependencia 'dnfile'. Rode: pip install dnfile", file=sys.stderr)
    raise SystemExit(1)


def is_unity_assembly(asm: str) -> bool:
    return asm.startswith("Unity") or "TextMesh" in asm or "TMPro" in asm


def scope_assembly(typeref_row) -> str | None:
    """Resolve o AssemblyRef de um TypeRef (segue TypeRef aninhado)."""
    seen = 0
    cur = typeref_row
    while seen < 8:
        scope = cur.ResolutionScope
        table_name = type(scope.table).__name__
        if table_name == "AssemblyRef":
            return str(scope.row.Name)
        if table_name == "TypeRef":
            cur = scope.row  # tipo aninhado: sobe para o enclosing
            seen += 1
            continue
        return None  # ModuleRef (proprio assembly), Module, etc.
    return None


def full_type_name(typeref_row) -> str:
    ns = str(typeref_row.TypeNamespace)
    name = str(typeref_row.TypeName)
    # monta prefixo de tipos aninhados: Outer+Inner
    prefix = []
    seen = 0
    cur = typeref_row
    while seen < 8:
        scope = cur.ResolutionScope
        if type(scope.table).__name__ == "TypeRef":
            cur = scope.row
            prefix.append(str(cur.TypeName))
            seen += 1
        else:
            break
    if prefix:
        name = "+".join(reversed(prefix)) + "+" + name
    return f"{ns}.{name}" if ns else name


def main() -> int:
    ap = argparse.ArgumentParser(description="Checklist UnityEngine a partir do Assembly-CSharp.dll")
    ap.add_argument("--dll", required=True, help="caminho do Assembly-CSharp.dll do jogo")
    ap.add_argument("--out", default="apicheck.json", help="JSON de saida")
    ap.add_argument("--all", action="store_true", help="inclui tambem mscorlib/System no JSON (default: so Unity*)")
    args = ap.parse_args()

    dll = Path(args.dll)
    if not dll.is_file():
        print(f"ERRO: DLL nao encontrada: {dll}", file=sys.stderr)
        return 1

    pe = dnfile.dnPE(str(dll))
    md = pe.net.mdtables

    # 1. TypeRefs (usos de tipos: casts, declaracoes, GetComponent<T>, etc.)
    types: dict[str, dict] = {}
    for t in md.TypeRef:
        asm = scope_assembly(t)
        if asm is None:
            continue
        if not args.all and not is_unity_assembly(asm):
            continue
        fq = full_type_name(t)
        key = f"{asm}::{fq}"
        if key not in types:
            types[key] = {"assembly": asm, "type": fq, "members": set()}

    # 2. MemberRefs (chamadas reais: metodos, props, campos externos)
    skipped_generics = 0
    for m in md.MemberRef:
        parent = m.Class
        table_name = type(parent.table).__name__
        if table_name == "TypeRef":
            asm = scope_assembly(parent.row)
            if asm is None:
                continue
            if not args.all and not is_unity_assembly(asm):
                continue
            fq = full_type_name(parent.row)
            key = f"{asm}::{fq}"
            types.setdefault(key, {"assembly": asm, "type": fq, "members": set()})
            types[key]["members"].add(str(m.Name))
        elif table_name == "TypeSpec":
            skipped_generics += 1  # ex: List<Transform>.Add — raro no checklist
        # TypeDef/MethodDef/ModuleRef = codigo proprio, ignora

    # 2b. MethodSpec (chamadas genericas: GetComponent<Rigidbody>, Instantiate<T>, etc.)
    generic_calls = 0
    for ms in md.MethodSpec:
        target = ms.Method
        if type(target.table).__name__ != "MemberRef":
            continue  # generico de metodo proprio, ignora
        mrow = target.row
        parent = mrow.Class
        if type(parent.table).__name__ != "TypeRef":
            continue
        asm = scope_assembly(parent.row)
        if asm is None:
            continue
        if not args.all and not is_unity_assembly(asm):
            continue
        fq = full_type_name(parent.row)
        key = f"{asm}::{fq}"
        types.setdefault(key, {"assembly": asm, "type": fq, "members": set()})
        name = str(mrow.Name) + "<T>"
        if name not in types[key]["members"]:
            types[key]["members"].add(name)
            generic_calls += 1

    # 3. Agrupa por modulo -> classe
    by_module: dict[str, dict[str, list[str]]] = defaultdict(dict)
    for key, v in types.items():
        by_module[v["assembly"]][v["type"]] = sorted(v["members"])

    out = {
        "dll": str(dll),
        "typeref_unity_count": len(types),
        "memberref_skipped_generics": skipped_generics,
        "methodspect_generic_calls": generic_calls,
        "modules": {mod: {"classes": len(c), "members": sum(len(m) for m in c.values())}
                    for mod, c in by_module.items()},
        "by_module": {mod: dict(sorted(c.items())) for mod, c in sorted(by_module.items())},
    }
    Path(args.out).write_text(json.dumps(out, indent=2, ensure_ascii=False), encoding="utf-8")

    # relatorio checklist
    total_members = sum(len(m) for c in by_module.values() for m in c.values())
    print(f"Tipos Unity referenciados: {len(types)}  |  membros: {total_members}")
    for mod in sorted(by_module):
        classes = by_module[mod]
        n_mem = sum(len(m) for m in classes.values())
        print(f"\n== {mod} — {len(classes)} classes, {n_mem} membros ==")
        for cls in sorted(classes):
            mems = classes[cls]
            print(f"  [ ] {cls} ({len(mems)})")
            for mem in mems:
                print(f"        [ ] {mem}")
    if skipped_generics:
        print(f"\n(obs: {skipped_generics} MemberRefs genericos/TypeSpec ignorados)")
    print(f"\nchecklist salva em: {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
