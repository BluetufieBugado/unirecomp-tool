import json, struct, sys

idx = json.load(open('docs/sfindex_level0.json'))
typs = [t['class_id'] for t in idx['types']]
data = open('Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data/level0', 'rb').read()
base = idx['header']['data_offset']
mbm = json.load(open('docs/mbmap_stardust.json'))['level0']
mapped = {m['pathID']: m for m in mbm['mapped']}

goname = {}
for o in idx['objects']:
    if typs[o['type_id']] != 1:
        continue
    p = base + o['start']
    n = struct.unpack_from('<i', data, p)[0]; p += 4 + 12 * n
    p += 4
    nl = struct.unpack_from('<i', data, p)[0]; p += 4
    goname[o['path_id']] = data[p:p + nl].decode('utf-8', errors='replace')

class R:
    def __init__(s, p): s.p = p
    def i32(s):
        v = struct.unpack_from('<i', data, s.p)[0]; s.p += 4; return v
    def f32(s):
        v = struct.unpack_from('<f', data, s.p)[0]; s.p += 4; return v
    def u8(s):
        v = data[s.p]; s.p += 1; return v

for o in idx['objects']:
    if typs[o['type_id']] != 114:
        continue
    m = mapped.get(o['path_id'])
    if not m or not m['script'].endswith('::Image'):
        continue
    r = R(base + o['start'])
    r.i32(); r.i32(); r.i32()
    r.u8(); r.p = (r.p + 3) & ~3
    r.i32(); r.i32(); r.i32()
    nl = r.i32(); r.p += nl; r.p = (r.p + 3) & ~3
    mat = (r.i32(), r.i32(), r.i32())
    col = tuple(round(r.f32(), 3) for _ in range(4))
    ray = r.i32()
    npc = r.i32()
    for _ in range(npc):
        r.i32(); r.i32(); r.i32(); ml = r.i32(); r.p += ml; r.p = (r.p + 3) & ~3
        r.i32(); r.i32(); r.i32(); r.i32()
        ol = r.i32(); r.p += ol; r.p = (r.p + 3) & ~3
        r.i32(); r.i32(); r.i32(); r.i32()
    tnl = r.i32(); r.p += tnl; r.p = (r.p + 3) & ~3  # m_TypeName por ultimo
    import struct as _st
    f = r.i32(); sp = _st.unpack_from('<q', data, r.p)[0]; r.p += 8
    consumed = r.p - (base + o['start'])
    print(f"GO='{goname.get(m['go_pathID'])}' MB={o['path_id']} color={col} sprite=({f},{sp}) consumed={consumed}/{o['size']}")
