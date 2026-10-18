#!/usr/bin/env python3
"""Decodifica um blob MB campo a campo com layouts_engine.json e mostra offsets.
Uso: decode_mb.py <level> <path_id> [maxfields]
"""
import json, struct, sys

QUIET = '--quiet' in sys.argv
import builtins as _b
def P(*a, **k):
    if not QUIET:
        _b.print(*a, **k)

level, pid = sys.argv[1], int(sys.argv[2])
maxf = int(sys.argv[3]) if len(sys.argv) > 3 else 400
idx = json.load(open(f'docs/sfindex_{level}.json'))
data = open(f'Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data/{level}', 'rb').read()
eng = json.load(open('docs/layouts_engine.json'))
game = json.load(open('docs/layouts_stardust.json'))
# une namespaces: game (scripts do jogo) + engine
ALL = {'scripts': dict(game['scripts']), 'nested': dict(game.get('nested', {}))}
for k, v in eng['scripts'].items():
    ALL['scripts'][k] = v
for k, v in eng['nested'].items():
    ALL['nested'].setdefault(k, v)
base = idx['header']['data_offset']
typs = [t['class_id'] for t in idx['types']]
o = next(x for x in idx['objects'] if x['path_id'] == pid)
p = base + o['start']
end = p + o['size']
P(f'obj {pid} start={o["start"]} size={o["size"]} end-rel={o["start"]+o["size"]}')

pos = [p]
def u8(): v = data[pos[0]]; pos[0] += 1; return v
def u16(): v = struct.unpack_from('<H', data, pos[0])[0]; pos[0] += 2; return v
def i32(): v = struct.unpack_from('<i', data, pos[0])[0]; pos[0] += 4; return v
def u32(): v = struct.unpack_from('<I', data, pos[0])[0]; pos[0] += 4; return v
def i64(): v = struct.unpack_from('<q', data, pos[0])[0]; pos[0] += 8; return v
def f32(): v = struct.unpack_from('<f', data, pos[0])[0]; pos[0] += 4; return v
def f64(): v = struct.unpack_from('<d', data, pos[0])[0]; pos[0] += 8; return v
def align(): pos[0] = (pos[0] + 3) & ~3
def lstr():
    n = i32(); s = data[pos[0]:pos[0]+n].decode('utf-8', errors='replace'); pos[0] += n; align(); return s

# base 32B: GO pptr + enabled + script pptr + m_Name
gofile, gopath = i32(), i64()
en = u8(); align()
sfile, spath = i32(), i64()
nm = lstr()
P(f'base: go=({gofile},{gopath}) en={en} script=({sfile},{spath}) name={nm!r} pos-rel={pos[0]-p}')

# cadeia de layouts base-primeiro (igual FullFields)
def chain(script):
    c, out = [], script
    seen = set()
    while out and out not in seen:
        seen.add(out); c.append(out)
        b = ALL['scripts'].get(out, {}).get('base', '')
        out = b if b in ALL['scripts'] else None
    c.reverse(); return c

