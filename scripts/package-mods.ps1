param(
  [string]$Configuration = "Release",
  [string]$STS2GameDir = "D:\Steam\steamapps\common\Slay the Spire 2",
  [string]$OutputDir = "",
  [int]$LocalPackagesToKeep = 1,
  [int]$FrierenAssetMaxDimension = 512,
  [int]$FrierenAssetColors = 256,
  [switch]$DisableFrierenAssetOptimization,
  [switch]$SkipLocalPackageCleanup
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $OutputDir = Join-Path $repoRoot "artifacts"
}
if (-not (Test-Path -LiteralPath $OutputDir)) {
  New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

& (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration -STS2GameDir $STS2GameDir
& (Join-Path $PSScriptRoot "build-frieren.ps1") -Configuration $Configuration -STS2GameDir $STS2GameDir

$challengeOut = Join-Path $repoRoot "src\GongdouSts2ChallengeMod\bin\$Configuration\net9.0"
$frierenOut = Join-Path $repoRoot "src\GongdouSts2FrierenMod\bin\$Configuration\net9.0"
$frierenArtRoot = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot "docs") -Directory |
  Where-Object { $_.Name -like "*800*" -and $_.Name -like "*20260527" } |
  Select-Object -First 1 -ExpandProperty FullName)[0]
