// uidump — extrai layouts de fields serializados das DLLs engine do jogo
// (UnityEngine.UI.dll, CoreModule, TextMeshPro) via System.Reflection.Metadata.
// Ordem = metadata (declaração), base PRIMEIRO no JSON (FullFields encadeia).
// Uso: uidump <ManagedDir> <outJson>
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

sealed class TN
{
    public string K;             // pNN | def | ref | spec | arr | gen | unsup
    public EntityHandle H;
    public TypeSpecificationHandle SH; // spec
    public TN Elem;              // arr
    public TN G;                 // gen: tipo genérico
    public ImmutableArray<TN> Args; // gen: argumentos
}

sealed class Prov : ISignatureTypeProvider<TN, object>
{
    public TN GetPrimitiveType(PrimitiveTypeCode c) => new TN { K = "p" + (int)c };
    public TN GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte raw) => new TN { K = "def", H = h };
    public TN GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte raw) => new TN { K = "ref", H = h };
    public TN GetTypeFromSpecification(MetadataReader r, object g, TypeSpecificationHandle h, byte raw) => new TN { K = "spec", SH = h };
    public TN GetSZArrayType(TN e) => new TN { K = "arr", Elem = e };
    public TN GetArrayType(TN e, ArrayShape s) => s.Rank == 1 ? new TN { K = "arr", Elem = e } : new TN { K = "unsup" };
    public TN GetGenericInstantiation(TN g, ImmutableArray<TN> a) => new TN { K = "gen", G = g, Args = a };
    public TN GetPointerType(TN e) => new TN { K = "unsup" };
    public TN GetByReferenceType(TN e) => new TN { K = "unsup" };
    public TN GetFunctionPointerType(MethodSignature<TN> s) => new TN { K = "unsup" };
    public TN GetGenericMethodParameter(object g, int i) => new TN { K = "unsup" };
    public TN GetGenericTypeParameter(object g, int i) => new TN { K = "unsup" };
    public TN GetModifiedType(TN m, TN u, bool req) => u;
    public TN GetPinnedType(TN e) => e;
    public TN GetTypeFromHandle(MetadataReader r, EntityHandle h, byte raw) => new TN { K = "unsup" };
}

sealed class Mod
{
    public string Path;
    public PEReader Pe;
    public MetadataReader R;
}

static class Dump
{
    internal static readonly List<Mod> mods = new();
    static readonly HashSet<string> enumNames = new(); // "ns.Name"
    static readonly Dictionary<string, object> scripts = new();
    static readonly Dictionary<string, object> nested = new();
    static readonly List<object> skipped = new();
    static readonly Queue<(string mod, EntityHandle h)> work = new();
    static readonly HashSet<string> doneNested = new();

    static readonly Dictionary<string, string> wellKnown = new()
    {
        ["UnityEngine.Vector2"] = "vector2", ["UnityEngine.Vector3"] = "vector3",
        ["UnityEngine.Vector4"] = "vector4", ["UnityEngine.Quaternion"] = "quaternion",
        ["UnityEngine.Color"] = "color", ["UnityEngine.Color32"] = "color32",
        ["UnityEngine.Rect"] = "rect", ["UnityEngine.Bounds"] = "bounds",
        ["UnityEngine.LayerMask"] = "layermask", ["UnityEngine.Matrix4x4"] = "matrix",
        ["UnityEngine.RangeInt"] = "rangeint", ["UnityEngine.Vector2Int"] = "vector2int",
        ["UnityEngine.Vector3Int"] = "vector3int",
    };

    static string Str(MetadataReader r, StringHandle h) => r.GetString(h);

