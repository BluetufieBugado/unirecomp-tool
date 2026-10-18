// Tier 1 — AudioClip com metadados reais (vêm do manifesto offline).
// Amostras decodificadas (fatia A2) entram em PCM via AudioSystem.
namespace UnityEngine
{
    public partial class AudioClip
    {
        public float length { get; set; }
        public int frequency { get; set; } = 44100;
        public int channels { get; set; } = 2;
        public int samples => (int)(length * frequency) * channels;

        // Bruto FSB/Vorbis do .resource do jogo (fatia A2 decodifica).
        public byte[] RawAudio { get; set; }
        public string ResourceFile { get; set; } = "";
        public long ResourceOffset { get; set; }
        public int ResourceSize { get; set; }
    }
}
