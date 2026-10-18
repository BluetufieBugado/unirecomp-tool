// Tier 1 — AudioMixer com SetFloat no-op (sem backend de áudio real).
namespace UnityEngine.Audio
{
    public partial class AudioMixer
    {
        public void SetFloat(string name, float value) { }
    }
}
