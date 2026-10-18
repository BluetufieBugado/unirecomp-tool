// GERADO por tools/genshells.py a partir de docs/apicheck_stardust.json — nao editar a mao.
// Cascas para o assembly do jogo carregar (Fase 1). Membros reais vêm nos Tiers 1-2.
using UnityEngine;

namespace UnityEngine
{
    public partial class AnimationCurve : Object { }
    // Font moved to TextRenderingModule
    public partial class Camera : Behaviour { }
    public partial class FlareLayer : Behaviour { }
    public static partial class Input { }
    public partial class Light : Behaviour { }
    public static partial class LightmapSettings { }
    public partial class Material : Object { }
    public partial class Mesh : Object { }
    public partial class MeshFilter : Component { }
    public partial class MeshRenderer : Renderer { }
    public static partial class QualitySettings { }
    public partial struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }
    public partial class RectTransform : Transform
    {
        public Vector3 position { get; set; }
        public Rect rect { get; set; }
        public Vector2 sizeDelta { get; set; }
    }
    public static partial class RenderSettings { }
    public partial class Renderer : Component { }
    public partial struct Resolution { }
    public static partial class Resources { }
    public static partial class Screen { }
    public partial class SpriteRenderer : Renderer { }
    public partial struct Touch { }
    public partial class TrailRenderer : Renderer { }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction<T0>(T0 arg0);
    public delegate void UnityAction<T0, T1>(T0 arg0, T1 arg1);
    public delegate void UnityAction<T0, T1, T2>(T0 arg0, T1 arg1, T2 arg2);
    public class UnityEvent<T0> { }
    public class UnityEvent<T0, T1> { }
    public class UnityEvent<T0, T1, T2> { }
}
