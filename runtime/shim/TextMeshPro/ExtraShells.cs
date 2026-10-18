// Cascas dirigidas por CENAS (não pelo apicheck): tipos que o jogo não
// referencia no CIL mas aparecem instanciados nos levels (via mbmap).
// Não vai em Shells.cs (gerado por genshells.py) para não brigar com o gerador.
// - TMP_SubMeshUI: 1 instância no level3 (credits). Morrre/completa no Tier 2.
using UnityEngine;

namespace TMPro
{
    public class TMP_SubMeshUI : Behaviour { }
}
