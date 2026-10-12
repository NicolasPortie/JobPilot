$CommandName = $args[0]
$CommandArguments = @($args | Select-Object -Skip 1)

$names = @('ASPNETCORE_ENVIRONMENT', 'Auth__JwtKey', 'Security__SecretKey',
  'ConnectionStrings__DefaultConnection', 'POSTGRES_PASSWORD', 'JOBPILOT_JWT_KEY',
  'JOBPILOT_BOOTSTRAP_EMAIL', 'JOBPILOT_BOOTSTRAP_PASSWORD', 'JOBPILOT_SECRET_KEY')
$environment = @{}
foreach ($name in $names) { $environment[$name] = [Environment]::GetEnvironmentVariable($name) }
$entry = @{ command = $CommandName; arguments = @($CommandArguments); environment = $environment }
Add-Content -LiteralPath $env:QUALITY_TEST_LOG -Value ($entry | ConvertTo-Json -Compress -Depth 4)
$count = @(Get-Content -LiteralPath $env:QUALITY_TEST_LOG).Count
if ($count -eq [int]$env:QUALITY_TEST_FAIL_AT) { exit ([int]$env:QUALITY_TEST_EXIT_CODE) }
if ($CommandName -eq 'git') {
  if ($env:QUALITY_TEST_GIT_MATCH -eq 'true') { Write-Output 'fixture:1:possible secret'; exit 0 }
  exit 1
}
exit 0
