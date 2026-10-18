// Tier 2 — Sprite (para Image/SpriteRenderer). O host (SpriteLibrary) liga
// texture/textureRect a partir dos .assets do jogo; sem sprite, Image usa cor.
using UnityEngine;

namespace UnityEngine
{
    public class Sprite : Object
    {
        public string name { get; set; }
        public Rect textureRect { get; set; }
        public Vector2 pivot { get; set; } = new Vector2(0.5f, 0.5f);
        public float pixelsPerUnit { get; set; } = 100f;
        public Texture2D texture { get; set; }
    }
}