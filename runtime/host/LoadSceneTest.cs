// Teste --load-scene: índice C# vs oráculo + hierarquia main_menu validada.
// Roda da raiz do repo (lê docs/*.json). Exit != 0 em qualquer divergência.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Unirecomp.Serialization;
using UnityEngine;

namespace Unirecomp.Host
{
    public static class LoadSceneTest
    {
        static int fails = 0;
        static void Check(bool cond, string name, string extra = "")
        {
            if (!cond) { fails++; Console.WriteLine("  FALHOU: " + name + " " + extra); }
        }
        static void Ok(string name) => Console.WriteLine("  ok: " + name);

        public static int Run(IPlatform platform, string dataDir, string level)
        {
            var gameAsm = new UnirecompLoadContext().LoadFromAssemblyPath(
                Path.GetFullPath(Path.Combine(dataDir, "Managed", "Assembly-CSharp.dll")));
            var shimAsms = new List<Assembly>
            {
                typeof(UnityEngine.GameObject).Assembly, typeof(UnityEngine.Animator).Assembly,
                typeof(UnityEngine.AudioSource).Assembly, typeof(UnityEngine.Rigidbody).Assembly,
                typeof(UnityEngine.TextMesh).Assembly, typeof(UnityEngine.UI.Text).Assembly,
                typeof(UnityEngine.Canvas).Assembly, typeof(global::TMPro.TMP_Text).Assembly,
                typeof(UnityEngine.ParticleSystem).Assembly,
            };
            var levelSf = SerializedFile.Load(Path.Combine(dataDir, level));
            var gmSf = SerializedFile.Load(Path.Combine(dataDir, "globalgamemanagers.assets"));

            JsonDocument sfIdx = LoadJson($"docs/sfindex_{level}.json");
            JsonDocument scData = LoadJson("docs/scene_data.json");
            JsonDocument scenes = LoadJson("docs/scenes_stardust.json");
            JsonDocument mbmap = LoadJson("docs/mbmap_stardust.json");

            // A1: índice == oráculo.
            var oObjs = sfIdx.RootElement.GetProperty("objects");
            Check(oObjs.GetArrayLength() == levelSf.Objects.Count, "n objetos",
                $"{levelSf.Objects.Count} vs {oObjs.GetArrayLength()}");
            int i = 0;
            foreach (var e in oObjs.EnumerateArray())
            {
                var o = levelSf.Objects[i++];
                if (e.GetProperty("path_id").GetInt64() != o.PathID
                    || e.GetProperty("start").GetUInt32() + levelSf.DataOffset != o.AbsStart
                    || e.GetProperty("size").GetUInt32() != o.Size
                    || e.GetProperty("type_id").GetInt32() != o.TypeID)
                    Check(false, "objeto", $"path {o.PathID}");
            }
            Ok($"índice: {levelSf.Objects.Count} objetos == oráculo");
            var oTypes = sfIdx.RootElement.GetProperty("types");
            Check(oTypes.GetArrayLength() == levelSf.Types.Count, "n tipos");
            i = 0;
            foreach (var e in oTypes.EnumerateArray())
            {
                var t = levelSf.Types[i++];
                Check(e.GetProperty("class_id").GetInt32() == t.ClassID
                    && e.GetProperty("script_idx").GetInt32() == t.ScriptIdx, "tipo", $"idx {i}");
            }
            Ok("tipos == oráculo");
            var oExt = sfIdx.RootElement.GetProperty("externals");
            Check(oExt.GetArrayLength() == levelSf.Externals.Count, "n externals");
            i = 0;
            foreach (var e in oExt.EnumerateArray())
                Check(e.GetString() == levelSf.Externals[i++], "external", e.GetString());
            Ok("externals == oráculo");

            // A2: MonoScripts (classe 115) cobrem os links m_Script.
            var monoSet = new HashSet<long>();
            foreach (var o in gmSf.Objects)
                if (gmSf.Types[o.TypeID].ClassID == 115) monoSet.Add(o.PathID);
            platform.Log($"MonoScripts em globalgamemanagers.assets: {monoSet.Count}");

            // scriptOf via mbmap (oráculo temporário p/ NOMES; linkage é provado em A2b).
            var mbLevel = mbmap.RootElement.GetProperty(level).GetProperty("mapped");
            var scriptOf = new Dictionary<long, string>();
            var mbOracle = new Dictionary<long, (string cls, bool enabled)>();
            foreach (var e in mbLevel.EnumerateArray())
            {
                long pid = e.GetProperty("pathID").GetInt64();
                string[] parts = e.GetProperty("script").GetString().Split(new[] { "::" }, StringSplitOptions.None);
                scriptOf[pid] = e.GetProperty("script").GetString();
                mbOracle[pid] = (parts.Length > 1 ? parts[1] : parts[0], e.GetProperty("enabled").GetInt32() == 1);
            }

            var scene = SceneBuilder.BuildLevel(levelSf, gameAsm, scriptOf, shimAsms,
                Layouts.Load("docs/layouts_stardust.json"));
            foreach (var w in scene.Warnings) platform.Log("warn: " + w);

            // A2b: linkage m_Script (fileID==1 + pathID no set), sem oráculo de nomes.
            foreach (var kv in scene.ScriptLink)
            {
                Check(kv.Value.file == 1, "script fileID==1", $"mb {kv.Key}");
                Check(monoSet.Contains(kv.Value.path), "script pathID existe", $"mb {kv.Key}");
            }
            Ok($"linkage m_Script: {scene.ScriptLink.Count} behaviours");

            // A3/A4/A5: GOs, transforms, raízes.
            var oGos = scData.RootElement.GetProperty(level).GetProperty("gos");
            var oTrs = scData.RootElement.GetProperty(level).GetProperty("transforms");
            Check(scene.ByPath.Count == oGos.EnumerateObject().Count(), "n GOs",
                $"{scene.ByPath.Count}");
            foreach (var g in oGos.EnumerateObject())
            {
                long pid = long.Parse(g.Name);
                var go = scene.ByPath[pid];
                Check(go.name == g.Value.GetProperty("name").GetString(), "GO nome", g.Name);
                Check(go.activeSelf == g.Value.GetProperty("active").GetBoolean(), "GO active", go.name);
                Check(scene.TagIdx[pid] == g.Value.GetProperty("tag").GetInt32(), "GO tag", go.name);
                Check(scene.Layers[pid] == g.Value.GetProperty("layer").GetUInt32(), "GO layer", go.name);
            }
            Ok("GOs: nomes/active/tag/layer");
            foreach (var t in oTrs.EnumerateObject())
            {
                long tid = long.Parse(t.Name);
                var tr = FindTransform(scene, tid, t.Value.GetProperty("go").GetInt64());
                var p = t.Value.GetProperty("pos");
                var r = t.Value.GetProperty("rot");
                var s = t.Value.GetProperty("scale");
                Check(Close(tr.localPosition.x, p.GetProperty("x").GetDouble())
                    && Close(tr.localPosition.y, p.GetProperty("y").GetDouble())
                    && Close(tr.localPosition.z, p.GetProperty("z").GetDouble()), "TR pos", tid.ToString());
                var q = tr.localRotation;
                Check(Close(q.x, r.GetProperty("x").GetDouble())
                    && Close(q.y, r.GetProperty("y").GetDouble())
                    && Close(q.z, r.GetProperty("z").GetDouble())
                    && Close(q.w, r.GetProperty("w").GetDouble()), "TR rot", tid.ToString());
                Check(Close(tr.localScale.x, s.GetProperty("x").GetDouble())
                    && Close(tr.localScale.y, s.GetProperty("y").GetDouble())
                    && Close(tr.localScale.z, s.GetProperty("z").GetDouble()), "TR scale", tid.ToString());
                Check(t.Value.GetProperty("father").GetInt32() == 0
                    ? tr.parent == null : tr.parent != null, "TR father?", tid.ToString());
                int nkids = 0;
                foreach (var _ in tr) nkids++;
                Check(nkids == t.Value.GetProperty("children").GetArrayLength(), "TR filhos", tid.ToString());
                Check(scene.IsRect[tid] == (t.Value.GetProperty("kind").GetString() == "RectTransform"),
                    "TR kind", tid.ToString());
            }
            Ok("transforms: pos/rot/scale/father/filhos/kind");
            var oRoots = new HashSet<string>();
            foreach (var lv in scenes.RootElement.GetProperty("levels").EnumerateArray())
                if (lv.GetProperty("file").GetString() == level)
                    foreach (var rn in lv.GetProperty("roots").EnumerateArray())
                        oRoots.Add(rn.GetString());
            var bRoots = new HashSet<string>();
            foreach (var r in scene.Roots) bRoots.Add(r.name);
            Check(oRoots.SetEquals(bRoots), "raízes", string.Join(",", bRoots));
            Ok($"raízes: {scene.Roots.Count}");

            // A6: behaviours com tipos certos + enabled (ProBuilder pulado no parser).
            int expectBeh = scriptOf.Values.Count(s => !s.StartsWith("ProBuilder"));
            Check(scene.Behaviours.Count == expectBeh, "n behaviours",
                $"{scene.Behaviours.Count} vs {expectBeh}");
            foreach (var (mb, type, enabled) in scene.Behaviours)
            {
                var (cls, en) = mbOracle[mb];
                Check(type.Name == cls, "behaviour tipo", $"{mb}={type.Name} vs {cls}");
                Check(enabled == en, "behaviour enabled", $"{mb}");
            }
            Ok($"behaviours: {scene.Behaviours.Count} tipos+enabled");

            // A7: componentes engine vs by_type.
            JsonElement byType = default;
            foreach (var lv in scenes.RootElement.GetProperty("levels").EnumerateArray())
                if (lv.GetProperty("file").GetString() == level)
                    byType = lv.GetProperty("by_type");
            foreach (var kv in scene.Attached)
                Check(byType.TryGetProperty(kv.Key, out var n) && n.GetInt32() == kv.Value,
                    "attach " + kv.Key, $"{kv.Value}");
            Ok("attach engine == by_type");

            // A8: mundo composto de forma independente (double) == Transform.position.
            var oTrAll = oTrs;
            var goOf = new Dictionary<long, long>();
            foreach (var t in oTrAll.EnumerateObject())
                goOf[long.Parse(t.Name)] = t.Value.GetProperty("go").GetInt64();
            var fatherOf = new Dictionary<long, long>();
            foreach (var t in oTrAll.EnumerateObject())
                fatherOf[long.Parse(t.Name)] = t.Value.GetProperty("father").GetInt64();
            int checked_ = 0;
            double maxErr = 0; int maxTid = 0;
            foreach (var t in oTrAll.EnumerateObject())
            {
                long tid = long.Parse(t.Name);
                var w = WorldOf(oTrAll, fatherOf, tid);
                var tr = FindTransform(scene, tid, goOf[tid]);
                double e = Math.Max(Math.Abs(tr.position.x - w[0]), Math.Max(Math.Abs(tr.position.y - w[1]), Math.Abs(tr.position.z - w[2])));
                double m = Math.Max(Math.Abs(w[0]), Math.Max(Math.Abs(w[1]), Math.Abs(w[2])));
                if (e > maxErr) { maxErr = e; maxTid = (int)tid; }
                // Tolerância pela magnitude do VETOR: rotação mistura eixos, o
                // erro do eixo gigante (z~2300, ulp f32 2.4e-4) vaza p/ x/y.
                double tol = 1e-4 + 1e-6 * m;
                if (Math.Abs(tr.position.x - w[0]) <= tol && Math.Abs(tr.position.y - w[1]) <= tol && Math.Abs(tr.position.z - w[2]) <= tol)
                    checked_++;
                else Check(false, "mundo", $"{tid} shim=({tr.position.x:F3},{tr.position.y:F3},{tr.position.z:F3}) oracle=({w[0]:F3},{w[1]:F3},{w[2]:F3})");
            }
            platform.Log($"mundo: maxErr={maxErr:G5} em tid {maxTid}");
            Ok($"mundo composto: {checked_} transforms");

            // A9 (2b-iii): fields aplicados com assert de tamanho + valores.
            int expectGame = scriptOf.Values.Count(s => s.StartsWith("Assembly-CSharp"));
            Check(scene.FieldApplied == expectGame, "fields aplicados",
                $"{scene.FieldApplied} vs {expectGame}");
            Check(scene.FieldApplied + scene.FieldSkipped == scene.Behaviours.Count, "fields cobertura total");
            Check(scene.FieldValues.Count == scene.FieldApplied, "field values registrados");
            platform.Log($"fields: {scene.FieldApplied} aplicados, {scene.FieldSkipped} engine pulados, {scene.UnresolvedPPtr} PPtrs cross-file->null");
            if (level == "level0")
            {
                CheckGm(scene, 605);
                CheckRailMover(scene, 588);
                CheckRail(scene, 625);
                CheckMainMenu(scene, 511);
                CheckAudio(scene, 635);
            }
            Ok("fields: valores verificados");

            platform.Log(fails == 0 ? $"load-scene {level} OK" : $"load-scene {level} com {fails} FALHAS");
            return fails == 0 ? 0 : 4;
        }

