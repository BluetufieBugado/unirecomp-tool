// Tier 2 mínimo — TextMesh (neutro; mesh de texto real depois).
namespace UnityEngine
{
    public partial class TextMesh
    {
        public string text { get; set; } = "";
        public int fontSize { get; set; } = 14;
        public TextAnchor anchor { get; set; }
        public Color color { get; set; } = new Color(1, 1, 1);
        public Font font { get; set; }
    }
}
