param(
  [string]$Configuration = "Release",
  [string]$STS2GameDir = "D:\Steam\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

& (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration -STS2GameDir $STS2GameDir
& (Join-Path $PSScriptRoot "build-frieren.ps1") -Configuration $Configuration -STS2GameDir $STS2GameDir

$enumJson = Join-Path $repoRoot "artifacts\difficulty1-cultist-enumeration.json"
python (Join-Path $PSScriptRoot "enumerate-difficulty1-cultist.py") --summary-only --json $enumJson

$enum = Get-Content -LiteralPath $enumJson -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$enum.caseCount -ne 162) { throw "Unexpected difficulty1 caseCount: $($enum.caseCount)" }
if ([int]$enum.stableCount -ne 0) { throw "Unexpected difficulty1 stableCount: $($enum.stableCount)" }

$challengeOut = Join-Path $repoRoot "src\GongdouSts2ChallengeMod\bin\$Configuration\net9.0"
$frierenOut = Join-Path $repoRoot "src\GongdouSts2FrierenMod\bin\$Configuration\net9.0"
$required = @(
  (Join-Path $challengeOut "GongdouSts2ChallengeMod.dll"),
  (Join-Path $challengeOut "mod_manifest.json"),
  (Join-Path $frierenOut "GongdouSts2FrierenMod.dll"),
  (Join-Path $frierenOut "mod_manifest.json")
)

foreach ($path in $required) {
  if (-not (Test-Path -LiteralPath $path)) {
    throw "Required build output missing: $path"
  }
}

$challengeManifest = Get-Content -LiteralPath (Join-Path $challengeOut "mod_manifest.json") -Raw | ConvertFrom-Json
$challengeSource = Get-Content -LiteralPath (Join-Path $repoRoot "src\GongdouSts2ChallengeMod\GongdouSts2ChallengeMod.cs") -Raw
if ($challengeSource -notmatch 'public const string Version = "([^"]+)";') {
  throw "Failed to read challenge source version."
}
$challengeSourceVersion = $Matches[1]
if ($challengeManifest.version -ne $challengeSourceVersion) {
  throw "Challenge manifest version mismatch: $($challengeManifest.version)"
}

[pscustomobject]@{
  challengeVersion = $challengeManifest.version
  challengeSourceVersion = $challengeSourceVersion
  difficulty1CaseCount = $enum.caseCount
  difficulty1ViableCount = $enum.viableCount
  difficulty1StableCount = $enum.stableCount
  challengeDll = Join-Path $challengeOut "GongdouSts2ChallengeMod.dll"
  frierenDll = Join-Path $frierenOut "GongdouSts2FrierenMod.dll"
}
