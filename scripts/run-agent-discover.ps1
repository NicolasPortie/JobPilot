param(
  [string]$Keywords = "",
  [string]$Location = "",
  [int]$Limit = 25
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$legacyRuntime = [Environment]::GetEnvironmentVariable("JOBPILOT_ENABLE_LEGACY_RUNTIME", "Process")
if ($legacyRuntime -ne "true") {
  throw "O runtime legado esta desativado. Use o painel web para salvar um foco e trazer a vaga manualmente."
}

$token = [Environment]::GetEnvironmentVariable("JOBPILOT_AGENT_TOKEN", "User")
$apiUrl = [Environment]::GetEnvironmentVariable("JOBPILOT_API_URL", "User")
$browserDriver = [Environment]::GetEnvironmentVariable("JOBPILOT_BROWSER_DRIVER", "User")

if ([string]::IsNullOrWhiteSpace($token)) {
  throw "JOBPILOT_AGENT_TOKEN is not configured. Pair the agent in the web app."
}

if ([string]::IsNullOrWhiteSpace($apiUrl)) {
  $apiUrl = "http://localhost:5000"
}

if ([string]::IsNullOrWhiteSpace($browserDriver)) {
  $browserDriver = "extension"
}

dotnet run --project (Join-Path $root "apps\agent") -- discover --token $token --api $apiUrl --keywords $Keywords --location $Location --limit $Limit --browser-driver $browserDriver
