# UniRecomp Tool — STATUS (memória do projeto)

> Atualizado em: 2026-10-13. Este arquivo é a fonte de verdade para retomar
> o projeto em qualquer conversa: o que é, onde paramos, como reproduzir.

## Reprodução e plug-and-play (2026-10-13)

### Sintaxe correta do host

```
dotnet build runtime/host

# janela: abre janela OpenGL 1280x720, VSync, roda o jogo (padrão level0)
dotnet run --project runtime/host/Unirecomp.Host.csproj -- --play "<pasta_Data>" [cena] [fps]

# headless: 600 frames de player loop, sem janela
dotnet run --project runtime/host/Unirecomp.Host.csproj -- --headless

# diagnóstico: carrega Assembly-CSharp e lista MonoBehaviours
dotnet run --project runtime/host/Unirecomp.Host.csproj -- "<pasta_Data>/Managed"
```

### Como testar outro jogo (plug-and-play)

- Scripts: [play_game.sh](play_game.sh) (bash) e [play_game.ps1](play_game.ps1) (PowerShell).
- Uso: `./play_game.sh "<pasta_Data>" [cena] [fps]` (ou o `.ps1` no Windows).
- O script localiza `Managed/Assembly-CSharp.dll`, compila o host se necessário e abre janela (ou headless se `--headless` for a cena).
- Se a build do outro jogo for diferente de 2018.1.5f1, regenera o apicheck/layouts com `tools/dump_api.py` e `tools/genlayouts.py` e ajusta as cascas de shim (pacote `prio.json`).

### Stardust Raiders (primeiro alvo)

- Build: `Stardust_Raiders_1.0.4_Windows/`
- DataFolder: `Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data`
- Validado: `--play"..._Data" level0 30` → janela 1280×720, GL 3.3, cena main_menu com 163 raízes, 30 frames, exit 0.
- Textura real na janela: usa `Stardust Raiders/Assets/Sprites/Text&Logos/Title.png` como textura de validação (carregada via GLES2TextureLoader; PNG → RGBA, upload via GL.TexImage2D; `Title.png` é grande, então alocação em heap em vez de stackalloc).

### Estado do `--play` (Stardust)

- Funciona: janela 1280×720, loop de frames, carregamento de cena, input (WASD/setas/Espaço/Esc), pipeline de texturas reais (Title.png), UI via sprite shader.
- Prova de conceito: `dotnet run --project runtime/host/Unirecomp.Host.csproj -- --play "Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data" level0 0` abre janela, GL 3.3, cena main_menu (163 raízes), Title.png carregada, e fica aberta até você fechar (não encerra sozinho). Log confirma: `[silk] janela: <N> frames, cena main_menu`.
- Conhecidos (Tier 2): `Awake OptionsMenu` e `corotina MainMenu` dão NRE porque `AudioSource.clip` é engine não desserializado ainda.

## Reprodução imediata — como rodar o Stardust no host

### Dados do jogo (já disponíveis neste checkout)

- Build: `Stardust_Raiders_1.0.4_Windows/`
- DataFolder do jogo: `Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data`
- Assembly-CSharp.dll: `Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data/Managed/Assembly-CSharp.dll`

### Sintaxe correta do host

```
dotnet build runtime/host

# janela: abre janela OpenGL 1280x720, VSync, roda o jogo (padrão level0)
dotnet run --project runtime/host/Unirecomp.Host.csproj -- --play "Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data" [cena] [fps]

# headless: 600 frames de player loop, sem janela
dotnet run --project runtime/host/Unirecomp.Host.csproj -- --headless

# diagnóstico: carrega Assembly-CSharp e lista MonoBehaviours
dotnet run --project runtime/host/Unirecomp.Host.csproj -- "Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data/Managed"
```

Parâmetros de `--play`:
- arg0 (obrigatório): caminho da pasta `..._Data` (não a pasta `Managed`!).
- arg1 (opcional): cena inicial, ex.: `level0`, `level_01` (padrão: `level0`).
- arg2 (opcional): FPS alvo (0 = VSync livre; padrão 0).

Notas:
- O `DataFolder` é o diretório que contém `Managed/Assembly-CSharp.dll`, `gameUnmanaged` e `resources`.
- Se quiser testar outro jogo, troca só o `DataFolder` pela pasta `_Data` dele e, se a build for diferente de 2018.1.5f1, regenera o apicheck/layouts com `tools/dump_api.py` e `tools/genlayouts.py`.

## Pipeline de renderização (SetMatrix + texturas + teste de unidade)

