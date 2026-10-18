// Tier UI-tex — Texture2D mínimo: metadados + pixels RGBA decodificados.
// O host (SpriteLibrary) preenche a partir dos .assets/.resS do jogo;
// o UIRenderer faz upload p/ GL sob demanda. Sem dependências.
using System;

namespace UnityEngine
{
    public class Texture2D : Object
    {
        public int width { get; set; }
        public int height { get; set; }
        public int textureFormat { get; set; }
        /// <summary>Pixels RGBA, linha 0 = topo (convenção Unity). Null = não decodificado.</summary>
        public byte[] PixelData { get; set; }
    }
}
