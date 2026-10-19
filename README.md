# UniRecomp Tool

> **⚠️ Archived — research record, not a working product.**
> This project was an attempt to run a *closed-source* Unity game (Mono build) on Android
> **without the Unity engine present**, by reimplementing the engine's API surface
> (`UnityEngine.*`) as a shim. It worked as a proof of concept on a small game and then
> hit a wall that cannot be climbed.
> **Read [`docs/POSTMORTEM.md`](docs/POSTMORTEM.md) first** — it explains what worked,
> what failed, and why. The parsers below are reusable; the strategy is not.

> **⚠️ Arquivado — registro de pesquisa, não é um produto funcional.**
> Este projeto foi uma tentativa de rodar um jogo Unity *de código fechado* (build Mono)
> no Android **sem a engine Unity**, reimplementando a superfície de API (`UnityEngine.*`)
> como um *shim*. Funcionou como prova de conceito num jogo pequeno e depois bateu num
> muro que não dá pra escalar.
> **Leia [`docs/POSTMORTEM.md`](docs/POSTMORTEM.md) primeiro** — explica o que funcionou,
> o que falhou e por quê. Os parsers abaixo são reaproveitáveis; a estratégia não é.

---

# 🇬🇧 English

## What this is

An experimental toolkit that reads **your own** Unity Mono game files and (attempted to)
execute them natively outside the Unity engine. It never redistributes any game: you point
it at a game you already own.

The idea was "ReXGlue for Unity Mono": parse the asset containers, decompile the game's CIL,
and run it against a reimplemented `UnityEngine` shim on a new platform (Android).

## TL;DR — the lesson

**You cannot reimplement the Unity engine.** Asset and scene *reading* is a bounded,
solvable problem (and this repo solves a good chunk of it). The **engine** is not: it is an
unbounded API and behavioural surface, and every game pulls on a new part of it. Without the
real engine as an oracle, you enter an endless "change → test → fail" loop.

This repo is the honest record of that discovery, plus the reusable pieces.

## What actually works (and is reusable)

| Piece | What it does | Evidence |
|---|---|---|
| `tools/sfindex.py` | Parses Unity `SerializedFile` (v17 / 2018.1) containers and lists every object | **11/11 files parsed 100% matching UnityPy** |
| `runtime/shim/CoreModule/Serialization/SerializedFile.cs` | C# port of the same container reader | validated against UnityPy oracle |
| `tools/dump_api.py` | Builds the CIL checklist: exactly which `UnityEngine.*` types/members a game's `Assembly-CSharp.dll` uses (via `dnfile`) | 120 types / 354 members on the test game |
| `tools/uidump/` | Extracts engine **field layouts** from Unity's managed DLLs using `System.Reflection.Metadata` (no Unity needed) | 39 scripts / 19 nested / 502 enums |
| `runtime/host/FieldReader.cs` | Reads `MonoBehaviour` binary blobs by merging engine + game layouts, with a size assert per object | 4 levels, 0 errors |
| `tools/mbscript_map.py` | Maps stripped `MonoScript` references back to (GameObject, script) | **2046/2046 = 100%** |
| `tools/analyze.py` | Profiles a build: Mono vs IL2CPP? native plugins? `DllImport`? network? shaders? | used to screen candidate games |
| `runtime/shim/` | A partial shim of Unity modules (Core, Audio, UI, TextMeshPro, Physics, …) | proof of concept only |

## What the shim prototype achieved (demo game: *Stardust Raiders*)

A small open Unity 2018.1 Mono game, used as the MVP target:

- Opens a 1280×720 OpenGL window and runs the game's **real** `Assembly-CSharp.dll` via an
  `AssemblyLoadContext`, resolving `UnityEngine*` to the shim.
- Loads the `main_menu` scene (163 roots) from the game's serialized files.
- Renders real sprites (custom DXT1/DXT5 decoder), toon-shaded 3D meshes with a simple
  diffuse light, a procedural starfield, and UI in the correct `Canvas.sortOrder`.
- Plays the menu music through a native FMOD backend.
- Headless mode runs 600 frames of the real player loop with **zero errors**.

That is a genuine result. It is also the ceiling of the approach — see the post-mortem.

## Why it was abandoned — the three walls

Measured on a real, larger target (*Steven Universe: Save the Light*, PC/Mono):

1. **Build wall.** Importing a commercial Unity project needs 8–16 GB RAM. The author's PC
   has 4 GB. Batch mode does not help; asset import is the wall. → forced to build in the
   cloud (cost, third party).
