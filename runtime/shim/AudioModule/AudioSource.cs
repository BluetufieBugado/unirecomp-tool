// Tier 1 — AudioSource com estado simulado + ledger no AudioSystem.
// Play/Stop/Pause/PlayOneShot funcionam; o tempo avança em AudioSystem.Tick.
// Backend audível (fatia A2) consome o mesmo ledger.
using System;
using UnityEngine;

namespace UnityEngine
{
    public partial class AudioSource
    {
        AudioClip _clip;
        float _playTime;
        bool _playing;
        public AudioClip clip { get => _clip; set { _clip = value; _playTime = 0f; } }
        public float volume { get; set; } = 1f;
        public float pitch { get; set; } = 1f;
        public bool loop { get; set; }
        public bool mute { get; set; }
        public bool playOnAwake { get; set; } = true;
        public bool isPlaying => _playing;
        public float time => _playTime;
        public Audio.AudioMixerGroup outputAudioMixerGroup { get; set; }
        public void Play() { _playing = true; _playTime = 0f; AudioSystem.Backend?.OnPlay(this); }
        public void PlayDelayed(float delay) { Play(); }
        public void PlayOneShot(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            AudioSystem.PlayOneShot(clip, volume * volumeScale);
        }
        public void Stop() { _playing = false; _playTime = 0f; AudioSystem.Backend?.OnStop(this); }
        public void Pause() { _playing = false; AudioSystem.Backend?.OnPause(this, true); }
        public void UnPause() { if (_clip != null) _playing = true; AudioSystem.Backend?.OnPause(this, false); }

        internal void Advance(float dt)
        {
            if (!_playing || _clip == null) return;
            _playTime += dt * pitch;
            float len = _clip.length;
            if (len > 0 && _playTime >= len)
            {
                if (loop) _playTime = _playTime % len;
                else { _playing = false; _playTime = 0f; }
            }
        }
    }

    /// <summary>Relógio das fontes + ledger p/ o backend audível (fatia A2).
    /// Backend real implementa IAudioBackend e assina AudioSystem.Backend.</summary>
    public interface IAudioBackend
    {
        void OnPlay(AudioSource src);
        void OnOneShot(AudioClip clip, float volume);
        void OnStop(AudioSource src);
        void OnPause(AudioSource src, bool paused);
        void OnTick();
    }

    public static class AudioSystem
    {
        public static IAudioBackend Backend { get; set; }
        public static float MasterVolume { get; set; } = 1f;
        static int oneShots;

        public static void PlayOneShot(AudioClip clip, float volume)
        {
            oneShots++;
            Backend?.OnOneShot(clip, volume);
        }

        /// <summary>Avança o tempo das fontes (chamado todo frame).</summary>
        public static void Tick()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) { Backend?.OnTick(); return; }
            foreach (var src in Object.AllOf<AudioSource>())
                src.Advance(dt);
            Backend?.OnTick();
        }
    }
}
