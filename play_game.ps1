# play_game.ps1 — avaliação plug-and-play de um jogo Unity Mono (5.x–2018.x).
#
# Uso (PowerShell):
#   .\play_game.ps1 <caminho_para_a_pasta_Data> [cena] [fps]
#
# Exemplo (Stardust):
#   .\play_game.ps1 "Stardust_Raiders_1.0.4_Windows/Stardust Raiders 1.0.4_Data" level0 0

param(
    [Parameter(Mandatory=$true)]
    [string]$DataDir,

    [string]$Scene = "level0",

    [int]$Fps = 0
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$hostDir  = Join-Path $scriptDir "runtime/host"
$hostProj = Join-Path $hostDir "Unirecomp.Host.csproj"

$managedDir = Join-Path $DataDir "Managed"
$gameDll    = Join-Path $managedDir "Assembly-CSharp.dll"

if (-not (Test-Path $gameDll)) {
    Write-Error "Assembly-CSharp.dll não encontrado em: $gameDll"
    Write-Error "Verifique se <pasta_Data>/Managed/Assembly-CSharp.dll existe."
    exit 2
}

Write-Host "[play_game] dados: $DataDir"
Write-Host "[play_game] Assembly-CSharp.dll: $gameDll"

$hostDll = Join-Path $hostDir "bin/Debug/net8.0/Unirecomp.Host.dll"
if (-not (Test-Path $hostDll)) {
    Write-Host "[play_game] compilando o host..."
    Push-Location $hostDir
    dotnet build $hostProj -c Debug -v q
    Pop-Location
}

Write-Host "[play_game] modo janela: cena=$Scene fps=$Fps"
Push-Location $hostDir
dotnet run --project $hostProj -- --play $DataDir $Scene $Fps
Pop-Location
