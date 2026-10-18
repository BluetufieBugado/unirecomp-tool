// Decodificação DXT do jogo (Unity TextureFormat 10=DXT1, 11=DXT1Crunched,
// 12=DXT5) para RGBA8888, preservando a ordem bottom-first do .assets
// (SpriteLibrary normaliza p/ top-first). Implementação direta da spec S3TC;
// RGBA32 (formato 4) é cópia — fica no SpriteLibrary.
// Apenas mip 0 é decodificado (é o que vai p/ tela; SpriteLibrary corta o resto).
using System;

namespace Unirecomp.Host
{
    public static class DxtDecoder
    {
        public static byte[] Decode(byte[] src, int width, int height, int unityFormat)
        {
            if (unityFormat == 10 || unityFormat == 11)
                return DecodeDxt1(src, width, height);
            if (unityFormat == 12)
                return DecodeDxt5(src, width, height);
            throw new InvalidOperationException($"formato de textura sem decoder: {unityFormat}");
        }

        static byte[] DecodeDxt1(byte[] src, int w, int h)
        {
            var dst = new byte[w * h * 4];
            int bw = (w + 3) / 4, bh = (h + 3) / 4, p = 0;
            for (int by = 0; by < bh; by++)
                for (int bx = 0; bx < bw; bx++, p += 8)
                {
                    ushort c0 = (ushort)(src[p] | (src[p + 1] << 8));
                    ushort c1 = (ushort)(src[p + 2] | (src[p + 3] << 8));
                    uint bits = (uint)(src[p + 4] | (src[p + 5] << 8) | (src[p + 6] << 16) | (src[p + 7] << 24));
                    Expand565(c0, out byte r0, out byte g0, out byte b0);
                    Expand565(c1, out byte r1, out byte g1, out byte b1);
                    bool punch = c0 <= c1;
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 4; x++)
                        {
                            int px = bx * 4 + x, py = by * 4 + y;
                            if (px >= w || py >= h) continue;
                            int code = (int)((bits >> (2 * (4 * y + x))) & 3);
                            int o = (py * w + px) * 4;
                            switch (code)
                            {
                                case 0: dst[o] = r0; dst[o + 1] = g0; dst[o + 2] = b0; dst[o + 3] = 255; break;
                                case 1: dst[o] = r1; dst[o + 1] = g1; dst[o + 2] = b1; dst[o + 3] = 255; break;
                                case 2:
                                    if (punch) { dst[o] = dst[o + 1] = dst[o + 2] = 0; dst[o + 3] = 0; }
                                    else Mix(dst, o, r0, g0, b0, r1, g1, b1, 2, 1);
                                    break;
                                default:
                                    if (punch) { dst[o] = dst[o + 1] = dst[o + 2] = 0; dst[o + 3] = 0; }
                                    else Mix(dst, o, r0, g0, b0, r1, g1, b1, 1, 2);
                                    break;
                            }
                        }
                }
            return dst;
        }

        static byte[] DecodeDxt5(byte[] src, int w, int h)
        {
            var dst = new byte[w * h * 4];
            int bw = (w + 3) / 4, bh = (h + 3) / 4, p = 0;
            for (int by = 0; by < bh; by++)
                for (int bx = 0; bx < bw; bx++, p += 16)
                {
                    byte a0 = src[p], a1 = src[p + 1];
                    ulong abits = 0;
                    for (int i = 0; i < 6; i++) abits |= (ulong)src[p + 2 + i] << (8 * i);
                    byte[] alpha = new byte[8];
                    alpha[0] = a0; alpha[1] = a1;
                    if (a0 > a1)
                    {
                        for (int i = 0; i < 6; i++)
                            alpha[2 + i] = (byte)(((7 - i) * a0 + (i + 1) * a1) / 7);
                    }
                    else
                    {
                        for (int i = 0; i < 4; i++)
                            alpha[2 + i] = (byte)(((5 - i) * a0 + (i + 1) * a1) / 5);
                        alpha[6] = 0; alpha[7] = 255;
                    }
                    ushort c0 = (ushort)(src[p + 8] | (src[p + 9] << 8));
                    ushort c1 = (ushort)(src[p + 10] | (src[p + 11] << 8));
                    uint cbits = (uint)(src[p + 12] | (src[p + 13] << 8) | (src[p + 14] << 16) | (src[p + 15] << 24));
                    Expand565(c0, out byte r0, out byte g0, out byte b0);
                    Expand565(c1, out byte r1, out byte g1, out byte b1);
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 4; x++)
                        {
                            int px = bx * 4 + x, py = by * 4 + y;
                            if (px >= w || py >= h) continue;
                            int o = (py * w + px) * 4;
                            int ai = (int)((abits >> (3 * (4 * y + x))) & 7);
                            int cc = (int)((cbits >> (2 * (4 * y + x))) & 3);
                            if (cc == 0) { dst[o] = r0; dst[o + 1] = g0; dst[o + 2] = b0; }
                            else if (cc == 1) { dst[o] = r1; dst[o + 1] = g1; dst[o + 2] = b1; }
                            else if (cc == 2) Mix(dst, o, r0, g0, b0, r1, g1, b1, 2, 1);
                            else Mix(dst, o, r0, g0, b0, r1, g1, b1, 1, 2);
                            dst[o + 3] = alpha[ai];
                        }
                }
            return dst;
        }

        static void Mix(byte[] dst, int o,
            byte r0, byte g0, byte b0, byte r1, byte g1, byte b1, int w0, int w1)
        {
            dst[o] = (byte)((w0 * r0 + w1 * r1) / 3);
            dst[o + 1] = (byte)((w0 * g0 + w1 * g1) / 3);
            dst[o + 2] = (byte)((w0 * b0 + w1 * b1) / 3);
            dst[o + 3] = 255; // DXT5 sobrescreve com o alfa real logo depois
        }

        static void Expand565(ushort c, out byte r, out byte g, out byte b)
        {
            r = (byte)(((c >> 11) & 31) * 255 / 31);
            g = (byte)(((c >> 5) & 63) * 255 / 63);
            b = (byte)((c & 31) * 255 / 31);
        }
    }
}