    public static int Run(string managedDir, string outJson)
    {
        foreach (var f in Directory.GetFiles(managedDir, "UnityEngine.*Module.dll"))
            Open(f);
        Open(Path.Combine(managedDir, "UnityEngine.UI.dll"));
        Open(Path.Combine(managedDir, "UnityEngine.dll"));
        var tmp = Directory.GetFiles(managedDir, "TextMeshPro*.dll");
        foreach (var f in tmp) Open(f);

        // enums conhecidos (todos os TypeDefs com base System.Enum)
        foreach (var m in mods)
            foreach (var th in m.R.TypeDefinitions)
            {
                var td = m.R.GetTypeDefinition(th);
                if (BaseName(m, td.BaseType) == "System.Enum")
                    enumNames.Add(FullOfDef(m, th));
            }

        var uiTargets = ("Graphic,MaskableGraphic,Text,Image,RawImage,Mask,Outline,Shadow," +
            "Button,Selectable,Toggle,ToggleGroup,Slider,Scrollbar,ScrollRect,Dropdown,InputField," +
            "CanvasScaler,GraphicRaycaster," +
            "AspectRatioFitter,ContentSizeFitter,GridLayoutGroup,HorizontalLayoutGroup," +
            "LayoutElement,VerticalLayoutGroup").Split(',');
        foreach (var t in uiTargets) EmitScript("UnityEngine.UI", t);
        EmitScript("UnityEngine.EventSystems", "EventSystem");
        EmitScript("UnityEngine.EventSystems", "StandaloneInputModule");
        EmitScript("UnityEngine.EventSystems", "EventTrigger");
        EmitScript("UnityEngine", "CanvasGroup");
        EmitScript("TMPro", "TMP_Text");
        EmitScript("TMPro", "TextMeshProUGUI");
        EmitScript("TMPro", "TMP_Dropdown");

        while (work.Count > 0)
        {
            var (mp, h) = work.Dequeue();
            EmitNested(mp, h);
        }

        var doc = new Dictionary<string, object>
        {
            ["scripts"] = scripts, ["nested"] = nested,
            ["enums"] = enumNames.OrderBy(e => e).ToList(),
            ["skipped"] = skipped,
            ["report"] = new Dictionary<string, object>
            {
                ["scripts_total"] = scripts.Count, ["nested"] = nested.Count,
            },
        };
        File.WriteAllText(outJson, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"scripts: {scripts.Count}, nested: {nested.Count}, enums: {enumNames.Count}, skipped: {skipped.Count}");
        return 0;
    }

    static void Open(string path)
    {
        if (!File.Exists(path) || mods.Any(m => m.Path == path)) return;
        var pe = new PEReader(File.OpenRead(path));
        mods.Add(new Mod { Path = path, Pe = pe, R = pe.GetMetadataReader() });
    }

    static Mod FindMod(string path) => mods.FirstOrDefault(m => m.Path == path);

    internal static string FullOfDef(Mod m, TypeDefinitionHandle th)
    {
        var td = m.R.GetTypeDefinition(th);
        string name = Str(m.R, td.Name), ns = Str(m.R, td.Namespace);
        // aninhado: Outer+Inner
        if (td.IsNested)
        {
            var parent = td.GetDeclaringType();
            return FullOfDef(m, parent) + "+" + name;
        }
        return (ns.Length > 0 ? ns + "." : "") + name;
    }

    static string ShortOf(string full) => full.Split('.').Last().Split('+').Last();

    internal static string BaseName(Mod m, EntityHandle h)
    {
        if (h.IsNil) return "";
        if (h.Kind == HandleKind.TypeDefinition) return FullOfDef(m, (TypeDefinitionHandle)h);
        if (h.Kind == HandleKind.TypeReference)
        {
            var tr = m.R.GetTypeReference((TypeReferenceHandle)h);
            string ns = Str(m.R, tr.Namespace), nm = Str(m.R, tr.Name);
            return (ns.Length > 0 ? ns + "." : "") + nm;
        }
        return "?";
    }

