<#
.SYNOPSIS
  Backup e restore do PostgreSQL do JobPilot (pg_dump custom + verificação).

.DESCRIPTION
  Rode antes de qualquer remoção de tabela legada (ARCHITECTURE.md exige
  backup + migração reversível). Usa as mesmas variáveis do compose de dev.

.EXAMPLE
  .\scripts\backup-postgres.ps1
  .\scripts\backup-postgres.ps1 -Restore .\backup\jobpilot-20260929.dump
#>
[CmdletBinding()]
param(
  [Alias("Host")]
  [string]$DatabaseHost = "127.0.0.1",
  [int]$Port = 5434,
  [string]$User = "jobpilot",
  [string]$Database = "jobpilot",
  [string]$BackupDir = (Join-Path (Split-Path -Parent $PSScriptRoot) ".build\backup"),
  [string]$Restore = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$password = $env:JOBPILOT_POSTGRES_PASSWORD
if ([string]::IsNullOrWhiteSpace($password)) {
  throw "Defina JOBPILOT_POSTGRES_PASSWORD antes de rodar (mesma senha do compose)."
}
$previousPassword = [Environment]::GetEnvironmentVariable("PGPASSWORD", "Process")
try {
  $env:PGPASSWORD = $password
  if (-not [string]::IsNullOrWhiteSpace($Restore)) {
    if (-not (Test-Path -LiteralPath $Restore)) { throw "Arquivo de restore não encontrado: $Restore" }
    & pg_restore --host $DatabaseHost --port $Port --username $User --dbname $Database --clean --if-exists --no-owner "$Restore"
    if ($LASTEXITCODE -ne 0) { throw "pg_restore falhou com código $LASTEXITCODE." }
    Write-Output "Restore concluído a partir de $Restore."
    return
  }

  New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
  $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
  $target = Join-Path $BackupDir "jobpilot-$stamp.dump"
  & pg_dump --host $DatabaseHost --port $Port --username $User --dbname $Database --format custom --file "$target"
  if ($LASTEXITCODE -ne 0) { throw "pg_dump falhou com código $LASTEXITCODE." }
  & pg_restore --list "$target" | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "Verificação do dump falhou (pg_restore --list)." }
  Write-Output "Backup verificado em $target."
}
finally {
  [Environment]::SetEnvironmentVariable("PGPASSWORD", $previousPassword, "Process")
}
