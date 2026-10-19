# Post-mortem: why UniRecomp Tool stopped

> **English + Português** — the English section comes first, the Portuguese version follows.
> **Inglês + Português** — a seção em inglês vem primeiro, a versão em português depois.
>
> This document is the most valuable part of the repository. It is the *why*.
> Este documento é a parte mais valiosa do repositório. É o *porquê*.

---

# 🇬🇧 English

## TL;DR

I set out to run a closed-source Unity game on Android **without the Unity engine**, by
reimplementing `UnityEngine` as a shim. The reading half worked — well, even. The engine half
is unbounded, and that is the wall. **Asset/scene parsing is finite; the engine is not.**
The project stopped when a realistic target (*Save the Light*) showed that the remaining work
was measured in months and the payoff (running on a 4 GB Mali phone) was a coin flip.

**The one-line lesson:** *the engine is the infinite surface. You can read a Unity game;
you cannot reimplement Unity.*

## 1. The goal

Take a game you own (a Unity **Mono** build) and get it running on a platform the developer
never shipped: Android. Not by emulation, not by a compatibility layer — by executing the
game's own CIL against a reimplementation of the engine API. Nothing redistributed: the user
supplies the game files.

The model was **ReXGlue** (static recompilation of Xbox 360 games into native C++), adapted to
the Unity Mono world: read the game's containers, read its code, and provide the engine.

## 2. The approach

1. **Screen the game.** `tools/analyze.py` decides whether a build is even in-scope: is it
   Mono (not IL2CPP)? any native plugins (`DllImport`)? network? custom shaders?
2. **Get the API checklist.** `tools/dump_api.py` reads the game's `Assembly-CSharp.dll` and
   lists every `UnityEngine.*` type and member it touches. That list *is* the work order.
