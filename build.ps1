param([switch]$Test, [string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework compiler was not found.' }
$output = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $taskRoot $OutputDirectory }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$fontResources = Join-Path $output 'HolidayTimer.g.resources'
$fontWriter = New-Object System.Resources.ResourceWriter($fontResources)
try {
    foreach ($fontName in @('InterDisplay-Regular.ttf','InterDisplay-SemiBold.ttf','InterDisplay-Bold.ttf')) {
        $fontStream = [System.IO.File]::OpenRead((Join-Path $taskRoot ('assets\fonts\' + $fontName)))
        $fontWriter.AddResource(('fonts/' + $fontName.ToLowerInvariant()), $fontStream, $true)
    }
    $fontWriter.Generate()
} finally { $fontWriter.Dispose() }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$arguments = @('/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/main:HolidayTimer.Program', ('/out:' + (Join-Path $output 'HolidayTimer.exe')), ('/win32manifest:' + (Join-Path $taskRoot 'src\app.manifest')))
$arguments += '/resource:' + $fontResources + ',HolidayTimer.g.resources'
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
foreach ($layout in @('SettingsWindow','WidgetWindow','GreetingWindow','Controls','CalendarStyles')) { $arguments += '/resource:' + (Join-Path $taskRoot ('src\' + $layout + '.xaml')) + ',HolidayTimer.' + $layout + '.xaml' }
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Holiday Timer build failed.' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'src\HolidayTimer.exe.config') -Destination (Join-Path $output 'HolidayTimer.exe.config') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'assets\fonts\LICENSE.txt') -Destination (Join-Path $output 'Inter-LICENSE.txt') -Force
Write-Output ('Built ' + (Join-Path $output 'HolidayTimer.exe'))
if ($Test) {
    $testExe = Join-Path $output 'CoreTests.exe'
    & $compiler /nologo /target:exe /optimize+ ('/out:' + $testExe) ('/reference:' + (Join-Path $framework 'System.Runtime.Serialization.dll')) (Join-Path $taskRoot 'src\Core.cs') (Join-Path $taskRoot 'src\WorkCalendar.cs') (Join-Path $taskRoot 'src\PublicHolidayCalendar.cs') (Join-Path $taskRoot 'tests\CoreTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Core test build failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
    $effectsTestExe = Join-Path $output 'EffectSettingsTests.exe'
    & $compiler /nologo /target:exe /optimize+ ('/out:' + $effectsTestExe) ('/reference:' + (Join-Path $framework 'System.Runtime.Serialization.dll')) (Join-Path $taskRoot 'src\Core.cs') (Join-Path $taskRoot 'src\WorkCalendar.cs') (Join-Path $taskRoot 'src\PublicHolidayCalendar.cs') (Join-Path $taskRoot 'tests\EffectSettingsTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Effect settings test build failed.' }
    & $effectsTestExe
    if ($LASTEXITCODE -ne 0) { throw 'Effect settings checks failed.' }
    $workTestExe = Join-Path $output 'WorkCalendarTests.exe'
    & $compiler /nologo /target:exe /optimize+ ('/out:' + $workTestExe) ('/reference:' + (Join-Path $framework 'System.Runtime.Serialization.dll')) (Join-Path $taskRoot 'src\Core.cs') (Join-Path $taskRoot 'src\WorkCalendar.cs') (Join-Path $taskRoot 'src\PublicHolidayCalendar.cs') (Join-Path $taskRoot 'tests\WorkCalendarTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Work-calendar test build failed.' }
    & $workTestExe
    if ($LASTEXITCODE -ne 0) { throw 'Work-calendar checks failed.' }
    $holidayTestExe = Join-Path $output 'PublicHolidayCalendarTests.exe'
    & $compiler /nologo /target:exe /optimize+ ('/out:' + $holidayTestExe) ('/reference:' + (Join-Path $framework 'System.Runtime.Serialization.dll')) (Join-Path $taskRoot 'src\PublicHolidayCalendar.cs') (Join-Path $taskRoot 'tests\PublicHolidayCalendarTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Public-holiday test build failed.' }
    & $holidayTestExe
    if ($LASTEXITCODE -ne 0) { throw 'Public-holiday checks failed.' }
    $musicTestExe = Join-Path $output 'MusicTests.exe'
    & $compiler /nologo /target:exe /optimize+ ('/out:' + $musicTestExe) (Join-Path $taskRoot 'src\AudioAnalysis.cs') (Join-Path $taskRoot 'tests\MusicTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Audio test build failed.' }
    & $musicTestExe
    if ($LASTEXITCODE -ne 0) { throw 'Audio checks failed.' }
    $updateTestExe = Join-Path $output 'GitHubReleaseTests.exe'
    & $compiler /nologo /target:exe /optimize+ ('/out:' + $updateTestExe) ('/reference:' + (Join-Path $framework 'System.Runtime.Serialization.dll')) (Join-Path $taskRoot 'src\GitHubReleaseClient.cs') (Join-Path $taskRoot 'tests\GitHubReleaseTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Update test build failed.' }
    & $updateTestExe (Join-Path $output 'HolidayTimer.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Update validation checks failed.' }
    $check = Start-Process -FilePath (Join-Path $output 'HolidayTimer.exe') -ArgumentList '--smoke-test' -WindowStyle Hidden -PassThru -Wait
    if ($check.ExitCode -ne 0) { throw 'Interface checks failed. See dist\ui-checks.txt.' }
    Get-Content -LiteralPath (Join-Path $output 'ui-checks.txt')
}
