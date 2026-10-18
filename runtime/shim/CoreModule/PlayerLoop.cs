// Fase 2a — player loop headless: invokes, lifecycle, corotinas, cenas.
// Semantica Unity: Awake sempre; Start/Update só com enabled+ativo;
// corotinas pausam quando desabilitadas e morrem com o dono.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unirecomp.Host")]

namespace UnityEngine
{
    static class InvokeRunner
    {
        struct Item { public MonoBehaviour target; public string method; public float time; }
        static readonly List<Item> pending = new List<Item>();
        public static void Add(MonoBehaviour t, string m, float d)
            => pending.Add(new Item { target = t, method = m, time = d });
        public static void Tick(float dt)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (p.target == null) { pending.RemoveAt(i); continue; }
                p.time -= dt;
                if (p.time <= 0) { pending.RemoveAt(i); PlayerLoop.Call(p.target, p.method); }
                else pending[i] = p;
            }
        }
    }

    static class CoroutineRunner
    {
        class Slot
        {
            public readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
            public MonoBehaviour owner;
            public object wait;
            public float timer;
        }
        static readonly List<Slot> active = new List<Slot>();
        static readonly List<Slot> endOfFrame = new List<Slot>();
        public static void Track(IEnumerator routine, MonoBehaviour owner)
        {
            var s = new Slot { owner = owner };
            s.stack.Push(routine);
            active.Add(s);
        }
        public static void Tick(float dt)
        {
            active.RemoveAll(s => s.owner == null);
            foreach (var s in active.ToArray())
            {
                if (!s.owner.enabled || s.owner.gameObject?.activeInHierarchy != true)
                    continue;
                if (s.wait is WaitForSeconds) { s.timer -= dt; if (s.timer > 0) continue; s.wait = null; }
                else if (s.wait != null) continue;
                Step(s);
            }
            active.RemoveAll(s => s.stack.Count == 0);
        }
        static void Step(Slot s)
        {
            while (s.stack.Count > 0)
            {
                var top = s.stack.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (Exception ex) { Debug.LogError($"corotina {s.owner?.GetType().Name}: " + PlayerLoop.Fmt(ex)); s.stack.Clear(); return; }
                if (!more) { s.stack.Pop(); continue; }
                var y = top.Current;
                if (y == null) return;
                if (y is WaitForSeconds ws) { s.wait = y; s.timer = ws.Seconds; return; }
                if (y is WaitForEndOfFrame) { s.wait = y; endOfFrame.Add(s); return; }
                if (y is IEnumerator nested) { s.stack.Push(nested); continue; }
                return;
            }
        }
        public static void TickEndOfFrame()
        {
            var run = endOfFrame.ToArray();
            endOfFrame.Clear();
            foreach (var s in run)
            {
                if (s.stack.Count == 0 || s.owner == null) continue;
                s.wait = null;
                Step(s);
            }
        }
    }

    public static class PlayerLoop
    {
        public static Action TickTriggers;
        static readonly HashSet<MonoBehaviour> _awake = new HashSet<MonoBehaviour>();
        static readonly HashSet<MonoBehaviour> _started = new HashSet<MonoBehaviour>();
        static readonly Dictionary<MonoBehaviour, bool> _enableState = new Dictionary<MonoBehaviour, bool>();
        static readonly Dictionary<(Type, string), MethodInfo> _cache
            = new Dictionary<(Type, string), MethodInfo>();
        public static int Frame { get; private set; }
        public static void Tick(float dt)
        {
            Frame++;
            _awake.RemoveWhere(m => m == null);
            _started.RemoveWhere(m => m == null);
            foreach (var k in _enableState.Keys.ToArray())
                if (k == null) _enableState.Remove(k);
            InvokeRunner.Tick(dt);
            // Ordem de Awake hierárquica (pais antes dos filhos), como no Unity
            // no load da cena: evita NRE de singleton (ex. OptionsMenu lê
            // GameManager.Instance no Awake e nasce depois dele no arquivo).
            var all = Object.AllOf<MonoBehaviour>().OrderBy(Depth).ToList();
            foreach (var mb in all)
                if (!_awake.Contains(mb)) { _awake.Add(mb); Call(mb, "Awake"); }
            foreach (var mb in all)
            {
                bool cur = mb.enabled && mb.gameObject != null && mb.gameObject.activeInHierarchy;
                bool last = _enableState.TryGetValue(mb, out var l) && l;
                if (cur != last) { _enableState[mb] = cur; Call(mb, cur ? "OnEnable" : "OnDisable"); }
            }
            foreach (var mb in all)
            {
                if (!mb.enabled || mb.gameObject?.activeInHierarchy != true) continue;
                if (!_started.Contains(mb)) { _started.Add(mb); Call(mb, "Start"); }
                Call(mb, "Update");
            }
            foreach (var mb in all)
            {
                if (!mb.enabled || mb.gameObject?.activeInHierarchy != true) continue;
                Call(mb, "LateUpdate");
            }
            TickTriggers?.Invoke();
            CoroutineRunner.Tick(dt);
            CoroutineRunner.TickEndOfFrame();
            SceneManagement.SceneManager.Tick();
        }
        internal static void Call(MonoBehaviour mb, string name)
        {
            var t = mb.GetType();
            if (!_cache.TryGetValue((t, name), out var mi))
            {
                mi = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                _cache[(t, name)] = mi;
            }
            try { mi?.Invoke(mb, null); }
            catch (Exception ex) { Debug.LogError($"{name} {mb.GetType().Name}: " + Fmt(ex)); }
        }

        internal static string Fmt(Exception ex)
        {
            var e = ex.InnerException ?? ex;
            string frame = e.StackTrace != null
                ? (e.StackTrace.Split('\n')[0].Trim())
                : "";
            return $"{e.GetType().Name}: {e.Message} {frame}";
        }

        static int Depth(MonoBehaviour mb)
        {
            int d = 0;
            var t = mb.gameObject?.transform?.parent;
            while (t != null) { d++; t = t.parent; }
            return d;
        }
    }
}
