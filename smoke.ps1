param([Parameter(Mandatory=$true)][string]$OutputDirectory, [switch]$LiveTyping, [ValidateRange(0,86400)][int]$RequireIdleSeconds = 0)
$ErrorActionPreference = 'Stop'
if ($RequireIdleSeconds -gt 0 -and -not $LiveTyping) { throw '-RequireIdleSeconds applies only to -LiveTyping.' }
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$protected = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'AutoWrite')).TrimEnd('\') + '\'
if (($OutputDirectory.TrimEnd('\') + '\').StartsWith($protected, [StringComparison]::OrdinalIgnoreCase)) { throw 'Diagnostics must use an isolated directory outside the real AutoWrite data directory.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$data = Join-Path $OutputDirectory ('smoke-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $data | Out-Null
$previous = $env:AUTOWRITE_DATA_DIR
$process = $null
try {
    $env:AUTOWRITE_DATA_DIR = $data
    $argument = '--sendinput-probe'
    $windowStyle = 'Hidden'
    if ($LiveTyping) { $argument = '--typing-smoke-probe'; $windowStyle = 'Normal' }
    if ($RequireIdleSeconds -gt 0) { $argument += ' --require-idle-seconds ' + $RequireIdleSeconds }
    $artifact = Join-Path $root 'dist/AutoWrite.exe'
    Write-Output ('ARTIFACT SHA256 ' + (Get-FileHash -Algorithm SHA256 -LiteralPath $artifact).Hash)
    $process = Start-Process -FilePath $artifact -ArgumentList $argument -WindowStyle $windowStyle -PassThru
    if (-not $process.WaitForExit(15000)) { $process.Kill(); $process.WaitForExit(); throw 'Owned smoke process exceeded the 15-second hard timeout.' }
    $process.Refresh()
    $code = $process.ExitCode
    Write-Output ("PROBE $argument exit=$code pid=" + $process.Id + ' exited=' + $process.HasExited)
    if ($LiveTyping -and (Test-Path -LiteralPath (Join-Path $data 'live-probe.txt'))) { Get-Content -LiteralPath (Join-Path $data 'live-probe.txt') }
    if ($LiveTyping -and $code -eq 0) { Write-Output 'PASS real SendInput produced exact owned-window text: AutoWrite: aB [12] (3.5%)?<Enter>OK; foreground-loss guard enabled.' }
} finally {
    $env:AUTOWRITE_DATA_DIR = $previous
    if ($process) { if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }; $process.Dispose() }
}
exit $code
