// Unirecomp.Host — boot Tier 0.
// Abstracao de plataforma (IPlatform) desde o dia 1: o mesmo host compila
// no Windows (validacao de paridade) e no Android via .NET for Android
// (o APK do futuro). Só a implementacao de IPlatform muda.
//
// Uso (requer .NET 8 SDK — ausente nesta maquina, ver STATUS.md):
//   dotnet build runtime/host
//   dotnet run --project runtime/host -- "<jogo>_Data/Managed"
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unirecomp.Host
{
    /// <summary>Superfície que varia por plataforma. Renderer/input/arquivos
    /// entram aqui nos Tiers 1-2; hoje só tempo + log.</summary>
    public interface IPlatform
    {
        string Name { get; }
        void BeginFrame(float deltaTime);
        void Log(string message);
    }

    public sealed class WindowsPlatform : IPlatform
    {
        public string Name => "Windows";
        public void BeginFrame(float dt)
        {
            Time.deltaTime = dt;
            Time.unscaledDeltaTime = dt;
            Time.time += dt * Time.timeScale;
        }
        public void Log(string message) => Console.WriteLine("[host] " + message);
    }

    // Futuro (APK): AndroidPlatform : IPlatform com RuntimePlatform.Android,
    // leitura dos dados do jogo via AssetManager/pasta do app e input touch.
    // O boot abaixo não muda.

    /// <summary>Resolve os assemblies UnityEngine.* / TextMeshPro para o shim,
    /// ignorando versao (o jogo pede Version=0.0.0.0/PublicKeyToken=null).</summary>
    public sealed class UnirecompLoadContext : AssemblyLoadContext
    {
        readonly Dictionary<string, Assembly> shims = new(StringComparer.OrdinalIgnoreCase);
        public UnirecompLoadContext()
        {
            Add(typeof(UnityEngine.GameObject).Assembly);   // CoreModule
            Add(typeof(UnityEngine.Animator).Assembly);      // AnimationModule
            Add(typeof(UnityEngine.AudioSource).Assembly);   // AudioModule
            Add(typeof(UnityEngine.Rigidbody).Assembly);     // PhysicsModule
            Add(typeof(UnityEngine.TextMesh).Assembly);      // TextRenderingModule
            Add(typeof(UnityEngine.UI.Text).Assembly);       // UnityEngine.UI
            Add(typeof(UnityEngine.Canvas).Assembly);        // UIModule
            Add(typeof(global::TMPro.TMP_Text).Assembly);    // TextMeshPro
            Add(typeof(UnityEngine.ParticleSystem).Assembly);    // ParticleSystemModule
        }
        void Add(Assembly a) => shims[a.GetName().Name] = a;
        protected override Assembly Load(AssemblyName name)
            => name.Name != null && shims.TryGetValue(name.Name, out var a) ? a : null;
    }

    public static class Program
    {
        public static int Main(string[] args)
        {
            IPlatform platform = new WindowsPlatform();
            TriggerSystem.Init();

            static string DataDirForPlay(string arg) => arg;
            if (args.Length > 0 && args[0] == "--headless") return Headless.Run(platform);
            if (args.Length > 2 && args[0] == "--load-scene") return LoadSceneTest.Run(platform, args[1], args[2]);
            if (args.Length > 1 && args[0] == "--boot")
                return BootTest.Run(platform, args[1],
                    args.Length > 2 ? args[2] : "level0",
                    args.Length > 3 ? args[3] : null,
                    args.Length > 4 && int.TryParse(args[4], out var n) ? n : 600);
            if (args.Length > 1 && args[0] == "--ui")
                return UiCheck.Run(platform, args[1],
                    args.Length > 2 ? args[2] : "level0");
            if (args.Length > 1 && args[0] == "--play")
                return WindowRunner.Run(DataDirForPlay(args[1]),
                    args.Length > 2 ? args[2] : "level0",
                    args.Length > 3 && int.TryParse(args[3], out var pf) ? pf : 0);
            if (args.Length == 0)
            {
                Console.WriteLine("Uso: Unirecomp.Host \"<jogo>_Data/Managed\"");
                Console.WriteLine("Smoke test do shim Tier 0 (sem jogo):");
                Smoke.Run(platform);
                return 0;
            }
            string dataDir = args[0];
            string gameDll = Path.Combine(dataDir, "Managed", "Assembly-CSharp.dll");
            if (!File.Exists(gameDll))
            {
                Console.Error.WriteLine("Assembly-CSharp.dll nao encontrado em " + gameDll);
                return 2;
            }
            Assembly game;
            try
            {
                var alc = new UnirecompLoadContext();
                game = alc.LoadFromAssemblyPath(Path.GetFullPath(gameDll));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Falha ao carregar jogo: " + ex.Message);
                return 3;
            }
            var behaviours = new System.Collections.Generic.List<Type>();
            try
            {
                behaviours.AddRange(game.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(MonoBehaviour)) && !t.IsAbstract));
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Esperado antes do binding (TODO Fase 1): vira diagnostico, nao crash.
                behaviours.AddRange(ex.Types.Where(t => t != null
                    && t.IsSubclassOf(typeof(MonoBehaviour)) && !t.IsAbstract));
                platform.Log("Binding pendente — assemblies que o jogo pediu e o host ainda nao resolve:");
                foreach (var msg in ex.LoaderExceptions.Select(e => e.Message.Split('\n')[0]).Distinct().Take(15))
                    platform.Log("  ! " + msg);
            }
            platform.Log($"Jogo carregado: {behaviours.Count} MonoBehaviours em {Path.GetFileName(gameDll)} (dataDir: {dataDir})");
            foreach (var b in behaviours.Take(20))
                platform.Log("  - " + b.FullName);
            Smoke.Run(platform);
            return 0;
        }
    }

    /// <summary>Espelho C# do prototipo executavel runtime/proto/boot_test.py.</summary>
    public static class Smoke
    {
        static void Check(bool cond, string name)
        {
            if (!cond) throw new Exception("FALHOU: " + name);
            Console.WriteLine("  ok: " + name);
        }

        public static void Run(IPlatform platform)
        {
            platform.BeginFrame(1f / 60f);
            var go = new GameObject("player");
            go.transform.localPosition = new Vector3(1, 2, 3);
            Check(go.transform.position.x == 1, "posicao local");
            var child = new GameObject("arma");
            child.transform.SetParent(go.transform);
            child.transform.localPosition = new Vector3(0, 1, 0);
            Check(child.transform.position.y == 3, "posicao mundo via pai");
            Check(Vector3.Dot(Vector3.right, Vector3.up) == 0, "dot ortogonal");
            Check(Quaternion.identity.eulerAngles.magnitude < 1e-3f, "identidade euler~0");
            Check(Mathf.Clamp(5, 0, 1) == 1, "clamp");
            Check(Time.deltaTime > 0, "time injecao plataforma");
            var clone = (GameObject)UnityEngine.Object.Instantiate(go);
            Check(clone != null && clone != go && clone.name == "player", "instantiate clona");
            Check(GameObject.Find("player") == go, "find raiz");
            UnityEngine.Object.Destroy(clone);
            Check(!clone, "destroy invalida (bool)");
            Debug.Log("smoke Tier 0 ok na plataforma " + platform.Name);
        }
    }

    /// <summary>Teste headless Fase 2a: 600 frames de player loop sem assets.</summary>
    public static class Headless
    {
        public class Spinner : MonoBehaviour
        {
            public int updates, starts;
            public bool awoke, coroutineDone, invoked;
            void Awake() { awoke = true; }
            void Start() { starts++; StartCoroutine(Routine()); Invoke(nameof(Flag), 0.1f); }
            System.Collections.IEnumerator Routine()
            {
                yield return new WaitForSeconds(0.15f);
                coroutineDone = true;
            }
            void Flag() { invoked = true; }
            void Update() { updates++; }
        }
        public class Sleeper : MonoBehaviour
        {
            public bool awoke, destroyed;
            public int updates;
            void Awake() { awoke = true; }
            void Update() { updates++; }
            void OnDestroy() { destroyed = true; }
        }
        static void Check(bool cond, string name)
        {
            if (!cond) throw new Exception("HEADLESS FALHOU: " + name);
            Console.WriteLine("  ok: " + name);
        }
        public static int Run(IPlatform platform)
        {
            SceneManager.SetBuildScenes(new[] { "main_menu", "level_01" });
            var go = new GameObject("runner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var sp = go.AddComponent<Spinner>();
            var off = new GameObject("off");
            var sl = off.AddComponent<Sleeper>();
            sl.enabled = false;
            var temp = new GameObject("temp");
            var tmp = temp.AddComponent<Sleeper>();
            int sceneEvents = 0;
            SceneManager.sceneLoaded += (s, m) => sceneEvents++;
            const float dt = 1f / 60f;
            for (int f = 1; f <= 600; f++)
            {
                platform.BeginFrame(dt);
                PlayerLoop.Tick(dt);
                if (f == 10) UnityEngine.Object.Destroy(temp);
                if (f == 20) SceneManager.LoadSceneAsync("level_01");
            }
            Check(sp.awoke, "awake");
            Check(sp.starts == 1, "start 1x");
            Check(sp.updates == 600, "update 600x");
            Check(sp.coroutineDone, "corotina wait 0.15s");
            Check(sp.invoked, "invoke 0.1s");
            Check(sl.awoke && sl.updates == 0, "desabilitado: awake sim, update nao");
            Check(tmp.updates == 10 && tmp.destroyed && !temp, "destroy no frame 10 + ondestroy");
            Check(UnityEngine.Object.KeepAlive.Contains(go), "dontdestroyonload marca");
            Check(SceneManager.GetActiveScene().buildIndex == 1, "cena ativa level_01");
            Check(SceneManager.sceneCountInBuildSettings == 2, "2 cenas no build");
            Check(sceneEvents == 1, "sceneLoaded 1x");
            platform.Log("headless 600 frames ok");
            return 0;
        }
    }
}
