# One-time developer setup (Windows PowerShell). Run from the repository root:  .\scripts\setup-dev.ps1
$ErrorActionPreference = 'Stop'
Write-Host '== MiniERP dev setup ==' -ForegroundColor Cyan
dotnet --version | Out-Null; node --version | Out-Null

$api = Join-Path $PSScriptRoot '..\backend\MiniERP.Api'
Push-Location $api
$bytes = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$key = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:Key" $key | Out-Null
Write-Host 'JWT signing key stored in user-secrets (not in source control).'

if (-not (Get-Command dotnet-ef -ErrorAction SilentlyContinue)) { dotnet tool install --global dotnet-ef }
if (-not (Test-Path (Join-Path $api 'Migrations'))) {
  dotnet ef migrations add InitialCreate
}
dotnet ef database update
Pop-Location

Push-Location (Join-Path $PSScriptRoot '..\frontend')
if (-not (Test-Path '.env')) { Copy-Item '.env.example' '.env' }
npm install
Pop-Location
Write-Host 'Done. Start the API (dotnet run) and the web app (npm run dev). See README.md.' -ForegroundColor Green
