// Resolvedor de MonoScript do globalgamemanagers.assets.
// Permite que o SceneBuilder/SceneDirector resolvam tipos de comportamento
// do jogo (Assembly-CSharp.dll) a partir dos MonoScripts originais,
// antes de ler os campos via layouts.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Unirecomp.Host
{
    public static class MonoScriptResolver
    {
        public static Dictionary<string, (string assembly, string cls)> MonoScriptsForGame()
        {
            var db = JsonSerializer.Deserialize<Dictionary<long, MonoScriptEntry>>(
                File.ReadAllText("docs/gm_mono_scripts.json"));
            var res = new Dictionary<string, (string, string)>();
            foreach (var kv in db)
            {
                var m = kv.Value;
                if (string.IsNullOrEmpty(m.Name)) continue;
                if (m.Assembly == "Assembly-CSharp.dll")
                    res[m.Name] = (m.Assembly, m.Class ?? m.Name);
            }
            return res;
        }
    }

    public sealed class MonoScriptEntry
    {
        public string Name { get; set; }
        public string Class { get; set; }
        public string Assembly { get; set; }
        public string Namespace { get; set; }
    }
}
