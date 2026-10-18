// Tier UI-1 — RectTransform real (anchors/pivot presentes; Shells.cs mantém
// position/rect/sizeDelta como armazenamento).
using UnityEngine;

namespace UnityEngine
{
    public partial class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; } = new Vector2(0, 0);
        public Vector2 anchorMax { get; set; } = new Vector2(1, 1);
        public Vector2 anchoredPosition { get; set; } = new Vector2(0, 0);
        public Vector2 pivot { get; set; } = new Vector2(0.5f, 0.5f);
        public Vector2 offsetMin { get; set; } = new Vector2(0, 0);
        public Vector2 offsetMax { get; set; } = new Vector2(0, 0);

        // RectData do loader: anchorMin(2) anchorMax(2) anchoredPos(2)
        // sizeDelta(2) pivot(2) — ordem da serialização 2018.1.
        internal void SetFromData(float[] d)
        {
            anchorMin = new Vector2(d[0], d[1]);
            anchorMax = new Vector2(d[2], d[3]);
            anchoredPosition = new Vector2(d[4], d[5]);
            sizeDelta = new Vector2(d[6], d[7]);
            pivot = new Vector2(d[8], d[9]);
        }

        // Rect em pixels no espaço do pai (origem inferior-esquerda, Y p/ cima).
        // Modelo geral do Unity: anchoredPosition é a posição do PIVÔ relativa
        // ao ponto de referência (âncoras interpoladas pelo pivô); vale tanto
        // para âncora-ponto quanto para stretch (só-ponto quebrava o segundo).
        public void GetPixelRect(float px, float py, float pw, float ph,
            out float x, out float y, out float w, out float h)
        {
            float spanX = anchorMax.x - anchorMin.x, spanY = anchorMax.y - anchorMin.y;
            w = spanX * pw + sizeDelta.x;
            h = spanY * ph + sizeDelta.y;
            float refX = px + (anchorMin.x + spanX * pivot.x) * pw;
            float refY = py + (anchorMin.y + spanY * pivot.y) * ph;
            x = refX + anchoredPosition.x - pivot.x * w;
            y = refY + anchoredPosition.y - pivot.y * h;
        }
    }
}
