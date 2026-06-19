param(
  [string]$OutputDir = "",
  [int]$Keep = 1,
  [string]$CurrentPackagePath = "",
  [switch]$IncludeSubdirectories,
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ($Keep -lt 1) {
  throw "Keep must be >= 1."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $OutputDir = Join-Path $repoRoot "artifacts"
}

if (-not (Test-Path -LiteralPath $OutputDir)) {
  [pscustomobject]@{
    outputDir = $OutputDir
    kept = @()
    deleted = @()
    skipped = "Output directory does not exist."
  }
  return
}

$rootFullPath = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $OutputDir).Path).TrimEnd('\', '/')
$packageNamePattern = '^Gongdou_STS2_Mods-\d+\.\d+\.\d+-\d{8}-\d{6}\.zip$'

function Assert-UnderRoot([string]$Path) {
  $full = [System.IO.Path]::GetFullPath($Path)
  if (-not ($full.Equals($rootFullPath, [StringComparison]::OrdinalIgnoreCase) -or
            $full.StartsWith($rootFullPath + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) {
    throw "Refusing to touch file outside output directory: $full"
  }
  return $full
}

$searchOption = if ($IncludeSubdirectories) { [System.IO.SearchOption]::AllDirectories } else { [System.IO.SearchOption]::TopDirectoryOnly }
$files = [System.IO.Directory]::EnumerateFiles($rootFullPath, "Gongdou_STS2_Mods-*.zip", $searchOption) |
  ForEach-Object { Get-Item -LiteralPath $_ } |
  Where-Object { $_.Name -match $packageNamePattern } |
  Sort-Object LastWriteTimeUtc, Name -Descending

$keepPaths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
if (-not [string]::IsNullOrWhiteSpace($CurrentPackagePath)) {
  if (-not (Test-Path -LiteralPath $CurrentPackagePath)) {
    throw "CurrentPackagePath does not exist: $CurrentPackagePath"
  }

  $currentItem = Get-Item -LiteralPath $CurrentPackagePath
  if ($currentItem.Name -notmatch $packageNamePattern) {
    throw "CurrentPackagePath is not a GongDou STS2 package: $CurrentPackagePath"
  }

  [void]$keepPaths.Add((Assert-UnderRoot $currentItem.FullName))
}

foreach ($file in $files) {
  if ($keepPaths.Count -ge $Keep) {
    break
  }

  [void]$keepPaths.Add((Assert-UnderRoot $file.FullName))
}

$deleted = New-Object System.Collections.Generic.List[string]
$kept = New-Object System.Collections.Generic.List[string]
foreach ($file in $files) {
  $full = Assert-UnderRoot $file.FullName
  if ($keepPaths.Contains($full)) {
    $kept.Add($full)
    continue
  }

  if ($DryRun) {
    $deleted.Add("[dry-run] $full")
    continue
  }

  Remove-Item -LiteralPath $full -Force
  $deleted.Add($full)
}

[pscustomobject]@{
  outputDir = $rootFullPath
  keep = $Keep
  kept = @($kept)
  deleted = @($deleted)
}
