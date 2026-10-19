Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$legacyRuntime = [Environment]::GetEnvironmentVariable("JOBPILOT_ENABLE_LEGACY_RUNTIME", "Process")
if ($legacyRuntime -ne "true") {
  throw "O runtime legado esta desativado. O JobPilot nao envia candidaturas automaticamente."
}

$token = [Environment]::GetEnvironmentVariable("JOBPILOT_AGENT_TOKEN", "User")
$apiUrl = [Environment]::GetEnvironmentVariable("JOBPILOT_API_URL", "User")
$autoSubmit = [Environment]::GetEnvironmentVariable("JOBPILOT_AUTO_SUBMIT", "User")
$maxApplications = [Environment]::GetEnvironmentVariable("JOBPILOT_MAX_APPLICATIONS", "User")
$browserDriver = [Environment]::GetEnvironmentVariable("JOBPILOT_BROWSER_DRIVER", "User")

if ([string]::IsNullOrWhiteSpace($token)) {
  throw "JOBPILOT_AGENT_TOKEN is not configured. Pair the agent in the web app."
}

if ([string]::IsNullOrWhiteSpace($apiUrl)) {
  $apiUrl = "http://localhost:5000"
}

if ([string]::IsNullOrWhiteSpace($autoSubmit)) {
  $autoSubmit = "false"
}

if ([string]::IsNullOrWhiteSpace($maxApplications)) {
  $maxApplications = "10"
}

if ([string]::IsNullOrWhiteSpace($browserDriver)) {
  $browserDriver = "extension"
}

dotnet run --project (Join-Path $root "apps\agent") -- run --token $token --api $apiUrl --auto-submit $autoSubmit --max-applications $maxApplications --browser-driver $browserDriver
