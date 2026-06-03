param(
  [string]$Keywords = "ASP.NET junior remote",
  [string]$Location = "Brazil",
  [int]$Limit = 25
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$token = [Environment]::GetEnvironmentVariable("JOBPILOT_AGENT_TOKEN", "User")
$apiUrl = [Environment]::GetEnvironmentVariable("JOBPILOT_API_URL", "User")
$browserDriver = [Environment]::GetEnvironmentVariable("JOBPILOT_BROWSER_DRIVER", "User")

if ([string]::IsNullOrWhiteSpace($token)) {
  throw "JOBPILOT_AGENT_TOKEN is not configured. Pair the agent in the web app or ask Codex to configure it again."
}

if ([string]::IsNullOrWhiteSpace($apiUrl)) {
  $apiUrl = "http://localhost:5000"
}

if ([string]::IsNullOrWhiteSpace($browserDriver)) {
  $browserDriver = "extension"
}

dotnet run --project (Join-Path $root "apps\agent") -- discover --token $token --api $apiUrl --keywords $Keywords --location $Location --limit $Limit --browser-driver $browserDriver
