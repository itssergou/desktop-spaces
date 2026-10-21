param([string]$Executable, [string]$ReportDirectory)
$ErrorActionPreference = 'Stop'
$shellTestRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $shellTestRoot 'src\DesktopSpaces.App\bin\Release\net8.0-windows\DesktopSpaces.exe' }
if (-not $ReportDirectory) { $ReportDirectory = Join-Path $shellTestRoot 'artifacts\shell-smoke' }
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$ReportDirectory = [IO.Path]::GetFullPath($ReportDirectory)
$shellTestProcess = Start-Process -FilePath $Executable -ArgumentList '--probe', ('"--report-dir=' + $ReportDirectory + '"') -PassThru -WindowStyle Hidden
if (-not $shellTestProcess.WaitForExit(20000)) {
    Stop-Process -Id $shellTestProcess.Id -ErrorAction SilentlyContinue
    throw 'Native Shell probe timed out.'
}
$shellTestReport = Get-Content -LiteralPath (Join-Path $ReportDirectory 'report.json') -Raw | ConvertFrom-Json
if (-not $shellTestReport.Success) { throw ('Native Shell probe failed: ' + ($shellTestReport.Errors -join ', ')) }
Write-Output 'Native Shell view: two entries and both icon positions verified.'
