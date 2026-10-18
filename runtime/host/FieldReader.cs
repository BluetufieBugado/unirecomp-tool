// FieldReader — fatia 2b-iii: desserializa fields de MonoBehaviour stripped.
// Formato provado nos bytes do level0/level2 (ver STATUS §4e):
//   base 32B (GO PPtr + enabled+pad + script PPtr + m_Name LString),
//   resto = fields do layout em ordem (base PRIMEIRO — BossController 224/224
//   prova), regras: PPtr 12B, int/uint/enum/float 4B, bool 4B (u32 0/1),
//   string LString+align4, array count+elementos inline, nested inline,
//   vector2/3/color N×f32. Todo objeto de script do jogo deve ser consumido
//   EXATAMENTE (assert de tamanho); engine/shim (sem layout) é pulado.
//   Tolerâncias (UI/TMP, 2026-10-15): sobra final só-de-zeros é padding do
//   Unity 2018 (TextMeshProUGUI sempre traz +72B zerados) — quem tolera é o
//   chamador (SceneBuilder). Arrays dentro de m_textInfo são cache de runtime:
//   counts velhos (ex. characterInfo=1 com blob sem o elemento) estourariam o
//   objeto — nesses casos o array é tratado como vazio (rewind + lista vazia).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Unirecomp.Serialization;
using UnityEngine;

namespace Unirecomp.Host
{
    public struct FieldPPtr
    {
        public int File;
        public long Path;
        public bool IsNull => File == 0 && Path == 0;
    }

    public sealed class LayoutField
    {
        public string Name;
        public string Kind;      // pptr|int|uint|float|double|bool|enum|string|vector2|vector3|color|color32|rect|layermask|rectoffset|unityevent|array|nested|...
        public string Target;    // pptr -> classe alvo
        public string Nested;    // nested -> nome do tipo
        public LayoutField Elem; // array -> elemento
    }

    // Chamada persistente de UnityEvent (fatia UI): alvo + método + args.
    public sealed class PersistentCallData
    {
        public FieldPPtr Target;
        public string MethodName = "";
        public int Mode;
        public FieldPPtr ObjectArgument;
        public string ObjectArgumentType = "";
        public int IntArgument;
        public float FloatArgument;
        public string StringArgument = "";
        public bool BoolArgument;
        public int CallState;
    }

    public sealed class UnityEventData
    {
        public readonly List<PersistentCallData> Calls = new();
        public string TypeName = "";
    }

    public sealed class Layouts
    {
        public readonly Dictionary<string, List<LayoutField>> Scripts = new(StringComparer.Ordinal);
        public readonly Dictionary<string, List<LayoutField>> Nested = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Bases = new(StringComparer.Ordinal);

        public static Layouts Load(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var l = new Layouts();
            l.Merge(doc);
            // Layouts engine (uidump das DLLs UnityEngine.UI/TMPro): fundido por
            // cima; nomes engine nunca colidem com scripts do jogo (uidump prova).
            string eng = Path.Combine(Path.GetDirectoryName(path) ?? "docs", "layouts_engine.json");
            if (File.Exists(eng))
            {
                using var edoc = JsonDocument.Parse(File.ReadAllText(eng));
                l.Merge(edoc);
            }
            return l;
        }

        void Merge(JsonDocument doc)
        {
            foreach (var s in doc.RootElement.GetProperty("scripts").EnumerateObject())
            {
                Bases[s.Name] = s.Value.TryGetProperty("base", out var b) ? b.GetString() ?? "" : "";
                var list = new List<LayoutField>();
                foreach (var f in s.Value.GetProperty("fields").EnumerateArray())
                    list.Add(ParseField(f));
                Scripts[s.Name] = list;
            }
            if (doc.RootElement.TryGetProperty("nested", out var nx))
                foreach (var s in nx.EnumerateObject())
                {
                    var list = new List<LayoutField>();
                    foreach (var f in s.Value.GetProperty("fields").EnumerateArray())
                        list.Add(ParseField(f));
                    // chave pode vir qualificada ("Arq.cs::Nome"); indexa também pelo nome nu
                    Nested[s.Name] = list;
                    var bare = s.Name.Contains("::") ? s.Name.Split(new[] { "::" }, StringSplitOptions.None).Last() : s.Name;
                    if (!Nested.ContainsKey(bare)) Nested[bare] = list;
                }
        }

