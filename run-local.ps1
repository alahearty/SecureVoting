#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Import-EnvFile {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return }
    Get-Content $Path | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]*)=(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim()
            $map = @{
                'SEED_ENABLED' = 'Seed__Enabled'
                'SEED_DEMO_PASSWORD' = 'Seed__DemoPassword'
                'EMAIL_ENABLED' = 'Email__Enabled'
                'EMAIL_HOST' = 'Email__Host'
                'EMAIL_PORT' = 'Email__Port'
                'EMAIL_USERNAME' = 'Email__UserName'
                'EMAIL_PASSWORD' = 'Email__Password'
                'EMAIL_FROM' = 'Email__FromAddress'
                'FRONTEND_BASE_URL' = 'App__FrontendBaseUrl'
            }
            if ($map.ContainsKey($name)) { $name = $map[$name] }
            Set-Item -Path "Env:$name" -Value $value
        }
    }
}

function Start-Console {
    param([string]$Title, [string]$Directory, [string]$Command)
    $safeTitle = $Title.Replace("'", "''")
    $safeDirectory = $Directory.Replace("'", "''")
    $script = @'
$Host.UI.RawUI.WindowTitle = '__TITLE__'
Set-Location -LiteralPath '__DIRECTORY__'
__COMMAND__
'@
    $script = $script.Replace('__TITLE__', $safeTitle).Replace('__DIRECTORY__', $safeDirectory).Replace('__COMMAND__', $Command)
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
    Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoExit',
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-EncodedCommand', $encoded
    ) | Out-Null
}

Import-EnvFile (Join-Path $repoRoot '.env')
Import-EnvFile (Join-Path $repoRoot '.env.local')

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Default)) {
    throw 'No local PostgreSQL connection was found. Run .\database\setup-local.ps1 first.'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK was not found. Install .NET 10 and reopen the terminal.'
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw 'Node.js/npm was not found. Install Node 22+ and reopen the terminal.'
}

$frontend = Join-Path $repoRoot 'frontend'
if (-not (Test-Path (Join-Path $frontend 'node_modules'))) {
    Write-Host 'Installing frontend dependencies...' -ForegroundColor Cyan
    Push-Location $frontend
    try { npm ci } finally { Pop-Location }
}

if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_ENVIRONMENT)) {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
}

Write-Host 'Starting SecureVote with installed PostgreSQL...' -ForegroundColor Green
Start-Console -Title 'SecureVote API (http://localhost:5000)' -Directory (Join-Path $repoRoot 'backend\src\SecureVoting.Api') -Command @'
dotnet run --no-launch-profile --urls 'http://localhost:5000'
'@

Start-Sleep -Seconds 5
Start-Console -Title 'SecureVote SPA (http://localhost:5173)' -Directory $frontend -Command 'npm run dev -- --host 127.0.0.1'
Start-Sleep -Seconds 5
Start-Process 'http://localhost:5173'
Write-Host ''
Write-Host 'API:      http://localhost:5000' -ForegroundColor Yellow
Write-Host 'Frontend: http://localhost:5173' -ForegroundColor Yellow
Write-Host 'Migrations and synthetic seed data run when the API starts.' -ForegroundColor Yellow
