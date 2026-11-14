param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) ('autowrite-tests-' + [Guid]::NewGuid().ToString('N'))
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$protected = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'AutoWrite')).TrimEnd('\') + '\'
if (($OutputDirectory.TrimEnd('\') + '\').StartsWith($protected, [StringComparison]::OrdinalIgnoreCase)) { throw 'Tests must use an isolated directory outside the real AutoWrite data directory.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src/AutoWrite') -Filter *.cs | ForEach-Object { $_.FullName })
$tests = @(Get-ChildItem -LiteralPath (Join-Path $root 'tests') -Filter *.cs | ForEach-Object { $_.FullName })
$exe = Join-Path $OutputDirectory 'AutoWrite.Tests.exe'
& $compiler /nologo /target:exe /main:AutoWrite.Tests.Runner /out:$exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sources $tests
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$previous = $env:AUTOWRITE_DATA_DIR
try {
    $env:AUTOWRITE_DATA_DIR = Join-Path $OutputDirectory 'data'
    & $exe $OutputDirectory $root
    $testExit = $LASTEXITCODE
} finally { $env:AUTOWRITE_DATA_DIR = $previous }
exit $testExit
