// Tier 0 — Time, Debug, Application, atributos (no-ops) e enums (só declarar).
using System;

namespace UnityEngine
{
    public static class Time
    {
        // Propriedades: o CIL do jogo chama get_/set_.
        public static float deltaTime { get; set; } = 1f / 60f;
        public static float fixedDeltaTime { get; set; } = 1f / 50f;
        public static float realtimeSinceStartup { get; set; }
        public static float time { get; set; }
        public static float timeScale { get; set; } = 1f;
        public static float unscaledDeltaTime { get; set; } = 1f / 60f;
    }

    public static class Debug
    {
        public static void Log(object msg) => Console.WriteLine("[LOG] " + msg);
        public static void LogError(object msg) => Console.Error.WriteLine("[ERR] " + msg);
        public static void LogWarning(object msg) => Console.WriteLine("[WARN] " + msg);
    }

    public enum RuntimePlatform { WindowsPlayer, LinuxPlayer, OSXPlayer, Android, WSAPlayerARM }

    public static class Application
    {
        public static void Quit() => Environment.Exit(0);
        public static RuntimePlatform platform => RuntimePlatform.WindowsPlayer; // host Android sobrescreve
        public static string version { get; set; } = "";
        public static int targetFrameRate { get; set; } = 60;
    }

    public class WaitForSeconds : Object
    {
        public float Seconds { get; }
        public WaitForSeconds(float seconds) { Seconds = seconds; }
    }

    public class WaitForEndOfFrame : Object
    {
        public WaitForEndOfFrame() { }
    }

    // Atributos: só metadados para o inspector; no-ops no runtime.
    public class SerializeField : Attribute { }
    public class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header) { }
    }
    public class HideInInspector : Attribute { }
    public class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max) { }
    }
    public class SpaceAttribute : Attribute
    {
        public SpaceAttribute(float height = 8) { }
    }
    public class TextAreaAttribute : Attribute { }
    public class RequireComponent : Attribute
    {
        public RequireComponent(Type t) { }
    }
    public class ExecuteInEditMode : Attribute { }

    // Enums referenciados como tipo (0 membros no apicheck): declarar basta.
    public enum KeyCode { None, W, A, S, D, Space, Escape, UpArrow, DownArrow, LeftArrow, RightArrow, Mouse0 }
    public enum Space { World, Self }
    public enum WrapMode { Clamp, Loop, PingPong }
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum ForceMode { Force, Impulse }
}

namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode { Single, Additive }
}