        static LayoutField ParseField(JsonElement f)
        {
            var lf = new LayoutField
            {
                Name = f.GetProperty("name").GetString(),
                Kind = f.GetProperty("kind").GetString(),
            };
            if (f.TryGetProperty("target", out var t)) lf.Target = t.GetString();
            if (lf.Kind == "nested" && f.TryGetProperty("type", out var nt)) lf.Nested = nt.GetString();
            if (lf.Kind == "array" && f.TryGetProperty("of", out var of))
                lf.Elem = ParseElem(of);
            return lf;
        }

        static LayoutField ParseElem(JsonElement of)
        {
            var lf = new LayoutField { Kind = of.GetProperty("kind").GetString() };
            if (of.TryGetProperty("target", out var t)) lf.Target = t.GetString();
            if (of.TryGetProperty("type", out var nt)) lf.Nested = nt.GetString();
            return lf;
        }

        // Fields na ordem de serialização: base primeiro (recursivo).
        public List<LayoutField> FullFields(string script)
        {
            var out_ = new List<LayoutField>();
            var chain = new List<string>();
            string cur = script;
            var seen = new HashSet<string>();
            while (cur != null && !seen.Contains(cur))
            {
                seen.Add(cur);
                chain.Add(cur);
                if (Bases.TryGetValue(cur, out var b) && Scripts.ContainsKey(b)) cur = b;
                else break;
            }
            chain.Reverse();
            foreach (var c in chain)
                if (Scripts.TryGetValue(c, out var fs)) out_.AddRange(fs);
            return out_;
        }
    }

    public static class FieldReader
    {
        public static Dictionary<string, object> Read(BlobReader r, List<LayoutField> fields, Layouts layouts, int end = int.MaxValue, bool inTextInfo = false)
        {
            var vals = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var f in fields)
                vals[f.Name] = ReadOne(r, f, layouts, end, inTextInfo || f.Name == "m_textInfo");
            return vals;
        }

