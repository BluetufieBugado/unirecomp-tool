// Tier 2 — Renderer GLES2 compatível com Android, implementa IRenderer.
// Desenha meshes 3D + UI. Integra-se ao loop de render existente.
// Regra dura Fase 4: só GLES2 API (VBO + IBO + shader + uniforms),
// nada de fixed pipeline ou GL desktop-only.
using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using UnityEngine;

namespace Unirecomp.Host
{
    /// <summary>Renderer GLES2 que implementa IRenderer e desenha o scene.</summary>
    public sealed class GLES2Renderer : IRenderer
    {
        /// <summary>Referência de baixo nível GL deste renderer (acesso interno/helper).</summary>
        private readonly GL gl;

        /// <summary>Referência de baixo nível GL deste renderer (acesso interno/helper).</summary>
        public GL Gl => gl;
        readonly Stack<Matrix4x4> matrixStack = new Stack<Matrix4x4>();
        readonly Dictionary<Mesh, (uint vao, uint vbo, uint ibo)> meshCache = new();
        readonly Dictionary<Mesh, GLES2Material> materialCache = new();

        public GLES2Renderer(GL gl) { this.gl = gl; }

        public void BeginFrame(int width, int height)
        {
            gl.Viewport(0, 0, (uint)width, (uint)height);
            gl.ClearColor(0.02f, 0.03f, 0.06f, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            gl.Enable(EnableCap.DepthTest);
            gl.DepthFunc(DepthFunction.Less);
            gl.Enable(EnableCap.CullFace);
            gl.CullFace((GLEnum)3); // Back
            gl.FrontFace(FrontFaceDirection.CW);

            matrixStack.Push(Matrix4x4.identity);
        }

        /// <inheritdoc />
        public void PreFillBackground(int width, int height)
        {
            BeginFrame(width, height);
        }

        public void EndFrame() { }

        public void PushMatrix(Matrix4x4 m) => matrixStack.Push(m);
        public void PopMatrix() { if (matrixStack.Count > 0) matrixStack.Pop(); }

        /// <summary>Desenha um mesh 3D com material + shader (difuso simples).</summary>
        /// <param name="subColors">[r,g,b,a] por submesh (null = branco).</param>
        /// <param name="subRanges">[firstElem,count] por submesh (null = tudo).</param>
        public unsafe void RenderMesh(Mesh mesh, Material mat, Matrix4x4 mvp, Matrix4x4 model,
            float[][] subColors = null, int[][] subRanges = null)
        {
            if (mesh == null || mesh.vertices.Length == 0) return;
            if (!meshCache.TryGetValue(mesh, out var buffers))
                buffers = CreateBuffers(mesh);

            var shaderMat = GetOrCreateMaterial(mesh);

            gl.UseProgram(shaderMat.Shader.Program);
            gl.BindVertexArray(buffers.vao);

            // Fatia 3D-1: cull desligado (winding Unity + espelho Z ainda sem
            // convenção fechada; depth test resolve a oclusão).
            gl.Disable(EnableCap.CullFace);

            // Passar uniformes
            PassUniform(shaderMat.Shader, "uMVP", mvp);
            PassUniform(shaderMat.Shader, "uModel", model);

            // Passar floats customizados (SetFloat)
            foreach (var kv in shaderMat.floats)
                PassUniform(shaderMat.Shader, kv.Key, kv.Value);

            // Passar matrices customizadas (SetMatrix)
            foreach (var kv in shaderMat.matrices)
                PassUniform(shaderMat.Shader, kv.Key, kv.Value);

            // Passar ints customizados
            foreach (var kv in shaderMat.ints)
                PassUniform(shaderMat.Shader, kv.Key, kv.Value);

            int n = subRanges != null ? subRanges.Length : 1;
            for (int s = 0; s < n; s++)
            {
                int first = 0, count = mesh.triangles.Length;
                if (subRanges != null && s < subRanges.Length && subRanges[s] != null && subRanges[s].Length >= 2)
                {
                    first = Math.Max(0, subRanges[s][0]);
                    count = Math.Max(0, subRanges[s][1]);
                    if (first + count > mesh.triangles.Length)
                        count = mesh.triangles.Length - first;
                    if (count <= 0) continue;
                }
                var c = shaderMat.color;
                if (subColors != null && s < subColors.Length && subColors[s] != null && subColors[s].Length >= 3)
                {
                    var sc = subColors[s];
                    c = new Color(sc[0], sc[1], sc[2], sc.Length > 3 ? sc[3] : 1f);
                }
                PassUniform(shaderMat.Shader, "uColor", c);
                gl.DrawElements(PrimitiveType.Triangles, (uint)count,
                    DrawElementsType.UnsignedInt, (void*)(first * 4));
            }
            gl.BindVertexArray(0);
        }

        unsafe (uint vao, uint vbo, uint ibo) CreateBuffers(Mesh mesh)
        {
            uint vao = gl.GenVertexArray();
            uint vbo = gl.GenBuffer();
            uint ibo = gl.GenBuffer();

            gl.BindVertexArray(vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

            // Interleaved pos(3) + normal(3); sem normais usa +Y.
            int n = mesh.vertices.Length;
            bool hasNrm = mesh.normals != null && mesh.normals.Length == n;
            float[] data = new float[n * 6];
            for (int i = 0; i < n; i++)
            {
                var v = mesh.vertices[i];
                data[i * 6] = v.x; data[i * 6 + 1] = v.y; data[i * 6 + 2] = v.z;
                var nr = hasNrm ? mesh.normals[i] : Vector3.up;
                data[i * 6 + 3] = nr.x; data[i * 6 + 4] = nr.y; data[i * 6 + 5] = nr.z;
            }
            fixed (float* ptr = data)
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4),
                    (void*)ptr, (GLEnum)BufferUsageARB.StaticDraw);

            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ibo);
            int idxSize = mesh.triangles.Length * 4;
            fixed (int* ptr = mesh.triangles)
                gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)idxSize,
                    (void*)ptr, (GLEnum)BufferUsageARB.StaticDraw);

            // Localiza atributos por nome (o shader básico usa aPosition/aNormal).
            var shader = GLES2ShaderManager.GetBasicShader(gl);
            uint posLoc = (uint)shader.AttributeLocations["aPosition"];
            gl.EnableVertexAttribArray(posLoc);
            gl.VertexAttribPointer(posLoc, 3, (GLEnum)VertexAttribType.Float, false, 24, (void*)0);
            if (shader.AttributeLocations.TryGetValue("aNormal", out int nrmLoc) && nrmLoc >= 0)
            {
                gl.EnableVertexAttribArray((uint)nrmLoc);
                gl.VertexAttribPointer((uint)nrmLoc, 3, (GLEnum)VertexAttribType.Float, false, 24, (void*)(3 * 4));
            }

            gl.BindVertexArray(0);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);

            meshCache[mesh] = (vao, vbo, ibo);
            return (vao, vbo, ibo);
        }

        GLES2Material GetOrCreateMaterial(Mesh mesh)
        {
            if (materialCache.TryGetValue(mesh, out var mat)) return mat;
            mat = GLES2Material.CreateBasic(gl);
            materialCache[mesh] = mat;
            return mat;
        }

        /// <summary>Renderiza um sprite 2D com shader + textura.
        /// verts/uvs: 4 cantos (x,y) em pixels; mvp: projeção ortho do frame.
        /// Desenha como 2 triângulos (GLES2 não tem GL_QUADS).</summary>
        public unsafe void RenderSprite(Matrix4x4 mvp, float[] verts, float[] uvs, Color color, uint? texId)
        {
            if (verts == null || uvs == null || verts.Length < 8 || uvs.Length < 8) return;
            var shader = GLES2ShaderManager.GetSpriteShader(gl);

            // Expande 4 cantos -> 6 vértices (0,1,2, 0,2,3).
            int[] quad = { 0, 1, 2, 0, 2, 3 };
            const int stride = 8 * 4; // pos2 + uv2 + cor4
            float[] data = new float[6 * 8];
            for (int k = 0; k < 6; k++)
            {
                int c = quad[k];
                int o = k * 8;
                data[o] = verts[c * 2];
                data[o + 1] = verts[c * 2 + 1];
                data[o + 2] = uvs[c * 2];
                data[o + 3] = uvs[c * 2 + 1];
                data[o + 4] = color.r;
                data[o + 5] = color.g;
                data[o + 6] = color.b;
                data[o + 7] = color.a;
            }

            uint vao = gl.GenVertexArray();
            uint vbo = gl.GenBuffer();

            gl.BindVertexArray(vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

            fixed (float* ptr = data)
                gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 4),
                    (void*)ptr, (GLEnum)BufferUsageARB.StaticDraw);

            gl.UseProgram(shader.Program);

            int posLoc = shader.AttributeLocations["aPosition"];
            int texLoc = shader.AttributeLocations["aTexCoord"];
            int colLoc = shader.AttributeLocations["aColor"];

            gl.EnableVertexAttribArray((uint)posLoc);
            gl.VertexAttribPointer((uint)posLoc, 2, (GLEnum)VertexAttribType.Float, false, (uint)stride, (void*)0);

            gl.EnableVertexAttribArray((uint)texLoc);
            gl.VertexAttribPointer((uint)texLoc, 2, (GLEnum)VertexAttribType.Float, false, (uint)stride, (void*)(2 * 4));

            gl.EnableVertexAttribArray((uint)colLoc);
            gl.VertexAttribPointer((uint)colLoc, 4, (GLEnum)VertexAttribType.Float, false, (uint)stride, (void*)(4 * 4));

            PassUniform(shader, "uMVP", mvp);
            PassUniform(shader, "uColor", color);

            // Liga a textura na unidade 0 (sampler espera índice da unidade,
            // nunca o ID). Sem textura usa fallback branco 1x1 para cor sólida.
            uint tex = texId ?? GetWhiteTexture();
            gl.ActiveTexture(TextureUnit.Texture0);
            gl.BindTexture(TextureTarget.Texture2D, tex);
            if (shader.UniformLocations.TryGetValue("uTex", out int samplerLoc))
                gl.Uniform1(samplerLoc, 0);

            gl.Disable(EnableCap.DepthTest);
            gl.Disable(EnableCap.CullFace);
            gl.Enable(EnableCap.Blend);
            gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

            gl.Disable(EnableCap.Blend);
            gl.BindTexture(TextureTarget.Texture2D, 0);
            gl.BindVertexArray(0);
            gl.DeleteVertexArray(vao);
            gl.DeleteBuffer(vbo);
        }

        uint? whiteTex;
        uint GetWhiteTexture()
        {
            if (whiteTex.HasValue) return whiteTex.Value;
            uint tex = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, tex);
            gl.TexParameter(TextureTarget.Texture2D, (GLEnum)TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            gl.TexParameter(TextureTarget.Texture2D, (GLEnum)TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            byte[] px = { 255, 255, 255, 255 };
            unsafe
            {
                fixed (byte* ptr = px)
                    gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, 1, 1, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, (void*)ptr);
            }
            gl.BindTexture(TextureTarget.Texture2D, 0);
            whiteTex = tex;
            return tex;
        }

        void PassUniform(GLES2Shader shader, string name, Matrix4x4 value)
        {
            if (shader.UniformLocations.TryGetValue(name, out int loc))
            {
                unsafe
                {
                    // Matrix4x4.Data é row-major (ver TRS/Ortho); GL espera
                    // column-major com transpose=false, então transpõe aqui.
                    fixed (float* ptr = value.Data)
                        gl.UniformMatrix4(loc, 1, true, ptr);
                }
            }
        }

        void PassUniform(GLES2Shader shader, string name, float value)
        {
            if (shader.UniformLocations.TryGetValue(name, out int loc))
                gl.Uniform1(loc, value);
        }

        void PassUniform(GLES2Shader shader, string name, Color value)
        {
            if (shader.UniformLocations.TryGetValue(name, out int loc))
                gl.Uniform4(loc, value.r, value.g, value.b, value.a);
        }

        void PassUniform(GLES2Shader shader, string name, int value)
        {
            if (shader.UniformLocations.TryGetValue(name, out int loc))
                gl.Uniform1(loc, value);
        }
    }
}