        static Transform FindTransform(BuiltScene scene, long tid, long goPath)
        {
            // tid->GO via IsRect+ByPath: o transform pertence ao GO goPath
            return scene.ByPath[goPath].transform;
        }

        // ---- A9 helpers (2b-iii) ----
        static object FGet(object o, string name)
        {
            for (var t = o.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                var fi = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (fi != null) return fi.GetValue(o);
            }
            throw new InvalidOperationException("field ausente: " + name);
        }

        static void CheckGm(BuiltScene scene, long pid)
        {
            var raw = scene.FieldValues[pid];
            Check((int)raw["gameState"] == 1, "GameManager.gameState==MainMenu(1)");
            var pi = (Dictionary<string, object>)raw["playerInfo"];
            Check((int)pi["lives"] == 3, "GameManager.lives==3");
            Check(!(bool)pi["invertYAxis"], "GameManager.invertY==false");
            var comp = scene.MbByPath[pid];
            Check(FGet(comp, "player") == null, "GameManager.player==null");
            Check((int)FGet(comp, "gameState") == 1, "GameManager inst.gameState==1");
            var pio = FGet(comp, "playerInfo");
            Check((int)FGet(pio, "lives") == 3, "GameManager inst.lives==3");
            Check(FGet(comp, "loadingScreen") != null, "GameManager.loadingScreen ligado");
            Check(FGet(comp, "loadSlider") != null, "GameManager.loadSlider ligado");
            Check(FGet(comp, "progressText") != null, "GameManager.progressText ligado");
        }

