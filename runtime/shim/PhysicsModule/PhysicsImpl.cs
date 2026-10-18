// Fase 3 — física de trigger: isTrigger, OnTriggerEnter/Stay/Exit,
// Collider.ClosestPointOnBounds, Physics.Raycast, Rigidbody.AddForce.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnityEngine
{
    public partial class Collider
    {
        public bool isTrigger { get; set; }
        public Vector3 ClosestPointOnBounds(Vector3 position)
        {
            var t = transform;
            if (t == null) return position;
            var local = t.InverseTransformPoint(position);
            if (this is BoxCollider)
            {
                var s = t.lossyScale;
                local.x = Mathf.Clamp(local.x, -s.x / 2, s.x / 2);
                local.y = Mathf.Clamp(local.y, -s.y / 2, s.y / 2);
                local.z = Mathf.Clamp(local.z, -s.z / 2, s.z / 2);
            }
            else if (this is SphereCollider || this is CapsuleCollider)
            {
                var s = t.lossyScale;
                var r = Mathf.Max(s.x, Mathf.Max(s.y, s.z)) / 2;
                if (local.x * local.x + local.y * local.y + local.z * local.z > r * r) local = local.normalized * r;
            }
            return t.TransformPoint(local);
        }
    }

    public partial class Rigidbody
    {
        public Vector3 velocity { get; set; }
        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force) => velocity += force;
    }

    public static partial class Physics
    {
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance = float.MaxValue, int layerMask = -1)
        {
            hit = default;
            float best = maxDistance;
            bool found = false;
            foreach (var c in Object.AllOf<Collider>())
            {
                var t = c.transform;
                if (t == null) continue;
                var toC = t.position - origin;
                var proj = Vector3.Dot(toC, direction);
                if (proj < 0 || proj > best) continue;
                var closest = origin + direction * proj;
                var diff = closest - t.position;
                var dist = diff.magnitude;
                var r = Mathf.Max(t.lossyScale.x, Mathf.Max(t.lossyScale.y, t.lossyScale.z)) / 2;
                if (dist <= r)
                {
                    best = proj;
                    hit = new RaycastHit { point = closest, normal = dist > 0 ? diff / dist : Vector3.up, distance = proj, transform = t };
                    found = true;
                }
            }
            return found;
        }
    }

    public partial struct RaycastHit
    {
        public Vector3 point { get; set; }
        public Vector3 normal { get; set; }
        public float distance { get; set; }
        public Transform transform { get; set; }
    }

    public static class TriggerSystem
    {
        class Pair { public Collider a, b; }
        static readonly List<Pair> pairs = new List<Pair>();
        static readonly Dictionary<(Type, string), MethodInfo> _cache = new Dictionary<(Type, string), MethodInfo>();
        static readonly List<MonoBehaviour> _mbs = new List<MonoBehaviour>();
        static readonly Dictionary<Transform, MonoBehaviour> _mbByTr = new Dictionary<Transform, MonoBehaviour>();

        public static void Init() => PlayerLoop.TickTriggers += Tick;

        public static void Tick()
        {
            RebuildMbIndex();
            var all = Object.AllOf<Collider>().Where(c => c.isTrigger && c.transform != null).ToList();
            var current = new HashSet<(Collider, Collider)>();
            for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
            {
                var a = all[i];
                var b = all[j];
                if (Overlaps(a, b))
                {
                    var key = RefCompare(a) < RefCompare(b) ? (a, b) : (b, a);
                    current.Add(key);
                }
            }
            pairs.RemoveAll(p =>
            {
                var key = RefCompare(p.a) < RefCompare(p.b) ? (p.a, p.b) : (p.b, p.a);
                return !current.Contains(key) || p.a.transform == null || p.b.transform == null;
            });
            foreach (var (a, b) in current)
            {
                var exists = pairs.Any(p => (p.a == a && p.b == b) || (p.a == b && p.b == a));
                if (!exists)
                {
                    pairs.Add(new Pair { a = a, b = b });
                    CallTrigger(a, b, "OnTriggerEnter");
                    CallTrigger(b, a, "OnTriggerEnter");
                }
                CallTrigger(a, b, "OnTriggerStay");
                CallTrigger(b, a, "OnTriggerStay");
            }
            var toRemove = pairs.Where(p =>
            {
                var key = RefCompare(p.a) < RefCompare(p.b) ? (p.a, p.b) : (p.b, p.a);
                return !current.Contains(key);
            }).ToArray();
            foreach (var p in toRemove)
            {
                if (p.a.transform != null && p.b.transform != null)
                {
                    CallTrigger(p.a, p.b, "OnTriggerExit");
                    CallTrigger(p.b, p.a, "OnTriggerExit");
                }
                pairs.Remove(p);
            }
        }

        static void RebuildMbIndex()
        {
            _mbs.Clear();
            _mbByTr.Clear();
            foreach (var mb in Object.AllOf<MonoBehaviour>())
            {
                _mbs.Add(mb);
                if (mb.transform != null && !_mbByTr.ContainsKey(mb.transform))
                    _mbByTr[mb.transform] = mb;
            }
        }

        static int RefCompare(Collider c)
        {
            var idx = 0;
            foreach (var mb in _mbs)
            {
                if (mb.gameObject == c.gameObject) return idx;
                idx++;
            }
            return int.MaxValue;
        }

        static bool Overlaps(Collider a, Collider b)
        {
            var ta = a.transform;
            var tb = b.transform;
            if (ta == null || tb == null) return false;
            var sa = ta.lossyScale;
            var sb = tb.lossyScale;
            var ra = Mathf.Max(sa.x, Mathf.Max(sa.y, sa.z)) / 2;
            var rb = Mathf.Max(sb.x, Mathf.Max(sb.y, sb.z)) / 2;
            var dx = ta.position.x - tb.position.x;
            var dy = ta.position.y - tb.position.y;
            var dz = ta.position.z - tb.position.z;
            return (dx * dx + dy * dy + dz * dz) <= (ra + rb) * (ra + rb);
        }

        static void CallTrigger(Collider self, Collider other, string method)
        {
            if (_mbByTr.TryGetValue(self.transform, out var mb))
            {
                var t = mb.GetType();
                if (!_cache.TryGetValue((t, method), out var mi))
                {
                    mi = t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new[] { typeof(Collider) }, null);
                    _cache[(t, method)] = mi;
                }
                if (mi != null)
                {
                    try { mi.Invoke(mb, new object[] { other }); }
                    catch (Exception ex) { Debug.LogError($"{method} {t.Name}: " + (ex.InnerException?.Message ?? ex.Message)); }
                }
            }
        }
    }

}