        static object ReadOne(BlobReader r, LayoutField f, Layouts layouts, int end = int.MaxValue, bool inTextInfo = false)
        {
            switch (f.Kind)
            {
                case "pptr": return new FieldPPtr { File = r.I32(), Path = r.I64() };
                case "int": case "enum": return r.I32();
                case "uint": return unchecked((int)r.U32());
                case "long": return r.I64();
                case "float": return r.F32();
                case "double": return r.F64();
                case "bool":
                    {
                        uint v = r.U32(); // bool ocupa 4B (RailMover/GameManager provam);
                        // cache velho do editor pode trazer 2/3 (ex. TMP 642):
                        // Unity trata !=0 como true — aceita sem quebrar.
                        return v != 0;
                    }
                case "byte": case "sbyte": { byte vb = r.U8(); r.Align4(); return (int)vb; }
                case "short": case "ushort": case "char": { ushort v = r.U16(); r.Align4(); return (int)v; }
                case "string": { string s = r.LString(); r.Align4(); return s; }
                case "vector2": return new Vector2(r.F32(), r.F32());
                case "vector3": return new Vector3(r.F32(), r.F32(), r.F32());
                case "vector4": return new Vector4(r.F32(), r.F32(), r.F32(), r.F32());
                case "quaternion":
                    return new Quaternion(r.F32(), r.F32(), r.F32(), r.F32());
                case "color": return new Color(r.F32(), r.F32(), r.F32(), r.F32());
                case "color32":
                    {
                        byte cr = r.U8(), cg = r.U8(), cb = r.U8(), ca = r.U8();
                        return new Color32(cr, cg, cb, ca);
                    }
                case "rect": return new Rect(r.F32(), r.F32(), r.F32(), r.F32());
                case "layermask": return r.I32();
                case "rectoffset": return new RectOffset(r.I32(), r.I32(), r.I32(), r.I32());
                case "unityevent": return ReadUnityEvent(r);
                case "array":
                    {
                        int n = r.I32();
                        if (n < 0 || n > 1 << 20) throw new InvalidDataException($"array len absurdo: {n} ({f.Name})");
                        int elemsPos = r.Pos;
                        var list = new List<object>(n);
                        try
                        {
                            for (int i = 0; i < n; i++) list.Add(ReadElem(r, f.Elem, layouts, end, inTextInfo));
                        }
                        catch when (inTextInfo)
                        {
                            // cache velho de m_textInfo: descarta e volta ao sync
                            r.Pos = elemsPos;
                            return new List<object>(0);
                        }
                        if (inTextInfo && r.Pos > end)
                        {
                            // count velho sem bytes no blob: trata como vazio
                            r.Pos = elemsPos;
                            return new List<object>(0);
                        }
                        return list;
                    }
                case "nested":
                    {
                        if (!layouts.Nested.TryGetValue(f.Nested ?? "", out var sub))
                        {
                            // Quirk genlayouts: classe [Serializable] + MonoBehaviour
                            // (ex. Sector) sai como nested, mas a Unity serializa
                            // referência de MB como PPtr — lê como PPtr.
                            if (layouts.Scripts.ContainsKey(f.Nested ?? ""))
                                return new FieldPPtr { File = r.I32(), Path = r.I64() };
                            throw new InvalidDataException($"nested sem layout: {f.Nested} ({f.Name})");
                        }
                        return Read(r, sub, layouts, end, inTextInfo);
                    }
                default:
                    throw new InvalidDataException($"kind sem leitor: {f.Kind} ({f.Name})");
            }
        }

        static object ReadElem(BlobReader r, LayoutField e, Layouts layouts, int end = int.MaxValue, bool inTextInfo = false)
        {
            // elemento de array: mesmo formato do campo escalar
            switch (e.Kind)
            {
                case "pptr": return new FieldPPtr { File = r.I32(), Path = r.I64() };
                case "nested":
                    if (!layouts.Nested.TryGetValue(e.Nested ?? "", out var sub))
                    {
                        if (layouts.Scripts.ContainsKey(e.Nested ?? ""))
                            return new FieldPPtr { File = r.I32(), Path = r.I64() };
                        throw new InvalidDataException($"nested sem layout: {e.Nested}");
                    }
                    return Read(r, sub, layouts, end, inTextInfo);
                default: return ReadOne(r, e, layouts, end, inTextInfo);
            }
        }
        // UnityEvent 2018.1: PersistentCallGroup { m_Calls: PersistentCall[] }
        // + m_TypeName. Ordem provada pelo assert de tamanho (Pass 5).
        static UnityEventData ReadUnityEvent(BlobReader r)
        {
            var ev = new UnityEventData();
            int n = r.I32();
            if (n < 0 || n > 1 << 16) throw new InvalidDataException($"unityevent calls absurdo: {n}");
            for (int i = 0; i < n; i++)
            {
                var c = new PersistentCallData
                {
                    Target = new FieldPPtr { File = r.I32(), Path = r.I64() },
                };
                c.MethodName = r.LString(); r.Align4();
                c.Mode = r.I32();
                c.ObjectArgument = new FieldPPtr { File = r.I32(), Path = r.I64() };
                c.ObjectArgumentType = r.LString(); r.Align4();
                c.IntArgument = r.I32();
                c.FloatArgument = r.F32();
                c.StringArgument = r.LString(); r.Align4();
                uint bv = r.U32();
                c.BoolArgument = bv != 0;
                c.CallState = r.I32();
                ev.Calls.Add(c);
            }
            ev.TypeName = r.LString(); r.Align4();
            return ev;
        }
    }
}