        static void CheckRailMover(BuiltScene scene, long pid)
        {
            var raw = scene.FieldValues[pid];
            Check(Math.Abs((float)raw["speed"] - 25f) < 1e-5, "RailMover.speed==25");
            Check(Math.Abs((float)raw["timeBetweenSegments"] - 2.5f) < 1e-5, "RailMover.timeBetweenSegments==2.5");
            Check((bool)raw["isLooping"], "RailMover.isLooping");
            var comp = scene.MbByPath[pid];
            Check(Math.Abs((float)FGet(comp, "speed") - 25f) < 1e-5, "RailMover inst.speed==25");
            Check(FGet(comp, "rail") != null, "RailMover.rail ligado");
        }

        static void CheckRail(BuiltScene scene, long pid)
        {
            var raw = scene.FieldValues[pid];
            Check(((List<object>)raw["nodes"]).Count == 2, "Rail.nodes==2");
            var comp = scene.MbByPath[pid];
            Check(((Array)FGet(comp, "nodes")).Length == 2, "Rail inst.nodes==2");
        }

        static void CheckMainMenu(BuiltScene scene, long pid)
        {
            var comp = scene.MbByPath[pid];
            Check(FGet(comp, "titleScreenUI") != null, "MainMenu.titleScreenUI ligado");
            Check(FGet(comp, "mainMenuUI") != null, "MainMenu.mainMenuUI ligado");
            Check(FGet(comp, "loopClip") == null, "MainMenu.loopClip cross-file->null");
        }

