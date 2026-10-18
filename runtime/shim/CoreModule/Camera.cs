// Tier 1/2 mínimo — Camera.main + conversões (neutras; renderer real no Tier 2).
namespace UnityEngine
{
    public partial class Camera
    {
        public static Camera main
        {
            get
            {
                foreach (var c in Object.AllOf<Camera>())
                    return c;
                return null;
            }
        }
        public float orthographicSize { get; set; } = 5f;
        public Ray ScreenPointToRay(Vector3 pos) => new Ray(Vector3.zero, Vector3.forward);
        public Vector3 WorldToScreenPoint(Vector3 pos) => Vector3.zero;
        public Vector3 ViewportToWorldPoint(Vector3 pos) => Vector3.zero;
    }
}
