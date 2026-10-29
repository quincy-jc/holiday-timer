param(
    [switch]$Test,
    [switch]$SkipAppBuild,
    [string]$PayloadDirectory = 'dist\celebration-defaults',
    [string]$OutputDirectory = 'dist\celebration-defaults-setup',
    [string]$MetadataDirectory = 'dist\celebration-defaults-setup-verification',
    [string]$CompilerPath = 'tools\inno-6.7.3\ISCC.exe',
    [switch]$IsolationTest,
    [string]$TestRoot
)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
function Resolve-ProjectPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $taskRoot $Path))
}
$payload = Resolve-ProjectPath $PayloadDirectory
$output = Resolve-ProjectPath $OutputDirectory
$compiler = Resolve-ProjectPath $CompilerPath
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Inno Setup 6.7.3 ISCC.exe was not found. See installer\README.md for compiler setup.'
}
if (-not $SkipAppBuild) {
    & (Join-Path $taskRoot 'build.ps1') -Test:$Test -OutputDirectory $payload
}
Copy-Item -LiteralPath (Join-Path $taskRoot 'README.md') -Destination (Join-Path $payload 'README.md') -Force
foreach ($file in @('HolidayTimer.exe','HolidayTimer.exe.config','Inter-LICENSE.txt','LICENSE.txt','README.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $file) -PathType Leaf)) { throw ('Missing payload: ' + $file) }
}
New-Item -ItemType Directory -Force -Path $output | Out-Null
$arguments = @('/Qp', ('/DPayloadDir=' + $payload), ('/DSetupOutputDir=' + $output))
if ($IsolationTest) {
    if (-not $TestRoot) { throw 'An isolated install requires TestRoot.' }
    $isolatedRoot = Resolve-ProjectPath $TestRoot
    if (-not $isolatedRoot.StartsWith($taskRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The isolated install directory must be inside this project.'
    }
    $arguments += @('/DIsolationTest', ('/DTestRoot=' + $isolatedRoot))
}
$arguments += Join-Path $taskRoot 'installer\HolidayTimer.iss'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Holiday Timer setup build failed.' }
$setupName = if ($IsolationTest) { 'HolidayTimerSetup-Test.exe' } else { 'HolidayTimerSetup.exe' }
$setupFile = Join-Path $output $setupName
$setupHash = (Get-FileHash -LiteralPath $setupFile -Algorithm SHA256).Hash.ToLowerInvariant()
$metadata = Resolve-ProjectPath $MetadataDirectory
New-Item -ItemType Directory -Force -Path $metadata | Out-Null
$metadataName = if ($IsolationTest) { 'package-info-test.json' } else { 'package-info.json' }
@{ File=$setupName; Bytes=(Get-Item -LiteralPath $setupFile).Length; SHA256=$setupHash;
    Compiler='Inno Setup 6.7.3'; AppSHA256=(Get-FileHash -LiteralPath (Join-Path $payload 'HolidayTimer.exe')).Hash.ToLowerInvariant();
    Isolated=[bool]$IsolationTest } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $metadata $metadataName) -Encoding UTF8
Write-Output ('Built ' + $setupFile)
if ($Test -and -not $IsolationTest) {
    & (Join-Path $taskRoot 'installer\test-setup.ps1') -CompilerPath $compiler -PayloadDirectory $payload
}
