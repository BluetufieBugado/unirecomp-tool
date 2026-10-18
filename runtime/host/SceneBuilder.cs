// SceneBuilder — instancia hierarquia de level a partir do índice + dados.
// level0 (main_menu): sem prefabs, tudo inline. PPtrs cross-file e fields de
// MonoBehaviour vêm nas próximas fatias (2b-iii). Tags/layers: raw por ora.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unirecomp.Serialization;
using UnityEngine;

namespace Unirecomp.Host
{
    public sealed class BuiltScene
    {
        public List<GameObject> Roots = new List<GameObject>();
        public Dictionary<long, GameObject> ByPath = new Dictionary<long, GameObject>();
        public Dictionary<long, int> TagIdx = new Dictionary<long, int>();
        public Dictionary<long, uint> Layers = new Dictionary<long, uint>();
        public Dictionary<long, bool> IsRect = new Dictionary<long, bool>();
        public Dictionary<long, float[]> RectData = new Dictionary<long, float[]>();
        public List<(long mb, Type type, bool enabled)> Behaviours = new List<(long, Type, bool)>();
        public Dictionary<long, (int file, long path)> ScriptLink = new Dictionary<long, (int, long)>();
        public Dictionary<string, int> Attached = new Dictionary<string, int>();
        public List<string> Warnings = new List<string>();
        // 2b-iii: resolução de PPtrs + fields aplicados (por pathID do MB).
        public Dictionary<long, Component> CompByPath = new Dictionary<long, Component>();
        public Dictionary<long, Transform> TrByPath = new Dictionary<long, Transform>();
        public Dictionary<long, Component> MbByPath = new Dictionary<long, Component>();
        public Dictionary<long, Dictionary<string, object>> FieldValues = new Dictionary<long, Dictionary<string, object>>();
        public int FieldApplied, FieldSkipped, UnresolvedPPtr;
    }

    public static class SceneBuilder
    {
        // classIDs provados no level0 (2018.1.5f1). 1/4/224/114 = especiais.
        // 43/104/157 = pulados (asset/global, sem attach).
        static readonly Dictionary<int, Type> Attach = new Dictionary<int, Type>
        {
            { 20, typeof(UnityEngine.Camera) },
            { 23, typeof(UnityEngine.MeshRenderer) },
            { 33, typeof(UnityEngine.MeshFilter) },
            { 54, typeof(UnityEngine.Rigidbody) },
            { 64, typeof(UnityEngine.MeshCollider) },
            { 65, typeof(UnityEngine.BoxCollider) },
            { 81, typeof(UnityEngine.AudioListener) },
            { 82, typeof(UnityEngine.AudioSource) },
            { 95, typeof(UnityEngine.Animator) },
            { 96, typeof(UnityEngine.TrailRenderer) },
            { 108, typeof(UnityEngine.Light) },
            { 124, typeof(UnityEngine.FlareLayer) },
            { 135, typeof(UnityEngine.SphereCollider) },
            { 136, typeof(UnityEngine.CapsuleCollider) },
            { 198, typeof(UnityEngine.ParticleSystem) },
            { 199, typeof(UnityEngine.ParticleSystemRenderer) },
            { 212, typeof(UnityEngine.SpriteRenderer) },
            { 222, typeof(UnityEngine.CanvasRenderer) },
            { 223, typeof(UnityEngine.Canvas) },
        };
        static readonly HashSet<int> Skip = new HashSet<int> { 43, 104, 157 };

