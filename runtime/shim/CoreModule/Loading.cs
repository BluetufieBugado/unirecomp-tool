// Fase 2a — mecanismo de troca de cena (conteudo real vem com o asset pipeline 2b).
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class AsyncOperation
    {
        public bool isDone { get; internal set; }
        public float progress { get; internal set; }
        internal int framesLeft = 2;
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        internal int index;
        internal string sceneName;
        public int buildIndex => index;
        public string name => sceneName;
        public bool IsValid() => index >= 0;
    }

    public static class SceneManager
    {
        static readonly List<string> buildScenes = new List<string>();
        static readonly List<(AsyncOperation op, int index)> pending = new List<(AsyncOperation, int)>();
        static int active = -1;
        // 2b-iv: host injeta a troca real de conteúdo (destruir + instanciar).
        // Null = só troca o índice (testes mecânicos como o headless).
        internal static Func<int, bool> SwapContent;
        // O CIL do jogo assina com UnityAction<Scene,LoadSceneMode> — o tipo
        // do evento tem que ser exatamente esse (era Action, quebrava o Awake
        // do GameManager com MissingMethod em add_sceneLoaded).
        public static event UnityEngine.Events.UnityAction<Scene, LoadSceneMode> sceneLoaded;
        public static int sceneCountInBuildSettings => buildScenes.Count;
        internal static void SetBuildScenes(IEnumerable<string> names)
        {
            buildScenes.Clear();
            buildScenes.AddRange(names);
            if (active < 0 && buildScenes.Count > 0) active = 0;
        }
        public static Scene GetActiveScene() => new Scene
        {
            index = active,
            sceneName = active >= 0 && active < buildScenes.Count ? buildScenes[active] : ""
        };
        public static AsyncOperation LoadSceneAsync(string name) => LoadSceneAsync(buildScenes.IndexOf(name));
        public static AsyncOperation LoadSceneAsync(int index)
        {
            var op = new AsyncOperation();
            pending.Add((op, index));
            return op;
        }
        internal static void Tick()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var (op, index) = pending[i];
                op.progress = 0.9f;
                if (--op.framesLeft > 0) continue;
                pending.RemoveAt(i);
                // Conteúdo real (host) antes de virar a cena ativa e o evento.
                SwapContent?.Invoke(index);
                active = index;
                op.progress = 1f;
                op.isDone = true;
                sceneLoaded?.Invoke(GetActiveScene(), LoadSceneMode.Single);
            }
        }
    }
}
