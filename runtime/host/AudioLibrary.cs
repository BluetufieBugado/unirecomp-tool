// Áudio fatia A1 — resolve clipes do manifesto offline e liga nas fontes.
// Cria AudioClips com metadados reais (backend ainda silencioso; A2 decodifica).
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using UnityEngine;

namespace Unirecomp.Host
{
    public static class AudioLibrary
    {
        public static int Clips, SourcesLinked, MbFieldsResolved;
        static readonly Dictionary<string, AudioClip> byKey = new(StringComparer.OrdinalIgnoreCase);
        const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void Apply(BuiltScene scene, string file, string dataDir)
        {
            FmodBackend.Reset(); // sons da cena anterior morrem com ela
            const string path = "docs/audio_stardust.json";
            if (!File.Exists(path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            byKey.Clear();
            foreach (var kv in root.GetProperty("clips").EnumerateObject())
            {
                var m = kv.Value;
                var clip = new AudioClip
                {
                    length = (float)m.GetProperty("length").GetDouble(),
                    frequency = m.GetProperty("freq").GetInt32(),
                    channels = m.GetProperty("ch").GetInt32(),
                };
                clip.name = m.GetProperty("name").GetString();
                clip.ResourceFile = m.GetProperty("resource").GetString();
                clip.ResourceOffset = m.GetProperty("offset").GetInt64();
                clip.ResourceSize = m.GetProperty("size").GetInt32();
                // Bruto lido do .resource original do jogo (A2 decodifica).
                try
                {
                    string res = Path.Combine(dataDir, clip.ResourceFile);
                    if (File.Exists(res))
                    {
                        using var fs = File.OpenRead(res);
                        fs.Seek(clip.ResourceOffset, SeekOrigin.Begin);
                        var buf = new byte[clip.ResourceSize];
                        int n = fs.Read(buf, 0, buf.Length);
                        if (n == buf.Length) clip.RawAudio = buf;
                    }
                }
                catch { }
                byKey[kv.Name] = clip;
            }
            Clips = byKey.Count;

            if (!root.GetProperty("levels").TryGetProperty(file, out var lv)) return;
            var externals = new List<string>();
            foreach (var e in lv.GetProperty("externals").EnumerateArray())
                externals.Add(e.GetString());
            string Ext(int fileID) => fileID >= 1 && fileID <= externals.Count
                ? externals[fileID - 1] : null;
            AudioClip Find(int fileID, long pathID)
            {
                string asset = Ext(fileID);
                // externals podem vir como "sharedassets0.assets" ou com prefixo
                if (asset != null && asset.Contains("/"))
                    asset = asset.Substring(asset.LastIndexOf('/') + 1);
                return asset != null && byKey.TryGetValue($"{asset}|{pathID}", out var c) ? c : null;
            }

            foreach (var e in lv.GetProperty("sources").EnumerateArray())
            {
                long srcPath = e.GetProperty("src").GetInt64();
                if (!scene.CompByPath.TryGetValue(srcPath, out var c)) continue;
                if (c is not AudioSource src) continue;
                src.volume = (float)e.GetProperty("volume").GetDouble();
                src.pitch = (float)e.GetProperty("pitch").GetDouble();
                src.loop = e.GetProperty("loop").GetBoolean();
                src.mute = e.GetProperty("mute").GetBoolean();
                src.playOnAwake = e.GetProperty("playOnAwake").GetBoolean();
                var clip = Find(e.GetProperty("clipFile").GetInt32(),
                    e.GetProperty("clipPath").GetInt64());
                if (clip != null)
                {
                    src.clip = clip;
                    SourcesLinked++;
                    if (src.playOnAwake) src.Play();
                }
            }

            // Campos AudioClip de MonoBehaviours (MainMenu.loopClip,
            // AudioManager.sounds[].clip): PPtrs cross-file viraram null no
            // FieldApplier; resolve aqui via FieldValues brutos.
            foreach (var (mbPath, comp) in scene.MbByPath)
            {
                if (!scene.FieldValues.TryGetValue(mbPath, out var vals)) continue;
                var t = comp.GetType();
                foreach (var kv in vals)
                {
                    var fi = FindField(t, kv.Key);
                    if (fi == null) continue;
                    if (fi.FieldType == typeof(AudioClip) && kv.Value is FieldPPtr p)
                    {
                        var clip = Find(p.File, p.Path);
                        if (clip != null && fi.GetValue(comp) == null)
                        {
                            fi.SetValue(comp, clip);
                            MbFieldsResolved++;
                        }
                    }
                    else if (kv.Value is List<object> list)
                    {
                        var et = fi.FieldType.IsArray ? fi.FieldType.GetElementType() : null;
                        var clipFi = et == null ? null : FindField(et, "clip");
                        if (clipFi == null || clipFi.FieldType != typeof(AudioClip)) continue;
                        var arr = fi.GetValue(comp) as Array;
                        if (arr == null) continue;
                        for (int i = 0; i < Math.Min(arr.Length, list.Count); i++)
                        {
                            if (list[i] is Dictionary<string, object> sub
                                && sub.TryGetValue("clip", out var cv)
                                && cv is FieldPPtr sp)
                            {
                                var clip = Find(sp.File, sp.Path);
                                var elem = arr.GetValue(i);
                                if (clip != null && elem != null && clipFi.GetValue(elem) == null)
                                {
                                    clipFi.SetValue(elem, clip);
                                    MbFieldsResolved++;
                                }
                            }
                        }
                    }
                }
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
    }
}