$frierenPackageArtRoot = $frierenArtRoot
if (-not $DisableFrierenAssetOptimization) {
  $optimizer = Join-Path $PSScriptRoot "optimize-frieren-package-assets.py"
  if (-not (Test-Path -LiteralPath $optimizer)) {
    throw "Frieren asset optimizer missing: $optimizer"
  }
  $optimizedRoot = Join-Path $OutputDir "frieren-package-assets-optimized"
  & python $optimizer `
    --source-root $frierenArtRoot `
    --output-root $optimizedRoot `
    --max-dimension $FrierenAssetMaxDimension `
    --colors $FrierenAssetColors | Out-Host
  $frierenPackageArtRoot = $optimizedRoot
}
$challengeManifest = Get-Content -LiteralPath (Join-Path $challengeOut "mod_manifest.json") -Raw | ConvertFrom-Json
$frierenManifest = Get-Content -LiteralPath (Join-Path $frierenOut "mod_manifest.json") -Raw | ConvertFrom-Json
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$zipPath = Join-Path $OutputDir "Gongdou_STS2_Mods-$($challengeManifest.version)-$stamp.zip"
if (Test-Path -LiteralPath $zipPath) {
  throw "Package already exists: $zipPath"
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
  function Get-RelativeArchivePath([string]$RootPath, [string]$FilePath) {
    $root = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $file = [System.IO.Path]::GetFullPath($FilePath)
    return $file.Substring($root.Length).Replace('\', '/')
  }

  function Get-FrierenAssetEntries {
    if ([string]::IsNullOrWhiteSpace($frierenPackageArtRoot) -or -not (Test-Path -LiteralPath $frierenPackageArtRoot)) {
      throw "Frieren art root missing: $frierenPackageArtRoot"
    }

    $assetDirs = @(Get-ChildItem -LiteralPath $frierenPackageArtRoot -Directory | ForEach-Object {
      [pscustomobject]@{
        FullName = $_.FullName
        PngCount = @(Get-ChildItem -LiteralPath $_.FullName -Recurse -File -Filter "*.png").Count
      }
    })

    $mappings = @(
      @{ SourceRoot = @($assetDirs | Where-Object { $_.PngCount -eq 91 } | Select-Object -First 1 -ExpandProperty FullName)[0]; Target = "cards" },
      @{ SourceRoot = @($assetDirs | Where-Object { $_.PngCount -eq 10 } | Select-Object -First 1 -ExpandProperty FullName)[0]; Target = "relics" },
      @{ SourceRoot = @($assetDirs | Where-Object { $_.PngCount -eq 5 } | Select-Object -First 1 -ExpandProperty FullName)[0]; Target = "potions" }
    )

    foreach ($mapping in $mappings) {
      $sourceRoot = $mapping.SourceRoot
      if (-not (Test-Path -LiteralPath $sourceRoot)) {
        throw "Frieren art source missing: $sourceRoot"
      }

      Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter "*.png" | ForEach-Object {
        [pscustomobject]@{
          Source = $_.FullName
          Entry = "mods/assets/frieren/$($mapping.Target)/$(Get-RelativeArchivePath $sourceRoot $_.FullName)"
        }
      }
    }

    Add-Type -AssemblyName System.Drawing
    $rootPngs = @(Get-ChildItem -LiteralPath $frierenArtRoot -File -Filter "*.png" | ForEach-Object {
      $image = [System.Drawing.Image]::FromFile($_.FullName)
      try {
        [pscustomobject]@{
          FullName = $_.FullName
          Length = $_.Length
          Width = $image.Width
          Height = $image.Height
        }
      }
      finally {
        $image.Dispose()
      }
    })
    $modelFile = @($rootPngs |
      Where-Object { $_.Width -le 1000 -and $_.Height -le 1000 } |
      Sort-Object Length -Descending |
      Select-Object -First 1)[0]
    $coverFile = @($rootPngs |
      Where-Object { $_.Width -ge 1600 -and $_.Height -ge 900 } |
      Sort-Object Length -Descending |
      Select-Object -First 1)[0]
    if ($null -eq $modelFile -or $null -eq $coverFile) {
      throw "Expected Frieren character model and cover images under: $frierenArtRoot"
    }
    $characterMappings = @(
      @{ Source = $modelFile.FullName; Entry = "mods/assets/frieren/character/model.png" },
      @{ Source = $coverFile.FullName; Entry = "mods/assets/frieren/character/cover.png" },
      @{ Source = $modelFile.FullName; Entry = "mods/Gongdou_STS2_Frieren_Character/assets/frieren/character/model.png" },
      @{ Source = $coverFile.FullName; Entry = "mods/Gongdou_STS2_Frieren_Character/assets/frieren/character/cover.png" }
    )
    foreach ($mapping in $characterMappings) {
      if (-not (Test-Path -LiteralPath $mapping.Source)) {
        throw "Frieren character art source missing: $($mapping.Source)"
      }

      [pscustomobject]@{
        Source = $mapping.Source
        Entry = $mapping.Entry
      }
    }
  }

  function Add-ZipEntry([string]$SourcePath, [string]$EntryName) {
    if (-not (Test-Path -LiteralPath $SourcePath)) {
      throw "Package source missing: $SourcePath"
    }
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $SourcePath, $EntryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }

  function Add-ZipTextEntry([string]$EntryName, [string]$Content) {
    $entry = $zip.CreateEntry($EntryName, [System.IO.Compression.CompressionLevel]::Optimal)
    $writer = [System.IO.StreamWriter]::new($entry.Open(), [System.Text.UTF8Encoding]::new($false))
    try {
      $writer.Write($Content)
    }
    finally {
      $writer.Dispose()
    }
  }

  function New-DummyModManifestJson([string]$Id, [string]$Name) {
    return ([ordered]@{
      id = $Id
      name = $Name
      author = "x1a0reN"
      description = "Compatibility marker generated by GongDou package."
      version = $frierenManifest.version
      has_pck = $false
      has_dll = $false
      affects_gameplay = $false
    } | ConvertTo-Json -Depth 4)
  }

  $frierenAssetEntries = @(Get-FrierenAssetEntries)
  $frierenCompatibilityEntries = @()
  $requiredFiles = @(
    "mods/Gongdou_STS2_Challenge.dll",
    "mods/GongdouSts2ChallengeMod.dll",
    "mods/Gongdou_STS2_Challenge.json",
    "mods/Gongdou_STS2_Frieren_Character.dll",
    "mods/GongdouSts2FrierenMod.dll",
    "mods/Gongdou_STS2_Frieren_Character.json",
    "mods/Gongdou_STS2_Frieren_Character/Gongdou_STS2_Frieren_Character.dll",
    "mods/Gongdou_STS2_Frieren_Character/GongdouSts2FrierenMod.dll",
    "mods/Gongdou_STS2_Frieren_Character/mod_manifest.json"
  ) + @($frierenCompatibilityEntries | ForEach-Object { $_.Entry }) + @($frierenAssetEntries | ForEach-Object { $_.Entry })
  $obsoleteFiles = @(
    "mods/assets/frieren/校验报告.json",
    "mods/assets/frieren/资源清单.json",
    "mods/Gongdou_STS2_Frieren_Character/assets/frieren/校验报告.json",
    "mods/Gongdou_STS2_Frieren_Character/assets/frieren/资源清单.json"
  )
  $metadataEntry = $zip.CreateEntry(".gongdou_mod_package.json", [System.IO.Compression.CompressionLevel]::Optimal)
  $metadataWriter = [System.IO.StreamWriter]::new($metadataEntry.Open(), [System.Text.UTF8Encoding]::new($false))
  try {
    $metadataWriter.Write(($([ordered]@{ requiredFiles = $requiredFiles; obsoleteFiles = $obsoleteFiles } | ConvertTo-Json -Depth 4)))
  }
  finally {
    $metadataWriter.Dispose()
  }

  Add-ZipEntry (Join-Path $challengeOut "GongdouSts2ChallengeMod.dll") "mods/Gongdou_STS2_Challenge.dll"
  Add-ZipEntry (Join-Path $challengeOut "GongdouSts2ChallengeMod.dll") "mods/GongdouSts2ChallengeMod.dll"
  Add-ZipEntry (Join-Path $challengeOut "mod_manifest.json") "mods/Gongdou_STS2_Challenge.json"
  Add-ZipEntry (Join-Path $frierenOut "GongdouSts2FrierenMod.dll") "mods/Gongdou_STS2_Frieren_Character.dll"
  Add-ZipEntry (Join-Path $frierenOut "GongdouSts2FrierenMod.dll") "mods/GongdouSts2FrierenMod.dll"
  Add-ZipTextEntry "mods/Gongdou_STS2_Frieren_Character.json" (New-DummyModManifestJson "Gongdou_STS2_Frieren_Character_CompatMarker" "GongDou STS2 Frieren Character Compat Marker")
  Add-ZipEntry (Join-Path $frierenOut "GongdouSts2FrierenMod.dll") "mods/Gongdou_STS2_Frieren_Character/Gongdou_STS2_Frieren_Character.dll"
  Add-ZipEntry (Join-Path $frierenOut "GongdouSts2FrierenMod.dll") "mods/Gongdou_STS2_Frieren_Character/GongdouSts2FrierenMod.dll"
  Add-ZipEntry (Join-Path $frierenOut "mod_manifest.json") "mods/Gongdou_STS2_Frieren_Character/mod_manifest.json"
  foreach ($compatibilityEntry in $frierenCompatibilityEntries) {
    if ($compatibilityEntry.Entry -eq "mods/Gongdou_STS2_Frieren_Character.json") {
      continue
    }

    Add-ZipTextEntry $compatibilityEntry.Entry (New-DummyModManifestJson $compatibilityEntry.Id $compatibilityEntry.Name)
  }

  foreach ($asset in $frierenAssetEntries) {
    Add-ZipEntry $asset.Source $asset.Entry
  }
}
finally {
  $zip.Dispose()
}

if (-not $SkipLocalPackageCleanup) {
  & (Join-Path $PSScriptRoot "cleanup-local-mod-packages.ps1") `
    -OutputDir $OutputDir `
    -Keep $LocalPackagesToKeep `
    -CurrentPackagePath $zipPath `
    -IncludeSubdirectories | Out-Host
}

Get-Item -LiteralPath $zipPath
