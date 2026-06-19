param(
    [string]$CodexSkillsDir = "$env:USERPROFILE/.codex/skills"
)

$ErrorActionPreference = "Stop"

$packageRoot = Split-Path -Parent $PSScriptRoot
$src = Join-Path $PSScriptRoot "skills"

if (-not (Test-Path $src)) {
    throw "skills directory not found: $src"
}

if (-not (Test-Path $CodexSkillsDir)) {
    New-Item -ItemType Directory -Path $CodexSkillsDir -Force | Out-Null
}

Get-ChildItem -Path $src -Directory | ForEach-Object {
    $dest = Join-Path $CodexSkillsDir $_.Name
    Copy-Item -Path $_.FullName -Destination $dest -Recurse -Force
    Write-Host "Installed skill: $($_.Name)"
}

Write-Host "Done. Restart Codex or reload skills if the environment requires it."
