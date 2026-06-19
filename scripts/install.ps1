param(
  [string]$Configuration = "Release",
  [string]$STS2GameDir = "D:\Steam\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\GongdouSts2ChallengeMod\GongdouSts2ChallengeMod.csproj"
$outputDir = Join-Path $repoRoot "src\GongdouSts2ChallengeMod\bin\$Configuration\net9.0"
$modsDir = Join-Path $STS2GameDir "mods"
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet)) {
  $dotnet = "dotnet"
}

& $dotnet build $project -c $Configuration -p:STS2GameDir="$STS2GameDir"

if (-not (Test-Path -LiteralPath $modsDir)) {
  New-Item -ItemType Directory -Force -Path $modsDir | Out-Null
}

$builtDll = Join-Path $outputDir "GongdouSts2ChallengeMod.dll"
# STS2 resolves the managed assembly from the manifest id, so the DLL name
# must match Gongdou_STS2_Challenge.json's "id" value.
Copy-Item -LiteralPath $builtDll -Destination (Join-Path $modsDir "Gongdou_STS2_Challenge.dll") -Force
Copy-Item -LiteralPath $builtDll -Destination (Join-Path $modsDir "GongdouSts2ChallengeMod.dll") -Force
Copy-Item -LiteralPath (Join-Path $outputDir "mod_manifest.json") -Destination (Join-Path $modsDir "Gongdou_STS2_Challenge.json") -Force

Get-Item -LiteralPath (Join-Path $modsDir "Gongdou_STS2_Challenge.dll")
Get-Item -LiteralPath (Join-Path $modsDir "GongdouSts2ChallengeMod.dll")
Get-Item -LiteralPath (Join-Path $modsDir "Gongdou_STS2_Challenge.json")
