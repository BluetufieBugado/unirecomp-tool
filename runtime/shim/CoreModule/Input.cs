// Phase 3 implementation — Input teclado/mouse via Silk.NET.
// WindowRunner alimenta IInputState a cada frame; o jogo lê via Input.*
using System;

namespace UnityEngine
{
    public interface IInputState
    {
        bool IsKeyDown(KeyCode key);
        bool IsMouseDown(int button);
        float GetAxis(string name);
    }

    public static partial class Input
    {
        static IInputState _state;

        internal static void SetState(IInputState state) => _state = state;

        public static float GetAxis(string name)
        {
            if (_state != null) return _state.GetAxis(name);
            return 0f;
        }

        public static bool GetButtonDown(string name) => name == "Jump" && IsKeyDown(KeyCode.Space);
        public static bool GetButtonUp(string name) => false;
        public static bool GetButton(string name) => name == "Jump" && IsKeyDown(KeyCode.Space);
        public static bool GetKey(KeyCode key) => IsKeyDown(key);
        public static bool GetKeyDown(KeyCode key) => IsKeyDown(key);
        public static bool GetKeyUp(KeyCode key) => false;
        public static bool GetMouseButton(int button) => IsMouseDown(button);
        public static bool GetMouseButtonDown(int button) => IsMouseDown(button);
        public static bool GetMouseButtonUp(int button) => false;
        public static Vector3 mousePosition => Vector3.zero;
        public static int touchCount => 0;
        public static Touch[] touches => new Touch[0];
        public static Touch GetTouch(int index) => default;
        public static bool simulateMouseWithTouches { get; set; }

        static bool IsKeyDown(KeyCode key)
        {
            if (_state == null) return false;
            try { return _state.IsKeyDown(key); }
            catch { return false; }
        }

        static bool IsMouseDown(int button)
        {
            if (_state == null) return false;
            try { return _state.IsMouseDown(button); }
            catch { return false; }
        }
    }
}
