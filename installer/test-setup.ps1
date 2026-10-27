param(
    [Parameter(Mandatory = $true)][string]$CompilerPath,
    [string]$PayloadDirectory = 'dist\companions-update',
    [string]$PreviousPayloadDirectory = 'dist\mit-update',
    [string]$TestRoot
)

$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskRoot = [IO.Path]::GetFullPath($taskRoot).TrimEnd('\')
if ([string]::IsNullOrWhiteSpace($TestRoot)) {
    $TestRoot = Join-Path $taskRoot ('dist\installer-tests\' + [Guid]::NewGuid().ToString('N'))
}
$TestRoot = [IO.Path]::GetFullPath($TestRoot).TrimEnd('\')
if (-not $TestRoot.StartsWith($taskRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $TestRoot -eq $taskRoot -or (Test-Path -LiteralPath $TestRoot)) {
    throw 'Installer checks require a new, empty test directory inside this workspace.'
}
$ancestor = Split-Path -Parent $TestRoot
while ($ancestor -and $ancestor.StartsWith($taskRoot, [StringComparison]::OrdinalIgnoreCase)) {
    if (Test-Path -LiteralPath $ancestor) {
        $ancestorItem = Get-Item -LiteralPath $ancestor
        if (($ancestorItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Installer checks cannot use a test directory under a junction or symbolic link.'
        }
    }
    if ($ancestor -eq $taskRoot) { break }
    $ancestor = Split-Path -Parent $ancestor
}
if (-not [IO.Path]::IsPathRooted($PayloadDirectory)) { $PayloadDirectory = Join-Path $taskRoot $PayloadDirectory }
$PayloadDirectory = [IO.Path]::GetFullPath($PayloadDirectory)
$CompilerPath = [IO.Path]::GetFullPath($CompilerPath)
if (-not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) { throw 'The Inno Setup compiler was not found.' }

$testRegistryPath = 'Software\HolidayTimer\InstallerTests'
$uninstallParentPath = 'Software\Microsoft\Windows\CurrentVersion\Uninstall'
$testUninstallName = 'HolidayTimer-Installer-Test_is1'
$testUninstallPath = $uninstallParentPath + '\' + $testUninstallName
$testMutexName = 'Local\HolidayTimer.SetupTest.Instance'
$installRoot = Join-Path $TestRoot 'installed'
$preferencesRoot = Join-Path $TestRoot 'preferences'
$shortcutPath = Join-Path $TestRoot 'Holiday Timer.lnk'
$outputRoot = Join-Path $TestRoot 'compiled'
$checks = 0
$heldMutex = $null
$isolatedSetup = $null

function Assert-Check([bool]$Passed, [string]$Description) {
    if (-not $Passed) { throw ('FAILED: ' + $Description) }
    $script:checks++
    Write-Output ('PASS: ' + $Description)
}

function Get-RegistrySnapshot([string]$Path) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($Path)
    if ($null -eq $key) { return '<absent>' }
    try {
        $values = @($key.GetValueNames() | Sort-Object | ForEach-Object {
            [ordered]@{ Name = $_; Kind = $key.GetValueKind($_).ToString(); Value = $key.GetValue($_, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) }
        })
        return ConvertTo-Json -InputObject $values -Compress -Depth 5
    } finally { $key.Dispose() }
}

function Get-FileSnapshot([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return '<absent>' }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-PersonalSnapshot {
    $prefs = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'HolidayTimer'
    $files = @()
    if (Test-Path -LiteralPath $prefs -PathType Container) {
        $files = @(Get-ChildItem -LiteralPath $prefs -File | Sort-Object Name | ForEach-Object {
            [ordered]@{ Name = $_.Name; Hash = Get-FileSnapshot $_.FullName }
        })
    }
    $apps = @()
    $parent = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($uninstallParentPath)
    if ($null -ne $parent) {
        try {
            $apps = @($parent.GetSubKeyNames() | Sort-Object | ForEach-Object {
                $name = $_
                if ($name -ne $testUninstallName) {
                    $key = $parent.OpenSubKey($name)
                    try {
                        if ([string]$key.GetValue('DisplayName') -eq 'Holiday Timer' -or $name -match '^HolidayTimer') {
                            [ordered]@{ Name = $name; Values = Get-RegistrySnapshot ($uninstallParentPath + '\' + $name) }
                        }
                    } finally { if ($null -ne $key) { $key.Dispose() } }
                }
            })
        } finally { $parent.Dispose() }
    }
    $processes = @(Get-Process -Name HolidayTimer -ErrorAction SilentlyContinue | Sort-Object Id | ForEach-Object {
        [ordered]@{ Id = $_.Id; Path = $_.Path; Started = $_.StartTime.ToUniversalTime().Ticks; ExecutableHash = Get-FileSnapshot $_.Path }
    })
    [ordered]@{
        Run = Get-RegistrySnapshot 'Software\Microsoft\Windows\CurrentVersion\Run'
        PreferencesExist = Test-Path -LiteralPath $prefs -PathType Container
        Preferences = $files
        Apps = $apps
        Shortcut = Get-FileSnapshot (Join-Path ([Environment]::GetFolderPath('Programs')) 'Holiday Timer.lnk')
        Processes = $processes
    } | ConvertTo-Json -Compress -Depth 8
}

function Invoke-CheckedProcess([string]$Executable, [string[]]$Arguments, [string]$Description, [bool]$ExpectSuccess = $true) {
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -PassThru -WindowStyle Hidden
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(45)
        while (-not $process.WaitForExit(500)) {
            if ([DateTime]::UtcNow -ge $deadline) { throw ($Description + ' did not finish within 45 seconds. The test did not terminate it.') }
        }
        Assert-Check ($ExpectSuccess -eq ($process.ExitCode -eq 0)) ($Description + ' exit status (' + $process.ExitCode + ')')
    } finally { $process.Dispose() }
}

function Invoke-Install([string]$LogName, [bool]$ExpectSuccess = $true) {
    Invoke-CheckedProcess $isolatedSetup @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', ('/LOG="' + (Join-Path $TestRoot $LogName) + '"')) 'Isolated setup' $ExpectSuccess
}

function Invoke-Uninstall([string]$LogName, [bool]$ExpectSuccess = $true) {
    $uninstaller = Join-Path $installRoot 'unins000.exe'
    Invoke-CheckedProcess $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $TestRoot $LogName) + '"')) 'Isolated uninstall' $ExpectSuccess
    if ($ExpectSuccess) {
        # Inno runs uninstall from a temporary copy. Its original process may
        # exit before that copy finishes removing the original uninstaller.
        $deadline = [DateTime]::UtcNow.AddSeconds(10)
        do {
            $remaining = @(@('HolidayTimer.exe', 'HolidayTimer.exe.config', 'Inter-LICENSE.txt', 'LICENSE.txt', 'README.md', 'unins000.exe', 'unins000.dat') |
                Where-Object { Test-Path -LiteralPath (Join-Path $installRoot $_) })
            $finished = $remaining.Count -eq 0 -and (Get-RegistrySnapshot $testUninstallPath) -eq '<absent>' -and
                -not (Test-Path -LiteralPath $shortcutPath)
            if (-not $finished) { [Threading.Thread]::Sleep(100) }
        } while (-not $finished -and [DateTime]::UtcNow -lt $deadline)
        Assert-Check $finished 'Uninstall completes its temporary-process cleanup'
    }
}

function Assert-Payload([string]$ExpectedPayload = $PayloadDirectory) {
    $expectedFiles = @('HolidayTimer.exe', 'HolidayTimer.exe.config', 'Inter-LICENSE.txt', 'README.md')
    if (Test-Path -LiteralPath (Join-Path $ExpectedPayload 'LICENSE.txt')) { $expectedFiles += 'LICENSE.txt' }
    foreach ($name in $expectedFiles) {
        Assert-Check ((Get-FileSnapshot (Join-Path $installRoot $name)) -eq (Get-FileSnapshot (Join-Path $ExpectedPayload $name))) ('Installed payload matches: ' + $name)
    }
}

function Assert-AppsEntry([string]$ExpectedVersion = '1.8.0') {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($testUninstallPath)
    try {
        Assert-Check ($null -ne $key) 'One isolated Windows Apps uninstall entry exists'
        Assert-Check ([string]$key.GetValue('DisplayName') -eq 'Holiday Timer') 'Apps entry uses the product name'
        Assert-Check (-not [string]::IsNullOrWhiteSpace([string]$key.GetValue('DisplayVersion'))) 'Apps entry includes a version'
        Assert-Check ([string]$key.GetValue('DisplayVersion') -eq $ExpectedVersion) ('Apps entry shows version ' + $ExpectedVersion)
        Assert-Check ([IO.Path]::GetFullPath([string]$key.GetValue('InstallLocation')).TrimEnd('\') -eq $installRoot) 'Apps entry points to the isolated install directory'
        Assert-Check ([string]$key.GetValue('UninstallString') -match [Regex]::Escape($installRoot)) 'Apps uninstall command points to its own uninstaller'
        Assert-Check ([string]$key.GetValue('QuietUninstallString') -match [Regex]::Escape($installRoot)) 'Apps quiet uninstall command points to its own uninstaller'
    } finally { if ($null -ne $key) { $key.Dispose() } }
    $parent = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($uninstallParentPath)
    try {
        $matches = @($parent.GetSubKeyNames() | Where-Object { $_ -like 'HolidayTimer-Installer-Test*' })
        Assert-Check ($matches.Count -eq 1 -and $matches[0] -eq $testUninstallName) 'Updating does not create duplicate Apps entries'
    } finally { $parent.Dispose() }
}

function Set-IsolatedRun([string]$Command) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($testRegistryPath)
    try {
        if ([string]::IsNullOrWhiteSpace($Command)) { $key.DeleteValue('HolidayTimer', $false) }
        else { $key.SetValue('HolidayTimer', $Command, [Microsoft.Win32.RegistryValueKind]::String) }
    }
    finally { $key.Dispose() }
}

function Get-IsolatedRun {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($testRegistryPath)
    if ($null -eq $key) { return $null }
    try { return $key.GetValue('HolidayTimer') }
    finally { $key.Dispose() }
}

function Hold-TestMutex {
    $created = $false
    $script:heldMutex = [Threading.Mutex]::new($true, $testMutexName, [ref]$created)
    Assert-Check $created 'The isolated test mutex is not held by another process'
}

function Release-TestMutex {
    if ($null -ne $script:heldMutex) {
        $script:heldMutex.ReleaseMutex()
        $script:heldMutex.Dispose()
        $script:heldMutex = $null
    }
}

Assert-Check ((Get-RegistrySnapshot $testRegistryPath) -eq '<absent>') 'Test registry namespace is unused'
Assert-Check ((Get-RegistrySnapshot $testUninstallPath) -eq '<absent>') 'Test uninstall namespace is unused'
foreach ($name in @('HolidayTimer.exe', 'HolidayTimer.exe.config', 'Inter-LICENSE.txt', 'LICENSE.txt', 'README.md')) {
    Assert-Check (Test-Path -LiteralPath (Join-Path $PayloadDirectory $name) -PathType Leaf) ('Payload contains: ' + $name)
}
$personalBefore = Get-PersonalSnapshot
New-Item -ItemType Directory -Path $TestRoot, $outputRoot | Out-Null

try {
    $setupScript = Join-Path $PSScriptRoot 'HolidayTimer.iss'
    Invoke-CheckedProcess $CompilerPath @('/Qp', '/DIsolationTest', ('/DTestRoot="' + $TestRoot + '"'), ('/DPayloadDir="' + $PayloadDirectory + '"'), ('/DSetupOutputDir="' + $outputRoot + '"'), ('"' + $setupScript + '"')) 'Isolated installer compilation'
    $setups = @(Get-ChildItem -LiteralPath $outputRoot -Filter '*.exe' -File)
    Assert-Check ($setups.Count -eq 1) 'The isolated compiler produces one setup executable'
    $isolatedSetup = $setups[0].FullName
    $updatedSetup = $isolatedSetup
    $previousPayload = if ([IO.Path]::IsPathRooted($PreviousPayloadDirectory)) { [IO.Path]::GetFullPath($PreviousPayloadDirectory) } else { Join-Path $taskRoot $PreviousPayloadDirectory }
    $previousSetup = $null
    if (Test-Path -LiteralPath (Join-Path $previousPayload 'HolidayTimer.exe')) {
        foreach ($name in @('HolidayTimer.exe', 'HolidayTimer.exe.config', 'Inter-LICENSE.txt', 'README.md')) {
            Assert-Check (Test-Path -LiteralPath (Join-Path $previousPayload $name)) ('Prior version payload contains: ' + $name)
        }
        $previousVersion = ((Get-Item -LiteralPath (Join-Path $previousPayload 'HolidayTimer.exe')).VersionInfo.FileVersion.Split('.')[0..2]) -join '.'
        $previousOutput = Join-Path $outputRoot 'previous'
        New-Item -ItemType Directory -Path $previousOutput | Out-Null
        $previousArguments = @('/Qp', '/DIsolationTest', ('/DPreviousVersion="' + $previousVersion + '"'), ('/DTestRoot="' + $TestRoot + '"'), ('/DPayloadDir="' + $previousPayload + '"'), ('/DSetupOutputDir="' + $previousOutput + '"'))
        if (Test-Path -LiteralPath (Join-Path $previousPayload 'LICENSE.txt')) { $previousArguments += '/DPreviousHasLicense' }
        $previousArguments += ('"' + $setupScript + '"')
        Invoke-CheckedProcess $CompilerPath $previousArguments 'Prior version isolated installer compilation'
        $previousSetup = Join-Path $previousOutput 'HolidayTimerSetup-Test.exe'
        Assert-Check (Test-Path -LiteralPath $previousSetup) 'Prior version installer exists for the upgrade check'
    }

    Hold-TestMutex
    try {
        Invoke-Install 'blocked-install.log' $false
        Assert-Check (-not (Test-Path -LiteralPath $installRoot)) 'A running instance blocks installation before writing payload'
        Assert-Check ((Get-RegistrySnapshot $testUninstallPath) -eq '<absent>') 'Blocked installation creates no Apps entry'
    } finally { Release-TestMutex }

    if ($previousSetup) { $isolatedSetup = $previousSetup }
    Invoke-Install 'install.log'
    if ($previousSetup) { Assert-Payload $previousPayload; Assert-AppsEntry $previousVersion }
    else { Assert-Payload; Assert-AppsEntry }
    Assert-Check (Test-Path -LiteralPath $shortcutPath -PathType Leaf) 'The isolated Start menu shortcut exists'
    $shortcutShell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shortcutShell.CreateShortcut($shortcutPath)
        Assert-Check ($shortcut.TargetPath -eq (Join-Path $installRoot 'HolidayTimer.exe')) 'Shortcut launches the installed app'
    } finally {
        if ($null -ne $shortcut) { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) | Out-Null }
        [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcutShell) | Out-Null
    }
    $ownedRun = '"' + (Join-Path $installRoot 'HolidayTimer.exe') + '" --startup'
    Assert-Check ((Get-IsolatedRun) -eq $ownedRun) 'Setup registers the installed app for the next sign-in'

    New-Item -ItemType Directory -Path $preferencesRoot | Out-Null
    [IO.File]::WriteAllText((Join-Path $preferencesRoot 'settings.json'), '{"sentinel":"keep my countdown","StartAtLogin":false}')
    [IO.File]::WriteAllText((Join-Path $preferencesRoot 'settings.json.bak'), 'backup countdown sentinel')
    [IO.File]::WriteAllText((Join-Path $preferencesRoot 'public-holidays.json'), 'cached calendar sentinel')
    [IO.File]::WriteAllText((Join-Path $preferencesRoot 'public-holiday-edits.json'), 'personal holiday edits sentinel')
    $preferenceHashes = @((Get-FileSnapshot (Join-Path $preferencesRoot 'settings.json')), (Get-FileSnapshot (Join-Path $preferencesRoot 'settings.json.bak')))
    $calendarHashes = @((Get-FileSnapshot (Join-Path $preferencesRoot 'public-holidays.json')), (Get-FileSnapshot (Join-Path $preferencesRoot 'public-holiday-edits.json')))
    Set-IsolatedRun $null
    $unknownFile = Join-Path $installRoot 'my-personal-note.txt'
    [IO.File]::WriteAllText($unknownFile, 'Uninstall must leave unknown files alone.')
    $unknownHash = Get-FileSnapshot $unknownFile
    $isolatedSetup = $updatedSetup
    Invoke-Install 'update.log'
    Assert-Payload
    Assert-AppsEntry
    Assert-Check ((Get-FileSnapshot $unknownFile) -eq $unknownHash) 'Updating preserves an unknown user file'
    Assert-Check ($null -eq (Get-IsolatedRun)) 'Updating keeps a previously disabled sign-in preference'
    Assert-Check ((Get-FileSnapshot (Join-Path $preferencesRoot 'settings.json')) -eq $preferenceHashes[0]) 'Updating preserves saved countdown preferences'
    Assert-Check ((Get-FileSnapshot (Join-Path $preferencesRoot 'public-holidays.json')) -eq $calendarHashes[0] -and (Get-FileSnapshot (Join-Path $preferencesRoot 'public-holiday-edits.json')) -eq $calendarHashes[1]) 'Updating preserves cached holidays and personal edits'
    Hold-TestMutex
    try {
        $handoff = Start-Process -FilePath $isolatedSetup -ArgumentList @('/AUTOUPDATE','/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -PassThru -WindowStyle Hidden
        Start-Sleep -Milliseconds 900
        Assert-Check (-not $handoff.HasExited) 'Automatic update waits for the app mutex instead of forcing it closed'
    } finally { Release-TestMutex }
    Assert-Check ($handoff.WaitForExit(30000)) 'Automatic update finishes after the old app exits'
    Assert-Check ($handoff.ExitCode -eq 0) 'Automatic update installer exits successfully'
    Assert-Payload
    Assert-Check ((Get-FileSnapshot (Join-Path $preferencesRoot 'public-holiday-edits.json')) -eq $calendarHashes[1]) 'Automatic update preserves personal holiday edits'
    Set-IsolatedRun $ownedRun

    Hold-TestMutex
    try {
        Invoke-Uninstall 'blocked-uninstall.log' $false
        Assert-Payload
        Assert-AppsEntry
        Assert-Check ((Get-IsolatedRun) -eq $ownedRun) 'Blocked uninstall preserves startup registration'
    } finally { Release-TestMutex }

    Invoke-Uninstall 'uninstall-owned-run.log'
    foreach ($name in @('HolidayTimer.exe', 'HolidayTimer.exe.config', 'Inter-LICENSE.txt', 'LICENSE.txt', 'README.md', 'unins000.exe', 'unins000.dat')) {
        Assert-Check (-not (Test-Path -LiteralPath (Join-Path $installRoot $name))) ('Uninstall removes owned file: ' + $name)
    }
    Assert-Check ((Get-RegistrySnapshot $testUninstallPath) -eq '<absent>') 'Uninstall removes its Windows Apps entry'
    Assert-Check ($null -eq (Get-IsolatedRun)) 'Uninstall removes the startup command it owns'
    Assert-Check (-not (Test-Path -LiteralPath $shortcutPath)) 'Uninstall removes its shortcut'
    Assert-Check ((Get-FileSnapshot $unknownFile) -eq $unknownHash) 'Uninstall preserves an unknown user file'
    Assert-Check ((Get-FileSnapshot (Join-Path $preferencesRoot 'settings.json')) -eq $preferenceHashes[0]) 'Uninstall retains saved countdown preferences'
    Assert-Check ((Get-FileSnapshot (Join-Path $preferencesRoot 'settings.json.bak')) -eq $preferenceHashes[1]) 'Uninstall retains the preferences backup'
    Assert-Check ((Get-FileSnapshot (Join-Path $preferencesRoot 'public-holidays.json')) -eq $calendarHashes[0] -and (Get-FileSnapshot (Join-Path $preferencesRoot 'public-holiday-edits.json')) -eq $calendarHashes[1]) 'Uninstall retains cached holidays and personal edits'

    Invoke-Install 'reinstall.log'
    Assert-Payload
    Assert-Check ($null -eq (Get-IsolatedRun)) 'Reinstalling preserves the retained sign-in preference'
    $portableRun = '"' + (Join-Path $TestRoot 'portable\HolidayTimer.exe') + '" --startup'
    Set-IsolatedRun $portableRun
    Invoke-Uninstall 'uninstall-portable-run.log'
    Assert-Check ((Get-IsolatedRun) -eq $portableRun) 'Uninstall preserves startup redirected to a portable copy'
    Assert-Check ((Get-RegistrySnapshot $testUninstallPath) -eq '<absent>') 'The reinstall cycle leaves no isolated Apps entry'
    Assert-Check ((Get-FileSnapshot $unknownFile) -eq $unknownHash) 'The second uninstall still preserves an unknown file'
    Assert-Check ((Get-FileSnapshot (Join-Path $preferencesRoot 'settings.json')) -eq $preferenceHashes[0]) 'The reinstall cycle preserves saved preferences'

    Assert-Check ((Get-PersonalSnapshot) -eq $personalBefore) 'Personal preferences, startup, Apps entries, shortcuts, and the running app remain untouched'
    $result = [ordered]@{ Passed = $checks; TestRoot = $TestRoot; SetupHash = Get-FileSnapshot $isolatedSetup; Payload = $PayloadDirectory; PreviousVersion = $previousVersion; UpgradeVersion = '1.8.0'; PersonalStateUnchanged = $true }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $TestRoot 'result.json') -Encoding UTF8
    Write-Output ('Installer acceptance checks passed: ' + $checks)
    Write-Output ('Evidence: ' + (Join-Path $TestRoot 'result.json'))
} finally {
    Release-TestMutex
    if ((Get-RegistrySnapshot $testUninstallPath) -ne '<absent>') {
        $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($testUninstallPath)
        try { $belongsToTest = [string]$key.GetValue('InstallLocation') -and [IO.Path]::GetFullPath([string]$key.GetValue('InstallLocation')).TrimEnd('\') -eq $installRoot }
        finally { $key.Dispose() }
        if ($belongsToTest -and (Test-Path -LiteralPath (Join-Path $installRoot 'unins000.exe'))) {
            Invoke-Uninstall 'cleanup-uninstall.log'
        }
    }
    # This namespace was required to be absent before the harness created it.
    # Delete only the known value, then the empty key; never recurse through registry or files.
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($testRegistryPath, $true)
    if ($null -ne $key) {
        try { $key.DeleteValue('HolidayTimer', $false); $empty = $key.ValueCount -eq 0 -and $key.SubKeyCount -eq 0 }
        finally { $key.Dispose() }
        if ($empty) { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKey($testRegistryPath, $false) }
    }
}