        static void CheckAudio(BuiltScene scene, long pid)
        {
            var raw = scene.FieldValues[pid];
            var sounds = (List<object>)raw["sounds"];
            Check(sounds.Count == 14, "AudioManager.sounds==14", sounds.Count.ToString());
            Check((string)((Dictionary<string, object>)sounds[0])["name"] == "Selection", "AudioManager[0]==Selection");
            var comp = scene.MbByPath[pid];
            Check(((Array)FGet(comp, "sounds")).Length == 14, "AudioManager inst.sounds==14");
        }

        static bool Close(double a, double b) => Math.Abs(a - b) < 1e-5;

        static double[] WorldOf(JsonElement oTrAll, Dictionary<long, long> fatherOf, long tid)
        {
            // composição independente em double: mundo = pai * local
            var chain = new List<long>();
            long cur = tid;
            while (cur != 0) { chain.Add(cur); cur = fatherOf[cur]; }
            chain.Reverse();
            double[] pos = { 0, 0, 0 };
            double[] rot = { 0, 0, 0, 1 };
            double[] scl = { 1, 1, 1 };
            foreach (var id in chain)
            {
                var t = oTrAll.GetProperty(id.ToString());
                var lp = V3(t.GetProperty("pos"));
                var lr = V4(t.GetProperty("rot"));
                var ls = V3(t.GetProperty("scale"));
                double[] rp = QMulVec(rot, new[] { lp[0] * scl[0], lp[1] * scl[1], lp[2] * scl[2] });
                pos = new[] { pos[0] + rp[0], pos[1] + rp[1], pos[2] + rp[2] };
                rot = QMul(rot, lr);
                scl = new[] { scl[0] * ls[0], scl[1] * ls[1], scl[2] * ls[2] };
            }
            return pos;
        }

