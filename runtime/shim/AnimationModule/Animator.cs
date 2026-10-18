// Tier 2 mínimo — Animator (neutro; state machines reais depois).
namespace UnityEngine
{
    public partial class Animator
    {
        public float speed { get; set; } = 1f;
        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex) => default;
    }

    public partial struct AnimatorStateInfo
    {
        public bool IsName(string name) => false;
    }
}
