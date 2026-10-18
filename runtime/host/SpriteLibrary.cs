// Sprites — resolve Image.m_Sprite cross-file e decodifica Texture2D.
// level0 referencia Sprites em sharedassets0.assets (fileID 2); level0 não
// tem nenhum 213 próprio. Texturas vêm do .resS via StreamData (offset/size)
// e viram RGBA (formato 4 = cópia, 10/11 = DXT1, 12 = DXT5). Só mip 0.
// Modelo igual ao AudioLibrary: parse sob demanda com cache por dataDir.
using System;
using System.Collections.Generic;
using System.IO;
using Unirecomp.Serialization;
using UnityEngine;

namespace Unirecomp.Host
{
    public static class SpriteLibrary
    {
        public static int SpritesLinked, TexturesDecoded;
        public static readonly List<string> Warnings = new List<string>();

        sealed class AssetCache
        {
            public readonly Dictionary<long, Sprite> Sprites = new Dictionary<long, Sprite>();
        }
        static readonly Dictionary<string, AssetCache> cache =
            new Dictionary<string, AssetCache>(StringComparer.OrdinalIgnoreCase);
        static string curDir;

        public static void Apply(BuiltScene scene, string file, string dataDir)
        {
            SpritesLinked = 0; TexturesDecoded = 0; Warnings.Clear();
            if (curDir != dataDir) { cache.Clear(); curDir = dataDir; }
            SerializedFile level;
            try { level = SerializedFile.Load(Path.Combine(dataDir, file)); }
            catch { return; }
            string Ext(int fileID) => fileID >= 1 && fileID <= level.Externals.Count
                ? Strip(level.Externals[fileID - 1]) : null;

            foreach (var (mbPath, comp) in scene.MbByPath)
            {
                if (comp is not UnityEngine.UI.Image img) continue;
                if (img.m_Sprite != null) continue;
                if (!scene.FieldValues.TryGetValue(mbPath, out var vals)) continue;
                if (!vals.TryGetValue("m_Sprite", out var v) || v is not FieldPPtr p) continue;
                if (p.IsNull) continue;
                string asset = Ext(p.File);
                if (asset == null) continue;
                var spr = FindSprite(asset, p.Path, dataDir);
                if (spr == null) { Warnings.Add($"sprite {asset}|{p.Path} nao achado"); continue; }
                img.m_Sprite = spr;
                SpritesLinked++;
            }
        }

        static string Strip(string e)
        {
            int i = e.LastIndexOf('/');
            return i >= 0 ? e.Substring(i + 1) : e;
        }

        static Sprite FindSprite(string asset, long path, string dataDir)
        {
            if (!cache.TryGetValue(asset, out var ac))
            {
                ac = new AssetCache();
                cache[asset] = ac;
                try { LoadAsset(asset, dataDir, ac); }
                catch (Exception ex) { Warnings.Add($"{asset}: {ex.Message}"); }
            }
            return ac.Sprites.TryGetValue(path, out var s) ? s : null;
        }

