// Fase 3 passo 1 — janela + loop real (Silk.NET) + seam IRenderer.
//
// Uso: dotnet run --project runtime/host -- --play "<jogo>_Data" level0 [frames]
// (frames=0: roda até fechar a janela; N>0: fecha sozinho — validação/CI).
//
// Decisão GLES (regra da Fase 4): o contexto aqui é OpenGL desktop, mas TODO
// código de render que entrar pelo IRenderer deve usar só o subset GLES2
// (VBOs + shaders ESSL, sem fixed pipeline), para o port Android não
// reescrever nada. Este passo só abre a janela e limpa a tela (Tier 2/4
// implementa o renderer de verdade neste seam).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Key = Silk.NET.Input.Key;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unirecomp.Host
{
    /// <summary>Costura de render: jogo nunca fala com GL direto, só aqui.</summary>
    public interface IRenderer
    {
        void BeginFrame(int width, int height);
        void EndFrame();

        /// <summary>
        /// Pré-render do frame: preenche o fundo (clear) e, se houver,
        /// desenha uma textura centralizada para evitar tela preta.
        /// Toda implementação deve suportar ser chamado antes do render
        /// principal do frame (Tier 2 — prova de conceito do foreground).
        /// </summary>
        void PreFillBackground(int width, int height);
    }

    public sealed class ClearRenderer : IRenderer
    {
        readonly GL gl;
        public ClearRenderer(GL gl) { this.gl = gl; }
        public void BeginFrame(int width, int height)
        {
            gl.Viewport(0, 0, (uint)width, (uint)height);
            gl.ClearColor(0.02f, 0.03f, 0.06f, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }
        public void EndFrame() { }

        /// <inheritdoc />
        public void PreFillBackground(int width, int height)
        {
            BeginFrame(width, height);
        }
    }

    public sealed class SilkInputState : IInputState
    {
        readonly IInputContext _input;
        readonly HashSet<Key> _keys = new();
        readonly HashSet<int> _mouseButtons = new();

        public SilkInputState(IWindow window)
        {
            _input = window.CreateInput();
            foreach (var kb in _input.Keyboards)
            {
                kb.KeyDown += (k, key, code) => _keys.Add(key);
                kb.KeyUp += (k, key, code) => _keys.Remove(key);
            }
            foreach (var mouse in _input.Mice)
            {
                mouse.MouseDown += (m, btn) => _mouseButtons.Add((int)btn);
                mouse.MouseUp += (m, btn) => _mouseButtons.Remove((int)btn);
            }
        }

        public bool IsKeyDown(KeyCode key)
        {
            var silkKey = KeyFor(key);
            return silkKey != null && _keys.Contains(silkKey.Value);
        }

        public bool IsMouseDown(int button) => _mouseButtons.Contains(button);

        public float GetAxis(string name)
        {
            float h = (IsKeyDown(KeyCode.A) || IsKeyDown(KeyCode.LeftArrow) ? -1f : 0f)
                    + (IsKeyDown(KeyCode.D) || IsKeyDown(KeyCode.RightArrow) ? 1f : 0f);
            float v = (IsKeyDown(KeyCode.W) || IsKeyDown(KeyCode.UpArrow) ? 1f : 0f)
                    + (IsKeyDown(KeyCode.S) || IsKeyDown(KeyCode.DownArrow) ? -1f : 0f);
            if (name == "Horizontal") return h;
            if (name == "Vertical") return v;
            return 0f;
        }

        static Key? KeyFor(KeyCode code) => code switch
        {
            KeyCode.W or KeyCode.UpArrow => Key.W,
            KeyCode.A or KeyCode.LeftArrow => Key.A,
            KeyCode.S or KeyCode.DownArrow => Key.S,
            KeyCode.D or KeyCode.RightArrow => Key.D,
            KeyCode.Space => Key.Space,
            KeyCode.Escape => Key.Escape,
            _ => null
        };
    }

    public sealed class SilkPlatform : IPlatform
    {
        public string Name => "Silk.NET";
        public void BeginFrame(float dt)
        {
            Time.deltaTime = dt;
            Time.unscaledDeltaTime = dt;
            Time.time += dt * Time.timeScale;
        }
        public void Log(string message) => Console.WriteLine("[silk] " + message);
    }

    public static class WindowRunner
    {
        public static int Run(string dataDir, string levelFile, int maxFrames)
        {
            var platform = new SilkPlatform();
            var gameAsm = BootSetup.LoadGame(dataDir);
            var shimAsms = BootSetup.ShimAsms();
            var layouts = BootSetup.LoadLayouts();
            using var mbmap = BootSetup.LoadMbmap();
            var director = new SceneDirector(dataDir, gameAsm, shimAsms, layouts, BootSetup.Map, mbmap);
            try { director.LoadInitial(BootSetup.SceneOf(levelFile)); }
            catch (Exception ex) { Console.WriteLine("  FALHOU: carga inicial " + ex.Message); return 5; }
            platform.Log($"cena {levelFile} instanciada: {GameObject.Roots.Count} raízes");
            platform.Log($"pass3d: {MeshLibrary.AppliedMeshes} malhas, {MeshLibrary.LinkedFilters} filtros ligados, {MeshLibrary.AppliedCameras} câmeras");
            platform.Log($"audio: {AudioLibrary.Clips} clipes, {AudioLibrary.SourcesLinked} fontes ligadas, {AudioLibrary.MbFieldsResolved} fields resolvidos");
            platform.Log(FmodBackend.Status());

            var options = WindowOptions.Default;
            options.Title = $"Stardust Raiders (Unirecomp) — {levelFile}";

            // Textura real de teste: usa o Title.png do jogo como validação.
            // Carregada depois do GL estar pronto (ver window.Load).
            uint? logoTex = null;

            options.Size = new Vector2D<int>(1280, 720);
            options.VSync = true;
            using var window = Window.Create(options);
            GL gl = null;
            IRenderer renderer = null;
            int frames = 0, fails = 0, fpsN = 0;
            double fpsAcc = 0;
            window.Load += () =>
            {
                gl = window.CreateOpenGL();
                renderer = new GLES2Renderer(gl);
                var inputState = new SilkInputState(window);
                Input.SetState(inputState);
                try { platform.Log("GL: " + gl.GetStringS(StringName.Version)); }
                catch (Exception ex) { platform.Log("GL sem version string: " + ex.Message); }

                // Textura real de teste: usa uma sprite do próprio jogo como
                // validação do pipeline de texturas na janela.
                var logoPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Stardust Raiders", "Assets", "Sprites", "Text&Logos", "Title.png");
                if (!File.Exists(logoPath))
                    logoPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Stardust Raiders", "Assets", "Sprites", "Text&Logos", "Title.png");
                if (File.Exists(logoPath))
                {
                    try
                    {
                        logoTex = GLES2TextureLoader.LoadFromFile(gl, logoPath);
                        platform.Log($"textura carregada: {Path.GetFullPath(logoPath)}");
                    }
                    catch (Exception ex)
                    {
                        platform.Log($"textura não carregou: {ex.Message}");
                    }
                }
                else
                {
                    platform.Log($"textura não encontrada em: {logoPath}");
                }
            };
            UIRenderer uiRenderer = null;
            window.Render += delta =>
            {
                float dt = Math.Min((float)delta, 0.1f);
                platform.BeginFrame(dt);
                try { PlayerLoop.Tick(dt); }
                catch (Exception ex) { fails++; platform.Log("FALHOU tick: " + ex.Message); window.Close(); return; }
                var fb = window.FramebufferSize;
                // Um único clear por frame (PreFill já chama BeginFrame).
                renderer.PreFillBackground(fb.X, fb.Y);
                if (uiRenderer == null) uiRenderer = new UIRenderer((GLES2Renderer)renderer, gl, logoTex);
                // Canvas ScreenSpaceCamera (fundo) antes do 3D; overlay depois.
                uiRenderer.RenderBackground(fb.X, fb.Y);
                // Pass 3D: malhas com a Camera principal (antes da UI).
                SceneRenderer3D.Render((GLES2Renderer)renderer, fb.X, fb.Y);

                uiRenderer.RenderAll(fb.X, fb.Y);
                renderer.EndFrame();
                frames++;
                fpsAcc += delta;
                fpsN++;
                if (fpsN >= 60)
                {
                    window.Title = $"Stardust Raiders (Unirecomp) — {levelFile} | {fpsN / fpsAcc:F0} fps | {SceneManager.GetActiveScene().name}";
                    fpsAcc = 0;
                    fpsN = 0;
                }
                if (maxFrames > 0 && frames >= maxFrames) window.Close();
            };
            window.Run();
            gl?.Dispose();
            platform.Log($"janela: {frames} frames, cena {SceneManager.GetActiveScene().name}");
            if (fails > 0) return 6;
            if (maxFrames > 0 && frames < maxFrames)
            {
                Console.WriteLine($"  FALHOU: fechou com {frames}/{maxFrames} frames");
                return 6;
            }
            return 0;
        }
    }
}
