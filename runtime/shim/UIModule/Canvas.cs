// Tier 2 mínimo — Canvas (neutro; renderMode/worldCamera p/ UI funcionar).
namespace UnityEngine
{
    public partial class Canvas
    {
        public RenderMode renderMode { get; set; } = RenderMode.ScreenSpaceOverlay;
        public Camera worldCamera { get; set; }
        // Distância do plano (ScreenSpaceCamera; default Unity = 100).
        public float planeDistance { get; set; } = 100f;
        // Ordem de desenho entre overlays (lido do .assets; maior = por cima).
        public int sortOrder { get; set; }
    }
}