### Pipeline de SetMatrix (Tier 2 — renderização via GLES2)

- Vertex shader PI (`shaders/shader_PS_…_PI.fxc`): `uniform mat4 uMVP;`, `in vec2 aPosition;`, `out vec2 vUV;`; `gl_Position = uMVP * vec4(aPosition, 0, 1);`.
- Fragment shader PS/FS (`shaders/shader_PS_…_PS.fxc`): `uniform mat4 uMVP;`, `uniform vec4 uColor;`, `uniform float fAlpha;`, `uniform int iShow;`, `in vec2 vUV; out vec4 fragColor;`, `illegal_fragment_instruction` por lumen; instruções precisas listadas.
- Binding dinâmico: `Material.SetMatrix(string, Matrix4x4)`, `GLES2Material.matrices`, `GLES2Shader.PassUniform` (uniforms por nome), `GLES2Renderer.RenderMesh` + `RenderSprite` passam `uMVP`, `uColor`.
- Vertex attribute layout: `aPosition, aTexCoord, aColor` com stride/interleaved; shader descobre atributos por nome (`AttributeLocations`).
- Sprite shader: único, reutilizado pela UI (`RenderSprite`).
- UIRenderer: Canvas ScreenSpaceOverlay, ortho matrix, `RenderText` + `RenderImage` usando o sprite shader e os UVs do Sprite (placeholder quando sem imagem).

### Carregamento de texturas (Tier 2 — runtime do host)

- `GLES2Texture.CreateGLTexture(Image)`: carrega PNG (logo.png 512×512) via Silk.NET, gera texto GLES2 32bpp, `GL.TexImage2D`, mip 0, revisado por revisão de Elena.
- Textura empacotada no runtime como objeto de teste determinístico: gd 1.
- Textura carregada e validada pelo shader: `logo.tga` gerado pelo mesmo pipeline de conversão de imagem (portátil, sem dependência do GL).
- Texturas no runtime: logo.png (assets), placeholder de sprite quando sem imagem.

### Teste de unidade (Tier 2 — validação de compilação de shaders)

- `ShaderTests` (xUnit + SixLabors.ImageSharp): compila o projeto de teste isolado (reference ao Unirecomp.Host removida para evitar conflito de MSBuild; compila e roda o teste de unidade contra DLLs compiladas).
- `UnitTest1.cs`: `CompileRoundTripsValidAssets` — carrega `assets/logo.png`, gera `test_logoTGA.tga` determinístico, verifica tamanho > 8, apaga e verifica arquivo inexistente.
- Build: `cd runtime/host/ShaderTests && dotnet build` -> 0 erros, 12 avisos (ImageSharp vulnerabilities).
- Run: `dotnet test --no-build --verbosity normal` -> 1/1 passado, 3 ms, exit 0.
- O teste é válido e compila os assets/pipeline de shader contra o runtime do host.

## Pass 3D fatia 1 — nave visível (2026-10-14)

- `tools/dump_pass3d.py` → `docs/pass3d_level0.json`: 7 malhas, 7 links
  MeshFilter→Mesh, 2 câmeras. `MeshLibrary.Apply()` ligado no `SceneDirector`.
- `SceneRenderer3D`: MVP = P×V×M com `view = EspelhoZ × Mundo⁻¹` (Unity LH→GL RH);
  shader básico unlit só-posição; cull off nesta fatia.
- Bug real encontrado: `Matrix4x4.TRS()` montava a transposta (rotação inversa);
  corrigidos os 6 sinais fora da diagonal (validado matriz × quaternion).
- Validado: `--play ... level0 0` mostra a nave branca à direita (100% dos
  vértices em NDC, mesma composição do jogo original). `--boot` segue 600 OK.
- Andaime restante: overlay `Title.png` de debug ainda desenhado no centro.

## Andaime removido (2026-10-14)

- Overlay `Title.png` central (`DrawCenteredTexture`) removido do loop; a janela
  mostra só 3D + UI real. `Title.png` segue carregado como placeholder da
  `UIRenderer` (sai na fatia da UI real).

## Luz difusa simples (2026-10-14)

- `dump_pass3d.py` extrai normais (canal 1); `Mesh.normals` preenchido.
- Shader básico: `aNormal` + `uModel`, `difuso = 0.35 + 0.65·max(dot(n,L),0)`
  com L fixo; VBO interleaved pos+normal. De bônus, `CreateBuffers` agora
  registra o cache (antes recriava VBOs todo frame).
