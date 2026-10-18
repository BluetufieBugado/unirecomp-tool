// Tier 2 — Shader GLSL compilado com uniforms e atributos.
// Suporta os usos do jogo: color, SetFloat, SetMatrix, material instantiation.
using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;

namespace Unirecomp.Host
{
    /// <summary>Shader GLSL compilado + cache de localizações de uniform/atrib.</summary>
    public sealed class GLES2Shader
    {
        public readonly uint Program;
        public readonly Dictionary<string, int> UniformLocations;
        public readonly Dictionary<string, int> AttributeLocations;

        GLES2Shader(uint program, Dictionary<string, int> uniforms, Dictionary<string, int> attrs)
        {
            Program = program;
            UniformLocations = uniforms;
            AttributeLocations = attrs;
        }

        /// <summary>Compila um shader a partir de código fonte GLSL.</summary>
        public static GLES2Shader Compile(GL gl, string vertSrc, string fragSrc)
        {
            uint vert = CompileShader(gl, ShaderType.VertexShader, vertSrc);
            uint frag = CompileShader(gl, ShaderType.FragmentShader, fragSrc);
            uint prog = LinkProgram(gl, vert, frag);
            gl.DeleteShader(vert);
            gl.DeleteShader(frag);

            var uniforms = new Dictionary<string, int>();
            var attrs = new Dictionary<string, int>();

            // Coletar todas as localizações de uniforms conhecidos
            foreach (var name in KnownUniforms(fragSrc, vertSrc))
            {
                int loc = (int)gl.GetUniformLocation(prog, name);
                if (loc >= 0) uniforms[name] = loc;
            }

            // Coletar atributos
            foreach (var name in KnownAttributes(vertSrc))
            {
                int loc = (int)gl.GetAttribLocation(prog, name);
                if (loc >= 0) attrs[name] = loc;
            }

            return new GLES2Shader(prog, uniforms, attrs);
        }

        static uint CompileShader(GL gl, ShaderType type, string src)
        {
            // Templates usam verbatim @"..." com newline inicial; #version precisa
            // ser o primeiro token, então remove whitespace inicial.
            src = src.TrimStart();
            uint shader = gl.CreateShader(type);
            gl.ShaderSource(shader, src);
            gl.CompileShader(shader);

            gl.GetShader(shader, GLEnum.CompileStatus, out int status);
            if (status == 0)
            {
                string err = gl.GetShaderInfoLog(shader);
                gl.DeleteShader(shader);
                throw new InvalidOperationException($"Shader não compilou ({type}): {err}");
            }
            return shader;
        }

        static uint LinkProgram(GL gl, uint vert, uint frag)
        {
            uint prog = gl.CreateProgram();
            gl.AttachShader(prog, vert);
            gl.AttachShader(prog, frag);
            gl.LinkProgram(prog);

            gl.GetProgram(prog, GLEnum.LinkStatus, out int status);
            if (status == 0)
            {
                string err = gl.GetProgramInfoLog(prog);
                throw new InvalidOperationException($"Link falhou: {err}");
            }
            return prog;
        }

        /// <summary>Uniforms que o shader usa (para cache de localizações).</summary>
        static IEnumerable<string> KnownUniforms(string frag, string vert)
        {
            var all = frag + "\n" + vert;
            if (all.Contains("uColor")) yield return "uColor";
            if (all.Contains("uMVP")) yield return "uMVP";
            if (all.Contains("uModel")) yield return "uModel";
            if (all.Contains("uView")) yield return "uView";
            if (all.Contains("uProjection")) yield return "uProjection";
            if (all.Contains("uTime")) yield return "uTime";
            if (all.Contains("uTex")) yield return "uTex";
            if (all.Contains("uTexCoord")) yield return "uTexCoord";
            if (all.Contains("uAlpha")) yield return "uAlpha";
            if (all.Contains("uVP")) yield return "uVP";
            if (all.Contains("uPxScale")) yield return "uPxScale";
            if (all.Contains("uCamPos")) yield return "uCamPos";
        }

        /// <summary>Atributos esperados no vertex shader.</summary>
        static IEnumerable<string> KnownAttributes(string vert)
        {
            var src = vert;
            if (src.Contains("aPosition")) yield return "aPosition";
            if (src.Contains("aColor")) yield return "aColor";
            if (src.Contains("aTexCoord")) yield return "aTexCoord";
            if (src.Contains("aNormal")) yield return "aNormal";
            if (src.Contains("aSize")) yield return "aSize";
        }
    }

    /// <summary>Tipos de shader pré-definidos para uso do jogo.</summary>
    public static class ShaderTypes
    {
        /// <summary>Shader difuso simples p/ meshes 3D (posição + normal).</summary>
        public const string BasicVert = @"
#version 100
attribute vec3 aPosition;
attribute vec3 aNormal;
uniform mat4 uMVP;
uniform mat4 uModel;
varying float vDiff;
void main() {
  vec3 n = normalize((uModel * vec4(aNormal, 0.0)).xyz);
  vec3 L = normalize(vec3(-0.35, 0.8, 0.45));
  vDiff = 0.8 + 0.2 * max(dot(n, L), 0.0);
  gl_Position = uMVP * vec4(aPosition, 1.0);
}
";

        public const string BasicFrag = @"
#version 100
precision mediump float;
varying float vDiff;
uniform vec4 uColor;
void main() {
  gl_FragColor = vec4(uColor.rgb * vDiff, uColor.a);
}
";

        /// <summary>Shader para sprites UI (pos + uv + color).</summary>
        public const string SpriteVert = @"
#version 100
attribute vec2 aPosition;
attribute vec2 aTexCoord;
attribute vec4 aColor;
uniform mat4 uMVP;
varying vec2 vTexCoord;
varying vec4 vColor;
void main() {
  vTexCoord = aTexCoord;
  vColor = aColor;
  gl_Position = uMVP * vec4(aPosition, 0.0, 1.0);
}
";

        public const string SpriteFrag = @"
#version 100
precision mediump float;
varying vec2 vTexCoord;
varying vec4 vColor;
uniform sampler2D uTex;
void main() {
  gl_FragColor = texture2D(uTex, vTexCoord) * vColor;
}
";

        /// <summary>Shader para textura + cor (para Image com sprite).</summary>
        public const string TextureVert = @"
#version 100
attribute vec2 aPosition;
attribute vec2 aTexCoord;
uniform mat4 uMVP;
varying vec2 vTexCoord;
void main() {
  vTexCoord = aTexCoord;
  gl_Position = uMVP * vec4(aPosition, 0.0, 1.0);
}
";

        public const string TextureFrag = @"
#version 100
precision mediump float;
varying vec2 vTexCoord;
uniform sampler2D uTex;
uniform vec4 uColor;
void main() {
  gl_FragColor = texture2D(uTex, vTexCoord) * uColor;
}
";

        /// <summary>Pontos quadrados p/ starfield (pos3 + cor4 + tamanho-mundo).</summary>
        public const string PointsVert = @"
#version 100
attribute vec3 aPosition;
attribute vec4 aColor;
attribute float aSize;
uniform mat4 uVP;
uniform vec3 uCamPos;
uniform float uPxScale;
varying vec4 vColor;
void main() {
  gl_Position = uVP * vec4(aPosition, 1.0);
  float dist = max(distance(aPosition, uCamPos), 0.1);
  gl_PointSize = clamp(aSize * uPxScale / dist, 1.0, 24.0);
  vColor = aColor;
}
";

        public const string PointsFrag = @"
#version 100
precision mediump float;
varying vec4 vColor;
void main() {
  gl_FragColor = vColor;
}
";
    }
}