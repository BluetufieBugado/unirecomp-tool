// Tier 0 — object model: Object, Component, GameObject, Transform.
// Reimplementacao limpa baseada na checklist docs/apicheck_stardust.json.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        static readonly List<Object> registry = new List<Object>();
        bool destroyed;
        // Propriedades (não campos!): o CIL do jogo chama get_/set_.
        public string name { get; set; }
        public Object() { registry.Add(this); }
        public static void Destroy(Object obj)
        {
            if (obj == null || obj.destroyed) return;
            if (obj is GameObject go)
            {
                foreach (Transform child in go.transform.Cast<Transform>().ToArray())
                    Destroy(child.gameObject);
                foreach (var c in go.components.ToArray())
                    Destroy(c);
                GameObject.Unregister(go);
            }
            if (obj is Component comp && comp.gameObject != null)
                comp.gameObject.components.Remove(comp);
            if (obj is MonoBehaviour mb)
            {
                // Unity: OnDisable (se estava ativo) antes do OnDestroy.
                if (mb.enabled && mb.gameObject != null && mb.gameObject.activeInHierarchy)
                    PlayerLoop.Call(mb, "OnDisable");
                PlayerLoop.Call(mb, "OnDestroy");
            }
            obj.destroyed = true;
            registry.Remove(obj);
            KeepAlive.Remove(obj);
        }
        internal static readonly HashSet<Object> KeepAlive = new HashSet<Object>();
        public static void DontDestroyOnLoad(Object obj) { if (obj != null) KeepAlive.Add(obj); }
        public static List<T> AllOf<T>() where T : Object
        {
            var list = new List<T>();
            foreach (var o in registry.ToArray())
                if (o is T t && !t.destroyed) list.Add(t);
            return list;
        }
        public static Object Instantiate(Object original)
        {
            if (original == null) return null;
            if (original is GameObject go) return CloneGameObject(go);
            Object clone = (Object)original.MemberwiseClone();
            clone.destroyed = false;
            registry.Add(clone);
            return clone;
        }
        public static T Instantiate<T>(T original) where T : Object => (T)Instantiate((Object)original);
        public static bool operator ==(Object a, Object b)
        {
            if (ReferenceEquals(a, null)) return ReferenceEquals(b, null) || (b != null && b.destroyed);
            if (ReferenceEquals(b, null)) return a.destroyed;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public static implicit operator bool(Object exists) => !(exists == null);
        public override bool Equals(object o) => this == (o as Object);
        public override int GetHashCode() => base.GetHashCode();
        static GameObject CloneGameObject(GameObject src)
        {
            var clone = new GameObject(src.name);
            clone.transform.localPosition = src.transform.localPosition;
            clone.transform.localRotation = src.transform.localRotation;
            clone.transform.localScale = src.transform.localScale;
            foreach (var c in src.components)
            {
                var cc = (Component)Activator.CreateInstance(c.GetType());
                cc.gameObject = clone;
                clone.components.Add(cc);
            }
            return clone;
        }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; }
        public string tag { get => gameObject?.tag; set { if (gameObject != null) gameObject.tag = value; } }
        public Transform transform => gameObject?.transform;
        public bool CompareTag(string t) => gameObject != null && gameObject.tag == t;
        public T GetComponent<T>() where T : Component => gameObject?.GetComponent<T>();
        public Component GetComponent(Type t) => gameObject?.GetComponent(t);
        public T GetComponentInChildren<T>() where T : Component => gameObject?.GetComponentInChildren<T>();
        public Component GetComponentInChildren(Type t) => gameObject?.GetComponentInChildren(t);
        public T[] GetComponentsInChildren<T>() where T : Component => gameObject?.GetComponentsInChildren<T>() ?? new T[0];
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; } = true;
    }

    public class MonoBehaviour : Behaviour
    {
        public void Invoke(string methodName, float time) => InvokeRunner.Add(this, methodName, time);
        public Coroutine StartCoroutine(IEnumerator routine) { CoroutineRunner.Track(routine, this); return new Coroutine(); }
        // 2b-iv: StartCoroutine por nome (muito usado pelo jogo: "BarrelRoll",
        // "Boost", "ArrowAnimation"...). Sem StopCoroutine no jogo — fora do escopo.
        public Coroutine StartCoroutine(string methodName) => StartCoroutine(methodName, null);
        public Coroutine StartCoroutine(string methodName, object value)
        {
            var t = GetType();
            MethodInfo mi = null;
            for (var c = t; c != null && c != typeof(object); c = c.BaseType)
            {
                foreach (var m in c.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (m.Name != methodName || !typeof(IEnumerator).IsAssignableFrom(m.ReturnType)) continue;
                    int n = m.GetParameters().Length;
                    if (n > 1) continue;
                    if (mi == null) mi = m;
                    // prefere aridade compatível com o argumento
                    if ((value == null && n == 0) || (value != null && n == 1)) { mi = m; break; }
                }
                if (mi != null && ((value == null && mi.GetParameters().Length == 0) || (value != null && mi.GetParameters().Length == 1))) break;
            }
            if (mi == null) { Debug.LogError($"StartCoroutine: {t.Name}.{methodName} nao encontrado"); return null; }
            object[] args = null;
            if (mi.GetParameters().Length == 1)
            {
                var pt = mi.GetParameters()[0].ParameterType;
                args = new[] { CoerceCoroutineArg(value, pt, $"{t.Name}.{methodName}") };
            }
            IEnumerator routine;
            try { routine = (IEnumerator)mi.Invoke(this, args); }
            catch (Exception ex) { Debug.LogError($"StartCoroutine {methodName}: " + (ex.InnerException?.Message ?? ex.Message)); return null; }
            if (routine == null) return null;
            CoroutineRunner.Track(routine, this);
            return new Coroutine();
        }
        static object CoerceCoroutineArg(object value, Type pt, string where)
        {
            if (value == null) return pt.IsValueType ? Activator.CreateInstance(pt) : null;
            if (pt.IsInstanceOfType(value)) return value;
            try { return Convert.ChangeType(value, pt); }
            catch (Exception) { throw new InvalidOperationException($"{where}: arg {value} incompativel com {pt.Name}"); }
        }
    }

    public class Coroutine : Object { }

    public class GameObject : Object
    {
        internal readonly List<Component> components = new List<Component>();
        static readonly List<GameObject> roots = new List<GameObject>();
        internal static IReadOnlyList<GameObject> Roots => roots;
        internal static void Unregister(GameObject go) => roots.Remove(go);
        public new string name { get => base.name; set => base.name = value; }
        public string tag { get; set; }
        public Transform transform { get; internal set; }
        bool _active = true;
        public GameObject() : this("New Game Object") { }
        public GameObject(string name)
        {
            this.name = name;
            transform = new Transform { gameObject = this };
            roots.Add(this);
        }
        // UI-1: troca o Transform provisório pelo RectTransform real (Pass 2).
        internal void UseRectTransform(RectTransform rt)
        {
            rt.gameObject = this;
            transform = rt;
            components.Add(rt);
        }
        public bool activeSelf => _active;
        public bool activeInHierarchy => _active && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public GameObject gameObject => this;
        public void SetActive(bool v) => _active = v;
        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T { gameObject = this };
            components.Add(c);
            return c;
        }
        public Component AddComponent(Type t)
        {
            var c = (Component)Activator.CreateInstance(t);
            c.gameObject = this;
            components.Add(c);
            return c;
        }
        public T GetComponent<T>() where T : Component
        {
            foreach (var c in components) if (c is T t) return t;
            return null;
        }
        public Component GetComponent(Type t)
        {
            foreach (var c in components) if (t.IsInstanceOfType(c)) return c;
            return null;
        }
        public T GetComponentInChildren<T>() where T : Component
        {
            var self = GetComponent<T>();
            if (self != null) return self;
            foreach (Transform child in transform)
            {
                var f = child.gameObject.GetComponentInChildren<T>();
                if (f != null) return f;
            }
            return null;
        }
        public Component GetComponentInChildren(Type t)
        {
            var self = GetComponent(t);
            if (self != null) return self;
            foreach (Transform child in transform)
            {
                var f = child.gameObject.GetComponentInChildren(t);
                if (f != null) return f;
            }
            return null;
        }
        public T[] GetComponentsInChildren<T>() where T : Component
        {
            var list = new List<T>();
            Collect(this, list);
            return list.ToArray();
        }
        static void Collect<T>(GameObject go, List<T> list) where T : Component
        {
            foreach (var c in go.components) if (c is T t) list.Add(t);
            foreach (Transform child in go.transform) Collect(child.gameObject, list);
        }
        public T GetComponentInParent<T>() where T : Component
        {
            Transform t = transform;
            while (t != null)
            {
                var f = t.gameObject.GetComponent<T>();
                if (f != null) return f;
                t = t.parent;
            }
            return null;
        }
        public static GameObject Find(string n)
        {
            foreach (var r in roots) { var f = FindIn(r, n); if (f != null) return f; }
            return null;
        }
        static GameObject FindIn(GameObject go, string n)
        {
            if (go.name == n) return go;
            foreach (Transform child in go.transform)
            {
                var f = FindIn(child.gameObject, n);
                if (f != null) return f;
            }
            return null;
        }
        public static GameObject FindGameObjectWithTag(string t)
        {
            foreach (var r in roots) { var f = FindTagIn(r, t); if (f != null) return f; }
            return null;
        }
        static GameObject FindTagIn(GameObject go, string t)
        {
            if (go.tag == t) return go;
            foreach (Transform child in go.transform)
            {
                var f = FindTagIn(child.gameObject, t);
                if (f != null) return f;
            }
            return null;
        }
        public void SendMessageUpwards(string methodName)
        {
            Transform t = transform;
            while (t != null)
            {
                foreach (var c in t.gameObject.components)
                    c.GetType().GetMethod(methodName)?.Invoke(c, null);
                t = t.parent;
            }
        }
    }

    public class Transform : Component, IEnumerable
    {
        readonly List<Transform> children = new List<Transform>();
        Transform _parent;
        public Vector3 localPosition { get; set; }
        public Quaternion localRotation { get; set; } = Quaternion.identity;
        public Vector3 localScale { get; set; } = Vector3.one;
        public Transform parent
        {
            get => _parent;
            set => SetParent(value);
        }
        public Vector3 position
        {
            get => _parent == null ? localPosition : _parent.TransformPoint(localPosition);
            set => localPosition = _parent == null ? value : _parent.InverseTransformPoint(value);
        }
        public Quaternion rotation
        {
            get => _parent == null ? localRotation : _parent.rotation * localRotation;
            set => localRotation = _parent == null ? value : QuaternionNormalize(Inverse(_parent.rotation) * value);
        }
        static Quaternion QuaternionNormalize(Quaternion q) => Quaternion.Slerp(Quaternion.identity, q, 1f);
        static Quaternion Inverse(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);
        public Vector3 forward { get => rotation * Vector3.forward; set => LookAt(position + value); }
        public Vector3 up => rotation * Vector3.up;
        public Vector3 lossyScale
        {
            get
            {
                Vector3 s = localScale;
                Transform p = _parent;
                while (p != null) { s = new Vector3(s.x * p.localScale.x, s.y * p.localScale.y, s.z * p.localScale.z); p = p._parent; }
                return s;
            }
        }
        public void SetParent(Transform p)
        {
            _parent?.children.Remove(this);
            _parent = p;
            p?.children.Add(this);
        }
        public void Translate(Vector3 t) => localPosition = localPosition + t;
        public void Rotate(Vector3 euler) => localRotation = localRotation * Quaternion.Euler(euler);
        public void Rotate(Vector3 euler, float degrees) => localRotation = localRotation * Quaternion.Euler(euler * degrees);
        public void RotateAround(Vector3 point, Vector3 axis, float angle)
        {
            Quaternion q = Quaternion.AngleAxis(angle, axis);
            Vector3 d = position - point;
            position = point + q * d;
            rotation = q * rotation;
        }
        public void LookAt(Vector3 target) => rotation = Quaternion.LookRotation(target - position);
        public void LookAt(Transform target) => LookAt(target.position);
        public Vector3 TransformPoint(Vector3 p) => position + rotation * Scale(p, lossyScale);
        public Vector3 TransformDirection(Vector3 d) => rotation * d;
        /// <summary>Matriz mundo row-major (TRS local composta com os pais).</summary>
        public Matrix4x4 LocalToWorldMatrix()
        {
            var local = Matrix4x4.TRS(localPosition, localRotation, localScale);
            if (_parent == null) return local;
            return _parent.LocalToWorldMatrix() * local;
        }
        public Vector3 InverseTransformPoint(Vector3 world)
        {
            Vector3 d = world - position;
            Quaternion inv = Inverse(rotation);
            Vector3 r = inv * d;
            Vector3 s = lossyScale;
            return new Vector3(r.x / s.x, r.y / s.y, r.z / s.z);
        }
        static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public IEnumerator GetEnumerator() => children.GetEnumerator();
    }
}
