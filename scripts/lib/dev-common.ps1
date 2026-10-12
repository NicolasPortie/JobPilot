Set-StrictMode -Version Latest

$script:JobPilotRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script:JobPilotPostgresPort = 5434
$script:JobPilotApiPort = 5000
$script:JobPilotWebPort = 5173
$script:JobPilotApiHealthUrl = "http://localhost:$JobPilotApiPort/health"
$script:JobPilotWebUrl = "http://127.0.0.1:$JobPilotWebPort"
$script:JobPilotPostgresContainer = "jobpilot-postgres"
$script:JobPilotComposeFile = Join-Path $JobPilotRoot "infra\docker-compose.yml"
$script:JobPilotDevSecretsPath = Join-Path $JobPilotRoot ".jobpilot-dev-secrets.json"

function New-JobPilotDevSecret {
  $bytes = New-Object byte[] 32
  $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
  try {
    $generator.GetBytes($bytes)
  } finally {
    $generator.Dispose()
  }
  return [Convert]::ToBase64String($bytes).Replace("+", "-").Replace("/", "_").TrimEnd("=")
}

function Get-JobPilotDevSecrets {
  $secrets = $null
  if (Test-Path -LiteralPath $script:JobPilotDevSecretsPath) {
    try {
      $secrets = Get-Content -LiteralPath $script:JobPilotDevSecretsPath -Raw | ConvertFrom-Json
    } catch {
      $secrets = $null
    }
  }

  $isComplete = $null -ne $secrets -and
    -not [string]::IsNullOrWhiteSpace($secrets.JwtKey) -and
    -not [string]::IsNullOrWhiteSpace($secrets.SecretKey) -and
    -not [string]::IsNullOrWhiteSpace($secrets.SeedPassword) -and
    -not [string]::IsNullOrWhiteSpace($secrets.PostgresPassword)

  if (-not $isComplete) {
    $secrets = [PSCustomObject]@{
      JwtKey = New-JobPilotDevSecret
      SecretKey = New-JobPilotDevSecret
      SeedEmail = "local-$(([Guid]::NewGuid()).ToString('N').Substring(0, 8))@jobpilot.local"
      SeedPassword = New-JobPilotDevSecret
      PostgresPassword = New-JobPilotDevSecret
    }
    $secrets | ConvertTo-Json | Set-Content -LiteralPath $script:JobPilotDevSecretsPath -Encoding UTF8
  }

  return $secrets
}

function Set-JobPilotDevEnvironment {
  $env:ASPNETCORE_ENVIRONMENT = "Development"
  $devSecrets = Get-JobPilotDevSecrets

  if ([string]::IsNullOrWhiteSpace($env:Auth__JwtKey)) {
    $env:Auth__JwtKey = $devSecrets.JwtKey
  }
  if ([string]::IsNullOrWhiteSpace($env:Security__SecretKey)) {
    $env:Security__SecretKey = $devSecrets.SecretKey
  }
  if ([string]::IsNullOrWhiteSpace($env:Auth__SeedPassword)) {
    $env:Auth__SeedPassword = $devSecrets.SeedPassword
  }
  if ([string]::IsNullOrWhiteSpace($env:Auth__SeedEmail)) {
    $env:Auth__SeedEmail = $devSecrets.SeedEmail
  }
  if ([string]::IsNullOrWhiteSpace($env:JOBPILOT_POSTGRES_PASSWORD)) {
    $env:JOBPILOT_POSTGRES_PASSWORD = $devSecrets.PostgresPassword
  }
  if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__DefaultConnection)) {
    $env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=$JobPilotPostgresPort;Database=jobpilot;Username=jobpilot;Password=$($env:JOBPILOT_POSTGRES_PASSWORD)"
  }
  if ([string]::IsNullOrWhiteSpace($env:KIMI_API_KEY)) {
    $env:KIMI_API_KEY = [Environment]::GetEnvironmentVariable("KIMI_API_KEY", "User")
  }
}

function Test-JobPilotTcpPort {
  param(
    [Parameter(Mandatory = $true)][int]$Port,
    [string]$HostName = "127.0.0.1"
  )

  try {
    $client = New-Object System.Net.Sockets.TcpClient
    $async = $client.BeginConnect($HostName, $Port, $null, $null)
    $connected = $async.AsyncWaitHandle.WaitOne(1200)
    if (-not $connected) {
      $client.Close()
      return $false
    }

    $client.EndConnect($async)
    $client.Close()
    return $true
  } catch {
    return $false
  }
}