    // cadeia base-primeiro, parando antes de MonoBehaviour/Component/UIBehaviour
    static readonly HashSet<string> stopBases = new()
        { "UnityEngine.MonoBehaviour", "UnityEngine.Component", "UnityEngine.Behaviour", "UnityEngine.UI.UIBehaviour" };

    static void EmitScript(string ns, string name)
    {
        var (m, th) = Find(ns, name);
        if (th.IsNil) { Console.WriteLine($"AVISO: {ns}.{name} nao achado"); return; }
        string key = ShortOf(FullOfDef(m, th));
        if (scripts.ContainsKey(key)) return;
        // monta cadeia (derivada -> ... -> base), emite cada nível como script
        var chain = new List<(Mod, TypeDefinitionHandle)>();
        var cm = m; var cth = th;
        var seen = new HashSet<string>();
        while (!cth.IsNil && seen.Add(FullOfDef(cm, cth)))
        {
            chain.Add((cm, cth));
            var td = cm.R.GetTypeDefinition(cth);
            string b = BaseName(cm, td.BaseType);
            if (stopBases.Contains(b)) break;
            var nb = FindByFull(b);
            if (nb.th.IsNil) break;
            cm = nb.m; cth = nb.th;
        }
        chain.Reverse(); // base primeiro
        foreach (var (lm, lth) in chain)
        {
            string lk = ShortOf(FullOfDef(lm, lth));
            if (scripts.ContainsKey(lk)) continue;
            var ltd = lm.R.GetTypeDefinition(lth);
            scripts[lk] = new Dictionary<string, object>
            {
                ["file"] = System.IO.Path.GetFileName(lm.Path),
                ["line"] = 0,
                ["base"] = ShortOf(BaseName(lm, ltd.BaseType)),
                ["fields"] = ReadFields(lm, ltd),
                ["unsupported"] = new List<object>(),
            };
        }
    }

    static void EmitNested(string modPath, EntityHandle h)
    {
        var m = FindMod(modPath);
        string full = h.Kind == HandleKind.TypeDefinition ? FullOfDef(m, (TypeDefinitionHandle)h) : "?";
        string key = ShortOf(full);
        if (doneNested.Contains(key)) return;
        doneNested.Add(key);
        if (h.Kind != HandleKind.TypeDefinition) return;
        var td = m.R.GetTypeDefinition((TypeDefinitionHandle)h);
        nested[key] = new Dictionary<string, object>
        {
            ["file"] = System.IO.Path.GetFileName(m.Path),
            ["line"] = 0,
            ["base"] = ShortOf(BaseName(m, td.BaseType)),
            ["fields"] = ReadFields(m, td),
            ["unsupported"] = new List<object>(),
        };
    }

    static (Mod m, TypeDefinitionHandle th) Find(string ns, string name)
    {
        foreach (var m in mods)
            foreach (var th in m.R.TypeDefinitions)
            {
                var td = m.R.GetTypeDefinition(th);
                if (!td.IsNested && Str(m.R, td.Namespace) == ns && Str(m.R, td.Name) == name)
                    return (m, th);
            }
        return (null, default);
    }

    internal static (Mod m, TypeDefinitionHandle th) FindByFull(string full)
    {
        foreach (var m in mods)
            foreach (var th in m.R.TypeDefinitions)
                if (FullOfDef(m, th) == full) return (m, th);
        return (null, default);
    }

    static string NormAttr(string an) => an.EndsWith("Attribute") ? an.Substring(0, an.Length - 9) : an;