2. **Code wall.** 4,393 types / 28,507 methods to decompile and reconcile, **727 Steamworks
   `P/Invoke` calls** to stub out, and **Rewired** (paid input middleware, platform-specific)
   to replace. → months, not weekends.
3. **Device wall.** A 3D game with custom shaders on a 4 GB MediaTek/Mali phone. → a coin
   flip at best.

The wall was never skill. It was the floor of the hardware and the size of the surface.

## Using the tools (if you want the reusable half)

Requirements: Python 3.10+, `pip install dnfile` (and `UnityPy` only if you want to compare),
.NET 8 SDK for the C# parts.

```bash
# Profile a game build (Mono? plugins? DllImport? shaders?)
python tools/analyze.py --project "<UnityProject>" --build "<Game>_Data" --out docs/profile.json

# What Unity API does the game's code actually use?
python tools/dump_api.py --dll "<Game>_Data/Managed/Assembly-CSharp.dll"

# Parse the game's serialized files and dump the object index
python tools/sfindex.py --input "<Game>_Data/level0"
```

See [`docs/PORTABILITY.md`](docs/PORTABILITY.md) for the full per-game protocol, and
[`docs/ROADMAP_Android.md`](docs/ROADMAP_Android.md) for the original Tier 0 → APK plan.

## Repository layout

```
tools/        Python + C# analysis tools (the reusable core)
runtime/host/ .NET 8 host: loads the game's assembly, drives the shim
runtime/shim/ partial UnityEngine reimplementation (proof of concept)
runtime/proto/ early Python prototypes
runtime/test/ shader/asset unit tests
docs/         findings, protocol, roadmap, and the post-mortem
STATUS.md     detailed engineering log (Portuguese)
```

## Legal

**No game files are distributed here.** No game code, assets, binaries, engine DLLs, or
third-party middleware. You must supply your own copy of any game you experiment on.
The tools are designed for interoperability research on software you own.

## License

MIT — see [`LICENSE`](LICENSE). Covers only the original code and docs in this repo.

## Credits

- Author: **EdDev**
- Demo game: *Stardust Raiders* by **Rubén Crispín** — a free Unity 2018.1 prototype whose
  project files are publicly downloadable: https://ruben-crispin.itch.io/stardust-raiders
  (art by Amaya Mateos, music by Markel Badallo). Used here only as the MVP test target;
  **not** redistributed in this repository.
- Case study: *Steven Universe: Save the Light* (measured only; **not** included).

---

# 🇧🇷 Português

## O que é isto

Um kit de ferramentas experimental que lê os arquivos de um jogo Unity Mono **seu** e
(tentou) executá-los nativamente fora da engine Unity. Ele nunca redistribui jogo nenhum:
você aponta pra um jogo que já possui.

A ideia era um "ReXGlue para Unity Mono": ler os containers de assets, descompilar o CIL do
jogo e rodá-lo contra um *shim* de `UnityEngine` reimplementado numa plataforma nova (Android).

## Resumo — a lição

**Não dá pra reimplementar a engine Unity.** Ler assets e cenas é um problema *finito* e
resolvível (e este repo resolve boa parte). A **engine** não é: é uma superfície de API e de
comportamento sem fim, e cada jogo puxa uma parte nova dela. Sem a engine real como oráculo,
você entra num ciclo infinito de "muda → testa → falha".

Este repositório é o registro honesto dessa descoberta, mais as peças reaproveitáveis.

## O que realmente funciona (e é reaproveitável)

| Peça | O que faz | Evidência |
|---|---|---|
| `tools/sfindex.py` | Lê containers `SerializedFile` do Unity (v17 / 2018.1) e lista cada objeto | **11/11 arquivos, 100% igual ao UnityPy** |
| `runtime/shim/CoreModule/Serialization/SerializedFile.cs` | Port C# do mesmo leitor | validado contra o oráculo UnityPy |
| `tools/dump_api.py` | Monta a checklist CIL: exatamente quais tipos/membros `UnityEngine.*` o `Assembly-CSharp.dll` usa (via `dnfile`) | 120 tipos / 354 membros no jogo de teste |
| `tools/uidump/` | Extrai os **layouts de fields** da engine a partir das DLLs gerenciadas da Unity, com `System.Reflection.Metadata` (sem Unity) | 39 scripts / 19 nested / 502 enums |
| `runtime/host/FieldReader.cs` | Lê blobs de `MonoBehaviour` fundindo layouts engine + jogo, com assert de tamanho por objeto | 4 levels, 0 erros |
| `tools/mbscript_map.py` | Mapeia `MonoScript` *stripped* de volta pra (GameObject, script) | **2046/2046 = 100%** |
| `tools/analyze.py` | Perfila um build: Mono ou IL2CPP? plugins nativos? `DllImport`? rede? shaders? | usado pra triagem de jogos |
| `runtime/shim/` | Shim parcial dos módulos Unity (Core, Audio, UI, TextMeshPro, Physics, …) | só prova de conceito |