function Get-JobPilotPostgresStatus {
  try {
    $raw = docker inspect $JobPilotPostgresContainer --format "{{.State.Status}}" 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($raw)) {
      return "missing"
    }
    return $raw.Trim().ToLowerInvariant()
  } catch {
    return "missing"
  }
}

function Test-JobPilotApiHealth {
  param([int]$TimeoutSeconds = 3)

  try {
    $response = Invoke-WebRequest -Uri $JobPilotApiHealthUrl -UseBasicParsing -TimeoutSec $TimeoutSeconds
    return $response.StatusCode -eq 200
  } catch {
    return $false
  }
}

function Wait-JobPilotApiHealth {
  param(
    [int]$TimeoutSeconds = 45,
    [int]$IntervalMilliseconds = 1000
  )

  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Date) -lt $deadline) {
    if (Test-JobPilotApiHealth) {
      return $true
    }
    Start-Sleep -Milliseconds $IntervalMilliseconds
  }

  return $false
}

function Get-JobPilotDevStatus {
  $postgresState = Get-JobPilotPostgresStatus
  $postgresHealthy = $postgresState -eq "running" -and (Test-JobPilotTcpPort -Port $JobPilotPostgresPort)
  $apiHealthy = Test-JobPilotApiHealth
  $webHealthy = Test-JobPilotTcpPort -Port $JobPilotWebPort

  [PSCustomObject]@{
    Postgres = [PSCustomObject]@{
      Container = $JobPilotPostgresContainer
      State = $postgresState
      Port = $JobPilotPostgresPort
      Healthy = $postgresHealthy
    }
    Api = [PSCustomObject]@{
      Url = "http://localhost:$JobPilotApiPort"
      HealthUrl = $JobPilotApiHealthUrl
      Port = $JobPilotApiPort
      Healthy = $apiHealthy
    }
    Web = [PSCustomObject]@{
      Url = $JobPilotWebUrl
      Port = $JobPilotWebPort
      Healthy = $webHealthy
    }
    Ready = $postgresHealthy -and $apiHealthy
  }
}

function Write-JobPilotDevStatus {
  param([Parameter(Mandatory = $true)]$Status)

  function Write-ServiceLine {
    param($Label, $Healthy, $Detail)
    $icon = if ($Healthy) { "[OK]" } else { "[--]" }
    Write-Host ("  {0} {1,-10} {2}" -f $icon, $Label, $Detail)
  }

  Write-Host ""
  Write-Host "JobPilot local stack"
  Write-Host "--------------------"
  Write-ServiceLine -Label "Postgres" -Healthy $Status.Postgres.Healthy -Detail ("{0} (porta {1})" -f $Status.Postgres.State, $Status.Postgres.Port)
  Write-ServiceLine -Label "API" -Healthy $Status.Api.Healthy -Detail $Status.Api.HealthUrl
  Write-ServiceLine -Label "Web" -Healthy $Status.Web.Healthy -Detail $Status.Web.Url
  Write-Host ""

  if ($Status.Ready) {
    Write-Host "Stack pronto para uso."
    Write-Host "Login: $($env:Auth__SeedEmail) / senha gerada em .jobpilot-dev-secrets.json"
    return
  }

  Write-Host "Para subir tudo:"
  Write-Host "  .\scripts\start-mvp.ps1"
  Write-Host ""
  Write-Host "Ou manualmente:"
  if (-not $Status.Postgres.Healthy) {
    Write-Host "  docker compose -f infra\docker-compose.yml up -d"
  }
  if (-not $Status.Api.Healthy) {
    Write-Host "  dotnet run --project apps\api"
  }
  if (-not $Status.Web.Healthy) {
    Write-Host "  cd apps\web && npm run dev"
  }
}

function Start-JobPilotProcessWindow {
  param(
    [Parameter(Mandatory = $true)][string]$Title,
    [Parameter(Mandatory = $true)][string]$WorkingDirectory,
    [Parameter(Mandatory = $true)][string]$Command
  )

  $shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { "pwsh" } else { "powershell" }
  $argumentList = @(
    "-NoExit",
    "-Command",
    "`$host.UI.RawUI.WindowTitle = '$Title'; Set-Location '$WorkingDirectory'; $Command"
  )

  Start-Process -FilePath $shell -ArgumentList $argumentList -WorkingDirectory $WorkingDirectory | Out-Null
}
