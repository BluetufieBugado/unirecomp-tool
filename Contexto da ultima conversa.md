## Objective
- Continuar UniRecomp Tool: fechar UI real (Text/Image/RectTransform) para o menu do jogo renderizar como o original. A2 áudio FMOD já concluído e validado com som audível.

## Important Details
- Jogo alvo: `Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data`, Unity 2018.1.5f1 Mono, `level0` = main_menu 163 raízes.
- Host: `dotnet run --project runtime/host/Unirecomp.Host.csproj -- --boot "<pasta_Data>" level0`; `--play ... level0 0`; `--headless`.
- Regra Fase 4: só subset GLES2 (VBO+IBO+shader, sem fixed, sem `GL_QUADS`).
- A2 concluído: FMOD nativo 2.04, `fmod.dll` oficial x64 em `runtime/host/bin/Debug/net8.0/fmod.dll`. Música do menu toca no `--play`. Armadilhas: `System_Create` exige `headerversion` (2.04→2.00); `OPENMEMORY` quebrado nesta DLL → usar `OPENMEMORY_POINT` + `AllocHGlobal`; banco FSB5 = container com 1 subsom → `playSound` exige `GetSubSound(0)`.
- UI: `layouts_stardust.json` só tem 53 scripts do jogo; zero classes engine. Componentes UI existem como MB 114 (`UnityEngine.UI.dll::Image/Text/...`) com `ok:true` no mbmap, mas fields pulados. level0 UI: 71 Image, 19 TextMeshProUGUI, 15 Text, 14 EventTrigger, 11 GraphicRaycaster, 11 CanvasScaler, 7 Button, 5 Toggle, 3 Slider, 3 Outline, 2 Scrollbar, 2 Mask, 2 ScrollRect, 1 EventSystem, 1 StandaloneInputModule, 1 Dropdown, 1 Shadow, 1 TMP_Dropdown.
- Unity 2018.1 UI: classes como OptionData/AnimationTriggers/TMP_TextInfo serializam SEM [Serializable]; UnityEvent base genérica (ToggleEvent : UnityEvent<bool>); RectOffset não é struct Unity (é classe); TMP_TextInfo com 13 arrays.
- Engine layouts extraídos por `tools/uidump/` (System.Reflection.Metadata puro, BCL) → `docs/layouts_engine.json`: 39 scripts, 19 nested, 502 enums. `FieldReader.Load` funde automaticamente `layouts_engine.json` ao lado de `layouts_stardust.json`.
- Kinds novos: `color32`, `rect`, `layermask` (int), `rectoffset` (4×int), `unityevent` (PersistentCallGroup: count + PersistentCall{target,mode,target,argtype,i,f,s,b,cs} + typename), `vector4` separado de `quaternion`, `char` = u16+align.

## Work State
### Completed
- A1/A2 áudio: FmodBackend com POINT, `Reset()` por cena, subsom, validado `2 sons, 2 plays, 2 canais vivos` zero `[ERR]`; música toca no `--play`.
- Cosmetic: `Path.GetFullPath` no log de textura (SilkPlatform.cs).
- `tools/uidump/` criado e funcionando: extrai layouts engine via metadata (base-primeiro, cadeia de herança, atributos SerializeField/NonSerialized, TypeSpec base, UnityEvent, RectOffset).
- `FieldReader.cs`: `Load` funde `layouts_engine.json`; `ReadOne` estendido com color32/rect/layermask/rectoffset/unityevent/vector4/char; `PersistentCallData`/`UnityEventData` host-side; `ReadUnityEvent` implementado.
- Shim CoreModule: `RectTransform.cs` (SetFromData 10 floats, GetPixelRect), `UIEvents.cs` (RectOffset + UnityEventData/PersistentCallData), `UseRectTransform` no GameObject, `TextAnchor` completo, `transform` setter internal.
- Shim UI: `UIFields.cs` (todos m_* fields: Graphic/Image/Text/FontData/Navigation/ColorBlock/SpriteState/AnimationTriggers/Selectable/Button/Dropdown/Toggle/ToggleGroup/Slider/Scrollbar/ScrollRect/Mask/GraphicRaycaster/CanvasScaler/EventTrigger/EventSystem/StandaloneInputModule), `UIImpl.cs` reescrito com props de comportamento, Shells.cs com bases corretas (Button/Dropdown/Toggle/Slider/Scrollbar : Selectable).
- Shim TextMeshPro: `TMPFields.cs` (TMP_Text 63 fields, TextMeshProUGUI, TMP_Dropdown : Selectable, TMP_CharacterInfo/WordInfo/LinkInfo/LineInfo/MeshInfo/TextInfo/Vertex/TextElement/Extents/VertexGradient), cross-ref UI adicionada.
- SceneBuilder: Convert estendido (char, Color32, Rect, RectOffset, UnityEventData→shim com Resolve de PPtrs); Pass2 rewire classID 224 → `UseRectTransform`.
- Build 0 erros; `uidump` roda sem warnings relevantes (só delegate m_OnValidateInput corretamente skipped).

