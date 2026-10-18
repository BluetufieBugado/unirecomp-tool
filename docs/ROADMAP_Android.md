# Roadmap — do Tier 0 até o APK Android (jogo 1: Stardust Raiders)

> Destino: APK instalável que, com os arquivos **originais do jogo fornecidos
> pelo usuário**, abre o Stardust Raiders no Android. Meta inicial modesta de
> propósito: **menu principal navegável por toque + level_01 rodando**.
> Detalhe por fase abaixo; contexto geral em `STATUS.md`.

## Onde estamos (feito, 2026-10-12)

Análise (`analyze`), checklist CIL (`dump_api`: 120 tipos / 354 membros),
priorização (`prio`: Tiers 0–3), host C# Tier 0 (fonte) + protótipo Python
validado (33 checks, cobertura Tier 0 100%).

## Fase 1 — Host Windows compila e carrega o jogo de verdade

**Instalar:** .NET 8 SDK (ver "Preciso instalar o quê?" no fim). Nada além disso.

- [x] `dotnet build runtime/host` passar limpo.
- [x] `AssemblyLoadContext` próprio resolvendo `UnityEngine*` → shim
      (`UnirecompLoadContext` em `runtime/host/Program.cs`; refs do jogo são
      Version=0.0.0.0/PublicKeyToken=null, então o ALC resolve por nome).
- [x] Carregar o `Assembly-CSharp.dll` **real** do Stardust via `-- <Managed>`
      (2026-10-12: **84 MonoBehaviours listados, 0 erros de binding**).
- [x] Smoke rodar contra tipos reais do jogo (Smoke passa no host compilado).

**Done:** host imprime a lista de MonoBehaviours do Stardust sem exceção de binding. ✅ FASE 1 CONCLUÍDA.

## Fase 2 — Ciclo de vida + cenas

### 2a. Mecânica (✅ concluída 2026-10-12)

- [x] Scheduler de corotinas (`WaitForSeconds` / `WaitForEndOfFrame`, aninhadas)
      + `Invoke` com delay (`PlayerLoop.cs`).
- [x] `DontDestroyOnLoad` de verdade (marca `KeepAlive`; troca de conteúdo no 2b).
- [x] `SceneManager` mecanismo: registro, `LoadSceneAsync`, evento `sceneLoaded`.
- [x] Headless: `dotnet run --project runtime/host -- --headless` → 12/12 em 600 frames.

### 2b. Conteúdo

- [x] Spike: inventário das cenas (`dump_scene.py` → `docs/scenes_stardust.json`):
      level0=main_menu (163 GOs), level1=level_01 (2384 GOs), level2=boss_fight (212),
      level3=credits (49). Build tem 4 das 7 cenas do fonte.
- [x] Mapa stripped→(GO,script) a 100% (`mbscript_map.py` → `docs/mbmap_stardust.json`,
      2046/2046; MonoScripts no globalgamemanagers via externals fileID 1).
- [x] Formato do container decifrado (`sfindex.py` → `docs/sfindex_*.json`, 11/11 a
      100% vs UnityPy; spec em `STATUS.md` §4c).
- [ ] **Parser próprio em C#** (port do sfindex) + instanciação da hierarquia de
      `main_menu` (nomes/tags/active/transforms, validado vs UnityPy).
- [x] Layouts de fields (`genlayouts.py` → `docs/layouts_stardust.json`: 53/53 full;
      só public + [SerializeField]; cobertura total dos 47 usados nos levels).
- [x] **Port C# do sfindex** (`SerializedFile.cs`: índice validado vs oráculo no
      `--load-scene`) + instanciação da hierarquia de `main_menu` (163 GOs,
      nomes/tags/active/transforms, 10 raízes, mundo composto 163/163).
- [x] Aplicação dos fields com assert de tamanho por objeto (2b-iii, 2026-10-13:
      `FieldReader.cs` + passe 5 no `SceneBuilder` + A9; 4 levels verdes —
      level0 12, level1 1090, level2 36, level3 6 aplicados; ver STATUS §4f).
- [x] `SceneManager` troca conteúdo de verdade (2b-iv, 2026-10-13: hook +
      `SceneDirector` + `--boot` level0→level3, GameManager DDL mesma instância;
      fields lives=3 intactos — ver STATUS §4g).
