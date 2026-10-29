#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sqlPath = Join-Path $PSScriptRoot 'setup.sql'

function Find-Psql {
    $fromPath = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }

    $programFiles = Get-ChildItem 'C:\Program Files\PostgreSQL' -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending
    foreach ($folder in $programFiles) {
        $candidate = Join-Path $folder.FullName 'bin\psql.exe'
        if (Test-Path $candidate) { return $candidate }
    }

    throw 'psql.exe was not found. Install PostgreSQL or add its bin directory to PATH.'
}

function Read-EnvValue {
    param([string]$Path, [string]$Name)
    if (-not (Test-Path $Path)) { return $null }
    $line = Get-Content $Path | Where-Object { $_ -match "^\s*$([regex]::Escape($Name))=" } | Select-Object -First 1
    if (-not $line) { return $null }
    return (($line -split '=', 2)[1]).Trim()
}

$psql = Find-Psql
$superPassword = Read-EnvValue -Path (Join-Path $repoRoot '.env') -Name 'POSTGRES_PASSWORD'
if ([string]::IsNullOrWhiteSpace($superPassword)) {
    $secure = Read-Host 'PostgreSQL postgres superuser password' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        $superPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Invoke-Setup {
    param([string]$Password)
    $env:PGPASSWORD = $Password
    try {
        $output = & $psql -h localhost -p 5432 -U postgres -d postgres -v ON_ERROR_STOP=1 -f $sqlPath 2>&1
        $code = $LASTEXITCODE
        $output | ForEach-Object { Write-Host $_ }
        return $code
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

$exitCode = Invoke-Setup -Password $superPassword
if ($exitCode -ne 0) {
    Write-Host 'The configured postgres password did not work. Enter the installed PostgreSQL superuser password.' -ForegroundColor Yellow
    $secure = Read-Host 'PostgreSQL postgres superuser password' -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        $superPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }

    $exitCode = Invoke-Setup -Password $superPassword
}

if ($exitCode -ne 0) {
    throw "Database setup failed with exit code $exitCode."
}

$connection = 'Host=localhost;Port=5432;Database=securevote;Username=securevote_app;Password=change-me-local'
$testConnection = 'Host=localhost;Port=5432;Database=securevote_test;Username=securevote_app;Password=change-me-local'
$localEnv = Join-Path $repoRoot '.env.local'
$lines = [System.Collections.Generic.List[string]]::new()
if (Test-Path $localEnv) {
    foreach ($line in @(Get-Content $localEnv)) { $lines.Add($line) }
}

foreach ($item in @(
    @{ Name = 'ConnectionStrings__Default'; Value = $connection },
    @{ Name = 'SECUREVOTING_TEST_CONNECTION'; Value = $testConnection },
    @{ Name = 'ASPNETCORE_ENVIRONMENT'; Value = 'Development' },
    @{ Name = 'Seed__Enabled'; Value = 'true' },
    @{ Name = 'Seed__DemoPassword'; Value = 'DemoPass!2026Secure' }
)) {
    $matching = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^\s*$([regex]::Escape($item.Name))=") {
            $matching = $i
            break
        }
    }

    $line = "$($item.Name)=$($item.Value)"
    if ($matching -ge 0) {
        $lines[$matching] = $line
    }
    else {
        $lines.Add($line)
    }
}

Set-Content -LiteralPath $localEnv -Value $lines -Encoding ASCII
Write-Host 'SecureVote local PostgreSQL setup complete.' -ForegroundColor Green
Write-Host 'Created/updated .env.local (ignored by source control).' -ForegroundColor Yellow
Write-Host 'Next: run .\run.cmd' -ForegroundColor Yellow
