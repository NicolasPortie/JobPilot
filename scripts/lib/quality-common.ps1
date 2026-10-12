function Invoke-QualityCommand {
  param(
    [Parameter(Mandatory = $true)][string]$FilePath,
    [string[]]$Arguments = @(),
    [int[]]$SuccessExitCodes = @(0)
  )

  # Check explicitly, including when callers enable PowerShell 7 native errors.
  $PSNativeCommandUseErrorActionPreference = $false
  $global:LASTEXITCODE = 0
  & $FilePath @Arguments
  $exitCode = $global:LASTEXITCODE
  if ($exitCode -notin $SuccessExitCodes) {
    throw "Command '$FilePath $($Arguments -join ' ')' failed with exit code $exitCode."
  }
}

function Invoke-QualityEnvironment {
  param(
    [Parameter(Mandatory = $true)][hashtable]$Variables,
    [Parameter(Mandatory = $true)][scriptblock]$Action
  )

  $previous = @{}
  foreach ($name in $Variables.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
  }
  try {
    foreach ($name in $Variables.Keys) {
      [Environment]::SetEnvironmentVariable($name, $Variables[$name], 'Process')
    }
    & $Action
  }
  finally {
    foreach ($name in $Variables.Keys) {
      [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
    }
  }
}
