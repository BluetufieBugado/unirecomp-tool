# Portabilidade — testar a ferramenta com outros jogos (futuro)

> Registrado em 2026-10-13 (pergunta: "funciona com qualquer jogo 2018.1 Mono,
> como um ReXGlue, com port para Android?"). Este arquivo é o protocolo para
> quando formos testar o jogo 2+: o que é automático, o que é trabalho por
> jogo, e o que bloqueia de verdade. Contexto geral em `STATUS.md`.

## Modelo (espírito ReXGlue)

ReXGlue = Xbox 360 PPC→C++ com manifests por jogo (`daytona_manifest.toml`).
Aqui o paralelo é o **modelo, não a técnica**: a ferramenta lê os arquivos
**originais do usuário** e gera executável nativo para outra plataforma, sem
redistribuir o jogo. Nossos "manifests por jogo" já existem: `profile_*` +
`apicheck_*` + `prio_*` + `layouts_*` + `mbmap_*` em `docs/`.

## O que generaliza de graça (mesma família Unity Mono)

- Container `SerializedFile` da versão (v17 = 2018.1.x): `sfindex.py` /
  `SerializedFile.cs` servem para todos os jogos da família.
- Código do jogo (CIL Mono) roda no runtime embarcado sem modificação.
- `IPlatform` (gancho Android) + regra GLES-desde-o-dia-1.

## Protocolo para um jogo novo

1. `python tools/analyze.py --project <P> --build <B> --out docs/profile_<jogo>.json`
   → veredito (Mono? plugins? rede? DllImport? cenas? shaders?).
2. `python tools/dump_api.py --dll <B>.../Managed/Assembly-CSharp.dll`
   → checklist CIL real (tipos/membros Unity referenciados).
3. `python tools/prio.py` → gap ordenado em tiers = a lista exata do que
   falta implementar no runtime.
4. `dump_scene.py` + `mbscript_map.py` + `sfindex.py` → oráculos da cena.
5. Layouts de fields: hoje `genlayouts.py` lê o **fonte .cs** (temos o do
   Stardust). Para jogos sem fonte, derivar do próprio
   `Assembly-CSharp.dll` (builds Mono têm metadados completos de fields) —
   **trabalho ainda não feito, pré-requisito para "qualquer jogo"**.
6. `--load-scene` no jogo novo: índice + hierarquia + mundo + fields.
7. Boot real + paridade de comportamento vs jogo original no Windows.
8. Só então: input/toque, GLES, empacotamento (ver `ROADMAP_Android.md` Fase 5).

## Critérios de aceite (mesmo perfil = Mono, mesma família, sem nativos)

- `analyze`: `Mono=True`, 0 plugins fora de `Managed`, 0 `DllImport`.
- `dump_api`: 100% dos tipos com casca ou impl.; membros Tier 0–1 reais.
- `--load-scene`: exit 0 em todos os levels (índice, GOs, mundo, fields).
- Boot: `main_menu` (ou equivalente) instancia e roda 600 frames sem crash.

## Bloqueadores duros (fora do perfil)

- Plugins nativos (`DllImport` de `.dll`/`.so` sem fonte para reimplementar).
- Builds IL2CPP (outro formato de código e de assets).
- Rede proprietária, vídeo, DRM/anti-tamper.
- Física pesada de verdade (PhysX): Stardust foi escolhido por quase não usar
  `Rigidbody`; outro jogo pode exigir plugar um motor de física real.
- Outra versão da Unity: cada uma muda o `SerializedFile` (matriz de formatos).
- Shaders custom em volume (Stardust tem 1).

## Candidatos futuros (a avaliar)

- Qualquer jogo Unity Mono 2018.1.x, DRM-free, sem plugins nativos.
- Anotar aqui: nome, versão (`ProjectVersion.txt`), perfil `analyze`,
  tamanho do gap (`prio`), resultado do `--load-scene`.
