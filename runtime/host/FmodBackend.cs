// Áudio fatia A2 — FmodBackend: IAudioBackend via FMOD nativo (P/Invoke cdecl x64).
// Sem fmod.dll ao lado do exe: backend fica indisponível, jogo segue mudo (A1).
// Com a DLL oficial (fmod.com/download, FMOD Engine 2.x x64): decodifica os
// bancos FSB5/Vorbis headerless que o shim entrega em AudioClip.RawAudio.
//
// Licença/EULA FMOD é do usuário; NÃO baixar de dll-files.com.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Unirecomp.Host
{
    public sealed class FmodBackend : IAudioBackend
    {
        public static readonly FmodBackend Instance = new FmodBackend();
        public bool Available { get; private set; }
        static bool warned;

        IntPtr sys;
        readonly Dictionary<AudioSource, IntPtr> channels = new();        readonly Dictionary<AudioSource, IntPtr> sounds = new();
        readonly Dictionary<AudioClip, IntPtr> soundCache = new();
        readonly Dictionary<AudioClip, IntPtr> parentSounds = new();
        readonly Dictionary<AudioClip, IntPtr> memStore = new();
        readonly List<IntPtr> oneShots = new();

        // FMOD_RESULT OK = 0; modes.
        const uint LOOP_OFF = 0x00000001;
        const uint LOOP_NORMAL = 0x00000002;
        // NOTA: FMOD_OPENMEMORY (cópia, 0x08000000) devolve FILE_NOTFOUND/
        // INTERNAL nesta fmod.dll 2.04 mesmo com exinfo correto (sondado via
        // ctypes: WAV e FSB falham); FMOD_OPENMEMORY_POINT funciona p/ ambos.
        // Com POINT o FMOD lê direto do nosso buffer: por isso cada clipe é
        // copiado p/ memória não-gerenciada (AllocHGlobal) e mantido vivo
        // enquanto o som existir (GC poderia mover byte[] gerenciado).
        const uint OPENMEMORY_POINT = 0x10000000;
        const uint CREATESAMPLE = 0x00000200;

        [StructLayout(LayoutKind.Sequential)]
        struct ExInfo
        {
            public int cbsize;
            public uint length;
            public uint fileoffset;
            public int numchannels;
            public int defaultfrequency;
            public uint format;
            public int decodebuffersize;
            public int initialseekposition;
            public uint initialseekpostype;
            public int ignoresetfilesystem;
            public uint audioqueuepolicy;
            public uint minmidigranularity;
            public int nonblockthreadid;
            public IntPtr fsbguid;
        }

        const string Dll = "fmod";
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_System_Create(out IntPtr system, uint headerversion);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_System_Init(IntPtr system, int maxchannels, uint flags, IntPtr extra);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_System_Update(IntPtr system);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_System_Close(IntPtr system);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_System_Release(IntPtr system);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern unsafe int FMOD_System_CreateSound(IntPtr system, IntPtr data, uint mode, ref ExInfo exinfo, out IntPtr sound);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_System_PlaySound(IntPtr system, IntPtr sound, IntPtr group, [MarshalAs(UnmanagedType.I1)] bool paused, out IntPtr channel);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_Sound_SetMode(IntPtr sound, uint mode);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_Sound_Release(IntPtr sound);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_Sound_GetNumSubSounds(IntPtr sound, out int num);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_Sound_GetSubSound(IntPtr sound, int index, out IntPtr subsound);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_Channel_Stop(IntPtr channel);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_Channel_IsPlaying(IntPtr channel, [MarshalAs(UnmanagedType.I1)] out bool playing);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_Channel_SetVolume(IntPtr channel, float volume);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_Channel_SetPitch(IntPtr channel, float pitch);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int FMOD_Channel_SetPaused(IntPtr channel, [MarshalAs(UnmanagedType.I1)] bool paused);

        FmodBackend() { }

        /// <summary>Tenta carregar fmod.dll e iniciar o system (32 canais).
        /// Falha silenciosa (log 1x): retorna false, jogo segue mudo.</summary>
        public static bool TryInstall()
        {
            if (AudioSystem.Backend is FmodBackend fb && fb.Available) return true;
            try
            {
                // FMOD exige headerversion == versão da DLL (senão erro 20).
                // Tenta do mais novo ao mais antigo; patch 00 vale p/ a série.
                uint[] candidates = {
                    0x00020400, 0x00020300, 0x00020200, 0x00020100,
                    0x00020000, 0x00020116, 0x00020207,
                };
                int r = -1;
                IntPtr sys = IntPtr.Zero;
                foreach (var v in candidates)
                {
                    r = FMOD_System_Create(out sys, v);
                    if (r == 0) break;
                    sys = IntPtr.Zero;
                }
                if (r != 0) { Warn("FMOD_System_Create != 0 (" + r + ")"); return false; }
                r = FMOD_System_Init(sys, 32, 0, IntPtr.Zero);
                if (r != 0) { Warn("init != 0 (" + r + ")"); return false; }
                Instance.sys = sys;
                Instance.Available = true;
                AudioSystem.Backend = Instance;
                Console.WriteLine("[audio] FmodBackend ativo (fmod.dll carregada).");
                return true;
            }
            catch (DllNotFoundException) { Warn("fmod.dll ausente — áudio mudo (A1). Baixe FMOD Engine 2.x x64 em fmod.com/download e copie fmod.dll p/ junto do Unirecomp.Host.exe."); return false; }
            catch (Exception ex) { Warn("FMOD indisponível: " + ex.Message); return false; }
        }

        static void Warn(string msg)
        {
            if (warned) return;
            warned = true;
            Console.WriteLine("[audio] " + msg);
        }

        public int SoundsCreated => soundCache.Count;
        public int ChannelsStarted { get; private set; }
        public int LastCreateSoundError { get; private set; }
        public int LastPlayError { get; private set; }
        public static string Status()
        {
            var b = Instance;
            if (!b.Available) return "fmod: inativo (mudo)";
            int live = 0;
            foreach (var kv in b.channels)
            {
                try { if (FMOD_Channel_IsPlaying(kv.Value, out var p) == 0 && p) live++; }
                catch { }
            }
            return $"fmod: ativo, {b.SoundsCreated} sons, {b.ChannelsStarted} plays, {live} canais vivos" +
                (b.LastCreateSoundError != 0 ? $" (createErr={b.LastCreateSoundError})" : "") +
                (b.LastPlayError != 0 ? $" (playErr={b.LastPlayError})" : "");
        }

        IntPtr SoundFor(AudioClip clip, bool loop)
        {
            if (!Available || clip?.RawAudio == null || clip.RawAudio.Length < 64) return IntPtr.Zero;
            if (soundCache.TryGetValue(clip, out var s) && s != IntPtr.Zero)
            {
                FMOD_Sound_SetMode(s, loop ? LOOP_NORMAL : LOOP_OFF);
                return s;
            }
            try
            {
                // Buffer estável fora do GC (POINT lê dele durante playback).
                IntPtr mem = Marshal.AllocHGlobal(clip.RawAudio.Length);
                Marshal.Copy(clip.RawAudio, 0, mem, clip.RawAudio.Length);
                var ex = new ExInfo { cbsize = Marshal.SizeOf<ExInfo>(), length = (uint)clip.RawAudio.Length };
                int r = FMOD_System_CreateSound(sys, mem, OPENMEMORY_POINT | CREATESAMPLE | (loop ? LOOP_NORMAL : LOOP_OFF), ref ex, out s);
                if (r != 0 || s == IntPtr.Zero) { LastCreateSoundError = r; Marshal.FreeHGlobal(mem); return IntPtr.Zero; }
                // Banco FSB5 = container com 1 subsom: playSound exige o subsom.
                IntPtr playable = s;
                try
                {
                    if (FMOD_Sound_GetNumSubSounds(s, out int n) == 0 && n > 0
                        && FMOD_Sound_GetSubSound(s, 0, out var sub) == 0 && sub != IntPtr.Zero)
                        playable = sub;
                }
                catch { playable = s; }
                FMOD_Sound_SetMode(playable, loop ? LOOP_NORMAL : LOOP_OFF);
                soundCache[clip] = playable;
                parentSounds[clip] = s; // container precisa viver junto
                memStore[clip] = mem;
                return playable;
            }
            catch { return IntPtr.Zero; }
        }

        /// <summary>Libera sons/buffers da cena anterior (chamado a cada
        /// troca de level; os AudioClips morrem junto com a cena).</summary>
        public static void Reset()
        {
            var b = Instance;
            foreach (var kv in b.soundCache)
                try { FMOD_Sound_Release(kv.Value); } catch { }
            foreach (var kv in b.parentSounds)
                try { FMOD_Sound_Release(kv.Value); } catch { }
            foreach (var kv in b.memStore)
                try { Marshal.FreeHGlobal(kv.Value); } catch { }
            b.soundCache.Clear();
            b.parentSounds.Clear();
            b.memStore.Clear();
            b.channels.Clear();
        }

        void StartChannel(AudioSource src)
        {
            if (!Available || src?.clip == null) return;
            var snd = SoundFor(src.clip, src.loop);
            if (snd == IntPtr.Zero) return;
            try
            {
                if (channels.TryGetValue(src, out var old) && old != IntPtr.Zero)
                    FMOD_Channel_Stop(old);
                int r = FMOD_System_PlaySound(sys, snd, IntPtr.Zero, false, out var ch);
                if (r != 0 || ch == IntPtr.Zero) { LastPlayError = r; return; }
                ChannelsStarted++;
                float vol = src.mute ? 0f : Math.Clamp(src.volume, 0f, 1f) * AudioSystem.MasterVolume;
                FMOD_Channel_SetVolume(ch, vol);
                try { FMOD_Channel_SetPitch(ch, Math.Clamp(src.pitch <= 0 ? 1f : src.pitch, 0.25f, 4f)); } catch { }
                channels[src] = ch;
                sounds[src] = snd;
            }
            catch { }
        }

        public void OnPlay(AudioSource src) => StartChannel(src);

        public void OnOneShot(AudioClip clip, float volume)
        {
            if (!Available || clip == null) return;
            var snd = SoundFor(clip, false);
            if (snd == IntPtr.Zero) return;
            try
            {
                int r = FMOD_System_PlaySound(sys, snd, IntPtr.Zero, false, out var ch);
                if (r == 0 && ch != IntPtr.Zero)
                {
                    FMOD_Channel_SetVolume(ch, Math.Clamp(volume, 0f, 1f) * AudioSystem.MasterVolume);
                    oneShots.Add(ch);
                    if (oneShots.Count > 32) oneShots.RemoveAt(0);
                }
            }
            catch { }
        }

        public void OnStop(AudioSource src)
        {
            if (!Available) return;
            try
            {
                if (channels.TryGetValue(src, out var ch) && ch != IntPtr.Zero)
                    FMOD_Channel_Stop(ch);
                channels.Remove(src);
            }
            catch { }
        }

        public void OnPause(AudioSource src, bool paused)
        {
            if (!Available) return;
            try
            {
                if (channels.TryGetValue(src, out var ch) && ch != IntPtr.Zero)
                    FMOD_Channel_SetPaused(ch, paused);
                else if (!paused && src != null && src.isPlaying)
                    StartChannel(src); // pausou antes do init: recomeça
            }
            catch { }
        }

        public void OnTick()
        {
            if (!Available) return;
            try { FMOD_System_Update(sys); } catch { return; }
            // Resync: fonte tocando (ledger A1) sem canal vivo → recomeça.
            // Cobre playOnAwake antes do init e canais roubados pelo mixer.
            try
            {
                foreach (var src in UnityEngine.Object.AllOf<AudioSource>())
                {
                    if (src == null || !src.isPlaying || src.clip == null) continue;
                    bool alive = false;
                    if (channels.TryGetValue(src, out var ch) && ch != IntPtr.Zero)
                    {
                        try { alive = FMOD_Channel_IsPlaying(ch, out var p) == 0 && p; }
                        catch { alive = false; }
                        if (alive)
                        {
                            float vol = src.mute ? 0f : Math.Clamp(src.volume, 0f, 1f) * AudioSystem.MasterVolume;
                            try { FMOD_Channel_SetVolume(ch, vol); } catch { }
                            continue;
                        }
                    }
                    StartChannel(src);
                }
            }
            catch { }
        }
    }
}
