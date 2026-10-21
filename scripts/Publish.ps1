param([ValidateSet('win-x64')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$publishRoot = Split-Path -Parent $PSScriptRoot
Push-Location $publishRoot
try {
    dotnet build DesktopSpaces.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet run --project tests/DesktopSpaces.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & "$PSScriptRoot\Test-Shell.ps1"
    $publishDirectory = Join-Path $publishRoot "artifacts\DesktopSpaces-$Runtime"
    dotnet publish src/DesktopSpaces.App/DesktopSpaces.App.csproj -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugSymbols=false -p:DebugType=None -o $publishDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath (Join-Path $publishRoot 'LICENSE') -Destination $publishDirectory
    Copy-Item -LiteralPath (Join-Path $publishRoot 'README.md') -Destination $publishDirectory
    Copy-Item -LiteralPath (Join-Path $publishRoot 'CONTRIBUTING.md') -Destination $publishDirectory
    Copy-Item -LiteralPath (Join-Path $publishRoot 'PLAN.md') -Destination $publishDirectory
    $publishDocs = Join-Path $publishDirectory 'docs'
    New-Item -ItemType Directory -Force -Path $publishDocs | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $publishRoot 'docs') -Filter '*.md' -File | Copy-Item -Destination $publishDocs
    $noticeDirectory = Join-Path $publishDirectory 'third-party-notices'
    New-Item -ItemType Directory -Force -Path $noticeDirectory | Out-Null
    $packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
    foreach ($package in @("microsoft.netcore.app.runtime.$Runtime", "microsoft.windowsdesktop.app.runtime.$Runtime")) {
        $packageRoot = Join-Path $packageCache $package
        $packageVersion = Get-ChildItem -LiteralPath $packageRoot -Directory | Where-Object Name -Match '^8\.0\.[0-9]+$' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
        if (-not $packageVersion) { throw "Missing runtime notices: $package" }
        $packageNotices = Get-ChildItem -LiteralPath $packageVersion.FullName -File | Where-Object Name -Match '^(LICENSE|THIRD-PARTY-NOTICES)(\.|$)'
        if (-not $packageNotices) { throw "No redistributable notices found: $package" }
        foreach ($noticeFile in $packageNotices) { Copy-Item -LiteralPath $noticeFile.FullName -Destination (Join-Path $noticeDirectory ($package + '-' + $noticeFile.Name)) }
    }
    # Explicitly select release inputs: previous PDBs and diagnostics are not distributed.
    $publishInputs = @((Join-Path $publishDirectory 'DesktopSpaces.exe'),(Join-Path $publishDirectory 'LICENSE'),(Join-Path $publishDirectory 'README.md'),(Join-Path $publishDirectory 'CONTRIBUTING.md'),(Join-Path $publishDirectory 'PLAN.md'),$publishDocs,$noticeDirectory)
    $publishArchive = Join-Path $publishRoot "artifacts\DesktopSpaces-0.1.0-$Runtime.zip"
    Compress-Archive -Path $publishInputs -DestinationPath $publishArchive -Force
    $publishHash = Get-FileHash -LiteralPath $publishArchive -Algorithm SHA256
    ($publishHash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($publishArchive)) | Set-Content -LiteralPath ($publishArchive + '.sha256') -Encoding ASCII
    Write-Output $publishArchive
} finally { Pop-Location }
