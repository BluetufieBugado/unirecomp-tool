// 2b-iv — SceneDirector: troca REAL de conteúdo para o SceneManager.
// Destrói tudo que não é KeepAlive (DontDestroyOnLoad) e instancia o level
// alvo via SceneBuilder (hierarquia + fields). Injetado no shim através de
// SceneManager.SwapContent; sem ele o SceneManager só troca o índice
// (comportamento usado pelo teste headless mecânico).
//
// Limitações desta fatia (dev-only): nomes/scripts/layouts vêm dos JSONs do
// repo (mbmap/layouts); a resolução runtime via MonoScripts do
// globalgamemanagers vem depois. Roda da raiz do repo.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Unirecomp.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unirecomp.Host
{
    // Setup compartilhado de boot (--boot e --play): mesma cena inicial,
    // mesmos JSONs, mesmos assemblies. Uma fonte da verdade.
    public static class BootSetup
    {
        // Ordem real do build (Stardust Raiders/ProjectSettings/EditorBuildSettings.asset).
        public static readonly (string scene, string file)[] Map =
        {
            ("main_menu", "level0"), ("level_01", "level1"),
            ("boss_fight", "level2"), ("credits", "level3"),
        };

        public static string SceneOf(string file) => Map.First(kv => kv.file == file).scene;

        public static Assembly LoadGame(string dataDir) =>
            new UnirecompLoadContext().LoadFromAssemblyPath(
                Path.GetFullPath(Path.Combine(dataDir, "Managed", "Assembly-CSharp.dll")));

        public static List<Assembly> ShimAsms() => new List<Assembly>
        {
            typeof(UnityEngine.GameObject).Assembly, typeof(UnityEngine.Animator).Assembly,
            typeof(UnityEngine.AudioSource).Assembly, typeof(UnityEngine.Rigidbody).Assembly,
            typeof(UnityEngine.TextMesh).Assembly, typeof(UnityEngine.UI.Text).Assembly,
            typeof(UnityEngine.Canvas).Assembly, typeof(global::TMPro.TMP_Text).Assembly,
            typeof(UnityEngine.ParticleSystem).Assembly,
        };

        public static Layouts LoadLayouts() => Layouts.Load("docs/layouts_stardust.json");
        public static JsonDocument LoadMbmap() =>
            JsonDocument.Parse(File.ReadAllText("docs/mbmap_stardust.json"));

        /// <summary>
        /// MonoScripts do globalgamemanagers.assets: mapeia nome-classe -> assembly+classe.
        /// Usado para resolver comportamentos que aparecem nas cenas (quando o mbmap
        /// nao cobre ou quando queremos usar o MonoScript em vez do mbmap).
        /// </summary>
        public static Dictionary<string, (string assembly, string cls)> MonoScriptsForGame()
        {
            var db = LoadMonoScripts();
            var res = new Dictionary<string, (string, string)>();
            foreach (var kv in db)
            {
                var m = kv.Value;
                if (string.IsNullOrEmpty(m.Name)) continue;
                if (m.Assembly == "Assembly-CSharp.dll")
                    res[m.Name] = (m.Assembly, m.ClassName);
            }
            return res;
        }

        public sealed class MonoScriptEntry
        {
            public string Name { get; set; } = "";
            public string ClassName { get; set; } = "";
            public string Assembly { get; set; } = "";

            public MonoScriptEntry() { }

            public MonoScriptEntry(string n, string c, string a)
            {
                Name = n; ClassName = c; Assembly = a;
            }
        }

        public static Dictionary<long, MonoScriptEntry> LoadMonoScripts()
        {
            return JsonSerializer.Deserialize<Dictionary<long, MonoScriptEntry>>(
                File.ReadAllText("docs/gm_mono_scripts.json"));
        }
    }

    public sealed class SceneDirector
    {
        readonly string dataDir;
        readonly Assembly gameAsm;
        readonly List<Assembly> shimAsms;
        readonly Layouts layouts;
        readonly List<string> order = new List<string>();
        readonly Dictionary<string, string> fileOf = new Dictionary<string, string>();
        readonly Dictionary<string, Dictionary<long, string>> scriptsOf = new Dictionary<string, Dictionary<long, string>>();
        public int Swaps;

        public SceneDirector(string dataDir, Assembly gameAsm, List<Assembly> shimAsms,
            Layouts layouts, IEnumerable<(string scene, string file)> map, JsonDocument mbmap)
        {
            this.dataDir = dataDir;
            this.gameAsm = gameAsm;
            this.shimAsms = shimAsms;
            this.layouts = layouts;
            foreach (var (scene, file) in map)
            {
                order.Add(scene);
                fileOf[scene] = file;
                var d = new Dictionary<long, string>();
                foreach (var e in mbmap.RootElement.GetProperty(file).GetProperty("mapped").EnumerateArray())
                    d[e.GetProperty("pathID").GetInt64()] = e.GetProperty("script").GetString();
                scriptsOf[file] = d;
            }
            SceneManager.SetBuildScenes(order);
            SceneManager.SwapContent = Swap;
            EnsureAudioClock();
        }

        static bool audioClockOn;
        static void EnsureAudioClock()
        {
            if (audioClockOn) return;
            audioClockOn = true;
            FmodBackend.TryInstall(); // sem fmod.dll: segue mudo (A1), 1 aviso
            PlayerLoop.TickTriggers += AudioSystem.Tick;
        }

        public void LoadInitial(string scene)
        {
            int idx = order.IndexOf(scene);
            if (idx < 0) throw new InvalidOperationException("cena fora do build: " + scene);
            if (!Swap(idx)) throw new InvalidOperationException("falha na carga inicial");
        }

        bool Swap(int index)
        {
            string scene = order[index];
            string file = fileOf[scene];
            var survivors = new HashSet<GameObject>();
            foreach (var o in UnityEngine.Object.KeepAlive.ToArray())
            {
                if (o is GameObject go) survivors.Add(go);
                else if (o is Component c && c.gameObject != null) survivors.Add(c.gameObject);
            }
            foreach (var r in GameObject.Roots.ToArray())
                if (!survivors.Contains(r)) UnityEngine.Object.Destroy(r);
            var level = SerializedFile.Load(Path.Combine(dataDir, file));
            var mono = MonoScriptResolver.MonoScriptsForGame();
            var built = SceneBuilder.BuildLevel(level, gameAsm, scriptsOf[file], shimAsms, layouts, mono);
            MeshLibrary.Apply(built, file);
            AudioLibrary.Apply(built, file, dataDir);
            SpriteLibrary.Apply(built, file, dataDir);
            Swaps++;
            return true;
        }
    }
}
