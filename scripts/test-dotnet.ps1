$ErrorActionPreference = 'Stop'

$dotnetPath = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($dotnetPath)) {
    $dotnetPath = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet-sdk\dotnet.exe'
}
if (-not (Test-Path -LiteralPath $dotnetPath)) {
    throw 'dotnet SDK not found.'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$platformSolution = Join-Path $repoRoot 'Mobile Application Cloud-Based Remote Management\CafeteriaRemotePlatform\CafeteriaRemotePlatform.sln'

Push-Location -LiteralPath $repoRoot
try {
    & $dotnetPath test 'SmartCafeteria.sln' --nologo --no-build --no-restore --settings 'SmartCafeteria.runsettings' -m:1
    if ($LASTEXITCODE -ne 0) { throw "Desktop tests failed with exit code $LASTEXITCODE." }

    & $dotnetPath test $platformSolution --nologo --no-build --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw "Platform tests failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}