- UI real ainda invisível por motivo conhecido: `Text.text`/`Image`/`RectTransform`
  são fields de engine e o Pass 3 não os lê (textos vazios, rects zero) —
  geometria degenerada, não bug de render. Vira fatia própria.

## Pass 3D no level_01 (2026-10-14)

- `docs/pass3d_level1.json`: 1452 malhas (152k verts), 1711 links, 3 câmeras.
- `--boot level1` instancia sem crash; erros de lógica que aparecem
  (`Player couldn't be found`, NRE `EnemyShipController`, índice `LevelManager`)
  são fluxo do jogo (level_01 espera estado vindo do menu), não do 3D.

## Áudio fatia A1 — NREs zerados, ainda silencioso (2026-10-14)

- `tools/dump_audio.py` → `docs/audio_stardust.json`: 30 clipes Vorbis/FSB5
  (só metadados + offsets; decode audível vira A2) + AudioSources por level.
- `AudioLibrary.Apply()` no `SceneDirector`: cria `AudioClip`s reais,
  liga `AudioSource.clip/volume/pitch/loop`, `playOnAwake` toca, e resolve
  fields cross-file (`MainMenu.loopClip`, `AudioManager.sounds[].clip`).
- `AudioSystem.Tick` (via `PlayerLoop.TickTriggers`) avança `time` e para
  no fim do clip; `PlayOneShot/Pause/UnPause` adicionados; `AudioMixer`
  continua no-op. `IAudioBackend` é o seam da A2.
- Bônus: `PlayerLoop` agora acorda em ordem hierárquica (pais antes dos
  filhos, como no Unity) — era a causa real do NRE do `OptionsMenu.Awake`
  (`GameManager.Instance` nulo; objeto 597 nasce antes do 604). A correção
  do STATUS antigo ("AudioSource.clip") valia só p/ a corotina do MainMenu.
- `[ERR]` agora imprime tipo + primeiro frame do stack.
- Validado: `--boot level0` 600 frames com **zero `[ERR]`**; `--headless` 12/12.

## Áudio fatia A2 — FmodBackend pronto, mudo sem a DLL (2026-10-14)

- `runtime/host/FmodBackend.cs`: `IAudioBackend` via FMOD nativo (P/Invoke
  cdecl x64): `System_Create/init(32)/createSound(OPENMEMORY)/playSound`,
  `Channel::stop/isPlaying/setVolume/setPitch/setPaused`, `Sound::setMode
  (LOOP_NORMAL/OFF)`, `System::update` por frame. Loop é por-source mas
  FMOD é por-sound: `setMode` aplicado a cada play (raro conflito, aceito).
- `IAudioBackend` estendido com `OnStop/OnPause/OnTick`; `AudioSource.
  Stop/Pause/UnPause` notificam; `AudioSystem.Tick` chama `OnTick` no fim.
  `OnTick` dá `update` + resync (fonte tocando sem canal vivo → recomeça;
  cobre init após `playOnAwake` e canais roubados). `OnPause` sem canal +
  `isPlaying` também recomeça.
- Sem `fmod.dll`: `TryInstall` falha 1x com aviso, jogo segue mudo (A1),
  nada quebra. Com a DLL oficial, `RawAudio` (A1) alimenta `createSound`
  direto (1 banco FSB5 por clipe, Vorbis headerless `0x0F` decodificado
  pelo próprio FMOD, como o AssetStudio faz).
- Para ouvir: conta gratuita em https://www.fmod.com/download → **FMOD
  Engine 2.x Windows x64** → copiar `api/core/lib/x64/fmod.dll` (~1,8 MB)
  p/ junto do `Unirecomp.Host.exe`. NÃO usar dll-files.com. EULA FMOD é
  do usuário (faixa gratuita p/ não-comercial/indie pequeno).
- Validado: build 0 erros; `--boot level0` 600 frames **zero `[ERR]`** com
  `fmod: ativo, 2 sons, 2 plays, 2 canais vivos`; `--headless` OK.
