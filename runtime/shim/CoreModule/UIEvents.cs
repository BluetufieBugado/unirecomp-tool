// Tier UI-1 — RectOffset + UnityEvent persistente (dados p/ dispatch futuro).
using UnityEngine;

namespace UnityEngine
{
    public class RectOffset
    {
        public int left, top, right, bottom;
        public RectOffset() { }
        public RectOffset(int left, int top, int right, int bottom)
        { this.left = left; this.top = top; this.right = right; this.bottom = bottom; }
    }
}

namespace UnityEngine.Events
{
    public sealed class PersistentCallData
    {
        public Object Target;
        public string MethodName = "";
        public int Mode;
        public Object ObjectArgument;
        public string ObjectArgumentType = "";
        public int IntArgument;
        public float FloatArgument;
        public string StringArgument = "";
        public bool BoolArgument;
        public int CallState;
    }

    public sealed class UnityEventData
    {
        public readonly System.Collections.Generic.List<PersistentCallData> Calls =
            new System.Collections.Generic.List<PersistentCallData>();
        public string TypeName = "";
    }
}
