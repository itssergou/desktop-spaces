# This opt-in check operates the real desktop host and verifies a fixed Known Folder.
# Do not run as part of CI; no existing user files are moved or deleted.
param([string]$Executable, [switch]$SimulateCrash)
$ErrorActionPreference = 'Stop'
function Read-TestJson([string]$Path) {
    $stream = [IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try { $reader = [IO.StreamReader]::new($stream); return ($reader.ReadToEnd() | ConvertFrom-Json) }
    finally { if ($reader) { $reader.Dispose() }; $stream.Dispose() }
}
$desktopTestRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $desktopTestRoot 'src\DesktopSpaces.App\bin\Release\net8.0-windows\DesktopSpaces.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$desktopTestReportDir = Join-Path $desktopTestRoot 'artifacts\desktop-integration'
New-Item -ItemType Directory -Force -Path $desktopTestReportDir | Out-Null
$desktopTestJournal = Join-Path $env:USERPROFILE '.desktop-spaces\recovery.json'
if (Test-Path -LiteralPath $desktopTestJournal) { throw 'An active or unfinished session exists; integration check not started.' }
$desktopTestOriginalFile = Join-Path $desktopTestReportDir 'original.txt'
$desktopTestOriginalReader = Start-Process -FilePath $Executable -ArgumentList '--read-desktop', ('"' + $desktopTestOriginalFile + '"') -PassThru -WindowStyle Hidden
if (-not $desktopTestOriginalReader.WaitForExit(10000)) { throw 'Original path read timed out.' }
$desktopTestOriginal = Get-Content -LiteralPath $desktopTestOriginalFile -Raw
$desktopTestSurfaceBeforeFile = Join-Path $desktopTestReportDir 'surface-before.json'
$desktopTestSurfaceReader = Start-Process -FilePath $Executable -ArgumentList '--read-surface', ('"' + $desktopTestSurfaceBeforeFile + '"') -PassThru -WindowStyle Hidden
[void]$desktopTestSurfaceReader.WaitForExit(10000)
$desktopTestSurfaceBefore = Read-TestJson $desktopTestSurfaceBeforeFile
$desktopTestProcess = $null
$desktopTestReport = [ordered]@{ Success = $false; OriginalPath = $desktopTestOriginal }
$desktopTestReport.SimulatedCrash = [bool]$SimulateCrash
try {
    $desktopTestProcess = Start-Process -FilePath $Executable -PassThru -WindowStyle Hidden
    $desktopTestDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath $desktopTestJournal)) {
        if ($desktopTestProcess.HasExited -or [DateTime]::UtcNow -gt $desktopTestDeadline) { throw 'Application session did not start.' }
        Start-Sleep -Milliseconds 100
    }
    $desktopTestCommand = Start-Process -FilePath $Executable -ArgumentList '--switch=2' -PassThru -WindowStyle Hidden
    [void]$desktopTestCommand.WaitForExit(10000)
    $desktopTestConfigPath = Join-Path $env:USERPROFILE '.desktop-spaces\settings.json'
    $desktopTestDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 100
        $desktopTestStatusProcess = Start-Process -FilePath $Executable -ArgumentList '--status' -PassThru -WindowStyle Hidden
        [void]$desktopTestStatusProcess.WaitForExit(5000)
        $desktopTestStatusFile = Join-Path $env:USERPROFILE '.desktop-spaces\status.json'
        $desktopTestLiveStatus = if (Test-Path -LiteralPath $desktopTestStatusFile) { Read-TestJson $desktopTestStatusFile } else { $null }
        $desktopTestConfig = Read-TestJson $desktopTestConfigPath
        if ([DateTime]::UtcNow -gt $desktopTestDeadline) { throw 'Space 2 activation timed out.' }
    } until ($desktopTestLiveStatus -and $desktopTestLiveStatus.ProcessId -eq $desktopTestProcess.Id -and $desktopTestLiveStatus.ActiveSpaceId -eq $desktopTestConfig.Spaces[1].Id -and $desktopTestLiveStatus.FolderViewReady)
    $desktopTestReadFile = Join-Path $desktopTestReportDir 'active.txt'
    $desktopTestReader = Start-Process -FilePath $Executable -ArgumentList '--read-desktop', ('"' + $desktopTestReadFile + '"') -PassThru -WindowStyle Hidden
    if (-not $desktopTestReader.WaitForExit(10000)) { throw 'Active desktop read timed out.' }
    $desktopTestActive = Get-Content -LiteralPath $desktopTestReadFile -Raw
    if ($desktopTestActive -ne $desktopTestOriginal) { throw 'Switching changed the fixed Desktop known folder.' }
    $desktopTestReport.ActivePath = $desktopTestActive
    $desktopTestProcess.Refresh()
    $desktopTestReport.NoMainTaskbarWindow = $desktopTestProcess.MainWindowHandle -eq 0
    if (-not $desktopTestReport.NoMainTaskbarWindow) { throw 'An unexpected main taskbar window exists.' }
    $desktopTestStatusProcess = Start-Process -FilePath $Executable -ArgumentList '--status' -PassThru -WindowStyle Hidden
    [void]$desktopTestStatusProcess.WaitForExit(10000)
    $desktopTestStatusFile = Join-Path $env:USERPROFILE '.desktop-spaces\status.json'
    $desktopTestStatusDeadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 100
        if ([DateTime]::UtcNow -gt $desktopTestStatusDeadline) { throw 'Surface status timed out.' }
        $desktopTestStatus = if (Test-Path -LiteralPath $desktopTestStatusFile) { Read-TestJson $desktopTestStatusFile } else { $null }
    } until ($desktopTestStatus -and $desktopTestStatus.ProcessId -eq $desktopTestProcess.Id)
    $desktopTestReport.SurfaceAttached = $desktopTestStatus.Attached
    $desktopTestReport.SurfaceVisible = $desktopTestStatus.SurfaceVisible
    $desktopTestReport.FolderViewReady = $desktopTestStatus.FolderViewReady
    $desktopTestReport.OriginalIconsHidden = -not $desktopTestStatus.NativeIconsVisible
    $desktopTestReport.SwitchMilliseconds = $desktopTestStatus.LastSwitchMilliseconds
    if (-not $desktopTestStatus.Attached -or -not $desktopTestStatus.SurfaceVisible -or -not $desktopTestStatus.FolderViewReady) { throw 'Native desktop surface is not ready and visible.' }
    $desktopTestReport.Success = $true
} catch {
    $desktopTestReport.Error = $_.Exception.Message
} finally {
    if ($desktopTestProcess -and -not $desktopTestProcess.HasExited) {
        if ($SimulateCrash) { $desktopTestProcess.Kill() }
        else {
            $desktopTestExit = Start-Process -FilePath $Executable -ArgumentList '--exit' -PassThru -WindowStyle Hidden
            [void]$desktopTestExit.WaitForExit(10000)
        }
        if (-not $desktopTestProcess.WaitForExit(10000)) { $desktopTestProcess.Kill(); [void]$desktopTestProcess.WaitForExit(5000) }
    }
    $desktopTestDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while ((Test-Path -LiteralPath $desktopTestJournal) -and [DateTime]::UtcNow -lt $desktopTestDeadline) { Start-Sleep -Milliseconds 100 }
    $desktopTestRestoredFile = Join-Path $desktopTestReportDir 'restored.txt'
    $desktopTestRestoredReader = Start-Process -FilePath $Executable -ArgumentList '--read-desktop', ('"' + $desktopTestRestoredFile + '"') -PassThru -WindowStyle Hidden
    [void]$desktopTestRestoredReader.WaitForExit(10000)
    $desktopTestReport.OriginalRestored = (Get-Content -LiteralPath $desktopTestRestoredFile -Raw) -eq $desktopTestOriginal
    $desktopTestReport.JournalCleared = -not (Test-Path -LiteralPath $desktopTestJournal)
    $desktopTestSurfaceAfterFile = Join-Path $desktopTestReportDir 'surface-after.json'
    $desktopTestSurfaceReader = Start-Process -FilePath $Executable -ArgumentList '--read-surface', ('"' + $desktopTestSurfaceAfterFile + '"') -PassThru -WindowStyle Hidden
    [void]$desktopTestSurfaceReader.WaitForExit(10000)
    $desktopTestReport.IconVisibilityRestored = (Read-TestJson $desktopTestSurfaceAfterFile).IconsVisible -eq $desktopTestSurfaceBefore.IconsVisible
    $desktopTestReport | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $desktopTestReportDir 'report.json') -Encoding UTF8
}
if (-not $desktopTestReport.Success -or -not $desktopTestReport.OriginalRestored -or -not $desktopTestReport.JournalCleared -or -not $desktopTestReport.IconVisibilityRestored) { throw 'Desktop integration failed; inspect the local report and recovery journal.' }
Write-Output 'Desktop surface, space 2 known-folder routing and exit restoration verified.'
