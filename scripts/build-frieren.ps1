param(
  [string]$Configuration = "Release",
  [string]$STS2GameDir = "D:\Steam\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\GongdouSts2FrierenMod\GongdouSts2FrierenMod.csproj"
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet)) {
  $dotnet = "dotnet"
}

& $dotnet build $project -c $Configuration -p:STS2GameDir="$STS2GameDir"
