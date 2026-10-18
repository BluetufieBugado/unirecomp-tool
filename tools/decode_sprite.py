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

def decode_sprite(path):
    o = sby[path]
    r = R(sdata, sbase + o['start'])
    end = sbase + o['start'] + o['size']
    print(f'--- Sprite path={path} size={o["size"]}')
    print('name:', r.lstr())
    print('rect:', [round(r.f32(), 3) for _ in range(4)])
    print('offset:', [round(r.f32(), 3) for _ in range(2)])
    print('border:', [round(r.f32(), 3) for _ in range(4)])
    print('ppu:', r.f32())
    print('pivot:', [round(r.f32(), 3) for _ in range(2)])
    print('extrude:', r.u32())
    print('ispolygon:', r.u8(), 'align->', end - ((r.p + 3) & ~3), 'restantes')
    r.p = (r.p + 3) & ~3
    print('renderdatakey_n:', r.i32())
    # guess: vector<pair<GUID,int>>? print raw
    print('  pos agora:', r.p - (sbase + o['start']), 'restam:', end - r.p)
    # tenta: m_AtlasTags vector<string>
    print('atlasTags_n:', r.i32())
    print('  pos:', r.p - (sbase + o['start']), 'restam:', end - r.p)
    print('spriteAtlas pptr:', r.pptr())
    print('  pos:', r.p - (sbase + o['start']), 'restam:', end - r.p)
    print('texture(renderdata?) pptr:', r.pptr())
    print('  pos:', r.p - (sbase + o['start']), 'restam:', end - r.p)

def decode_tex(path):
    o = sby[path]
    r = R(sdata, sbase + o['start'])
    end = sbase + o['start'] + o['size']
    print(f'--- Texture2D path={path} size={o["size"]}')
    print('name:', r.lstr())
    print('forcedFallback:', r.i32(), 'downscaleFallback:', r.i32())
    print('width:', r.i32(), 'height:', r.i32(), 'completeSize:', r.i32())
    print('format:', r.i32(), 'mipCount:', r.i32())
    print('isReadable:', r.i32())
    print('imageCount:', r.i32(), 'dimension:', r.i32())
    print('filter/aniso/mipbias/wrapU/wrapV/wrapW:', r.i32(), r.i32(), r.f32(), r.i32(), r.i32(), r.i32())
    print('lightmapFormat:', r.i32(), 'colorSpace:', r.i32())
    print('pos:', r.p - (sbase + o['start']), 'restam:', end - r.p)

decode_tex(24)
decode_tex(29)
decode_sprite(62)
