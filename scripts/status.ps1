Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "lib\dev-common.ps1")

Set-JobPilotDevEnvironment
$status = Get-JobPilotDevStatus
Write-JobPilotDevStatus -Status $status

if (-not $status.Ready) {
  exit 1
}