    static List<object> ReadFields(Mod m, TypeDefinition td)
    {
        var list = new List<object>();
        foreach (var fh in td.GetFields())
        {
            var fd = m.R.GetFieldDefinition(fh);
            string fname = Str(m.R, fd.Name);
            var attrs = fd.Attributes;
            bool isStatic = attrs.HasFlag(System.Reflection.FieldAttributes.Static);
            bool isLiteral = attrs.HasFlag(System.Reflection.FieldAttributes.Literal);
            bool isInitOnly = attrs.HasFlag(System.Reflection.FieldAttributes.InitOnly);
            bool isPublic = attrs.HasFlag(System.Reflection.FieldAttributes.Public);
            bool notSerFlag = attrs.HasFlag(System.Reflection.FieldAttributes.NotSerialized);
            if (isStatic || isLiteral || isInitOnly || notSerFlag)
            {
                skipped.Add(new { field = fname, reason = "static/const/readonly/nonserialized" });
                continue;
            }
            bool hasSF = false, hasNS = false;
            foreach (var cah in fd.GetCustomAttributes())
            {
                var ca = m.R.GetCustomAttribute(cah);
                string an = NormAttr(AttrName(m, ca.Constructor.Kind == HandleKind.MemberReference
                    ? m.R.GetMemberReference((MemberReferenceHandle)ca.Constructor).Parent
                    : ca.Constructor));
                if (an == "UnityEngine.SerializeField") hasSF = true;
                if (an == "System.NonSerialized") hasNS = true;
            }
            if (hasNS) { skipped.Add(new { field = fname, reason = "NonSerialized" }); continue; }
            if (!isPublic && !hasSF)
            {
                skipped.Add(new { field = fname, reason = "private" });
                continue;
            }
            var sig = fd.DecodeSignature(new Prov(), (object)null);
            var mapped = MapType(m, sig);
            if (mapped == null)
            {
                skipped.Add(new { field = fname, reason = "unsupported-type" });
                continue;
            }
            var e = new Dictionary<string, object> { ["name"] = fname, ["line"] = 0 };
            foreach (var kv in mapped) e[kv.Key] = kv.Value;
            list.Add(e);
        }
        return list;
    }

    internal static bool HasSerializableField(Mod m, TypeDefinition td)
    {
        foreach (var fh in td.GetFields())
        {
            var fd = m.R.GetFieldDefinition(fh);
            var attrs = fd.Attributes;
            if (attrs.HasFlag(System.Reflection.FieldAttributes.Static)
                || attrs.HasFlag(System.Reflection.FieldAttributes.Literal)
                || attrs.HasFlag(System.Reflection.FieldAttributes.InitOnly)
                || attrs.HasFlag(System.Reflection.FieldAttributes.NotSerialized))
                continue;
            bool isPublic = attrs.HasFlag(System.Reflection.FieldAttributes.Public);
            bool hasSF = false;
            foreach (var cah in fd.GetCustomAttributes())
            {
                var ca = m.R.GetCustomAttribute(cah);
                string an = NormAttr(AttrName(m, ca.Constructor.Kind == HandleKind.MemberReference
                    ? m.R.GetMemberReference((MemberReferenceHandle)ca.Constructor).Parent
                    : ca.Constructor));
                if (an == "UnityEngine.SerializeField") { hasSF = true; break; }
            }
            if (isPublic || hasSF) return true;
        }
        return false;
    }
    internal static string AttrName(Mod m, EntityHandle h)
    {
        if (h.Kind == HandleKind.TypeDefinition) return FullOfDef(m, (TypeDefinitionHandle)h);
        if (h.Kind == HandleKind.TypeReference)
        {
            var tr = m.R.GetTypeReference((TypeReferenceHandle)h);
            return Str(m.R, tr.Namespace) + "." + Str(m.R, tr.Name);
        }
        if (h.Kind == HandleKind.MethodDefinition)
        {
            var md = m.R.GetMethodDefinition((MethodDefinitionHandle)h);
            var td = m.R.GetTypeDefinition(md.GetDeclaringType());
            return Str(m.R, td.Namespace) + "." + Str(m.R, td.Name);
        }
        return "?";
    }

