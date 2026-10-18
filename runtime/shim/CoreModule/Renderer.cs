// Tier 2 mínimo — Renderer.material e Material (neutros; shaders/GLES depois).
namespace UnityEngine
{
    public partial class Renderer
    {
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
    }

    public partial class Material
    {
        public Color color { get; set; } = new Color(1, 1, 1);
        public void SetFloat(string name, float value) { }
        public void SetMatrix(string name, Matrix4x4 value) { }
    }
}
