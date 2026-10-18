// Starfield do menu (ParticleSystem "Stars" do level0): ~240 pontos
// quadrados seedados ancorados no frustum da câmera de render.
//
// Aproximação consciente: o emissor real é retângulo com campos ambíguos no
// .assets (type 17, donutRadius 60, length 5) e simulação por velocidade;
// aqui reproduzimos o LOOK (densidade, tamanhos 0.06–0.45 mundo ≈ 1–6px,
// tons branco/cinza-azulados) cobrindo a vista. Seed fixo => determinístico.
using System;
using Silk.NET.OpenGL;
using UnityEngine;

namespace Unirecomp.Host
{
    public static class StarfieldRenderer
    {
        const int Count = 240;
        const int Seed = 20181015;
        static bool built;
        static uint vao, vbo;

        public static unsafe void Render(GL gl, Camera cam, int fbW, int fbH, Matrix4x4 vp)
        {
            if (cam == null || fbH == 0) return;
            if (!built) { Build(gl, cam, fbW, fbH); built = true; }
            var shader = GLES2ShaderManager.GetPointsShader(gl);
            gl.UseProgram(shader.Program);
            gl.BindVertexArray(vao);

            if (shader.UniformLocations.TryGetValue("uVP", out int vpLoc))
            {
                var v = vp;
                fixed (float* ptr = v.Data)
                    gl.UniformMatrix4(vpLoc, 1, true, ptr);
            }
            if (shader.UniformLocations.TryGetValue("uCamPos", out int cpLoc))
            {
                var p = cam.transform.position;
                gl.Uniform3(cpLoc, p.x, p.y, p.z);
            }
            if (shader.UniformLocations.TryGetValue("uPxScale", out int pxLoc))
            {
                float halfTan = (float)Math.Tan(cam.fieldOfView * 0.5f * Math.PI / 180.0);
                gl.Uniform1(pxLoc, fbH / (2f * Math.Max(halfTan, 1e-3f)));
            }

            gl.Enable(EnableCap.DepthTest);
            gl.DepthFunc(DepthFunction.Less);
            gl.DepthMask(true);
            gl.Disable(EnableCap.Blend);
            gl.Disable(EnableCap.CullFace);

            gl.DrawArrays(PrimitiveType.Points, 0, (uint)Count);
            gl.BindVertexArray(0);
        }

        static unsafe void Build(GL gl, Camera cam, int fbW, int fbH)
        {
            var rnd = new System.Random(Seed);
            var t = cam.transform;
            var pos = t.position;
            var fwd = (t.rotation * Vector3.forward).normalized;
            var up = (t.rotation * Vector3.up).normalized;
            var right = (t.rotation * Vector3.right).normalized;
            float aspect = fbW / (float)Math.Max(fbH, 1);
            float halfTan = (float)Math.Tan(cam.fieldOfView * 0.5f * Math.PI / 180.0);

            // pos3 + cor4 + tamanho-mundo
            float[] data = new float[Count * 8];
            for (int i = 0; i < Count; i++)
            {
                float depth = 25f + (float)rnd.NextDouble() * 70f;
                float hh = depth * halfTan * 1.15f, hw = hh * aspect * 1.15f;
                float x = ((float)rnd.NextDouble() * 2f - 1f) * hw;
                float y = ((float)rnd.NextDouble() * 2f - 1f) * hh;
                var w = pos + fwd * depth + right * x + up * y;
                float r = (float)rnd.NextDouble();
                float size = r < 0.6f ? 0.06f + (float)rnd.NextDouble() * 0.08f
                    : r < 0.9f ? 0.14f + (float)rnd.NextDouble() * 0.14f
                    : 0.28f + (float)rnd.NextDouble() * 0.17f;
                float tone = 0.55f + (float)rnd.NextDouble() * 0.45f;
                float cr = tone, cg = tone, cb = tone;
                float tint = (float)rnd.NextDouble();
                if (tint < 0.2f) { cr *= 0.8f; cg *= 0.85f; }
                else if (tint < 0.3f) { cg *= 0.92f; cb *= 0.85f; }
                int o = i * 8;
                data[o] = w.x; data[o + 1] = w.y; data[o + 2] = w.z;
                data[o + 3] = cr; data[o + 4] = cg; data[o + 5] = cb; data[o + 6] = 1f;
                data[o + 7] = size;
            }

            var shader = GLES2ShaderManager.GetPointsShader(gl);
            vao = gl.GenVertexArray();
            vbo = gl.GenBuffer();
            gl.BindVertexArray(vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            fixed (float* ptr = data)
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4),
                    (void*)ptr, (GLEnum)BufferUsageARB.StaticDraw);
            const uint stride = 8 * 4;
            uint posLoc = (uint)shader.AttributeLocations["aPosition"];
            gl.EnableVertexAttribArray(posLoc);
            gl.VertexAttribPointer(posLoc, 3, (GLEnum)VertexAttribType.Float, false, stride, (void*)0);
            uint colLoc = (uint)shader.AttributeLocations["aColor"];
            gl.EnableVertexAttribArray(colLoc);
            gl.VertexAttribPointer(colLoc, 4, (GLEnum)VertexAttribType.Float, false, stride, (void*)(3 * 4));
            if (shader.AttributeLocations.TryGetValue("aSize", out int sizeLoc) && sizeLoc >= 0)
            {
                gl.EnableVertexAttribArray((uint)sizeLoc);
                gl.VertexAttribPointer((uint)sizeLoc, 1, (GLEnum)VertexAttribType.Float, false, stride, (void*)(7 * 4));
            }
            gl.BindVertexArray(0);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        }
    }
}
