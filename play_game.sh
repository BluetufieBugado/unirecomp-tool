#!/usr/bin/env bash
#
# play_game.sh — avaliação plug-and-play de um jogo Unity Mono (5.x–2018.x).
#
# Uso:
#   ./play_game.sh <caminho_para_a_pasta_Data> [cena] [fps]
#
# Exemplo (Stardust):
#   ./play_game.sh "Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data" level0 0
#
# O script:
#   1. localiza o Assembly-CSharp.dll da build,
#   2. compila o host (runtime/host) se necessário,
#   3. abre janela (ou headless) rodando o jogo escolhido.
#
set -euo pipefail

DATA_DIR="${1:?Uso: $0 <pasta_Data_do_jogo> [cena] [fps]}"
SCENE="${2:-level0}"
FPS="${3:-0}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOST_DIR="$SCRIPT_DIR/runtime/host"
HOST_PROJ="$HOST_DIR/Unirecomp.Host.csproj"

managed_dir="$DATA_DIR/Managed"
game_dll="$managed_dir/Assembly-CSharp.dll"

if [ ! -f "$game_dll" ]; then
  echo "ERROR: Assembly-CSharp.dll não encontrado em: $game_dll" >&2
  echo "       verifique se <pasta_Data>/Managed/Assembly-CSharp.dll existe." >&2
  exit 2
fi

echo "[play_game] dados: $DATA_DIR"
echo "[play_game] Assembly-CSharp.dll: $game_dll"

# Compila o host se o binário não existir (ou se o projeto mudou).
host_dll="$HOST_DIR/bin/Debug/net8.0/Unirecomp.Host.dll"
if [ ! -f "$host_dll" ]; then
  echo "[play_game] compilando o host..."
  (cd "$HOST_DIR" && dotnet build "$HOST_PROJ" -c Debug -v q)
fi

case "$SCENE" in
  --headless)
    echo "[play_game] modo headless (600 frames)"
    (cd "$HOST_DIR" && dotnet run --project "$HOST_PROJ" -- --headless)
    ;;
  *)
    echo "[play_game] modo janela: cena=$SCENE fps=$FPS"
    (cd "$HOST_DIR" && dotnet run --project "$HOST_PROJ" -- --play "$DATA_DIR" "$SCENE" "$FPS")
    ;;
esac