## O que o protótipo do shim alcançou (jogo demo: *Stardust Raiders*)

Um jogo Unity 2018.1 Mono pequeno e aberto, usado como alvo do MVP:

- Abre uma janela OpenGL 1280×720 e roda o `Assembly-CSharp.dll` **real** do jogo via um
  `AssemblyLoadContext`, resolvendo `UnityEngine*` pro shim.
- Carrega a cena `main_menu` (163 raízes) dos arquivos serializados do jogo.
- Renderiza sprites reais (decoder DXT1/DXT5 próprio), malhas 3D toon com luz difusa simples,
  um starfield procedural, e UI na ordem correta de `Canvas.sortOrder`.
- Toca a música do menu por um backend FMOD nativo.
- O modo headless roda 600 frames do player loop real com **zero erros**.

Isso é um resultado de verdade. E é também o teto da abordagem — ver o post-mortem.

## Por que foi abandonado — os três muros

Medidos num alvo real e maior (*Steven Universe: Save the Light*, PC/Mono):

1. **Muro de build.** Importar um projeto Unity comercial exige 8–16 GB de RAM. O PC do autor
   tem 4 GB. Batch mode não resolve; o import de assets é o muro. → obriga build na nuvem
   (custo, terceiros).
2. **Muro do código.** 4.393 tipos / 28.507 métodos pra descompilar e reconciliar, **727
   chamadas `P/Invoke` do Steamworks** pra neutralizar, e **Rewired** (middleware de input
   pago, específico de plataforma) pra trocar. → meses, não fins de semana.
3. **Muro do aparelho.** Um jogo 3D com shaders customizados num celular 4 GB MediaTek/Mali.
   → moeda no ar, na melhor das hipóteses.

O muro nunca foi habilidade. Foi o piso do hardware e o tamanho da superfície.

## Usando as ferramentas (a metade reaproveitável)

Requisitos: Python 3.10+, `pip install dnfile` (e `UnityPy` só se quiser comparar), .NET 8 SDK
para as partes em C#.

```bash
# Perfil de um build (Mono? plugins? DllImport? shaders?)
python tools/analyze.py --project "<ProjetoUnity>" --build "<Jogo>_Data" --out docs/profile.json

# Que API da Unity o código do jogo realmente usa?
python tools/dump_api.py --dll "<Jogo>_Data/Managed/Assembly-CSharp.dll"

# Lê os arquivos serializados do jogo e lista o índice de objetos
python tools/sfindex.py --input "<Jogo>_Data/level0"
```

O protocolo completo por jogo está em [`docs/PORTABILITY.md`](docs/PORTABILITY.md), e o plano
original Tier 0 → APK em [`docs/ROADMAP_Android.md`](docs/ROADMAP_Android.md).

## Estrutura do repositório

```
tools/        Ferramentas de análise em Python + C# (o núcleo reaproveitável)
runtime/host/ Host .NET 8: carrega o assembly do jogo, dirige o shim
runtime/shim/ Reimplementação parcial do UnityEngine (prova de conceito)
runtime/proto/ Protótipos iniciais em Python
runtime/test/ Testes de unidade (shader/assets)
docs/         Achados, protocolo, roadmap e o post-mortem
STATUS.md     Log técnico detalhado (em português)
```

## Legal

**Nenhum arquivo de jogo é distribuído aqui.** Sem código, assets, binários, DLLs da engine ou
middleware de terceiros. Você precisa fornecer sua própria cópia de qualquer jogo que queira
testar. As ferramentas são para pesquisa de interoperabilidade em software que você possui.

## Licença

MIT — ver [`LICENSE`](LICENSE). Cobre apenas o código e a documentação originais deste repo.

## Créditos

- Autor: **EdDev**
- Jogo demo: *Stardust Raiders*, de **Rubén Crispín** — um protótipo gratuito de Unity 2018.1
  cujos arquivos de projeto são públicos: https://ruben-crispin.itch.io/stardust-raiders
  (arte de Amaya Mateos, música de Markel Badallo). Usado aqui apenas como alvo de teste do
  MVP; **não** é redistribuído neste repositório.
- Estudo de caso: *Steven Universe: Save the Light* (apenas medido; **não** incluído).