    // retorna campos {kind, type, target?, type(nested)?, of?} ou null
    static Dictionary<string, object> MapType(Mod m, TN t)
    {
        if (t.K.StartsWith("p"))
        {
            return (PrimitiveTypeCode)(int.Parse(t.K.Substring(1))) switch
            {
                PrimitiveTypeCode.Boolean => F("bool", "bool"),
                PrimitiveTypeCode.Char => F("char", "char"),
                PrimitiveTypeCode.SByte => F("byte", "sbyte"),
                PrimitiveTypeCode.Byte => F("byte", "byte"),
                PrimitiveTypeCode.Int16 => F("short", "short"),
                PrimitiveTypeCode.UInt16 => F("ushort", "ushort"),
                PrimitiveTypeCode.Int32 => F("int", "int"),
                PrimitiveTypeCode.UInt32 => F("uint", "uint"),
                PrimitiveTypeCode.Int64 => F("long", "long"),
                PrimitiveTypeCode.UInt64 => F("ulong", "ulong"),
                PrimitiveTypeCode.Single => F("float", "float"),
                PrimitiveTypeCode.Double => F("double", "double"),
                PrimitiveTypeCode.String => F("string", "string"),
                _ => null,
            };
        }
        if (t.K == "arr")
        {
            var inner = MapType(m, t.Elem);
            if (inner == null) return null;
            return new Dictionary<string, object> { ["kind"] = "array", ["type"] = "array", ["of"] = inner };
        }
        if (t.K == "gen")
        {
            // List<T> -> array de T
            string g = TypeNameOf(m, t.G);
            if ((g == "System.Collections.Generic.List`1") && t.Args.Length == 1)
            {
                var inner = MapType(m, t.Args[0]);
                if (inner == null) return null;
                return new Dictionary<string, object> { ["kind"] = "array", ["type"] = "array", ["of"] = inner };
            }
            // UnityEvent<T> direto -> unityevent
            if (t.G != null && Category(m, new TN { K = t.G.K, H = t.G.H, SH = t.G.SH, G = t.G.G, Args = t.G.Args }) == "unityevent")
                return F("unityevent", ShortOf(g));
            return null;
        }
        if (t.K == "def" || t.K == "ref")
        {
            string full = TypeNameOf(m, t);
            string bare = ShortOf(full);
            if (wellKnown.TryGetValue(full, out var k))
                return F(k, bare);
            if (full == "UnityEngine.RectOffset")
                return F("rectoffset", bare);
            string cat = Category(m, t); // enum|object|struct|serclass|unityevent|unsup
            if (cat == "enum") return F("enum", bare);
            if (cat == "unityevent") return F("unityevent", bare);
            if (cat == "object")
                return new Dictionary<string, object> { ["kind"] = "pptr", ["type"] = bare, ["target"] = bare };
            if (cat == "struct" || cat == "serclass")
            {
                // enfileira dump aninhado
                var dm = DefMod(m, t);
                if (dm.m != null)
                {
                    work.Enqueue((dm.m.Path, dm.h));
                    return new Dictionary<string, object> { ["kind"] = "nested", ["type"] = bare };
                }
            }
            return null;
        }
        if (t.K == "spec")
        {
            // TypeSpec: genérico instanciado embutido (ex. UnityEvent<T>) ou array
            try
            {
                var spec = m.R.GetTypeSpecification(t.SH);
                var inner = spec.DecodeSignature(new Prov(), (object)null);
                return MapType(m, inner);
            }
            catch { return null; }
        }
        return null;
    }

    static Dictionary<string, object> F(string kind, string type)
        => new() { ["kind"] = kind, ["type"] = type };

    static string TypeNameOf(Mod m, TN t)
    {
        if (t.K == "def") return FullOfDef(m, (TypeDefinitionHandle)t.H);
        if (t.K == "ref")
        {
            var tr = m.R.GetTypeReference((TypeReferenceHandle)t.H);
            string ns = Str(m.R, tr.Namespace), nm = Str(m.R, tr.Name);
            return (ns.Length > 0 ? ns + "." : "") + nm;
        }
        if (t.K == "gen") return TypeNameOf(m, t.G);
        if (t.K == "spec")
        {
            try
            {
                var spec = m.R.GetTypeSpecification((TypeSpecificationHandle)t.H);
                return TypeNameOf(m, spec.DecodeSignature(new Prov(), (object)null));
            }
            catch { return "?"; }
        }
        return "?";
    }

