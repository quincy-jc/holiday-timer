param([string]$OutputDirectory = 'dist/celebration-dismissal-checks', [switch]$Before)
$ErrorActionPreference = 'Stop'
$dismissalRoot = Split-Path -Parent $PSScriptRoot
$dismissalOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $dismissalRoot $OutputDirectory }
New-Item -ItemType Directory -Force -Path $dismissalOutput | Out-Null
$dismissalFramework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$dismissalExe = Join-Path $dismissalOutput 'CelebrationDismissalTests.exe'
$dismissalArgs = @('/nologo','/target:exe','/optimize+',('/out:' + $dismissalExe))
foreach ($dismissalRef in @('System.dll','System.Core.dll','System.Xaml.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll')) { $dismissalArgs += '/reference:' + (Join-Path $dismissalFramework $dismissalRef) }
if ($Before) { $dismissalArgs += '/define:BEFORE' } else { $dismissalArgs += Join-Path $dismissalRoot 'src/CelebrationDismissal.cs' }
$dismissalArgs += Join-Path $dismissalRoot 'tests/CelebrationDismissalTests.cs'
& (Join-Path $dismissalFramework 'csc.exe') @dismissalArgs
if ($LASTEXITCODE -ne 0) { throw 'Celebration dismissal checks did not compile.' }
& $dismissalExe 2>&1 | Tee-Object -FilePath (Join-Path $dismissalOutput 'dismissal-checks.txt')
if ($LASTEXITCODE -ne 0) { throw 'Celebration dismissal checks failed.' }
