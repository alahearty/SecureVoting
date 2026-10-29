#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$envLocal = Join-Path $repoRoot '.env.local'

if (-not (Test-Path $envLocal)) {
    throw 'Run .\database\setup-local.ps1 first to create .env.local and the securevote_test database.'
}

Get-Content $envLocal | ForEach-Object {
    if ($_ -match '^\s*([^#][^=]*)=(.*)$') {
        Set-Item -Path "Env:$($matches[1].Trim())" -Value $matches[2].Trim()
    }
}

if ([string]::IsNullOrWhiteSpace($env:SECUREVOTING_TEST_CONNECTION)) {
    throw 'SECUREVOTING_TEST_CONNECTION is missing from .env.local.'
}

dotnet build (Join-Path $repoRoot 'backend\SecureVoting.slnx') -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test (Join-Path $repoRoot 'backend\tests\SecureVoting.UnitTests\SecureVoting.UnitTests.csproj') -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test (Join-Path $repoRoot 'backend\tests\SecureVoting.IntegrationTests\SecureVoting.IntegrationTests.csproj') -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
