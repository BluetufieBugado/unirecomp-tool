// Tier 2 — foreground helper: desenha uma textura centralizada via RenderSprite.
// Usa a mesma projeção ortho da UI (0,0 bottom-left -> w,h top-right).
using Silk.NET.OpenGL;
using UnityEngine;

namespace Unirecomp.Host
{
    public static class GLES2ForegroundDrawer
    {
        public static void PreFillBackground(IRenderer renderer, int width, int height)
        {
            renderer.BeginFrame(width, height);
        }

        public static void DrawCenteredTexture(GLES2Renderer renderer, GL gl, uint texId,
            int fbW, int fbH, int w, int h, float opacity)
        {
            float x0 = (fbW - w) / 2f;
            float y0 = (fbH - h) / 2f;
            float[] verts = { x0, y0, x0 + w, y0, x0 + w, y0 + h, x0, y0 + h };
            float[] uvs = { 0, 0, 1, 0, 1, 1, 0, 1 };
            var ortho = Matrix4x4.Ortho(0, fbW, 0, fbH, -1, 1);
            renderer.RenderSprite(ortho, verts, uvs, new Color(1, 1, 1, opacity), texId);
        }
    }
}
