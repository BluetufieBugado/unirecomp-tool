// Fase 4 — Mesh com vertices/triangles (para o renderer OpenGL).
using System.Collections.Generic;

namespace UnityEngine
{
    public partial class Mesh
    {
        public Vector3[] vertices { get; set; } = new Vector3[0];
        public int[] triangles { get; set; } = new int[0];
        public Vector3[] normals { get; set; } = new Vector3[0];
        public Vector2[] uv { get; set; } = new Vector2[0];
    }
}
