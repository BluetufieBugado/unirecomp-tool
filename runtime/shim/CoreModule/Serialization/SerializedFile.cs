// Port C# de tools/sfindex.py — leitor do container SerializedFile v17
// (Unity 2018.1.5f1, builds stripped). Layout provado em STATUS.md §4c.
// Sem dependências: struct puro sobre byte[].
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Unirecomp.Serialization
{
    public struct PPtr
    {
        public int FileID;
        public long PathID;
    }

    public sealed class SfType
    {
        public int ClassID;
        public bool Stripped;
        public short ScriptIdx;
        public byte[] ScriptID; // 16B ou null
        public byte[] Hash;     // 16B
    }

    public sealed class SfObject
    {
        public long PathID;
        public uint Start;   // relativo ao data_offset (formato é relativo!)
        public uint Size;
        public int TypeID;   // índice em Types[]
        public uint AbsStart;
    }

    public sealed class SerializedFile
    {
        public uint DataOffset;
        public string UnityVersion;
        public int TargetPlatform;
        public List<SfType> Types = new List<SfType>();
        public List<SfObject> Objects = new List<SfObject>();
        public List<PPtr> ScriptRefs = new List<PPtr>();
        public List<string> Externals = new List<string>();
        public byte[] Data;

        public static SerializedFile Load(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var r = new BlobReader(bytes);
            // Header big-endian (peculiaridade do formato!)
            uint metaSize = r.U32BE(), fileSize = r.U32BE();
            uint ver = r.U32BE(), doff = r.U32BE();
            byte endian = r.U8(); r.Blob(3);
            if (ver != 17) throw new InvalidDataException($"versao {ver}, esperado 17 em {path}");
            if (bytes.Length != fileSize) throw new InvalidDataException($"tamanho {bytes.Length} != header {fileSize}");
            var sf = new SerializedFile { DataOffset = doff, Data = bytes };
            sf.UnityVersion = r.CString();
            sf.TargetPlatform = r.U8();
            r.U8(); // enableTypeTree (0 = stripped)
            r.Align4();
            int nTypes = r.I32();
            for (int i = 0; i < nTypes; i++)
            {
                var t = new SfType { ClassID = r.I32(), Stripped = r.U8() != 0 };
                // scriptIdx é i16 (não i32!)
                t.ScriptIdx = (short)r.U16();
                if (t.ScriptIdx >= 0) t.ScriptID = r.Blob(16);
                t.Hash = r.Blob(16);
                sf.Types.Add(t);
            }
            int nObj = r.I32();
            r.Align4();
            for (int i = 0; i < nObj; i++)
            {
                var o = new SfObject { PathID = r.I64(), Start = r.U32(), Size = r.U32(), TypeID = r.I32() };
                o.AbsStart = doff + o.Start;
                if ((ulong)o.AbsStart + o.Size > (ulong)bytes.Length)
                    throw new InvalidDataException($"objeto {o.PathID} fora do arquivo");
                sf.Objects.Add(o);
            }
            int nScripts = r.I32();
            for (int i = 0; i < nScripts; i++)
                sf.ScriptRefs.Add(new PPtr { FileID = r.I32(), PathID = r.I64() });
            int nExt = r.I32();
            for (int i = 0; i < nExt; i++)
            {
                string temp = r.CString();
                r.Blob(16); // guid
                r.I32();    // type
                string name = r.CString(); // packed, sem align
                if (temp != "") throw new InvalidDataException($"tempEmpty inesperado: {temp}");
                sf.Externals.Add(name);
            }
            // Trailer: zeros até data_offset.
            while (r.Pos < doff)
                if (r.U8() != 0) throw new InvalidDataException($"trailer nao-zero em {r.Pos}");
            if (r.Pos != doff) throw new InvalidDataException($"offset {r.Pos} != data {doff}");
            return sf;
        }
    }

    public sealed class BlobReader
    {
        readonly byte[] b;
        public int Pos;
        public BlobReader(byte[] bytes) { b = bytes; }
        public byte U8() => b[Pos++];
        public ushort U16() => (ushort)(b[Pos++] | (b[Pos++] << 8));
        public uint U32() => (uint)(b[Pos++] | (b[Pos++] << 8) | (b[Pos++] << 16) | (b[Pos++] << 24));
        public int I32() => (int)U32();
        public long I64()
        {
            uint lo = U32(), hi = U32();
            return (long)(((ulong)hi << 32) | lo);
        }
        public float F32()
        {
            uint u = U32();
            return BitConverter.ToSingle(BitConverter.GetBytes(u), 0);
        }
        public double F64()
        {
            uint lo = U32(), hi = U32();
            return BitConverter.ToDouble(BitConverter.GetBytes(((ulong)hi << 32) | lo), 0);
        }
        public uint U32BE()
        {
            uint v = (uint)((b[Pos] << 24) | (b[Pos + 1] << 16) | (b[Pos + 2] << 8) | b[Pos + 3]);
            Pos += 4;
            return v;
        }
        public byte[] Blob(int n)
        {
            var r = new byte[n];
            Buffer.BlockCopy(b, Pos, r, 0, n);
            Pos += n;
            return r;
        }
        public string CString()
        {
            int end = Array.IndexOf(b, (byte)0, Pos);
            if (end < 0) throw new InvalidDataException("cstring sem terminador");
            string s = Encoding.UTF8.GetString(b, Pos, end - Pos);
            Pos = end + 1;
            return s;
        }
        public string LString() // u32 len + bytes (sem null, sem pad)
        {
            int n = I32();
            if (n < 0 || n > 1 << 20) throw new InvalidDataException($"string len absurdo: {n}");
            string s = Encoding.UTF8.GetString(b, Pos, n);
            Pos += n;
            return s;
        }
        public PPtr PPtr() => new PPtr { FileID = I32(), PathID = I64() };
        public void Align4() => Pos = (Pos + 3) & ~3;
    }
}