        static void LoadAsset(string asset, string dataDir, AssetCache ac)
        {
            string path = Path.Combine(dataDir, asset);
            if (!File.Exists(path)) return;
            var sf = SerializedFile.Load(path);
            var texMeta = new Dictionary<long, (int w, int h, int fmt, uint off, uint size, string stream)>();
            // Passada 1: metadados das texturas (ordem dos objetos não garantida).
            foreach (var o in sf.Objects)
            {
                if (sf.Types[o.TypeID].ClassID != 28) continue;
                var r = new BlobReader(sf.Data) { Pos = (int)o.AbsStart };
                r.LString(); r.Align4();
                r.I32(); r.I32(); // fallback, downscale
                int w = r.I32(), h = r.I32(), comp = r.I32();
                int fmt = r.I32(), mips = r.I32();
                r.I32(); // isReadable
                r.I32(); r.I32(); // imageCount, dimension
                r.I32(); r.I32(); r.F32(); r.I32(); r.I32(); r.I32(); // settings
                r.I32(); r.I32(); // lightmap, colorspace
                r.I32(); // usageMode
                uint off = r.U32(), size = r.U32();
                string stream = r.LString();
                texMeta[o.PathID] = (w, h, fmt, off, size, stream);
            }
            // Passada 2: sprites + decode das texturas usadas.
            foreach (var o in sf.Objects)
            {
                if (sf.Types[o.TypeID].ClassID != 213) continue;
                {
                    var r = new BlobReader(sf.Data) { Pos = (int)o.AbsStart };
                    string name = r.LString(); r.Align4(); // Sprite (2018.1)
                    float rx = r.F32(), ry = r.F32(), rw = r.F32(), rh = r.F32();
                    r.F32(); r.F32(); // offset
                    r.F32(); r.F32(); r.F32(); r.F32(); // border
                    float ppu = r.F32();
                    float pvx = r.F32(), pvy = r.F32();
                    r.U32(); // extrude
                    r.U8(); r.Align4(); // isPolygon
                    r.Blob(16); r.I64(); // renderDataKey (par único)
                    int na = r.I32();
                    for (int i = 0; i < na; i++) { r.LString(); r.Align4(); }
                    var atlas = r.PPtr();
                    var tex = r.PPtr();
                    var spr = new Sprite
                    {
                        textureRect = new Rect(rx, ry, rw, rh),
                        pivot = new Vector2(pvx, pvy),
                        pixelsPerUnit = ppu,
                    };
                    spr.name = name;
                    // Textura: mesmo arquivo (file 0) ou external do .assets.
                    long tpath = tex.PathID;
                    if (tpath != 0 && texMeta.TryGetValue(tpath, out var tm) && tex.FileID == 0)
                        spr.texture = DecodeTexture(tm, name, dataDir);
                    else if (tpath != 0 && tex.FileID != 0 && tex.FileID <= sf.Externals.Count)
                    {
                        string ext2 = Strip(sf.Externals[tex.FileID - 1]);
                        var spr2 = FindSprite(ext2, tpath, dataDir);
                        if (spr2 != null) spr.texture = spr2.texture;
                    }
                    ac.Sprites[o.PathID] = spr;
                    if (atlas.PathID != 0) Warnings.Add($"sprite '{name}': com atlas (sem suporte)");
                }
            }
        }

        static Texture2D DecodeTexture((int w, int h, int fmt, uint off, uint size, string stream) tm,
            string sprName, string dataDir)
        {
            var tex = new Texture2D { width = tm.w, height = tm.h, textureFormat = tm.fmt };
            tex.name = sprName + "_tex";
            try
            {
                if (tm.w <= 0 || tm.h <= 0 || tm.w > 8192 || tm.h > 8192) return tex;
                string sp = Path.Combine(dataDir, Strip(tm.stream));
                if (!File.Exists(sp)) { Warnings.Add($"stream {tm.stream} ausente"); return tex; }
                int need = tm.fmt == 4 ? tm.w * tm.h * 4
                    : ((tm.w + 3) / 4) * ((tm.h + 3) / 4) * (tm.fmt == 12 ? 16 : 8);
                if (tm.size < need) { Warnings.Add($"tex '{sprName}': stream curto ({tm.size}<{need})"); return tex; }
                var buf = new byte[need];
                using (var fs = File.OpenRead(sp))
                {
                    fs.Seek(tm.off, SeekOrigin.Begin);
                    int n = 0;
                    while (n < need)
                    {
                        int k = fs.Read(buf, n, need - n);
                        if (k <= 0) break;
                        n += k;
                    }
                    if (n < need) { Warnings.Add($"tex '{sprName}': leitura curta"); return tex; }
                }
                tex.PixelData = tm.fmt == 4 ? buf : DxtDecoder.Decode(buf, tm.w, tm.h, tm.fmt);
                // Unity armazena linhas bottom-first (convenção GL); o resto do
                // pipeline (ImageSharp, loader) assume top-first — normaliza aqui.
                FlipRowsVertical(tex.PixelData, tm.w, tm.h);
                TexturesDecoded++;
            }
            catch (Exception ex) { Warnings.Add($"tex '{sprName}': {ex.Message}"); }
            return tex;
        }

        // Inverte linhas in-place (RGBA8888): linha 0 vira topo.
        static void FlipRowsVertical(byte[] px, int w, int h)
        {
            var row = new byte[w * 4];
            for (int y = 0; y < h / 2; y++)
            {
                int a = y * w * 4, b = (h - 1 - y) * w * 4;
                Buffer.BlockCopy(px, a, row, 0, row.Length);
                Buffer.BlockCopy(px, b, px, a, row.Length);
                Buffer.BlockCopy(row, 0, px, b, row.Length);
            }
        }
    }
}
