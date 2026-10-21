# Opt-in regression: temporarily changes the real Desktop Known Folder. Keep out of CI.
param([string]$Executable,[string]$ReportDirectory)
$ErrorActionPreference='Stop'
$transferRoot=Split-Path -Parent $PSScriptRoot
if(-not $Executable){$Executable=Join-Path $transferRoot 'src\DesktopSpaces.App\bin\Release\net8.0-windows\DesktopSpaces.exe'}
if(-not $ReportDirectory){$ReportDirectory=Join-Path $transferRoot 'artifacts\transfer-regression'}
$journal=Join-Path $env:LOCALAPPDATA 'DesktopSpaces\recovery.json'
if(Test-Path -LiteralPath $journal){throw 'Exit DesktopSpaces before running the transfer regression.'}
$reportPath=Join-Path $ReportDirectory 'report.json'
$transferArgs=@('--surface-probe','--exercise-drop','--redirect-during-drop','--round-trip',('"--report-dir='+[IO.Path]::GetFullPath($ReportDirectory)+'"'))
$transferProcess=Start-Process -FilePath $Executable -ArgumentList $transferArgs -PassThru -WindowStyle Hidden
try {
    if(-not $transferProcess.WaitForExit(25000)){throw 'Transfer regression timed out.'}
    $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $paths=Get-Content -LiteralPath (Join-Path $ReportDirectory 'source-paths.json') -Raw | ConvertFrom-Json
    if($transferProcess.ExitCode -ne 0 -or -not $report.Success -or -not $paths.AllSourcesExist -or (Compare-Object $paths.Before $paths.After)){throw 'Paths changed or transfer failed.'}
    if(-not $report.Imported -or -not $report.Returned){throw 'Both transfer directions were not verified.'}
    $content=Get-Content -LiteralPath (Join-Path $report.Source 'Перенести сюда.txt') -Raw
    if($content -ne 'OLE drag and drop check'){throw 'Transferred content differs.'}
} finally {
    if(-not $transferProcess.HasExited){Stop-Process -Id $transferProcess.Id}
    $deadline=[DateTime]::UtcNow.AddSeconds(10)
    while((Test-Path -LiteralPath $journal) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
    if(Test-Path -LiteralPath $journal){throw 'Desktop restoration has not completed.'}
}
Write-Output 'Source paths and file content remain correct across real Desktop redirection.'
