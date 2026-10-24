$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root "src\AutoWrite\Program.cs"
$manifest = Join-Path $root "app.manifest"
$icon = Join-Path $root "assets\AutoWrite.ico"
$dist = Join-Path $root "dist"
$output = Join-Path $dist "AutoWrite.exe"

New-Item -ItemType Directory -Force -Path $dist | Out-Null

$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework C# compiler was not found under $env:WINDIR\Microsoft.NET."
}

if (-not (Test-Path -LiteralPath $icon)) {
    throw "The application icon was not found at $icon."
}

& $compiler `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    /out:$output `
    /win32manifest:$manifest `
    /win32icon:$icon `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $source

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Built $output"
