# Stardust Raiders — ficha do jogo 1 (MVP)

- Fonte: projeto Unity aberto + build Windows na pasta (`Stardust Raiders/` + `Stardust_Raiders_1.0.4_Windows/`)
- Versão: **2018.1.5f1** (ProjectVersion == globalgamemanagers, match=True)
- Perfil: **Mono=True, IL2CPP=False**, `Assembly-CSharp.dll` = 132KB, 55 scripts
- Plugins nativos fora de `Managed`: **0**
- Rede: **nenhum** script com Network/Photon/Mirror
- Riscos (DllImport/System.IO/System.Net): **0**
- `UnityEditor` restrito a 2 arquivos em `Assets/Scripts/Editor/` (fora do runtime)
- Levels no build: level0, level1, level2, level3
- Cenas do jogo (7): boss_fight, credits, enemy_test, level_01, main_menu, modeling, sound_test (+ cenas de exemplo do TextMeshPro, ignorar)
- Shaders: 1 custom do jogo (`Shaders/Normal-DiffuseCullFront.shader`) + 10 do TextMeshPro
- Top APIs (ver `docs/profile_stardust.json`): Vector3, Quaternion, Time, Destroy, Transform, Input, Camera, Canvas, Collider, AudioSource, Trigger, Text, SceneManager, Instantiate, AudioClip

Veredito do `tools/analyze.py`: **bom candidato para MVP**.

## Como reproduzir a análise

```powershell
python tools/analyze.py --project "Stardust Raiders" --build "Stardust_Raiders_1.0.4_Windows" --out "docs/profile_stardust.json"
```
