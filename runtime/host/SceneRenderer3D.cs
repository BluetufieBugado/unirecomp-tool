// Pass 3D (fatia 1) — desenha MeshFilter+MeshRenderer com a Camera de maior
// profundidade. Unlit (cor sólida clara), sem luz/textura/animação.
using System.Linq;
using UnityEngine;

namespace Unirecomp.Host
{
    public static class SceneRenderer3D
    {
        public static int DrawnMeshes;
        static bool loggedOnce;

        static void LogOnce(string msg)
        {
            if (loggedOnce) return;
            loggedOnce = true;
            System.Console.WriteLine(msg);
        }

        public static void Render(GLES2Renderer renderer, int fbW, int fbH)
        {
            var cams = UnityEngine.Object.AllOf<Camera>()
                .Where(c => c.gameObject != null && c.gameObject.activeInHierarchy)
                .OrderBy(c => c.depth)
                .ToList();
            if (cams.Count == 0 || fbH == 0)
            {
                LogOnce($"[3d] sem câmera (cams={cams.Count}) ou altura 0");
                return;
            }
            var cam = cams.Last();
            if (cam.orthographic)
            {
                LogOnce("[3d] câmera principal ortográfica — fatia 1 só faz perspectiva");
                return;
            }

            // Estado 3D: o passo de fundo UI (RenderBackground) deixa blend
            // ligado + depth desligado — restaura aqui.
            var gl = renderer.Gl;
            gl.Enable(Silk.NET.OpenGL.EnableCap.DepthTest);
            gl.DepthFunc(Silk.NET.OpenGL.DepthFunction.Less);
            gl.DepthMask(true);
            gl.Disable(Silk.NET.OpenGL.EnableCap.Blend);

            float aspect = fbW / (float)fbH;
            var proj = Matrix4x4.Perspective(cam.fieldOfView, aspect,
                cam.nearClipPlane, cam.farClipPlane);
            // Unity é left-handed (câmera olha +Z local); GL é right-handed
            // (espera -Z). Espelha Z após a view: view = MirrorZ * World^-1.
            var view = Matrix4x4.Scale(new Vector3(1, 1, -1))
                * cam.transform.LocalToWorldMatrix().Inverse();

            int filters = 0, noMesh = 0, inactive = 0, noRenderer = 0;
            DrawnMeshes = 0;
            foreach (var mf in UnityEngine.Object.AllOf<MeshFilter>())
            {
                filters++;
                var mesh = mf.sharedMesh;
                if (mesh == null || mesh.vertices.Length == 0 || mesh.triangles.Length == 0)
                { noMesh++; continue; }
                var go = mf.gameObject;
                if (go == null || !go.activeInHierarchy) { inactive++; continue; }
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null) { noRenderer++; continue; }
                // Ordem padrão P*V*M: as matrizes são row-major e o upload usa
                // transpose=true, então o GL interpreta exatamente P*V*M.
                var model = mf.transform.LocalToWorldMatrix();
                var mvp = proj * view * model;
                var (subColors, subRanges) = MeshLibrary.RenderInfo(mf);
                renderer.RenderMesh(mesh, null, mvp, model, subColors, subRanges);
                DrawnMeshes++;
            }
            // Starfield (partículas do menu) por cima do fundo, com depth.
            StarfieldRenderer.Render(gl, cam, fbW, fbH, proj * view);
            LogOnce($"[3d] filtros={filters} desenhadas={DrawnMeshes} semMalha={noMesh} inativas={inactive} semRenderer={noRenderer} cam={cam.gameObject.name} fov={cam.fieldOfView}");
        }
    }
}