        static double[] V3(JsonElement e) =>
            new[] { e.GetProperty("x").GetDouble(), e.GetProperty("y").GetDouble(), e.GetProperty("z").GetDouble() };
        static double[] V4(JsonElement e) =>
            new[] { e.GetProperty("x").GetDouble(), e.GetProperty("y").GetDouble(), e.GetProperty("z").GetDouble(), e.GetProperty("w").GetDouble() };
        static double[] QMul(double[] a, double[] b) => new[]
        {
            a[3]*b[0] + a[0]*b[3] + a[1]*b[2] - a[2]*b[1],
            a[3]*b[1] - a[0]*b[2] + a[1]*b[3] + a[2]*b[0],
            a[3]*b[2] + a[0]*b[1] - a[1]*b[0] + a[2]*b[3],
            a[3]*b[3] - a[0]*b[0] - a[1]*b[1] - a[2]*b[2],
        };
        static double[] QMulVec(double[] q, double[] v)
        {
            var qc = new[] { -q[0], -q[1], -q[2], q[3] };
            return QMul(QMul(q, new[] { v[0], v[1], v[2], 0.0 }), qc).Take(3).ToArray();
        }

        static JsonDocument LoadJson(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"rode da raiz do repo; faltando {path}");
            return JsonDocument.Parse(File.ReadAllText(path));
        }
    }
}