    static (Mod m, EntityHandle h) DefMod(Mod m, TN t)
    {
        if (t.K == "def") return (m, t.H);
        if (t.K == "ref")
        {
            var tr = m.R.GetTypeReference((TypeReferenceHandle)t.H);
            string full = (Str(m.R, tr.Namespace).Length > 0 ? Str(m.R, tr.Namespace) + "." : "") + Str(m.R, tr.Name);
            // aninhado via TypeRef? (Dropdown/OptionData como field é TypeDef local)
            var f = FindByFull(full);
            if (!f.th.IsNil) return (f.m, f.th);
        }
        return (null, default);
    }

    static string Category(Mod m, TN t)
    {
        // anda na cadeia de base
        Mod cm = m; EntityHandle ch = t.K == "def" ? (EntityHandle)t.H : default;
        if (t.K == "ref")
        {
            var d = DefMod(m, t);
            if (d.m == null)
            {
                string fn = TypeNameOf(m, t);
                if (fn is "System.Enum" or "System.ValueType" or "System.Object"
                    or "System.MulticastDelegate" or "System.Delegate") return "unsup";
                return "unsup";
            }
            cm = d.m; ch = d.h;
        }
        var seen = new HashSet<string>();
        bool sawUnityEventBase = false;
        while (ch.Kind == HandleKind.TypeDefinition)
        {
            var td = cm.R.GetTypeDefinition((TypeDefinitionHandle)ch);
            string full = FullOfDef(cm, (TypeDefinitionHandle)ch);
            if (!seen.Add(full)) break;
            if (full == "UnityEngine.Events.UnityEventBase") sawUnityEventBase = true;
            string b = BaseName(cm, td.BaseType);
            if (full == "UnityEngine.Object") return "object";
            if (b == "System.Enum") return "enum";
            if (b == "System.ValueType") return "struct";
            if (b == "System.MulticastDelegate" || b == "System.Delegate") return "unsup";
            if (b == "" || b == "System.Object")
            {
                // Unity 2018.1 serializa classes UI sem [Serializable] (OptionData,
                // AnimationTriggers, TMP_TextInfo): aceita se tem [Serializable]
                // ou se tem >=1 field de instância serializável.
                foreach (var cah in td.GetCustomAttributes())
                {
                    var ca = cm.R.GetCustomAttribute(cah);
                    string an = NormAttr(AttrName(cm, ca.Constructor.Kind == HandleKind.MemberReference
                        ? cm.R.GetMemberReference((MemberReferenceHandle)ca.Constructor).Parent
                        : ca.Constructor));
                    if (an == "System.Serializable") return "serclass";
                }
                if (sawUnityEventBase) return "unityevent";
                if (HasSerializableField(cm, td)) return "serclass";
                return "unsup";
            }
            // sobe
            if (td.BaseType.Kind == HandleKind.TypeDefinition) { ch = td.BaseType; continue; }
            if (td.BaseType.Kind == HandleKind.TypeSpecification)
            {
                // base genérica instanciada (ex. ToggleEvent : UnityEvent<bool>)
                try
                {
                    var bspec = cm.R.GetTypeSpecification((TypeSpecificationHandle)td.BaseType);
                    var binner = bspec.DecodeSignature(new Prov(), (object)null);
                    if (binner.K == "gen" && binner.G != null)
                    {
                        var gd = DefMod(cm, binner.G);
                        if (gd.m != null) { cm = gd.m; ch = gd.h; continue; }
                    }
                }
                catch { }
                break;
            }
            if (td.BaseType.Kind == HandleKind.TypeReference)
            {
                var d = DefMod(cm, new TN { K = "ref", H = td.BaseType });
                if (d.m == null)
                {
                    string fb = BaseName(cm, td.BaseType);
                    if (fb == "System.Enum") return "enum";
                    if (fb == "System.ValueType") return "struct";
                    if (fb == "System.MulticastDelegate" || fb == "System.Delegate") return "unsup";
                    if (fb == "UnityEngine.Object") return "object";
                    // Object pode estar em módulo não carregado? CoreModule está carregado.
                    return "unsup";
                }
                cm = d.m; ch = d.h; continue;
            }
            break;
        }
        return "unsup";
    }

