# This opt-in local check temporarily redirects the current user's Desktop.
# Do not run as part of CI; no existing user files are moved or deleted.
param([string]$Executable, [switch]$SimulateCrash)
$ErrorActionPreference = 'Stop'
$desktopTestRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $desktopTestRoot 'src\DesktopSpaces.App\bin\Release\net8.0-windows\DesktopSpaces.exe' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$desktopTestReportDir = Join-Path $desktopTestRoot 'artifacts\desktop-integration'
New-Item -ItemType Directory -Force -Path $desktopTestReportDir | Out-Null
$desktopTestJournal = Join-Path $env:LOCALAPPDATA 'DesktopSpaces\recovery.json'
if (Test-Path -LiteralPath $desktopTestJournal) { throw 'An active or unfinished session exists; integration check not started.' }
$desktopTestOriginalFile = Join-Path $desktopTestReportDir 'original.txt'
$desktopTestOriginalReader = Start-Process -FilePath $Executable -ArgumentList '--read-desktop', ('"' + $desktopTestOriginalFile + '"') -PassThru -WindowStyle Hidden
if (-not $desktopTestOriginalReader.WaitForExit(10000)) { throw 'Original path read timed out.' }
$desktopTestOriginal = Get-Content -LiteralPath $desktopTestOriginalFile -Raw
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
    $desktopTestConfigPath = Join-Path $env:LOCALAPPDATA 'DesktopSpaces\settings.json'
    do {
        Start-Sleep -Milliseconds 100
        $desktopTestConfig = Get-Content -LiteralPath $desktopTestConfigPath -Raw | ConvertFrom-Json
        if ([DateTime]::UtcNow -gt $desktopTestDeadline) { throw 'Space 2 activation timed out.' }
    } until ($desktopTestConfig.ActiveSpaceId -eq $desktopTestConfig.Spaces[1].Id)
    $desktopTestReadFile = Join-Path $desktopTestReportDir 'active.txt'
    $desktopTestReader = Start-Process -FilePath $Executable -ArgumentList '--read-desktop', ('"' + $desktopTestReadFile + '"') -PassThru -WindowStyle Hidden
    if (-not $desktopTestReader.WaitForExit(10000)) { throw 'Active desktop read timed out.' }
    $desktopTestActive = Get-Content -LiteralPath $desktopTestReadFile -Raw
    if ($desktopTestActive -ne $desktopTestConfig.Spaces[1].Folder) { throw 'Active known folder and displayed space differ.' }
    $desktopTestReport.ActivePath = $desktopTestActive
    $desktopTestProcess.Refresh()
    $desktopTestReport.NoMainTaskbarWindow = $desktopTestProcess.MainWindowHandle -eq 0
    if (-not $desktopTestReport.NoMainTaskbarWindow) { throw 'An unexpected main taskbar window exists.' }
    $desktopTestStatusProcess = Start-Process -FilePath $Executable -ArgumentList '--status' -PassThru -WindowStyle Hidden
    [void]$desktopTestStatusProcess.WaitForExit(10000)
    $desktopTestStatusFile = Join-Path $env:LOCALAPPDATA 'DesktopSpaces\status.json'
    $desktopTestStatusDeadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        Start-Sleep -Milliseconds 100
        if ([DateTime]::UtcNow -gt $desktopTestStatusDeadline) { throw 'Surface status timed out.' }
        $desktopTestStatus = if (Test-Path -LiteralPath $desktopTestStatusFile) { Get-Content -LiteralPath $desktopTestStatusFile -Raw | ConvertFrom-Json } else { $null }
    } until ($desktopTestStatus -and $desktopTestStatus.ProcessId -eq $desktopTestProcess.Id)
    $desktopTestReport.SurfaceAttached = $desktopTestStatus.Attached
    $desktopTestReport.SurfaceVisible = $desktopTestStatus.SurfaceVisible
    $desktopTestReport.FolderViewReady = $desktopTestStatus.FolderViewReady
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
    $desktopTestReport | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $desktopTestReportDir 'report.json') -Encoding UTF8
}
if (-not $desktopTestReport.Success -or -not $desktopTestReport.OriginalRestored -or -not $desktopTestReport.JournalCleared) { throw 'Desktop integration failed; inspect the local report and recovery journal.' }
Write-Output 'Desktop surface, space 2 known-folder routing and exit restoration verified.'
