$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildScript = Join-Path $root "build.ps1"
$exe = Join-Path $root "dist\AutoWrite.exe"

if (-not (Test-Path -LiteralPath $exe)) {
    & $buildScript
}

if (-not (Test-Path -LiteralPath $exe)) {
    throw "AutoWrite.exe was not found at $exe."
}

$programs = [Environment]::GetFolderPath("Programs")
$shortcutPath = Join-Path $programs "AutoWrite.lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = Split-Path -Parent $exe
$shortcut.IconLocation = "$exe,0"
$shortcut.Description = "AutoWrite - hotkey-triggered human-paced text writer"
$shortcut.Save()

Write-Host "Created Start Menu shortcut: $shortcutPath"
