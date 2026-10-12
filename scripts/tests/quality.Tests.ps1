# Standalone regression harness: pwsh -NoProfile -File scripts/tests/quality.Tests.ps1
# Runs a copy of the real gate in a temporary fixture with native fake commands.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('quality-tests-' + [guid]::NewGuid())
$names = @('ASPNETCORE_ENVIRONMENT', 'Auth__JwtKey', 'Security__SecretKey',
  'ConnectionStrings__DefaultConnection', 'POSTGRES_PASSWORD', 'JOBPILOT_JWT_KEY',
  'JOBPILOT_BOOTSTRAP_EMAIL', 'JOBPILOT_BOOTSTRAP_PASSWORD', 'JOBPILOT_SECRET_KEY')
$saved = @{}
foreach ($name in ($names + @('PATH', 'QUALITY_TEST_LOG', 'QUALITY_TEST_FAIL_AT',
  'QUALITY_TEST_EXIT_CODE', 'QUALITY_TEST_GIT_MATCH'))) {
  $saved[$name] = [Environment]::GetEnvironmentVariable($name)
}
$failures = [Collections.Generic.List[string]]::new()
$cases = 0
function Assert-Quality([bool]$Condition, [string]$Message) {
  if (-not $Condition) { $failures.Add($Message) }
}
try {
  foreach ($directory in @('scripts', 'scripts/lib', 'apps/web', 'bin')) {
    New-Item -ItemType Directory -Path (Join-Path $fixture $directory) -Force | Out-Null
  }
  Copy-Item -LiteralPath (Join-Path $root 'scripts/quality.ps1') -Destination (Join-Path $fixture 'scripts')
  Get-ChildItem -LiteralPath (Join-Path $root 'scripts/lib') -Filter '*.ps1' |
    Copy-Item -Destination (Join-Path $fixture 'scripts/lib')
  Set-Content -LiteralPath (Join-Path $fixture 'scripts/architecture-check.ps1') -Value '# Architecture is isolated from this orchestration test.'
  $hostPath = (Get-Process -Id $PID).Path
  $fakePath = Join-Path $PSScriptRoot 'fake-quality-command.ps1'
  foreach ($command in @('dotnet', 'npm', 'git', 'docker')) {
    if ($env:OS -eq 'Windows_NT') {
      $shim = "@echo off`r`n`"$hostPath`" -NoProfile -File `"$fakePath`" $command %*`r`nexit /b %errorlevel%"
      Set-Content -LiteralPath (Join-Path $fixture "bin/$command.cmd") -Value $shim
    }
    else {
      $shim = "#!/bin/sh`nexec '$hostPath' -NoProfile -File '$fakePath' $command " + '"$@"'
      $shimPath = Join-Path $fixture "bin/$command"
      Set-Content -LiteralPath $shimPath -Value $shim
      & chmod +x $shimPath
      if ($LASTEXITCODE -ne 0) { throw 'Could not make fake command executable.' }
    }
  }
  $env:PATH = (Join-Path $fixture 'bin') + [IO.Path]::PathSeparator + $saved['PATH']
  $env:QUALITY_TEST_LOG = Join-Path $fixture 'commands.jsonl'
  $scenarios = @(
    @{ name = 'success with existing environment'; failAt = 0; mode = 'existing' },
    @{ name = 'success with absent environment'; failAt = 0; mode = 'absent' },
    @{ name = 'success with mixed environment and native error preference'; failAt = 0; mode = 'mixed'; nativeErrors = $true },
    @{ name = 'secret match stops before npm'; failAt = 0; mode = 'existing'; secret = $true }
  )
  foreach ($index in 1..8) {
    $scenarios += @{ name = "native command $index fails immediately"; failAt = $index; mode = 'mixed' }
  }
  foreach ($scenario in $scenarios) {
    $cases++
    $before = @{}
    for ($index = 0; $index -lt $names.Count; $index++) {
      $value = if ($scenario.mode -eq 'absent' -or ($scenario.mode -eq 'mixed' -and $index % 2 -eq 0)) { $null } else { "user-value-$index" }
      [Environment]::SetEnvironmentVariable($names[$index], $value)
      $before[$names[$index]] = [Environment]::GetEnvironmentVariable($names[$index])
    }
    Set-Content -LiteralPath $env:QUALITY_TEST_LOG -Value '' -NoNewline
    $env:QUALITY_TEST_FAIL_AT = [string]$scenario.failAt
    $env:QUALITY_TEST_EXIT_CODE = '23'
    $env:QUALITY_TEST_GIT_MATCH = [string]($scenario.ContainsKey('secret'))
    $PSNativeCommandUseErrorActionPreference = $scenario.ContainsKey('nativeErrors')
    $location = (Get-Location).Path
    $errorRecord = $null
    try { & (Join-Path $fixture 'scripts/quality.ps1') } catch { $errorRecord = $_ }
    $entries = @(Get-Content -LiteralPath $env:QUALITY_TEST_LOG | ForEach-Object { $_ | ConvertFrom-Json })
    $expectedCount = if ($scenario.failAt -gt 0) { $scenario.failAt } elseif ($scenario.ContainsKey('secret')) { 5 } else { 8 }
    Assert-Quality ($entries.Count -eq $expectedCount) "$($scenario.name): expected $expectedCount commands, got $($entries.Count)."
    $shouldThrow = $scenario.failAt -gt 0 -or $scenario.ContainsKey('secret')
    Assert-Quality (($null -ne $errorRecord) -eq $shouldThrow) "$($scenario.name): incorrect success/failure result."
    if ($scenario.failAt -gt 0 -and $null -ne $errorRecord) {
      Assert-Quality ($errorRecord.ToString() -match '23') "$($scenario.name): failure must identify exit code 23."
    }
    Assert-Quality ((Get-Location).Path -eq $location) "$($scenario.name): working directory was not restored."
    foreach ($name in $names) {
      Assert-Quality ([Environment]::GetEnvironmentVariable($name) -ceq $before[$name]) "$($scenario.name): $name was not restored."
    }
    if ($entries.Count -ge 4) {
      Assert-Quality (($entries[3].arguments -join ' ') -match '--configuration Release') "$($scenario.name): migrations must use Release."
      Assert-Quality ($entries[3].environment.ASPNETCORE_ENVIRONMENT -eq 'Development') "$($scenario.name): migration environment missing."
    }
    if ($entries.Count -eq 8) {
      Assert-Quality ($entries[7].environment.POSTGRES_PASSWORD -eq 'quality-check-password') "$($scenario.name): compose environment missing."
    }
  }
  # Exercise the actual CI migration command through the same native fake.
  $workflow = Get-Content -LiteralPath (Join-Path $root '.github/workflows/quality.yml')
  $migrationCommand = @($workflow | Where-Object { $_ -match '^\s+dotnet ef migrations script\b' })
  Assert-Quality ($migrationCommand.Count -eq 1) 'Expected one CI migration command.'
  if ($migrationCommand.Count -eq 1) {
    $PSNativeCommandUseErrorActionPreference = $false
    $env:QUALITY_TEST_FAIL_AT = '0'
    Set-Content -LiteralPath $env:QUALITY_TEST_LOG -Value '' -NoNewline
    & ([scriptblock]::Create($migrationCommand[0].Trim()))
    $ciEntries = @(Get-Content -LiteralPath $env:QUALITY_TEST_LOG | ForEach-Object { $_ | ConvertFrom-Json })
    Assert-Quality ($ciEntries.Count -eq 1) 'CI migration command must execute once.'
    Assert-Quality (($ciEntries[0].arguments -join ' ') -match '--configuration Release') 'CI migrations must use Release.'
  }
}
finally {
  foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
  # Only remove the unique temporary fixture whose absolute path we created above.
  $resolved = [IO.Path]::GetFullPath($fixture)
  $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
  if (-not $resolved.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture cleanup path.' }
  if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
if ($failures.Count -gt 0) {
  $failures | ForEach-Object { Write-Host "FAIL: $_" }
  throw "$($failures.Count) assertions failed across $cases scenarios."
}
Write-Host "PASS: $cases scenarios plus CI Release migration check."
