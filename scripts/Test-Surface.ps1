param([string]$Executable,[string]$ReportDirectory)
$ErrorActionPreference='Stop'
$probeRoot=Split-Path -Parent $PSScriptRoot
if(-not $Executable){$Executable=Join-Path $probeRoot 'src\DesktopSpaces.App\bin\Release\net8.0-windows\DesktopSpaces.exe'}
if(-not $ReportDirectory){$ReportDirectory=Join-Path $probeRoot 'artifacts\surface-smoke'}
$probeArgs=@('--surface-probe','--exercise-drop',('"--report-dir='+[IO.Path]::GetFullPath($ReportDirectory)+'"'))
$probe=Start-Process -FilePath $Executable -ArgumentList $probeArgs -PassThru -WindowStyle Hidden
if(-not $probe.WaitForExit(25000)){Stop-Process -Id $probe.Id;throw 'Surface/OLE probe timed out.'}
$report=Get-Content (Join-Path $ReportDirectory 'report.json') -Raw | ConvertFrom-Json
if($probe.ExitCode -ne 0 -or -not $report.Success){throw 'Surface/OLE probe failed; inspect report.json.'}
Write-Output 'OLE background drop, same-folder icon movement and live-background colour verified.'
