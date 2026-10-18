// Tier 2 — Material associado a shader GLSL + propriedades para o renderer.
// Espelho do Material do Unity com SetFloat/SetMatrix/color usados pelo jogo.
using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using UnityEngine;

namespace Unirecomp.Host
{
    /// <summary>Material GLES2 com shader + uniforms dinâmicos.</summary>
    public sealed class GLES2Material
    {
        public readonly GLES2Shader Shader;
        public Color color { get; set; } = Color.white;
        public Dictionary<string, float> floats = new();
        public Dictionary<string, Matrix4x4> matrices = new();
        public Dictionary<string, int> ints = new();
        public uint? textureId;

        public GLES2Material(GLES2Shader shader)
        {
            Shader = shader;
        }

        /// <summary>Cria material do shader padrão (básico de cor).</summary>
        public static GLES2Material CreateBasic(GL gl)
        {
            var shader = GLES2ShaderManager.GetBasicShader(gl);
            return new GLES2Material(shader) { color = Color.white };
        }

        /// <summary>Cria material para sprites/texturas.</summary>
        public static GLES2Material CreateSprite(GL gl)
        {
            var shader = GLES2ShaderManager.GetSpriteShader(gl);
            return new GLES2Material(shader) { color = Color.white };
        }

        /// <summary>SetFloat — espelho da API do Unity Material.</summary>
        public void SetFloat(string name, float value)
        {
            floats[name] = value;
        }

        /// <summary>SetMatrix — espelho da API do Unity Material.</summary>
        public void SetMatrix(string name, Matrix4x4 value)
        {
            matrices[name] = value;
        }

        /// <summary>SetInt — helper para shaders que usam int.</summary>
        public void SetInt(string name, int value)
        {
            ints[name] = value;
        }
    }

    /// <summary>Gerencia shaders compilados (cache por tipo).</summary>
    public static partial class GLES2ShaderManager
    {
        static readonly Dictionary<GL, GLES2Shader> basicShaders = new();
        static readonly Dictionary<GL, GLES2Shader> spriteShaders = new();
        static readonly Dictionary<GL, GLES2Shader> textureShaders = new();
        static readonly Dictionary<GL, GLES2Shader> pointsShaders = new();

        public static GLES2Shader GetBasicShader(GL gl)
        {
            if (basicShaders.TryGetValue(gl, out var s)) return s;
            s = GLES2Shader.Compile(gl, ShaderTypes.BasicVert, ShaderTypes.BasicFrag);
            basicShaders[gl] = s;
            return s;
        }

        public static GLES2Shader GetSpriteShader(GL gl)
        {
            if (spriteShaders.TryGetValue(gl, out var s)) return s;
            s = GLES2Shader.Compile(gl, ShaderTypes.SpriteVert, ShaderTypes.SpriteFrag);
            spriteShaders[gl] = s;
            return s;
        }

        public static GLES2Shader GetTextureShader(GL gl)
        {
            if (textureShaders.TryGetValue(gl, out var s)) return s;
            s = GLES2Shader.Compile(gl, ShaderTypes.TextureVert, ShaderTypes.TextureFrag);
            textureShaders[gl] = s;
            return s;
        }

        public static GLES2Shader GetPointsShader(GL gl)
        {
            if (pointsShaders.TryGetValue(gl, out var s)) return s;
            s = GLES2Shader.Compile(gl, ShaderTypes.PointsVert, ShaderTypes.PointsFrag);
            pointsShaders[gl] = s;
            return s;
        }
    }
}