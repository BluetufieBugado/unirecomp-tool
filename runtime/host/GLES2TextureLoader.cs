// Tier 2 — Carregamento de texturas de imagem para GLES2.
// Usa SixLabors.ImageSharp (cross-platform, funciona no Windows e Android).
// Suporta PNG/JPG/BMP/GIF (primeiro frame) → textura GL com parâmetros GLES2.
using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Silk.NET.OpenGL;

namespace Unirecomp.Host
{
    /// <summary>Carrega imagens de disco/byte[] e cria texturas OpenGL.</summary>
    public static class GLES2TextureLoader
    {
        /// <summary>Carrega um arquivo de imagem e retorna o ID da textura OpenGL.</summary>
        public static uint LoadFromFile(GL gl, string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Textura não encontrada: {path}");
            var bytes = File.ReadAllBytes(path);
            return LoadFromBytes(gl, bytes);
        }

        /// <summary>Carrega a partir de bytes da imagem e retorna o ID da textura OpenGL.</summary>
        public static uint LoadFromBytes(GL gl, byte[] data)
        {
            using var img = Image.Load<Rgba32>(data);
            return CreateTexture(gl, img);
        }

        /// <summary>Cria textura a partir de um ImageSharp já decodificado.</summary>
        public static uint CreateTexture(GL gl, Image<Rgba32> img)
        {
            // Flip vertical porque OpenGL espera Y-down por padrão
            img.Mutate(x => x.Flip(FlipMode.Vertical));

            uint tex = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, tex);

            int width = img.Width;
            int height = img.Height;

            // GLES2: NPOT exige ClampToEdge e sem mipmaps, senão a textura
            // fica incompleta e amostra preto (ex.: Title 2048x758).
            bool pot = width > 0 && (width & (width - 1)) == 0
                    && height > 0 && (height & (height - 1)) == 0;
            var wrap = pot ? TextureWrapMode.Repeat : TextureWrapMode.ClampToEdge;

            // Configurar parâmetros GLES2 básicos
            gl.TexParameter(TextureTarget.Texture2D, (GLEnum)TextureParameterName.TextureWrapS,
                (int)wrap);
            gl.TexParameter(TextureTarget.Texture2D, (GLEnum)TextureParameterName.TextureWrapT,
                (int)wrap);
            gl.TexParameter(TextureTarget.Texture2D, (GLEnum)TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, (GLEnum)TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);

            // Upload dos pixels (alocado no heap para imagens grandes).
            byte[] pixels = new byte[width * height * 4];
            img.CopyPixelDataTo(pixels);
            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba,
                        (uint)width, (uint)height, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte,
                        (void*)ptr);
                }
            }

            // Generar mipmaps se suportado (GLES2 com extension) — só POT.
            if (pot)
            {
                try
                {
                    gl.GenerateMipmap(TextureTarget.Texture2D);
                }
                catch
                {
                    // Se a extensão não estiver disponível, ignora
                }
            }

            gl.BindTexture(TextureTarget.Texture2D, 0);
            return tex;
        }

        /// <summary>Carrega uma textura com coordenadas UV especificadas.</summary>
        /// <remarks>Use quando o sprite tem UVs customizados do atlas.</summary>
        public static uint LoadWithCoords(GL gl, byte[] data, float u0, float v0, float u1, float v1)
        {
            using var img = Image.Load<Rgba32>(data);
            // Para UVs customizados, podemos criar uma textura viewportada ou usar
            // a imagem completa e ajustar os UVs no shader. Por ora: mesma imagem.
            return CreateTexture(gl, img);
        }
    }
}