### Active
- Debug do assert de tamanho: `--boot level0` falha com `FALHOU: carga inicial MB 504 (TextMeshProUGUI): consumido 696 != size 768` (under-consumo de 72 bytes).
- `tools/decode_mb.py` criado para decodificar campo a campo o blob do MB 504 e mostrar offsets — ainda não executado.

### Blocked
- Under-consumo 72 bytes no TextMeshProUGUI (768-696): algum field está faltando no layout ou tem tamanho errado a partir de algum offset. `decode_mb.py` vai revelar o descompasso campo a campo.

## Next Move
1. Rodar `python3 tools/decode_mb.py level0 504` e comparar offsets com o layout para achar qual campo dessincroniza (provável: algum nested/array dentro de TMP_TextInfo ou field faltante em TMP_Text/TextMeshProUGUI).
2. Corrigir o layout no uidump ou ajustar o FieldReader; rebuild; `--boot level0` até zero `[ERR]`/warnings de fields.
3. Após assert limpo: UIRenderer — canvas-space layout (CanvasScaler scale factor), texturas de Sprite (Texture2D 28 do sharedassets0), atlas de fonte (Font 128), desenho de Image/Text/Button no espaço da tela; validar `--play level0` com menu visível.

## Relevant Files
- `runtime/host/FmodBackend.cs`: backend FMOD (POINT, subsom, Reset) — concluído.
- `runtime/host/FieldReader.cs`: layouts merge engine+game, ReadUnityEvent, kinds novos.
- `runtime/host/SceneBuilder.cs`: Convert estendido, Pass2 rewire RectTransform.
- `runtime/shim/CoreModule/RectTransform.cs`: SetFromData/GetPixelRect.
- `runtime/shim/CoreModule/UIEvents.cs`: RectOffset, UnityEventData/PersistentCallData shim.
- `runtime/shim/CoreModule/Core.cs`: UseRectTransform, transform internal setter.
- `runtime/shim/CoreModule/System.cs`: TextAnchor completo.
- `runtime/shim/UI/UIFields.cs`, `UIImpl.cs`, `Shells.cs`: fields+props uGUI.
- `runtime/shim/TextMeshPro/TMPFields.cs`, `Shells.cs`, `.csproj`: TMP fields + cross-ref UI.
- `tools/uidump/Program.cs`, `uidump.csproj`: extrator de layouts engine.
- `tools/decode_mb.py`: decodificador campo a campo de blob MB (debug assert).
- `docs/layouts_engine.json`: layouts engine extraídos (39 scripts, 19 nested).
- `STATUS.md`: A2 documentado; UI ainda não.
- `Stardust Raiders/Assets/Scripts/UI/*.cs`: MainMenu/SelectOnInput/OptionsMenu/PauseMenu — usam EventSystem.firstSelected/currentSelected/SetSelected, TMP_Dropdown, Toggle, Dropdown.