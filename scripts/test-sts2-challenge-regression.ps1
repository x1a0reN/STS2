param(
  [string]$GameDir = "D:\Steam\steamapps\common\Slay the Spire 2",
  [string]$ArtifactDir = "D:\Projects\SlayTheSpire2ChallengeMod\artifacts\ui-regression"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class Sts2UiRegressionNative {
  [StructLayout(LayoutKind.Sequential)]
  public struct RECT {
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
  }

  [DllImport("user32.dll")]
  public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

  [DllImport("user32.dll")]
  public static extern bool SetForegroundWindow(IntPtr hWnd);

  [DllImport("user32.dll")]
  public static extern bool SetCursorPos(int X, int Y);

  [DllImport("user32.dll")]
  public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

  [DllImport("user32.dll")]
  public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
  public const uint KEYUP = 0x0002;

  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
  }

  public static void Key(byte vk) {
    keybd_event(vk, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    keybd_event(vk, 0, KEYUP, UIntPtr.Zero);
  }
}
"@

function Get-Sts2Process {
  $process = Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($null -eq $process) {
    throw "SlayTheSpire2 process is not running."
  }

  return $process
}

function Get-Sts2WindowRect {
  $process = Get-Sts2Process
  [Sts2UiRegressionNative+RECT]$rect = New-Object Sts2UiRegressionNative+RECT
  [Sts2UiRegressionNative]::GetWindowRect($process.MainWindowHandle, [ref]$rect) | Out-Null
  [pscustomobject]@{
    Left = $rect.Left
    Top = $rect.Top
    Right = $rect.Right
    Bottom = $rect.Bottom
    Width = $rect.Right - $rect.Left
    Height = $rect.Bottom - $rect.Top
  }
}

function Focus-Sts2Window {
  $process = Get-Sts2Process
  [Sts2UiRegressionNative]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
}

function Start-Sts2RegressionGame {
  $exe = Join-Path $GameDir "SlayTheSpire2.exe"
  if (-not (Test-Path -LiteralPath $exe)) {
    throw "Game executable not found: $exe"
  }

  $env:SteamAppId = "2868840"
  $env:SteamGameId = "2868840"
  $env:SteamOverlayGameId = "2868840"
  Start-Process -FilePath $exe -WorkingDirectory $GameDir
}

function Save-Sts2Screenshot {
  param([string]$Name)

  $rect = Get-Sts2WindowRect
  if (-not (Test-Path -LiteralPath $ArtifactDir)) {
    New-Item -ItemType Directory -Path $ArtifactDir | Out-Null
  }

  $path = Join-Path $ArtifactDir "$Name.png"
  $bitmap = New-Object System.Drawing.Bitmap $rect.Width, $rect.Height
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  try {
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($rect.Width, $rect.Height))
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  }
  finally {
    $graphics.Dispose()
    $bitmap.Dispose()
  }

  Get-Item -LiteralPath $path
}

function Click-Sts2Normalized {
  param(
    [double]$X,
    [double]$Y
  )

  $rect = Get-Sts2WindowRect
  Focus-Sts2Window
  $screenX = [int]($rect.Left + ($rect.Width * $X))
  $screenY = [int]($rect.Top + ($rect.Height * $Y))
  [Sts2UiRegressionNative]::Click($screenX, $screenY)
}

function Click-Sts2Pixel {
  param(
    [int]$X,
    [int]$Y
  )

  $rect = Get-Sts2WindowRect
  Focus-Sts2Window
  [Sts2UiRegressionNative]::Click($rect.Left + $X, $rect.Top + $Y)
}

function Send-Sts2Key {
  param([byte]$VirtualKey)

  Focus-Sts2Window
  [Sts2UiRegressionNative]::Key($VirtualKey)
}

function Invoke-Sts2RegressionStep {
  param(
    [string]$Name,
    [scriptblock]$Action,
    [int]$WaitSeconds = 2
  )

  Write-Host "CASE_STEP $Name"
  & $Action
  if ($WaitSeconds -gt 0) {
    Start-Sleep -Seconds $WaitSeconds
  }
  Save-Sts2Screenshot -Name $Name | Select-Object FullName, Length, LastWriteTime
}

Write-Host "Loaded STS2 UI regression helpers."
Write-Host "Test cases:"
Write-Host "  TC01: main menu can enter GongDou co-op while normal/Frieren saves exist."
Write-Host "  TC02: preparation save-and-quit resumes to native card selection, not map."
Write-Host "  TC03: combat save-and-quit resumes to combat room, not map."
Write-Host "  TC04: top-right challenge info icon is visible and opens native-styled info screen."