3. **Read the assets.** `tools/sfindex.py` (+ the C# port) parses the `SerializedFile`
   containers. `mbscript_map.py` maps scripts back to objects. `layouts_*` describe how to
   read each `MonoBehaviour`'s fields.
4. **Load the game's code.** A .NET host loads the game's real `Assembly-CSharp.dll` in an
   `AssemblyLoadContext` and resolves `UnityEngine*` to the shim.
5. **Build the engine up, tier by tier**, until the scene renders and plays.

## 3. What worked

The first four steps worked, and the fifth worked far enough to be convincing on a small game
(*Stardust Raiders* — a free Unity 2018.1.5f1 prototype by Rubén Crispín — Mono, 55 scripts,
zero native plugins):

- **Container reader validated at 100%** against UnityPy across 11 files.
- **`MonoScript` map at 100%** (2046/2046).
- **Field layouts at 100%** for the 53 scripts the game uses, plus 39 engine-side layouts
  extracted straight from Unity's managed DLLs (no Unity installed).
- **A running window**: 1280×720 OpenGL, the game's real assembly loaded with zero binding
  errors, `main_menu` (163 roots) instantiated, sprites decoded (custom DXT), toon meshes lit,
  a starfield, UI sorted by `Canvas.sortOrder`, menu music via FMOD.
- **Headless boot**: 600 frames of the real player loop, zero errors.

For a small, uniform game, the shim approach *demonstrably works*. That is the interesting
part, and it is real.

## 4. Where it broke

Every step up the tiers cost more than the last, because the game kept reaching for engine
behaviour the shim did not have — and *there is no oracle* telling you what the right
behaviour is. You find out by testing, and the failures are often emergent and subtle.

- Text rendering (TextMeshPro) required reconstructing a serialization layout whose exact
  byte count had to match the game's blob (a 72-byte under-read stalled the whole scene).
- Drawing order, texture orientation (Unity stores rows bottom-first), NPOT textures,
  mipmaps, canvas sorting — each one a small archaeology project against a closed oracle.

The consequence: progress is real but the surface never ends. Each new game, each new
feature, pulls on a new region of an engine whose specification is a multi-million-line
closed-source codebase.

## 5. The measurement that ended it

I profiled a realistic, larger target — **Steven Universe: Save the Light** (PC build, Mono,
Unity 2018.1.x) — to see what "a real game" would cost:

| Measurement | Value |
|---|---|
| Build type | Mono (survivable) |
| `Assembly-CSharp.dll` | 5.2 MB — **4,393 types, 28,507 methods, 29,816 fields** |
| Referenced assemblies | 38 |
| `P/Invoke` in *game* code | **0** (good: game logic is pure managed) |
| `P/Invoke` in first-pass assembly | **727** — all Steamworks (`steam_api`, `sdkencryptedappticket`) |
| Paid/platform middleware | **Rewired** (86 uses; ships a **Windows-only** lib here — Android needs a paid licence or a rewrite) |
| Other middleware | Dialogue System, PlayMaker, DOTween Pro, ProBuilder, Decal, A* Pathfinding |
| Console leftovers | Sony PS4 assemblies present, but unreferenced (ignorable) |

For scale: the MVP game had **55 scripts**. This one had **4,393 types** (~80×).

## 6. The three walls

1. **Build wall.** Importing a commercial Unity project needs 8–16 GB RAM (the `Library` alone
   explodes). My PC has 4 GB. Batch mode does not help — asset import is the wall. Building
   would have to happen on someone else's machine or a paid VM.
2. **Code wall.** ~28,500 methods to decompile and reconcile, 727 Steam calls to stub, and a
   paid input plugin to replace. Realistically **months**, with an uncertain end.
3. **Device wall.** A 3D game with custom shaders on a 4 GB MediaTek/Mali phone. Best case, a
   coin flip.

None of these is a skill wall. They are the floor of the hardware and the size of the surface.

## 7. Conclusion

- **Reading a Unity game is a solved, reusable skill.** The parsers in `tools/` and
  `runtime/shim/*/Serialization/` genuinely work and are validated.
- **Reimplementing the Unity engine is not a project — it is the project.** It has no end,
  because the specification *is* a closed engine and the games sample it arbitrarily.
- **Choose the oracle first.** If you must reimplement an engine, pick one whose source is
  open (FNA/XNA, Godot) so you can *read* the answer instead of guessing it.

## 8. What I would do instead

- **Emulation/compatibility layer** (Wine/Box64, Winlator-style): no engine reimplementation,
  but heavy on weak hardware (this was ruled out here for that reason).
- **Reconstruct in the real engine**: import the assets, decompile the code, rebuild in Unity.
  Feasible, but the true cost is the code volume, the middleware, and a 16 GB build machine —
  exactly walls 1 and 2 above.
- **Pick a smaller target**: an open-engine game (Godot/FNA/XNA) or a Unity Mono game with no
  native plugins and a tiny API surface. The `tools/` here are a good starting point.

The point of publishing this is not the dead end. It is the map of the dead end.

---

# 🇧🇷 Português

## Resumo

Eu queria rodar um jogo Unity de código fechado no Android **sem a engine Unity**,
reimplementando `UnityEngine` como um *shim*. A metade da *leitura* funcionou — e bem. A
metade da *engine* é infinita, e é aí que está o muro. **Ler assets/cenas é finito; a engine
não é.** O projeto parou quando um alvo realista (*Save the Light*) mostrou que o trabalho
restante era de meses e o resultado (rodar num celular 4 GB Mali) era moeda no ar.

**A lição em uma linha:** *a engine é a superfície infinita. Dá pra ler um jogo Unity; não dá
pra reimplementar a Unity.*

## 1. O objetivo

Pegar um jogo que você possui (build Unity **Mono**) e rodá-lo numa plataforma que a
desenvolvedora nunca lançou: Android. Não por emulação, não por camada de compatibilidade —
executando o próprio CIL do jogo contra uma reimplementação da API da engine. Nada
redistribuído: o usuário fornece os arquivos do jogo.

O modelo era o **ReXGlue** (recompilação estática de jogos de Xbox 360 pra C++ nativo),
adaptado pro mundo Unity Mono: ler os containers do jogo, ler o código dele, e fornecer a
engine.

## 2. A abordagem

1. **Triagem.** `tools/analyze.py` decide se um build está no escopo: é Mono (não IL2CPP)?
   tem plugins nativos (`DllImport`)? rede? shaders customizados?
2. **A checklist de API.** `tools/dump_api.py` lê o `Assembly-CSharp.dll` do jogo e lista
   cada tipo/membro `UnityEngine.*` que ele toca. Essa lista *é* a ordem de serviço.
3. **Ler os assets.** `tools/sfindex.py` (+ o port C#) lê os containers `SerializedFile`.
   `mbscript_map.py` mapeia scripts de volta pros objetos. Os `layouts_*` descrevem como ler
   os fields de cada `MonoBehaviour`.
4. **Carregar o código do jogo.** Um host .NET carrega o `Assembly-CSharp.dll` **real** do
   jogo num `AssemblyLoadContext` e resolve `UnityEngine*` pro shim.
5. **Construir a engine por tiers**, até a cena renderizar e jogar.

## 3. O que funcionou

Os quatro primeiros passos funcionaram, e o quinto foi longe o bastante pra ser convincente num
jogo pequeno (*Stardust Raiders* — um protótipo gratuito de Unity 2018.1.5f1 de Rubén Crispín —
Mono, 55 scripts, zero plugins nativos):

- **Leitor de container validado a 100%** contra o UnityPy em 11 arquivos.
- **Mapa de `MonoScript` a 100%** (2046/2046).
- **Layouts de fields a 100%** pros 53 scripts que o jogo usa, mais 39 layouts do lado da
  engine extraídos direto das DLLs gerenciadas da Unity (sem Unity instalada).
- **Uma janela rodando**: OpenGL 1280×720, o assembly real do jogo carregado com zero erros de
  binding, `main_menu` (163 raízes) instanciado, sprites decodificados (DXT próprio), malhas
  toon iluminadas, starfield, UI ordenada por `Canvas.sortOrder`, música do menu via FMOD.
- **Boot headless**: 600 frames do player loop real, zero erros.

Pra um jogo pequeno e uniforme, a abordagem do shim **demonstravelmente funciona**. Essa é a
parte interessante, e é de verdade.

## 4. Onde quebrou

Cada degrau acima custava mais que o anterior, porque o jogo puxava comportamento de engine que
o shim não tinha — e **não existe oráculo** dizendo qual é o comportamento certo. Você
descobre testando, e as falhas costumam ser emergentes e sutis.

- Renderização de texto (TextMeshPro) exigiu reconstruir um layout de serialização cuja
  contagem exata de bytes tinha que bater com o blob do jogo (um sub-consumo de 72 bytes
  travou a cena inteira).
- Ordem de desenho, orientação de textura (a Unity guarda as linhas de baixo pra cima),
  texturas NPOT, mipmaps, ordenação de canvas — cada um um pequeno projeto de arqueologia
  contra um oráculo fechado.

A consequência: o progresso é real, mas a superfície nunca acaba. Cada jogo novo, cada recurso
novo, puxa uma região nova de uma engine cuja especificação é um codebase fechado de milhões
de linhas.

## 5. A medição que encerrou a conversa

Perfilei um alvo realista e maior — **Steven Universe: Save the Light** (build PC, Mono, Unity
2018.1.x) — pra ver quanto custaria "um jogo de verdade":

| Medição | Valor |
|---|---|
| Tipo de build | Mono (sobrevivível) |
| `Assembly-CSharp.dll` | 5,2 MB — **4.393 tipos, 28.507 métodos, 29.816 campos** |
| Assemblies referenciados | 38 |
| `P/Invoke` no código *do jogo* | **0** (bom: a lógica é C# puro gerenciado) |
| `P/Invoke` no assembly first-pass | **727** — todas Steamworks (`steam_api`, `sdkencryptedappticket`) |
| Middleware pago/de plataforma | **Rewired** (86 usos; aqui só traz a lib de **Windows** — Android exige licença paga ou reescrita) |
| Outros middlewares | Dialogue System, PlayMaker, DOTween Pro, ProBuilder, Decal, A* Pathfinding |
| Restos de console | Assemblies do PS4 (Sony) presentes, mas não referenciados (ignoráveis) |

Pra escala: o jogo do MVP tinha **55 scripts**. Este tinha **4.393 tipos** (~80×).

## 6. Os três muros

1. **Muro de build.** Importar um projeto Unity comercial exige 8–16 GB de RAM (só a `Library`
   explode). Meu PC tem 4 GB. Batch mode não resolve — o import de assets é o muro. O build
   teria que acontecer na máquina de outra pessoa ou numa VM paga.
2. **Muro do código.** ~28.500 métodos pra descompilar e reconciliar, 727 chamadas de Steam
   pra neutralizar, e um plugin de input pago pra trocar. Realisticamente **meses**, com fim
   incerto.
3. **Muro do aparelho.** Um jogo 3D com shaders customizados num celular 4 GB MediaTek/Mali.
   Na melhor das hipóteses, moeda no ar.

Nenhum desses é um muro de habilidade. São o piso do hardware e o tamanho da superfície.

## 7. Conclusão

- **Ler um jogo Unity é uma habilidade resolvida e reaproveitável.** Os parsers em `tools/` e
  `runtime/shim/*/Serialization/` funcionam de verdade e são validados.
- **Reimplementar a engine Unity não é um projeto — é *o* projeto.** Não tem fim, porque a
  especificação *é* uma engine fechada e os jogos a amostram de forma arbitrária.
- **Escolha o oráculo primeiro.** Se você *precisa* reimplementar uma engine, escolha uma de
  código aberto (FNA/XNA, Godot) pra poder **ler** a resposta em vez de adivinhar.

## 8. O que eu faria no lugar

- **Emulação/camada de compatibilidade** (Wine/Box64, estilo Winlator): sem reimplementar
  engine, mas pesado pra hardware fraco (foi descartado aqui por esse motivo).
- **Reconstruir na engine real**: importar os assets, descompilar o código, rebuildar na Unity.
  Viável, mas o custo real é o volume de código, os middlewares e uma máquina de build com
  16 GB — exatamente os muros 1 e 2 acima.
- **Escolher um alvo menor**: um jogo de engine aberta (Godot/FNA/XNA) ou um Unity Mono sem
  plugins nativos e com superfície de API pequena. As `tools/` daqui são um bom ponto de
  partida.

O ponto de publicar isto não é o beco sem saída. É o mapa do beco sem saída.
