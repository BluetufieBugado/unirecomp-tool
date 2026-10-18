// 2b-iv — BootTest --boot: o jogo de verdade instancia, roda e troca de cena.
// level0 (main_menu) → 300 frames → LoadSceneAsync(level3) → +300 frames.
// Prova: GameManager com DontDestroyOnLoad sobrevive (mesma instância, fields
// intactos), cena ativa vira credits, sceneLoaded dispara 1x com a cena certa.
// Roda da raiz do repo. Exit != 0 em qualquer divergência.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unirecomp.Host
{
    public static class BootTest
    {
        static readonly (string scene, string file)[] Map = BootSetup.Map;

        static int fails = 0;
        static void Check(bool cond, string name, string extra = "")
        {
            if (!cond) { fails++; Console.WriteLine("  FALHOU: " + name + " " + extra); }
            else Console.WriteLine("  ok: " + name);
        }

        static MonoBehaviour FindByName(string typeName)
        {
            foreach (var mb in UnityEngine.Object.AllOf<MonoBehaviour>())
                if (mb.GetType().Name == typeName) return mb;
            return null;
        }

        static object FGet(object o, string name)
        {
            for (var t = o.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                var fi = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (fi != null) return fi.GetValue(o);
            }
            throw new InvalidOperationException("field ausente: " + name);
        }

        public static int Run(IPlatform platform, string dataDir, string firstFile, string secondFile, int frames)
        {
            string first = BootSetup.SceneOf(firstFile);
            string second = secondFile != null ? BootSetup.SceneOf(secondFile) : null;
            var gameAsm = BootSetup.LoadGame(dataDir);
            var shimAsms = BootSetup.ShimAsms();
            var layouts = BootSetup.LoadLayouts();
            using var mbmap = BootSetup.LoadMbmap();
            var director = new SceneDirector(dataDir, gameAsm, shimAsms, layouts, Map, mbmap);

            int sceneEvents = 0;
            string lastScene = null;
            SceneManager.sceneLoaded += (s, m) => { sceneEvents++; lastScene = s.name; };

            try { director.LoadInitial(first); }
            catch (Exception ex) { Console.WriteLine("  FALHOU: carga inicial " + ex.Message); return 5; }
            Check(GameObject.Roots.Count > 0, "boot: raízes instanciadas", GameObject.Roots.Count.ToString());
            var gm0 = FindByName("GameManager");
            Check(gm0 != null, "boot: GameManager instanciado");

            const float dt = 1f / 60f;
            int swapAt = frames / 2;
            AsyncOperation op = null;
            for (int f = 1; f <= frames; f++)
            {
                platform.BeginFrame(dt);
                try { PlayerLoop.Tick(dt); }
                catch (Exception ex) { Check(false, "tick sem exceção", ex.GetType().Name + ": " + ex.Message); break; }
                if (f == 30)
                {
                    // Awake (DDL) + Start já rodaram: GameManager marcado?
                    Check(UnityEngine.Object.KeepAlive.Contains(gm0.gameObject), "boot: GameManager DontDestroyOnLoad");
                }
                if (f == swapAt && second != null) op = SceneManager.LoadSceneAsync(second);
            }

            if (second != null)
            {
                var gm1 = FindByName("GameManager");
                Check(gm1 != null, "swap: GameManager sobreviveu");
                Check(ReferenceEquals(gm0, gm1), "swap: mesma instância");
                Check(UnityEngine.Object.KeepAlive.Contains(gm1.gameObject), "swap: DDL mantido");
                Check((int)FGet(FGet(gm1, "playerInfo"), "lives") == 3, "swap: fields intactos (lives==3)");
                Check(SceneManager.GetActiveScene().name == second, "swap: cena ativa", SceneManager.GetActiveScene().name);
                Check(op != null && op.isDone, "swap: async done");
                Check(sceneEvents == 1 && lastScene == second, "swap: sceneLoaded 1x cena certa", $"{sceneEvents}x {lastScene}");
                Check(director.Swaps == 2, "swap: 2 instanciações", director.Swaps.ToString());
                Check(GameObject.Roots.Count > 0, "swap: nova cena tem raízes", GameObject.Roots.Count.ToString());
            }
            else
            {
                Check(SceneManager.GetActiveScene().name == first, "boot: cena ativa", SceneManager.GetActiveScene().name);
            }

            platform.Log(fails == 0 ? $"boot {firstFile}->{secondFile} OK ({frames} frames)" : $"boot com {fails} FALHAS");
            platform.Log(FmodBackend.Status());
            return fails == 0 ? 0 : 6;
        }
    }
}
