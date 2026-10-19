Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "lib\quality-common.ps1")

Push-Location $root
try {
  Invoke-QualityCommand (Join-Path $root "scripts\architecture-check.ps1")
  Invoke-QualityCommand dotnet -Arguments @('format', 'JobPilot.slnx', '--verify-no-changes', '--no-restore')

  # Testes do backend.
  Invoke-QualityCommand dotnet -Arguments @('test', 'apps/api.tests', '--configuration', 'Release', '--collect:XPlat Code Coverage')

  # Migrations precisam gerar script idempotente (valida o modelo sem banco).
  $migrationsSql = Join-Path $root ".build\migrations.sql"
  New-Item -ItemType Directory -Force -Path (Split-Path -Parent $migrationsSql) | Out-Null
  Invoke-QualityEnvironment @{
    ASPNETCORE_ENVIRONMENT = "Development"
    Auth__JwtKey = "quality-check-jwt-key-with-more-than-thirty-two-characters"
    Security__SecretKey = "quality-check-secret-key-with-more-than-thirty-two-characters"
    ConnectionStrings__DefaultConnection = "Host=localhost;Database=qualitycheck"
  } {
    Invoke-QualityCommand dotnet -Arguments @('ef', 'migrations', 'script', '--idempotent', '--project', 'apps/api', '--startup-project', 'apps/api', '--configuration', 'Release', '--no-build', '--output', $migrationsSql)
  }

  # Higiene de segredos: chaves privadas e IDs de acesso nunca entram no repo.
  # git grep: 0 = matches, 1 = no matches; other codes are execution failures.
  $leaks = Invoke-QualityCommand git -Arguments @('grep', '-nE', 'BEGIN [A-Z ]*PRIVATE KEY|AKIA[0-9A-Z]{16}|xox[bpas]-', '--', '.', ':!package-lock.json') -SuccessExitCodes @(0, 1)
  if ($leaks) { throw "Possível segredo no repo:`n$leaks" }

  Push-Location (Join-Path $root "apps\web")
  try {
    Invoke-QualityCommand npm -Arguments @('ci')
    Invoke-QualityCommand npm -Arguments @('run', 'check')
  }
  finally {
    Pop-Location
  }

  if (Get-Command docker -ErrorAction SilentlyContinue) {
    Invoke-QualityEnvironment @{
      POSTGRES_PASSWORD = "quality-check-password"
      JOBPILOT_JWT_KEY = "quality-check-jwt-key-with-more-than-thirty-two-characters"
      JOBPILOT_BOOTSTRAP_EMAIL = "quality-check@example.com"
      JOBPILOT_BOOTSTRAP_PASSWORD = "quality-check-bootstrap-password"
      JOBPILOT_SECRET_KEY = "quality-check-secret-key-with-more-than-thirty-two-characters"
    } {
      Invoke-QualityCommand docker -Arguments @('compose', '-f', (Join-Path $root "infra\docker-compose.production.yml"), 'config', '--quiet')
    }
  }
  else {
    Write-Warning "Docker não encontrado; validação do compose de produção pulada."
  }
}
finally {
  Pop-Location
}