    static class TypeExt { }
}

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("uso: uidump <ManagedDir> <outJson> [--dump Ns.Name ...]"); return 2; }
        string managedDir = args[0];
        foreach (var f in Directory.GetFiles(managedDir, "UnityEngine.*Module.dll"))
            Open(f);
        Open(Path.Combine(managedDir, "UnityEngine.UI.dll"));
        Open(Path.Combine(managedDir, "UnityEngine.dll"));
        foreach (var f in Directory.GetFiles(managedDir, "TextMeshPro*.dll")) Open(f);
        int di = Array.IndexOf(args, "--dump");
        if (di >= 0)
        {
            for (int i = di + 1; i < args.Length; i++) DebugType(args[i]);
            return 0;
        }
        int fi = Array.IndexOf(args, "--find");
        if (fi >= 0 && fi + 1 < args.Length)
        {
            foreach (var m in Dump.mods)
                foreach (var th in m.R.TypeDefinitions)
                {
                    string full = Dump.FullOfDef(m, th);
                    if (full.Contains(args[fi + 1])) Console.WriteLine($"{System.IO.Path.GetFileName(m.Path)} :: {full}");
                }
            return 0;
        }
        return Dump.Run(args[0], args[1]);
    }

    static void Open(string path)
    {
        if (!File.Exists(path) || Dump.mods.Any(m => m.Path == path)) return;
        var pe = new PEReader(File.OpenRead(path));
        Dump.mods.Add(new Mod { Path = path, Pe = pe, R = pe.GetMetadataReader() });
    }

    static void DebugType(string full)
    {
        var f = Dump.FindByFull(full);
        if (f.th.IsNil) { Console.WriteLine(full + " NAO ACHADO"); return; }
        var td = f.m.R.GetTypeDefinition(f.th);
        Console.WriteLine($"== {full} (base {Dump.BaseName(f.m, td.BaseType)}) ==");
        var catts = new List<string>();
        foreach (var cah in td.GetCustomAttributes())
        {
            var ca = f.m.R.GetCustomAttribute(cah);
            catts.Add($"{ca.Constructor.Kind}:{Dump.AttrName(f.m, ca.Constructor.Kind == HandleKind.MemberReference
                ? f.m.R.GetMemberReference((MemberReferenceHandle)ca.Constructor).Parent
                : ca.Constructor)}");
        }
        Console.WriteLine($"  class-attrs: [{string.Join(",", catts)}]");
        foreach (var fh in td.GetFields())
        {
            var fd = f.m.R.GetFieldDefinition(fh);
            var attrs = fd.Attributes;
            var atts = new List<string>();
            foreach (var cah in fd.GetCustomAttributes())
            {
                var ca = f.m.R.GetCustomAttribute(cah);
                atts.Add($"{ca.Constructor.Kind}:{Dump.AttrName(f.m, ca.Constructor.Kind == HandleKind.MemberReference
                    ? f.m.R.GetMemberReference((MemberReferenceHandle)ca.Constructor).Parent
                    : ca.Constructor)}");
            }
            Console.WriteLine($"  {attrs} {f.m.R.GetString(fd.Name)} [{string.Join(",", atts)}]");
        }
    }
}