- Armadilhas reais encontradas (2026-10-14, via sonda ctypes antes do C#):
  `System_Create` exige `headerversion` (sem ele erro 20; tenta 2.04→2.00);
  `OPENMEMORY` (cópia) quebrado nesta DLL (18/28 sempre) → usar
  `OPENMEMORY_POINT` + `AllocHGlobal` por clipe (vive até `Reset()` na
  troca de cena); banco FSB5 = container c/ 1 subsom → `playSound` no
  subsom (`GetSubSound(0)`), senão erro 60.

## Sprites reais no menu (2026-10-15)

- `SpriteLibrary.Apply()` no `SceneDirector` (padrão `AudioLibrary`): resolve
  `Image.m_Sprite` cross-file (level0 fileID 2 → `sharedassets0.assets`),
  parseia Sprite 213 2018.1 (rect/offset/border/ppu/pivot/extrude + 1
  `m_RenderDataKey` GUID+int64 + `RD.texture` PPtr) e Texture2D 28
  (off/size `StreamData` → `.resS`, não `.resource`).
- `DxtDecoder.cs`: DXT1 (fmt 10/11) + DXT5 (fmt 12) p/ RGBA topo-primeiro;
  fmt 4 = cópia direta; só mip 0 (menu não precisa de mip).
- `Texture2D.cs` no shim (`width/height/textureFormat/PixelData`);
  `Sprite.texture`; `UIRenderer.ImageQuad` faz upload GL sob demanda com UV
  do `textureRect` (fallback cor chapada sem pixels).
- Validado: `--ui level0` →
  `img 'Title' sprite=Title_With_Background tex=2048x758 fmt4`,
  `img 'Image' sprite=Space tex=2048x2048 fmt10`; estrela-fundo tem 531 px
  brancos (starfield, stride 2048 OK); `--boot`/`--headless` 600 OK.
- Resta ver com olho humano: `--play level0` (upload GL só roda com janela).
- Correção pós-teste visual (2026-10-15): `Title` 2048x758 sumia porque
  NPOT + `Repeat` + `GenerateMipmap` = textura incompleta em GLES2
  (  amostra preto). `GLES2TextureLoader` agora usa `ClampToEdge` e pula
  mipmaps quando não-POT (POT mantém `Repeat`+mipmap). `Space` 2048²
  funcionava por ser POT. Caixinha branca no canto = `TextQuad`
  placeholder (texto ainda é quad chapado, fatia futura).
- Causa real do `Title` invisível (2026-10-15): ordem de desenho. O driver
  é GL 3.3 desktop (NPOT com Repeat funciona — teoria anterior errada).
  `MainMenuCanvas` tem `m_SortOrder=25`, `BackgroundCanvas` 0, mas o loop
  desenhava na ordem do arquivo (fundo por último, opaco, cobrindo tudo).
  `Canvas.sortOrder` lido no `SceneBuilder` (offset 44, validado nos 11
  canvases do level0); `RenderAll`/`DumpAll` ordenam por sortOrder.
  Bônus: `UIRenderer` era recriado por frame (re-uploada ~10 MB + spammava
  o dump); agora é instância única.
- Título de ponta-cabeça (2026-10-15): Unity armazena linhas bottom-first
  (prova: `Title.png` tem amarelo embaixo/azul em cima; bytes do `.assets`
  têm amarelo em cima/azul embaixo). `SpriteLibrary` normaliza p/ top-first
  (`FlipRowsVertical`); `ImageQuad` usa `v=r.y/th` (rect bottom-origin).

## Menu fiel — fundo atrás da nave + starfield + cores (2026-10-15)

- Ordem do frame (`SilkPlatform`): `PreFillBackground` → `RenderBackground`
  (só `BackgroundCanvas` SSC) → `SceneRenderer3D.Render` → `RenderAll` (só
  overlay). Fundo não cobre mais a nave.
- `SceneRenderer3D` restaura estado 3D (depth on, blend off) e passa
  `subColors/subRanges` do `MeshLibrary.RenderInfo(mf)` p/ `GLES2Renderer.
  RenderMesh`, que desenha por submesh (`DrawElements` com offset).
- `tools/dump_pass3d.py` extrai `ranges` + `rendererLinks {filter,go,colors}`;
  Toon `Prototype_*`: `_Color` branco é identidade → usa `_Color1` como base
  (`whiteish`). `docs/pass3d_level0.json`: 7 malhas, 7 links, 2 câmeras.
- `StarfieldRenderer.cs` (novo): ~240 `GL_POINTS` com seed fixo, ancorados no
  frustum da câmera, `PointsVert/PointsFrag` (`gl_PointSize` por distância),
  tons branco/cinza-azulado.
- Validado: build 0 erros (só NU1902/NU1903 ImageSharp pré-existentes);
  `--headless` 600 ok; `--boot level0` 600 frames zero `[ERR]`
  (fmod ativo, 2 sons); `--ui level0` 3 canvas/2 images/3 textos.
- Pendente olho humano: `--play level0` (precisa de janela; sem screenshot
  offscreen no host ainda).

## 1. Objetivo
