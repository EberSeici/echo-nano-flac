param(
    [Parameter(Mandatory=$true)][string]$FfmpegPath,
    [string]$IsccPath
)
$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$appOutput = Join-Path $PSScriptRoot 'dist\app'
New-Item -ItemType Directory -Path $appOutput -Force | Out-Null
$sourcePath = Join-Path $PSScriptRoot 'Source\Program.cs'
$iconPath = Join-Path $PSScriptRoot 'Source\EchoNanoFLAC.ico'
$exePath = Join-Path $appOutput 'EchoNanoFLAC.exe'
& $compilerPath /nologo /target:winexe /optimize+ "/out:$exePath" "/win32icon:$iconPath" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $sourcePath
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación.' }
Copy-Item -LiteralPath $FfmpegPath -Destination (Join-Path $appOutput 'ffmpeg.exe')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Source') -Destination $appOutput -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $appOutput 'LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CONDICIONES.txt'),(Join-Path $PSScriptRoot 'COPYING.FFmpeg-GPLv3.txt') -Destination $appOutput
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $appOutput 'AVISOS-FFmpeg.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $appOutput 'LEEME.txt')
if ($IsccPath) {
    $payloadPath = Join-Path $PSScriptRoot 'Installer\payload'
    New-Item -ItemType Directory -Path $payloadPath -Force | Out-Null
    Copy-Item -Path (Join-Path $appOutput '*') -Destination $payloadPath -Recurse -Force
    & $IsccPath (Join-Path $PSScriptRoot 'Installer\EchoNanoFLAC.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Falló la creación del instalador.' }
}
Write-Output "Aplicación lista en $appOutput"
