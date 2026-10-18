// Tier 1 mínimo — Resources.Load (retorna null; assets reais vêm no Tier 2).
// GameManager.Instance só chama se _instance==null (nunca no boot normal).
namespace UnityEngine
{
    public static partial class Resources
    {
        public static Object Load(string path) => null;
        public static T Load<T>(string path) where T : Object => null;
    }
}
