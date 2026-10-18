import json, struct

sidx = json.load(open('docs/sfindex_sharedassets0.assets.json'))
styps = [t['class_id'] for t in sidx['types']]
sdata = open('Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data/sharedassets0.assets', 'rb').read()
sbase = sidx['header']['data_offset']
sby = {o['path_id']: o for o in sidx['objects']}

class R:
    def __init__(s, buf, p): s.buf = buf; s.p = p
    def i32(s):
        v = struct.unpack_from('<i', s.buf, s.p)[0]; s.p += 4; return v
    def u32(s):
        v = struct.unpack_from('<I', s.buf, s.p)[0]; s.p += 4; return v
    def i64(s):
        v = struct.unpack_from('<q', s.buf, s.p)[0]; s.p += 8; return v
    def f32(s):
        v = struct.unpack_from('<f', s.buf, s.p)[0]; s.p += 4; return v
    def u8(s):
        v = s.buf[s.p]; s.p += 1; return v
    def lstr(s):
        n = s.i32(); v = s.buf[s.p:s.p + n]; s.p += n; s.p = (s.p + 3) & ~3
        return v.decode('utf-8', errors='replace')
    def pptr(s):
        return (s.i32(), s.i64())
    def align(s): s.p = (s.p + 3) & ~3

def texmeta(path):
    o = sby[path]
    r = R(sdata, sbase + o['start'])
    nm = r.lstr()
    forced, down = r.i32(), r.i32()
    w, h, comp = r.i32(), r.i32(), r.i32()
    fmt, mips = r.i32(), r.i32()
    read = r.i32(); ic, dim = r.i32(), r.i32()
    filt, aniso = r.i32(), r.i32(); bias = r.f32(); wu, wv, ww = r.i32(), r.i32(), r.i32()
    lm, cs = r.i32(), r.i32(); usage = r.i32()
    off, sz = r.u32(), r.u32(); sp = r.lstr()
    return dict(nm=nm, w=w, h=h, comp=comp, fmt=fmt, mips=mips, off=off, sz=sz, sp=sp)

def sprmeta(path, full=False):
    o = sby[path]
    r = R(sdata, sbase + o['start'])
    start = sbase + o['start']
    nm = r.lstr()
    rect = [r.f32() for _ in range(4)]
    off2 = [r.f32() for _ in range(2)]
    bord = [r.f32() for _ in range(4)]
    ppu = r.f32(); piv = [r.f32() for _ in range(2)]
    ext = r.u32(); poly = r.u8(); r.align()
    guid = r.buf[r.p:r.p + 16]; r.p += 16; second = r.i64()
    na = r.i32()
    tags = [r.lstr() for _ in range(na)]
    atlas = r.pptr()
    tex = r.pptr(); alpha = r.pptr()
    info = dict(nm=nm, rect=rect, ppu=ppu, piv=piv, ext=ext, tex=tex, alpha=alpha, atlas=atlas)
    if full:
        nsub = r.i32()
        subs = [(r.u32(), r.u32(), r.i32(), r.u32(), r.u32()) for _ in range(nsub)]
        nib = r.i32(); ib = r.buf[r.p:r.p + nib]; r.p += nib; r.align()
        vcount = r.u32(); nch = r.i32()
        chs = [(r.u8(), r.u8(), r.u8(), r.u8()) for _ in range(nch)]; r.align()
        print('  submeshes:', subs, 'indexBytes:', nib, 'vcount:', vcount, 'channels:', chs, 'pos:', r.p - start)
    return info

for o in sidx['objects']:
    if styps[o['type_id']] != 213:
        continue
    s = sprmeta(o['path_id'])
    t = texmeta(s['tex'][1]) if s['tex'][0] == 0 else None
    print(f"sprite {o['path_id']} '{s['nm']}' rect={s['rect']} ppu={s['ppu']} texPPtr={s['tex']} alpha={s['alpha']} atlas={s['atlas']}")
    print(f"   -> tex: {t}")
