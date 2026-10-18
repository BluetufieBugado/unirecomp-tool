// UI — layout canvas-space real via RectTransform.GetPixelRect + cadeia de pais.
// Image: quad com a textura real do sprite (Texture2D→GL sob demanda) e UV do
// textureRect; sem sprite, cai p/ cor chapada (m_Color). TMP_Text/Text: quad
// estimado + dump — o dump valida o menu sem rasterização (fatia futura).
using System;
using System.Collections.Generic;
using SharpImage = SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.PixelFormats;
using Silk.NET.OpenGL;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Unirecomp.Host
{
    public sealed class UIRenderer
    {
        readonly GLES2Renderer renderer;
        readonly GL gl;
        bool dumped;

        public UIRenderer(GLES2Renderer renderer, GL gl, uint? logoTextureId = null)
        {
            this.renderer = renderer;
            this.gl = gl;
        }

        /// <summary>Renderiza os Canvases ScreenSpaceCamera ativos.</summary>
        /// <remarks>Chamar logo após o clear, ANTES do 3D: o plano do canvas
        /// (planeDistance, longe) fica atrás das malhas com depth test normal —
        /// é assim que a nave passa na frente do fundo no menu original.
        /// (Aproximação: sem paralaxe do plano; o quad usa o mesmo mapeamento
        /// pixel-exato do overlay.)</remarks>
        public void RenderBackground(int fbWidth, int fbHeight)
        {
            foreach (var canvas in SortedCanvases(RenderMode.ScreenSpaceCamera))
            {
                var go = canvas.gameObject;
                if (go == null || !go.activeInHierarchy || !canvas.enabled) continue;
                RootRect(go, fbWidth, fbHeight, out float rx, out float ry, out float rw, out float rh);
                var ortho = Matrix4x4.Ortho(rx, rx + rw, ry, ry + rh, -1, 1);
                Walk(go, rx, ry, rw, rh, ortho, true);
            }
        }

        /// <summary>Renderiza todos os Canvases ScreenSpaceOverlay ativos.</summary>
        public void RenderAll(int fbWidth, int fbHeight)
        {
            foreach (var canvas in SortedCanvases(RenderMode.ScreenSpaceOverlay))
            {
                var go = canvas.gameObject;
                if (go == null || !go.activeInHierarchy || !canvas.enabled) continue;
                // Espaço do canvas raiz: rect recalculado via scaler (o valor do
                // arquivo é de edição). Ortho cobre o root rect => mapeia exato p/ tela.
                RootRect(go, fbWidth, fbHeight, out float rx, out float ry, out float rw, out float rh);
                var ortho = Matrix4x4.Ortho(rx, rx + rw, ry, ry + rh, -1, 1);
                Walk(go, rx, ry, rw, rh, ortho, true);
            }
            if (!dumped) { dumped = true; DumpAll(fbWidth, fbHeight); }
        }

        // Canvases por modo em ordem de desenho: sortOrder crescente (maior =
        // por cima, desenhado por último). OrderBy é estável: empate mantém
        // ordem do arquivo.
        static List<Canvas> SortedCanvases(RenderMode mode)
        {
            var list = new List<Canvas>();
            foreach (var c in UnityEngine.Object.AllOf<Canvas>())
                if (c.renderMode == mode) list.Add(c);
            list.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
            return list;
        }

        // Rect raiz overlay: (0,0,tela/escala). Escala do CanvasScaler
        // (ScaleWithScreenSize: interpola log entre largura/altura pelo match).
        internal static void RootRect(GameObject go, int fbW, int fbH,
            out float x, out float y, out float w, out float h)
        {
            float scale = 1f;
            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.enabled)
            {
                if (scaler.m_UiScaleMode == 1) // ScaleWithScreenSize
                {
                    var rr = scaler.m_ReferenceResolution;
                    if (rr.x > 0 && rr.y > 0)
                    {
                        float logW = (float)(Math.Log(fbW / rr.x) / Math.Log(2));
                        float logH = (float)(Math.Log(fbH / rr.y) / Math.Log(2));
                        float m = scaler.m_ScreenMatchMode == 0
                            ? scaler.m_MatchWidthOrHeight : 0.5f;
                        m = Math.Max(0f, Math.Min(1f, m));
                        scale = (float)Math.Pow(2, logW + (logH - logW) * m);
                    }
                }
                else scale = scaler.m_ScaleFactor > 0 ? scaler.m_ScaleFactor : 1f;
            }
            x = 0; y = 0; w = fbW / scale; h = fbH / scale;
        }

        void Walk(GameObject go, float px, float py, float pw, float ph, Matrix4x4 ortho, bool isCanvasRoot)
        {
            if (!go.activeInHierarchy) return;
            float x = px, y = py, w = pw, h = ph;
            // Raiz do canvas: ignora o RT do arquivo (edit-time), usa o root rect.
            if (!isCanvasRoot)
            {
                var rt = go.GetComponent<RectTransform>();
                if (rt != null) rt.GetPixelRect(px, py, pw, ph, out x, out y, out w, out h);
            }

            var img = go.GetComponent<Image>();
            if (img != null && img.enabled) ImageQuad(ortho, x, y, w, h, img);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp != null && tmp.enabled) TextQuad(ortho, x, y, w, h,
                tmp.m_text, tmp.m_fontSize, TmpColor(tmp), TmpAlign(tmp.m_textAlignment));
            else
            {
                var legacy = go.GetComponent<Text>();
                if (legacy != null && legacy.enabled) TextQuad(ortho, x, y, w, h,
                    legacy.text, legacy.fontSize, legacy.color, LegacyAlign(legacy.alignment));
            }

            foreach (Transform child in go.transform)
                Walk(child.gameObject, x, y, w, h, ortho, false);
        }

        static Color TmpColor(TMP_Text t)
        {
            var c = t.m_fontColor;
            c.a *= t.m_Color.a;
            return c;
        }

        // Retorna (h: 0=esq,1=centro,2=dir; v: 0=baixo,1=meio,2=topo).
        static (int h, int v) TmpAlign(int a)
        {
            int h = (a & 4) != 0 ? 2 : (a & 2) != 0 ? 1 : 0;
            int v = (a & 256) != 0 ? 2 : (a & 512) != 0 ? 1 : 0;
            return (h, v);
        }

        static (int h, int v) LegacyAlign(TextAnchor a)
        {
            string s = a.ToString();
            int h = s.Contains("Right") ? 2 : s.Contains("Center") ? 1 : 0;
            int v = s.StartsWith("Upper") ? 2 : s.StartsWith("Middle") ? 1 : 0;
            return (h, v);
        }

        void TextQuad(Matrix4x4 ortho, float x, float y, float w, float h,
            string s, float fontSize, Color c, (int h, int v) al)
        {
            if (string.IsNullOrEmpty(s) || fontSize <= 0) return;
            float tw = s.Length * fontSize * 0.6f, th = fontSize;
            float tx = al.h == 2 ? x + w - tw : al.h == 1 ? x + (w - tw) / 2f : x;
            float ty = al.v == 2 ? y + h - th : al.v == 1 ? y + (h - th) / 2f : y;
            Quad(ortho, tx, ty, tw, th, c, null);
        }

        void ImageQuad(Matrix4x4 ortho, float x, float y, float w, float h, Image img)
        {
            var spr = img.sprite;
            var tex = spr != null ? spr.texture : null;
            if (tex == null || tex.PixelData == null)
            { Quad(ortho, x, y, w, h, img.color, null); return; }
            // UV do textureRect (origem serializada = bottom-left, convenção
            // Unity; PixelData normalizado p/ top-first e GL carrega
            // bottom-first, então v mede a partir da base).
            var r = spr.textureRect;
            float tw = tex.width, th = tex.height;
            float u0 = r.x / tw, u1 = (r.x + r.width) / tw;
            float v0 = r.y / th, v1 = (r.y + r.height) / th;
            float[] verts = { x, y, x + w, y, x + w, y + h, x, y + h };
            float[] uvs = { u0, v0, u1, v0, u1, v1, u0, v1 };
            renderer.RenderSprite(ortho, verts, uvs, img.color, TexId(tex));
        }

        readonly Dictionary<Texture2D, uint> texIds = new Dictionary<Texture2D, uint>();

        uint? TexId(Texture2D tex)
        {
            if (texIds.TryGetValue(tex, out uint id)) return id;
            using var img = SharpImage.LoadPixelData<Rgba32>(tex.PixelData, tex.width, tex.height);
            id = GLES2TextureLoader.CreateTexture(gl, img);
            texIds[tex] = id;
            return id;
        }

        void Quad(Matrix4x4 ortho, float x, float y, float w, float h, Color c, uint? tex)
        {
            if (w <= 0 || h <= 0) return;
            float[] verts = { x, y, x + w, y, x + w, y + h, x, y + h };
            float[] uvs = { 0, 0, 1, 0, 1, 1, 0, 1 };
            renderer.RenderSprite(ortho, verts, uvs, c, tex);
        }

        // Dump headless (sem GL): lista o menu real para validação.
        public static void DumpAll(int fbWidth, int fbHeight)
        {
            int canvases = 0, images = 0, texts = 0, lines = 0;
            foreach (var mode in new[] { RenderMode.ScreenSpaceCamera, RenderMode.ScreenSpaceOverlay })
            foreach (var canvas in SortedCanvases(mode))
            {
                var go = canvas.gameObject;
                if (go == null || !go.activeInHierarchy || !canvas.enabled) continue;
                canvases++;
                RootRect(go, fbWidth, fbHeight, out float rx, out float ry, out float rw, out float rh);
                Console.WriteLine($"[ui] canvas '{go.name}' [{mode}] root=({rx:F0},{ry:F0},{rw:F0}x{rh:F0})");
                WalkDump(go, rx, ry, rw, rh, ref images, ref texts, ref lines, true);
            }
            Console.WriteLine($"[ui] resumo: {canvases} canvas, {images} images, {texts} textos");
        }

        static void WalkDump(GameObject go, float px, float py, float pw, float ph,
            ref int images, ref int texts, ref int lines, bool isCanvasRoot)
        {
            if (lines > 80 || !go.activeInHierarchy) return;
            float x = px, y = py, w = pw, h = ph;
            if (!isCanvasRoot)
            {
                var rt = go.GetComponent<RectTransform>();
                if (rt != null) rt.GetPixelRect(px, py, pw, ph, out x, out y, out w, out h);
            }

            var img = go.GetComponent<Image>();
            if (img != null && img.enabled && lines <= 80)
            {
                images++; lines++;
                var sp = img.sprite; var tx = sp != null ? sp.texture : null;
                string texInfo = tx != null && tx.PixelData != null
                    ? $"tex={tx.width}x{tx.height} fmt{tx.textureFormat}" : "tex=<sem pixels>";
                Console.WriteLine($"[ui]   img '{go.name}' rect=({x:F0},{y:F0},{w:F0}x{h:F0}) " +
                    $"cor=({img.color.r:F2},{img.color.g:F2},{img.color.b:F2},{img.color.a:F2}) " +
                    $"sprite={(sp != null ? sp.name : "<nenhum>")} {texInfo}");
            }
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp != null && tmp.enabled && lines <= 80)
            {
                texts++; lines++;
                Console.WriteLine($"[ui]   txt '{go.name}' \"{tmp.m_text}\" rect=({x:F0},{y:F0},{w:F0}x{h:F0}) " +
                    $"fonte={tmp.m_fontSize:F0} cor=({tmp.m_fontColor.r:F2},{tmp.m_fontColor.g:F2},{tmp.m_fontColor.b:F2},{tmp.m_fontColor.a:F2}) align={tmp.m_textAlignment}");
            }
            else
            {
                var legacy = go.GetComponent<Text>();
                if (legacy != null && legacy.enabled && lines <= 80)
                {
                    texts++; lines++;
                    Console.WriteLine($"[ui]   txt '{go.name}' \"{legacy.text}\" rect=({x:F0},{y:F0},{w:F0}x{h:F0}) fonte={legacy.fontSize}");
                }
            }

            foreach (Transform child in go.transform)
                WalkDump(child.gameObject, x, y, w, h, ref images, ref texts, ref lines, false);
        }
    }

    // Validação headless do menu: instancia level0, roda 30 frames e despeja a UI.
    // Uso: --ui "<jogo>_Data" [levelN]. Exit != 0 se nenhum canvas/texto aparecer.
    public static class UiCheck
    {
        public static int Run(IPlatform platform, string dataDir, string levelFile)
        {
            var gameAsm = BootSetup.LoadGame(dataDir);
            var shimAsms = BootSetup.ShimAsms();
            var layouts = BootSetup.LoadLayouts();
            using var mbmap = BootSetup.LoadMbmap();
            var director = new SceneDirector(dataDir, gameAsm, shimAsms, layouts, BootSetup.Map, mbmap);
            try { director.LoadInitial(BootSetup.SceneOf(levelFile)); }
            catch (Exception ex) { Console.WriteLine("  FALHOU: carga inicial " + ex.Message); return 5; }
            const float dt = 1f / 60f;
            for (int f = 1; f <= 30; f++)
            {
                platform.BeginFrame(dt);
                try { PlayerLoop.Tick(dt); }
                catch (Exception ex) { Console.WriteLine("  FALHOU: tick " + ex.Message); return 6; }
            }
            UIRenderer.DumpAll(1280, 720);
            int n = 0;
            foreach (var c in UnityEngine.Object.AllOf<Canvas>())
                if (c.gameObject != null && c.gameObject.activeInHierarchy) n++;
            if (n == 0) { Console.WriteLine("  FALHOU: nenhum canvas ativo"); return 7; }
            Console.WriteLine($"  ok: ui {n} canvas ativo(s)");
            return 0;
        }
    }
}
