// Pass 3D — ligações MeshFilter.sharedMesh + campos da Camera lidos do .assets.
namespace UnityEngine
{
    public partial class MeshFilter
    {
        public Mesh sharedMesh { get; set; }
        public Mesh mesh { get => sharedMesh; set => sharedMesh = value; }
    }

    public partial class Camera
    {
        public float fieldOfView { get; set; } = 60f;
        public float nearClipPlane { get; set; } = 0.3f;
        public float farClipPlane { get; set; } = 1000f;
        public float depth { get; set; } = 0f;
        public bool orthographic { get; set; } = false;
        public Color backgroundColor { get; set; } = new Color(0.19f, 0.30f, 0.47f, 1f);
    }
}
