import json, struct

res = open('Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data/sharedassets0.assets.resS', 'rb').read()
sidx = json.load(open('docs/sfindex_sharedassets0.assets.json'))
styps = [t['class_id'] for t in sidx['types']]
sdata = open('Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data/sharedassets0.assets', 'rb').read()
sbase = sidx['header']['data_offset']
sby = {o['path_id']: o for o in sidx['objects']}

def texmeta(path):
    o = sby[path]; b = sdata[sbase + o['start']:sbase + o['start'] + o['size']]
    p = struct.unpack_from('<i', b, 0)[0]; p = (4 + p + 3) & ~3
    w, h, comp = struct.unpack_from('<3i', b, p + 8); fmt, mips = struct.unpack_from('<2i', b, p + 20)
    off, sz = struct.unpack_from('<2I', b, p + 76)
    return w, h, fmt, off, sz

def avg_rgba(raw, w, h):
    n = w * h; s = [0, 0, 0, 0]
    for i in range(0, n * 4, 4):
        for k in range(4): s[k] += raw[i + k]
    return tuple(round(v / n) for v in s)

def dec565(c):
    return (((c >> 11) & 31) * 255 // 31, ((c >> 5) & 63) * 255 // 63, (c & 31) * 255 // 31)

def ref_dxt(src, w, h, fmt, maxblocks=10**9):
    dst = bytearray(w * h * 4)
    bw = (w + 3) // 4; p = 0; nb = 0
    for by in range((h + 3) // 4):
        for bx in range(bw):
            if nb >= maxblocks: return dst
            nb += 1
            if fmt == 12:
                a0, a1 = src[p], src[p + 1]
                ab = int.from_bytes(src[p + 2:p + 8], 'little')
                al = [a0, a1]
                if a0 > a1: al += [((7 - i) * a0 + (i + 1) * a1) // 7 for i in range(6)]
                else: al += [((5 - i) * a0 + (i + 1) * a1) // 5 for i in range(4)] + [0, 255]
                p += 8
            c0, c1 = struct.unpack_from('<HH', src, p)
            bits = struct.unpack_from('<I', src, p + 4)[0]; p += 8
            r0, g0, b0 = dec565(c0); r1, g1, b1 = dec565(c1)
            punch = fmt == 10 and c0 <= c1
            for y in range(4):
                for x in range(4):
                    px, py = bx * 4 + x, by * 4 + y
                    if px >= w or py >= h: continue
                    code = (bits >> (2 * (4 * y + x))) & 3
                    o = (py * w + px) * 4
                    if code == 0: c = (r0, g0, b0, 255)
                    elif code == 1: c = (r1, g1, b1, 255)
                    elif code == 2:
                        c = (0, 0, 0, 0) if punch else ((2 * r0 + r1) // 3, (2 * g0 + g1) // 3, (2 * b0 + b1) // 3, 255)
                    else:
                        c = (0, 0, 0, 0) if punch else ((r0 + 2 * r1) // 3, (g0 + 2 * g1) // 3, (b0 + 2 * b1) // 3, 255)
                    a = al[(ab >> (3 * (4 * y + x))) & 7] if fmt == 12 else c[3]
                    dst[o:o + 4] = bytes((c[0], c[1], c[2], a))
    return dst

# 1) RGBA32 Title: estatisticas
w, h, fmt, off, sz = texmeta(29)
raw = res[off:off + w * h * 4]
print('Title RGBA32 avg=', avg_rgba(raw, w, h), 'alpha_min=', min(raw[3::4]), 'alpha_max=', max(raw[3::4]))
# 2) DXT pequenos: decode total de referencia (ArrowGreen 32x32 DXT5, Arrow 32x32 DXT5)
for pid in (23, 28):
    w, h, fmt, off, sz = texmeta(pid)
    ref = ref_dxt(res[off:off + sz], w, h, fmt)
    print(f'tex{pid} {w}x{h} fmt{fmt} ref-avg=', avg_rgba(ref, w, h))
# 3) crops grandes: 1a linha de blocos (Space DXT1 2048 -> 512 blocos; ShipIcon DXT5 1664 -> 416)
for pid in (27, 24):
    w, h, fmt, off, sz = texmeta(pid)
    bw = (w + 3) // 4; bs = 16 if fmt == 12 else 8
    first_row = res[off:off + bw * bs]
    ref = ref_dxt(first_row, w, 4, fmt)
    print(f'tex{pid} {w}x{h} fmt{fmt} primeira-faixa-avg=', avg_rgba(ref, w, 4))
print('OK')
