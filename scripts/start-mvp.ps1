Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "lib\dev-common.ps1")

Set-JobPilotDevEnvironment
$status = Get-JobPilotDevStatus
Write-JobPilotDevStatus -Status $status

if ($status.Ready -and $status.Web.Healthy) {
  Write-Host "Nada a fazer: stack ja esta no ar."
  exit 0
}

if (-not $status.Postgres.Healthy) {
  Write-Host "Subindo PostgreSQL..."
  docker compose -f $JobPilotComposeFile up -d | Out-Host

  $postgresDeadline = (Get-Date).AddSeconds(30)
  while ((Get-Date) -lt $postgresDeadline) {
    $status = Get-JobPilotDevStatus
    if ($status.Postgres.Healthy) { break }
    Start-Sleep -Seconds 1
  }

  $status = Get-JobPilotDevStatus
  if (-not $status.Postgres.Healthy) {
    throw "PostgreSQL nao ficou pronto na porta $JobPilotPostgresPort. Rode .\scripts\status.ps1 para diagnosticar."
  }

  Write-Host "PostgreSQL pronto."
}

if (-not $status.Api.Healthy) {
  Write-Host "Subindo API em nova janela..."
  Start-JobPilotProcessWindow `
    -Title "JobPilot API" `
    -WorkingDirectory $JobPilotRoot `
    -Command "dotnet run --project apps\api"

  if (-not (Wait-JobPilotApiHealth -TimeoutSeconds 60)) {
    throw "API nao respondeu em $JobPilotApiHealthUrl. Veja a janela 'JobPilot API' para o erro."
  }

  Write-Host "API pronta."
}

$status = Get-JobPilotDevStatus

if (-not $status.Web.Healthy) {
  Write-Host "Subindo Web em nova janela..."
  Start-JobPilotProcessWindow `
    -Title "JobPilot Web" `
    -WorkingDirectory (Join-Path $JobPilotRoot "apps\web") `
    -Command "npm run dev"

  $webDeadline = (Get-Date).AddSeconds(30)
  while ((Get-Date) -lt $webDeadline) {
    if (Test-JobPilotTcpPort -Port $JobPilotWebPort) { break }
    Start-Sleep -Seconds 1
  }
}

$status = Get-JobPilotDevStatus
Write-JobPilotDevStatus -Status $status

if (-not $status.Ready) {
  exit 1
}
