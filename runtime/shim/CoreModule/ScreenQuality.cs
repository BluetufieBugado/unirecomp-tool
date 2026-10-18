// Tier 3 mínimo — Screen/QualitySettings (usados pelo OptionsMenu no boot).
// Resolução real do device só na Fase 5 (Android).
namespace UnityEngine
{
    public partial struct Resolution
    {
        public int width { get; set; }
        public int height { get; set; }
        public int refreshRate { get; set; }
    }

    public static partial class Screen
    {
        public static int width => 1920;
        public static int height => 1080;
        public static bool fullScreen { get; set; } = true;
        public static Resolution[] resolutions => new[] { currentResolution };
        public static Resolution currentResolution => new Resolution { width = 1920, height = 1080, refreshRate = 60 };
        public static void SetResolution(int w, int h, bool full) { }
    }

    public static partial class QualitySettings
    {
        public static int vSyncCount { get; set; }
        public static int GetQualityLevel() => 0;
        public static void SetQualityLevel(int index, bool applyExpensiveChanges = true) { }
    }
}
