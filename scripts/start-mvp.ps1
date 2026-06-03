Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

Write-Host "Starting PostgreSQL..."
docker compose -f (Join-Path $root "infra\docker-compose.yml") up -d

Write-Host "Starting API on http://localhost:5000..."
$env:ASPNETCORE_ENVIRONMENT = "Development"
if ([string]::IsNullOrWhiteSpace($env:KIMI_API_KEY)) {
  $env:KIMI_API_KEY = [Environment]::GetEnvironmentVariable("KIMI_API_KEY", "User")
}
Start-Process -FilePath "dotnet" `
  -ArgumentList "run --project apps\api" `
  -WorkingDirectory $root `
  -WindowStyle Hidden

Start-Sleep -Seconds 5

Write-Host "Starting Web on http://127.0.0.1:5173..."
Start-Process -FilePath "npm.cmd" `
  -ArgumentList "run dev -- --host 127.0.0.1 --port 5173" `
  -WorkingDirectory (Join-Path $root "apps\web") `
  -WindowStyle Hidden

Write-Host ""
Write-Host "JobPilot MVP is starting:"
Write-Host "  API: http://localhost:5000/health"
Write-Host "  Web: http://127.0.0.1:5173"
Write-Host ""
Write-Host "Login:"
Write-Host "  admin@jobpilot.local"
Write-Host "  admin123"