        public static BuiltScene BuildLevel(SerializedFile level, Assembly gameAsm,
            IReadOnlyDictionary<long, string> scriptOf, List<Assembly> shimAsms, Layouts layouts = null,
                IReadOnlyDictionary<string, (string assembly, string cls)> monoScripts = null)
        {
            var scene = new BuiltScene();
            var goRecs = new Dictionary<long, (List<(int f, long p)> comps, string name)>();
            var trRecs = new Dictionary<long, (long go, PPtr father, int nKids)>();
            var resolveCache = new Dictionary<string, Type>();

            Type ResolveScript(string asmCls)
            {
                if (resolveCache.TryGetValue(asmCls, out var hit)) return hit;
                // "Assembly-CSharp.dll::GameManager" | "UnityEngine.UI.dll::Text"
                var parts = asmCls.Split(new[] { "::" }, StringSplitOptions.None);
                string cls = parts.Length > 1 ? parts[1] : parts[0];
                Type found = null;
                foreach (var t in gameAsm.GetTypes())
                    if (t.Name == cls) { found = t; break; }
                if (found == null)
                    foreach (var a in shimAsms)
                        foreach (var t in a.GetTypes())
                            if (t.Name == cls) { found = t; break; }
                if (found == null && monoScripts != null)
                    foreach (var kv in monoScripts)
                        if (kv.Key == cls)
                        {
                            found = gameAsm.GetType($"{kv.Value.assembly}::{kv.Value.cls}");
                            if (found == null)
                                foreach (var a in shimAsms)
                                    foreach (var t in a.GetTypes())
                                        if (t.Name == kv.Value.cls) { found = t; break; }
                        }
                if (found == null)
                    throw new InvalidOperationException($"tipo de script nao resolvido: {asmCls}");
                resolveCache[asmCls] = found;
                return found;
            }

            // Pass 1: GameObjects.
            foreach (var o in level.Objects)
            {
                int cid = level.Types[o.TypeID].ClassID;
                if (cid != 1) continue;
                var r = new BlobReader(level.Data) { Pos = (int)o.AbsStart };
                int n = r.I32();
                var comps = new List<(int, long)>();
                for (int i = 0; i < n; i++) { var p = r.PPtr(); comps.Add((p.FileID, p.PathID)); }
                uint layer = r.U32();
                string name = r.LString();
                r.Align4(); // strings em object-data alinham a 4 (LoadCanvas prova; Fill já alinhado)
                int tag = r.U16();
                bool active = r.U8() != 0;
                if (r.Pos - (int)o.AbsStart != o.Size)
                    throw new InvalidDataException($"GO {o.PathID}: consumido {r.Pos - (int)o.AbsStart} != size {o.Size}");
                var go = new GameObject(name);
                go.SetActive(active);
                scene.ByPath[o.PathID] = go;
                scene.TagIdx[o.PathID] = tag;
                scene.Layers[o.PathID] = layer;
                goRecs[o.PathID] = (comps, name);
            }

            // Pass 2: Transforms (+RectLINK: valores no Transform base; tipo real no sidecar).
            foreach (var o in level.Objects)
            {
                int cid = level.Types[o.TypeID].ClassID;
                if (cid != 4 && cid != 224) continue;
                var r = new BlobReader(level.Data) { Pos = (int)o.AbsStart };
                var gop = r.PPtr();
                var rot = new UnityEngine.Quaternion(r.F32(), r.F32(), r.F32(), r.F32());
                var pos = new UnityEngine.Vector3(r.F32(), r.F32(), r.F32());
                var scl = new UnityEngine.Vector3(r.F32(), r.F32(), r.F32());
                int nk = r.I32();
                for (int i = 0; i < nk; i++) r.PPtr();
                var father = r.PPtr();
                float[] rect = null;
                if (cid == 224)
                {
                    rect = new float[10];
                    for (int i = 0; i < 10; i++) rect[i] = r.F32();
                }
                if (r.Pos - (int)o.AbsStart != o.Size)
                    throw new InvalidDataException($"TR {o.PathID}: consumido {r.Pos - (int)o.AbsStart} != size {o.Size}");
                if (gop.FileID != 0 || !scene.ByPath.TryGetValue(gop.PathID, out var go))
                    throw new InvalidDataException($"TR {o.PathID}: GO invalido");
                if (cid == 224)
                {
                    // UI-1: RectTransform real no lugar do Transform provisório.
                    var rt = new RectTransform();
                    rt.SetFromData(rect);
                    go.UseRectTransform(rt);
                }
                go.transform.localPosition = pos;
                go.transform.localRotation = rot;
                go.transform.localScale = scl;
                scene.IsRect[o.PathID] = cid == 224;
                if (rect != null) scene.RectData[o.PathID] = rect;
                trRecs[o.PathID] = (gop.PathID, father, nk);
            }

            // Pass 2b: hierarquia (father manda; fileID externo falha alto).
            foreach (var kv in trRecs)
            {
                var (goPath, father, _) = kv.Value;
                if (father.PathID == 0) { scene.Roots.Add(scene.ByPath[goPath]); continue; }
                if (father.FileID != 0)
                    throw new InvalidDataException($"TR {kv.Key}: father cross-file");
                Transform ftr = null;
                foreach (var tkv in trRecs)
                    if (tkv.Key == father.PathID && scene.ByPath.TryGetValue(tkv.Value.go, out var fgo2))
                    { ftr = fgo2.transform; break; }
                if (ftr == null) throw new InvalidDataException($"TR {kv.Key}: father {father.PathID} nao existe");
                scene.ByPath[goPath].transform.SetParent(ftr);
            }
            foreach (var kv in trRecs)
                if (scene.ByPath.TryGetValue(kv.Value.go, out var tgo))
                    scene.TrByPath[kv.Key] = tgo.transform;

            // Pass 3: componentes engine (attach pelo m_GameObject; dados pulados).
            foreach (var o in level.Objects)
            {
                int cid = level.Types[o.TypeID].ClassID;
                if (cid == 1 || cid == 4 || cid == 224 || cid == 114) continue;
                if (Skip.Contains(cid)) continue;
                if (!Attach.TryGetValue(cid, out var t))
                    throw new InvalidDataException($"classID sem mapa: {cid} (obj {o.PathID})");
                var r = new BlobReader(level.Data) { Pos = (int)o.AbsStart };
                var gop = r.PPtr();
                if (gop.FileID != 0 || !scene.ByPath.TryGetValue(gop.PathID, out var go))
                    throw new InvalidDataException($"comp {o.PathID}: GO invalido");
                var ec = go.AddComponent(t);
                scene.CompByPath[o.PathID] = ec;
                if (cid == 223 && o.Size >= 48)
                {
                    // Canvas 2018.1: m_RenderMode (int) @16, m_PlaneDistance
                    // (float) @32, m_SortOrder (int) @44. Só BackgroundCanvas
                    // é ScreenSpaceCamera (1); resto overlay (0).
                    var sr = new BlobReader(level.Data) { Pos = (int)o.AbsStart + 16 };
                    var cv = (UnityEngine.Canvas)ec;
                    cv.renderMode = (UnityEngine.RenderMode)sr.I32();
                    sr.Pos = (int)o.AbsStart + 32;
                    cv.planeDistance = sr.F32();
                    sr.Pos = (int)o.AbsStart + 44;
                    cv.sortOrder = sr.I32();
                }
                var nm = t.Name;
                scene.Attached[nm] = scene.Attached.TryGetValue(nm, out var c) ? c + 1 : 1;
            }

            // Pass 4: MonoBehaviours (base + resolve de script; fields na passada 5).
            foreach (var o in level.Objects)
            {
                if (level.Types[o.TypeID].ClassID != 114) continue;
                var r = new BlobReader(level.Data) { Pos = (int)o.AbsStart };
                var gop = r.PPtr();
                bool enabled = r.U8() != 0;
                r.Align4();
                var sp = r.PPtr();
                string mbName = r.LString();
                if (mbName.Length > 256)
                    throw new InvalidDataException($"MB {o.PathID}: nome suspeito");
                if (gop.FileID != 0 || !scene.ByPath.TryGetValue(gop.PathID, out var go))
                    throw new InvalidDataException($"MB {o.PathID}: GO invalido");
                scene.ScriptLink[o.PathID] = (sp.FileID, sp.PathID);
                if (!scriptOf.TryGetValue(o.PathID, out var asmCls))
                    throw new InvalidDataException($"MB {o.PathID}: sem script no oráculo");
                // ProBuilder (169 pares no level1): meshes vêm do MeshFilter;
                // componentes pb_Object/pb_Entity ignorados (decisão STATUS §4b).
                if (asmCls.StartsWith("ProBuilder")) continue;
                var type = ResolveScript(asmCls);
                var comp = (Component)go.AddComponent(type);
                ((Behaviour)comp).enabled = enabled;
                scene.Behaviours.Add((o.PathID, type, enabled));
                scene.CompByPath[o.PathID] = comp;
                scene.MbByPath[o.PathID] = comp;
            }

            // Pass 5 (2b-iii): fields dos scripts do jogo via layouts, com assert
            // de tamanho por objeto. Engine/shim (sem layout) é pulado.
            if (layouts != null)
            {
                var byPath = level.Objects.ToDictionary(o => o.PathID);
                foreach (var o in level.Objects)
                {
                    if (level.Types[o.TypeID].ClassID != 114) continue;
                    if (!scene.MbByPath.TryGetValue(o.PathID, out var comp)) continue;
                    if (!scriptOf.TryGetValue(o.PathID, out var asmCls)) continue;
                    string cls = asmCls.Contains("::") ? asmCls.Split(new[] { "::" }, StringSplitOptions.None).Last() : asmCls;
                    if (!layouts.Scripts.ContainsKey(cls)) { scene.FieldSkipped++; continue; }
                    var fields = layouts.FullFields(cls);
                    var r = new BlobReader(level.Data) { Pos = (int)o.AbsStart };
                    r.PPtr(); r.U8(); r.Align4(); r.PPtr(); // base
                    int nlen = r.I32();
                    if (nlen < 0 || nlen > 1 << 20) throw new InvalidDataException($"MB {o.PathID}: namelen absurdo {nlen}");
                    r.Blob(nlen); // m_Name
                    int start = r.Pos;
                    Dictionary<string, object> vals;
                    int objEnd = (int)(o.AbsStart + o.Size);
                    try { vals = FieldReader.Read(r, fields, layouts, objEnd); }
                    catch (Exception ex) { throw new InvalidDataException($"MB {o.PathID} ({cls}): {ex.Message}"); }
                    if (r.Pos - (int)o.AbsStart != o.Size)
                    {
                        // Padding zerado do Unity 2018 (TMP traz +72B zeros): aceita.
                        if (r.Pos < objEnd)
                        {
                            bool allZero = true;
                            for (int i = r.Pos; i < objEnd; i++)
                                if (level.Data[i] != 0) { allZero = false; break; }
                            if (allZero) r.Pos = objEnd;
                        }
                        if (r.Pos - (int)o.AbsStart != o.Size)
                            throw new InvalidDataException($"MB {o.PathID} ({cls}): consumido {r.Pos - (int)o.AbsStart} != size {o.Size}");
                    }
                    scene.FieldValues[o.PathID] = vals;
                    FieldApplier.Apply(comp, cls, vals, scene);
                    scene.FieldApplied++;
                }
            }

            // Cross-check: todo componente (engine+MB) listado de volta no GO.
            foreach (var o in level.Objects)
            {
                int cid = level.Types[o.TypeID].ClassID;
                if (cid == 1 || cid == 4 || cid == 224) continue;
                if (Skip.Contains(cid)) continue;
                // acha o GO dono: engine comps e MBs têm m_GameObject nos 12B iniciais
                var r = new BlobReader(level.Data) { Pos = (int)o.AbsStart };
                var gop = r.PPtr();
                if (gop.FileID != 0) continue;
                if (!goRecs.TryGetValue(gop.PathID, out var rec))
                    throw new InvalidDataException($"comp {o.PathID}: GO {gop.PathID} inexistente");
                if (!rec.comps.Any(c => c.f == 0 && c.p == o.PathID))
                    throw new InvalidDataException($"comp {o.PathID}: GO {gop.PathID} nao o lista");
            }
            return scene;
        }
    }

