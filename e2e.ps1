param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$protected = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'AutoWrite')).TrimEnd('\') + '\'
if (($OutputDirectory.TrimEnd('\') + '\').StartsWith($protected, [StringComparison]::OrdinalIgnoreCase)) { throw 'Tests must use an isolated directory outside the real AutoWrite data directory.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$compilerRoot = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $compilerRoot 'csc.exe'
$wpf = Join-Path $compilerRoot 'WPF'
$exe = Join-Path $OutputDirectory 'CalibrationE2E.exe'
$source = Join-Path $root 'diagnostics/CalibrationE2E.cs'
& $compiler /nologo /target:exe /out:$exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:Accessibility.dll "/reference:$wpf/UIAutomationClient.dll" "/reference:$wpf/UIAutomationTypes.dll" "/reference:$wpf/WindowsBase.dll" $source
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Output ('ARTIFACT SHA256 ' + (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'dist/AutoWrite.exe')).Hash)
& $exe (Join-Path $root 'dist/AutoWrite.exe') $OutputDirectory (Join-Path $root 'tests/baseline-settings.ini')
exit $LASTEXITCODE