shown = [0]
def rec(fields, depth, where):
    for f in fields:
        if shown[0] >= maxf: return
        shown[0] += 1
        k, n = f['kind'], f['name']
        off = pos[0] - p
        if k == 'pptr':
            a, b = i32(), i64(); P(f'{off:4d} {where}.{n} pptr=({a},{b})')
        elif k in ('int', 'enum', 'layermask'): P(f'{off:4d} {where}.{n} int={i32()}')
        elif k == 'uint': P(f'{off:4d} {where}.{n} uint={u32()}')
        elif k == 'long': P(f'{off:4d} {where}.{n} long={i64()}')
        elif k == 'float': P(f'{off:4d} {where}.{n} float={f32():.4g}')
        elif k == 'double': P(f'{off:4d} {where}.{n} double={f64():.4g}')
        elif k == 'bool': P(f'{off:4d} {where}.{n} bool={u32()}')
        elif k in ('byte', 'sbyte'): v = u8(); align(); P(f'{off:4d} {where}.{n} byte={v}')
        elif k in ('short', 'ushort', 'char'): v = u16(); align(); P(f'{off:4d} {where}.{n} short={v}')
        elif k == 'string': P(f'{off:4d} {where}.{n} str={lstr()!r}')
        elif k == 'vector2': a, b = f32(), f32(); P(f'{off:4d} {where}.{n} v2=({a:.3g},{b:.3g})')
        elif k == 'vector3': a, b, c = f32(), f32(), f32(); P(f'{off:4d} {where}.{n} v3=({a:.3g},{b:.3g},{c:.3g})')
        elif k == 'vector4': P(f'{off:4d} {where}.{n} v4=({f32():.3g},{f32():.3g},{f32():.3g},{f32():.3g})')
        elif k == 'color': P(f'{off:4d} {where}.{n} col=({f32():.3g},{f32():.3g},{f32():.3g},{f32():.3g})')
        elif k == 'color32': v = [u8() for _ in range(4)]; P(f'{off:4d} {where}.{n} c32={v}')
        elif k == 'rect': P(f'{off:4d} {where}.{n} rect=({f32():.3g},{f32():.3g},{f32():.3g},{f32():.3g})')
        elif k == 'rectoffset': P(f'{off:4d} {where}.{n} ro=({i32()},{i32()},{i32()},{i32()})')
        elif k == 'unityevent':
            nn = i32(); P(f'{off:4d} {where}.{n} uev-calls={nn}')
            for _ in range(nn):
                ta, tb = i32(), i64(); mn = lstr(); mo = i32()
                oa, ob = i32(), i64(); oat = lstr(); ia = i32(); fa = f32(); sa = lstr(); ba = u32(); cs = i32()
                P(f'      call target=({ta},{tb}) m={mn!r} mode={mo} oarg=({oa},{ob}) i={ia} f={fa:.3g} s={sa!r} b={ba} cs={cs}')
            P(f'      typename={lstr()!r}')
        elif k == 'array':
            nn = i32(); P(f'{off:4d} {where}.{n} array[{nn}] of {f["of"]["kind"]}')
            for _ in range(nn): rec([dict(f['of'], name=n + '[]')], depth + 1, where)
        elif k == 'nested':
            P(f'{off:4d} {where}.{n} nested {f.get("type")}:')
            sub = ALL['nested'].get(f.get('type') or '')
            if sub is None: P('      SEM LAYOUT!'); return
            rec(sub['fields'], depth + 1, where + '.' + n)
        else:
            P(f'{off:4d} {where}.{n} KIND? {k}'); return

import sys as _s
_cls = _s.argv[4] if len(_s.argv) > 4 else 'TextMeshProUGUI'
if '--scan' in _s.argv:
    import traceback
    maxf = 10 ** 9
    mbmap = json.load(open('docs/mbmap_stardust.json'))[level]['mapped']
    scriptof = {m['pathID']: m['script'] for m in mbmap}
    bad = 0
    for o in idx['objects']:
        if typs[o['type_id']] != 114:
            continue
        full = scriptof.get(o['path_id'], '')
        cls = full.split('::')[-1] if full else ''
        if not cls or cls not in ALL['scripts']:
            continue
        pos[0] = base + o['start']
        shown[0] = 0
        try:
            gofile, gopath = i32(), i64()
            en = u8(); align()
            sfile, spath = i32(), i64()
            nm = lstr()
            for c in chain(cls):
                rec(ALL['scripts'][c]['fields'], 0, c)
            got = pos[0] - (base + o['start'])
            if got != o['size']:
                bad += 1
                print(f'{o["path_id"]} ({cls}): consumido {got} != size {o["size"]} (delta {o["size"]-got})')
        except Exception as ex:
            bad += 1
            print(f'{o["path_id"]} ({cls}): EXCECAO {ex} em rel={pos[0]-(base+o["start"])}')
    print(f'scan: {bad} problemas')
else:
    for cls in chain(_cls):
        P(f'--- {cls} ---')
        rec(ALL['scripts'][cls]['fields'], 0, cls)
    P(f'fim pos-rel={pos[0]-p} size={o["size"]}')