- [x] Fidelidade do player loop: `StartCoroutine(string[,object])`, `LateUpdate`,
      `OnEnable`/`OnDisable` (+ `OnDisable`-antes-`OnDestroy`; ver STATUS §4g).

**Done:** teste headless instancia `main_menu`, roda 600 frames de `Update`
sem crash e o log mostra os objetos esperados da cena. (Cumprido em forma
forte via `--boot level0→level3`, 600 frames com o jogo real — STATUS §4g.)

## Fase 3 — Gameplay no PC (Tier 1): janela + input + áudio + física mínima

- [x] Janela e loop principal — escolher **já mirando GLES** (regra da Fase 4):
      Silk.NET é o candidato natural no mundo .NET. (2026-10-13: `--play` OK)
- [x] `Input` teclado/mouse; `Touch` mapeado do mouse no PC. (2026-10-13: SilkInputState)
- [ ] `AudioSource/AudioClip` via backend com abstração (ex.: Miniaudio),
      com mixer/group/volume/pitch/loop da checklist.
- [ ] Física mínima: volumes de trigger + `Physics.Raycast` simples. Sorte nossa:
      o jogo quase não usa `Rigidbody` de verdade (1 `AddForce`, gatilhos dominam).

**Done:** `level_01` jogável no PC com teclado.

## Fase 4 — Apresentação no PC (Tier 2): render + UI

- [ ] **Renderer compatível com GLES2/3 desde o dia 1** — regra dura para não
      reescrever tudo no Android. Nada de API exclusiva de desktop.
- [ ] `Mesh/Renderer/Material/Light`; converter os shaders (1 custom do jogo +
      padrões Unity) para GLSL.
- [ ] uGUI mínimo: `Canvas, Text, Image, Button, EventSystem` + callbacks `UnityEvent`.
- [ ] **TextMeshPro mínimo** (`TMP_Text/UGUI` estático primeiro) — são 25 classes,
      o maior bloco do runtime; começar pelo subset que o jogo realmente usa.
- [ ] `Animator`: `speed` + leitura de estado (aproximação vale no início).

**Done:** `main_menu` e `level_01` visíveis e navegáveis no PC.

## Fase 5 — Port Android (o APK)

**Instalar:** Android SDK cmdline-tools (Java 17 já existe aqui) + workload
Android do .NET (`dotnet workload install maui-android`). Só nesta fase.

- [ ] `AndroidPlatform : IPlatform` (`RuntimePlatform.Android`): ciclo de vida da
      Activity, tempo, log via logcat.
- [ ] Arquivos do jogo no device: leitura via pasta do app/AssetManager.
      **Modelo de distribuição** (decidir aqui): (a) teste — player + pasta de
      dados copiada pelo usuário; (b) final — builder no PC empacota os arquivos
      originais no APK (modelo "recomp").
- [ ] Toque → `Input` (toque simples + arrasto mapeados para o que o jogo lê).
- [ ] `PlayerPrefs` → arquivo local; `Screen` fixo na resolução do device.
- [ ] Empacotar, assinar (debug), testar em **emulador + aparelho real**.

**Done:** APK abre `main_menu`, toque navega, `level_01` roda com áudio.

## Fase 6 — Polimento

Performance (batching, atlas de sprites), latência de áudio, saves,
tela de config (qualidade/resolução), ícone/splash, teste em 2–3 aparelhos.

## Riscos (top 3) e mitigação

1. **TextMeshPro (25 classes)** — mitigar com subset estático primeiro.
2. **Asset pipeline 2018.1** — mitigar estudando UnityPy/AssetStudio (leitura).
3. **Shaders → GLES** — 1 custom + padrões; fallback unlit no início.

## Preciso instalar o quê? (medo de entulhar o PC — respondido 2026-10-12)

- **Hoje: nada.** O protótipo Python valida o desenho atual sem instalar nada.
- **Fase 1: .NET 8 SDK** — é um programa instalado na máquina (instalador oficial
  Microsoft, ~300 MB), **não** fica na pasta do projeto. Instala lado a lado com
  outras versões, aparece em "Adicionar/Remover Programas" e desinstala limpo.
  É o equivalente ao JDK que já existe aqui — e o Gradle que você já usa continua
  servindo para a parte Android lá na Fase 5.
- **Fase 5: Android SDK cmdline-tools + workload `maui-android`** (download via
  `dotnet`, não outro instalador gigante). Só quando o PC já rodar o jogo.