    // Aplica valores lidos nos objetos reais via reflection. PPtr fileID != 0
    // (cross-file: audio, mixer...) resolve para null + conta UnresolvedPPtr;
    // incompatibilidade de tipo (ex. RectTransform ainda sem rewire) vira
    // warning, nunca crash — a fidelidade total vem nas fatias seguintes.
    public static class FieldApplier
    {
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void Apply(Component comp, string cls, Dictionary<string, object> vals, BuiltScene scene)
        {
            var t = comp.GetType();
            foreach (var kv in vals)
            {
                var fi = FindField(t, kv.Key);
                if (fi == null) { scene.Warnings.Add($"{cls}.{kv.Key}: field ausente no tipo real"); continue; }
                try { fi.SetValue(comp, Convert(kv.Value, fi.FieldType, scene, $"{cls}.{kv.Key}")); }
                catch (Exception ex) { scene.Warnings.Add($"{cls}.{kv.Key}: {ex.Message}"); }
            }
        }

        static FieldInfo FindField(Type t, string name)
        {
            for (var c = t; c != null && c != typeof(object); c = c.BaseType)
            {
                var fi = c.GetField(name, BF | BindingFlags.DeclaredOnly);
                if (fi != null) return fi;
            }
            return null;
        }

        static object Convert(object v, Type ft, BuiltScene scene, string where)
        {
            if (v is FieldPPtr p)
            {
                var o = Resolve(p, scene, where);
                if (o == null) return null;
                if (ft.IsInstanceOfType(o)) return o;
                if (o is Transform tr && ft == typeof(GameObject)) return tr.gameObject;
                if (o is GameObject go && typeof(Component).IsAssignableFrom(ft))
                {
                    var gc = go.GetComponent(ft);
                    if (gc != null) return gc;
                }
                scene.Warnings.Add($"{where}: PPtr {p.File},{p.Path} ({o.GetType().Name}) incompativel com {ft.Name}");
                return null;
            }
            if (v is int i)
            {
                if (ft.IsEnum) return Enum.ToObject(ft, i);
                if (ft == typeof(int)) return i;
                if (ft == typeof(uint)) return unchecked((uint)i);
                if (ft == typeof(char)) return (char)i;
                if (ft == typeof(short)) return (short)i;
                if (ft == typeof(ushort)) return (ushort)i;
                if (ft == typeof(byte)) return (byte)i;
                if (ft == typeof(float)) return (float)i;
                if (ft == typeof(double)) return (double)i;
                if (ft == typeof(long)) return (long)i;
                throw new InvalidOperationException($"int -> {ft.Name}");
            }
            if (v is float f)
            {
                if (ft == typeof(float)) return f;
                if (ft == typeof(double)) return (double)f;
                throw new InvalidOperationException($"float -> {ft.Name}");
            }
            if (v is double d)
            {
                if (ft == typeof(double)) return d;
                if (ft == typeof(float)) return (float)d;
                throw new InvalidOperationException($"double -> {ft.Name}");
            }
            if (v is bool b)
            {
                if (ft == typeof(bool)) return b;
                throw new InvalidOperationException($"bool -> {ft.Name}");
            }
            if (v is string s)
            {
                if (ft == typeof(string)) return s;
                throw new InvalidOperationException($"string -> {ft.Name}");
            }
            if (v is Vector2 v2 && ft == typeof(Vector2)) return v2;
            if (v is Vector3 v3 && ft == typeof(Vector3)) return v3;
            if (v is Vector4 v4 && ft == typeof(Vector4)) return v4;
            if (v is Quaternion q && ft == typeof(Quaternion)) return q;
            if (v is Color col && ft == typeof(Color)) return col;
            if (v is Color32 c32)
            {
                if (ft == typeof(Color32)) return c32;
                if (ft == typeof(Color)) return (Color)c32;
                throw new InvalidOperationException($"color32 -> {ft.Name}");
            }
            if (v is Rect rc && ft == typeof(Rect)) return rc;
            if (v is RectOffset ro && ft == typeof(RectOffset)) return ro;
            if (v is UnityEventData hev && ft == typeof(UnityEngine.Events.UnityEventData))
            {
                var se = new UnityEngine.Events.UnityEventData { TypeName = hev.TypeName };
                foreach (var c in hev.Calls)
                    se.Calls.Add(new UnityEngine.Events.PersistentCallData
                    {
                        Target = Resolve(c.Target, scene, where),
                        MethodName = c.MethodName,
                        Mode = c.Mode,
                        ObjectArgument = Resolve(c.ObjectArgument, scene, where),
                        ObjectArgumentType = c.ObjectArgumentType,
                        IntArgument = c.IntArgument,
                        FloatArgument = c.FloatArgument,
                        StringArgument = c.StringArgument,
                        BoolArgument = c.BoolArgument,
                        CallState = c.CallState,
                    });
                return se;
            }
            if (v is List<object> list)
            {
                if (ft.IsArray)
                {
                    var et = ft.GetElementType();
                    var arr = Array.CreateInstance(et, list.Count);
                    for (int k = 0; k < list.Count; k++)
                        arr.SetValue(Convert(list[k], et, scene, $"{where}[{k}]"), k);
                    return arr;
                }
                if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var et = ft.GetGenericArguments()[0];
                    var lst = (System.Collections.IList)Activator.CreateInstance(ft);
                    foreach (var e in list) lst.Add(Convert(e, et, scene, where + "[]"));
                    return lst;
                }
                throw new InvalidOperationException($"array -> {ft.Name}");
            }
            if (v is Dictionary<string, object> sub)
            {
                object target;
                if (ft.IsValueType) target = Activator.CreateInstance(ft);
                else
                {
                    // reusa instancia existente (inicializadores do C#) se houver
                    target = null; // preenchido pelo chamador? nested sempre cria aqui:
                    target = Activator.CreateInstance(ft);
                }
                foreach (var kv in sub)
                {
                    var fi = FindField(ft, kv.Key);
                    if (fi == null) { scene.Warnings.Add($"{where}.{kv.Key}: subfield ausente"); continue; }
                    try
                    {
                        object cur = target;
                        // value-type aninhado: trabalha boxeado
                        fi.SetValue(cur, Convert(kv.Value, fi.FieldType, scene, $"{where}.{kv.Key}"));
                        target = cur;
                    }
                    catch (Exception ex) { scene.Warnings.Add($"{where}.{kv.Key}: {ex.Message}"); }
                }
                return target;
            }
            throw new InvalidOperationException($"valor inesperado {v?.GetType().Name} -> {ft.Name}");
        }

        static UnityEngine.Object Resolve(FieldPPtr p, BuiltScene scene, string where)
        {
            if (p.IsNull) return null;
            if (p.File != 0) { scene.UnresolvedPPtr++; return null; }
            if (scene.CompByPath.TryGetValue(p.Path, out var c)) return c;
            if (scene.TrByPath.TryGetValue(p.Path, out var t)) return t;
            if (scene.ByPath.TryGetValue(p.Path, out var go)) return go;
            scene.Warnings.Add($"{where}: PPtr 0,{p.Path} sem alvo");
            return null;
        }
    }
}
