Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$violations = [System.Collections.Generic.List[string]]::new()

function Assert-MaxLines([string]$relativePath, [int]$maximum) {
  $path = Join-Path $root $relativePath
  $lines = (Get-Content -LiteralPath $path | Measure-Object -Line).Lines
  if ($lines -gt $maximum) {
    $violations.Add("$relativePath has $lines lines; budget is $maximum.")
  }
}

function Get-FileCount([string]$relativePath, [string]$filter) {
  $path = Join-Path $root $relativePath
  if (-not (Test-Path -LiteralPath $path)) {
    return 0
  }

  return @(Get-ChildItem -LiteralPath $path -Filter $filter -File).Count
}

Assert-MaxLines "apps\api\Program.cs" 80
Assert-MaxLines "apps\api\Services\ResumeExportService.cs" 400
Assert-MaxLines "apps\api\Services\ResumeExportRenderer.cs" 400
Assert-MaxLines "apps\api\Services\JobTargetService.cs" 400
Assert-MaxLines "apps\api\Services\JobTargetAnalyzer.cs" 400
Assert-MaxLines "apps\api\Services\AgentTaskQueueService.cs" 400
Assert-MaxLines "apps\api\Services\DiscoveredJobMapper.cs" 400
Assert-MaxLines "apps\api\Services\AutomationProcessService.cs" 400
Assert-MaxLines "apps\api\Services\AutomationReadinessService.cs" 400
Assert-MaxLines "apps\api\Services\AutomationRuntimeLauncher.cs" 400
Assert-MaxLines "apps\api\Services\ProfileImportService.cs" 400
Assert-MaxLines "apps\api\Services\ResumeImportSectionBuilder.cs" 400
Assert-MaxLines "apps\api\Services\ResumeProfileInference.cs" 400
Assert-MaxLines "apps\api\Services\ResumeTailoringService.cs" 400
Assert-MaxLines "apps\api\Services\ResumeDraftBuilder.cs" 400
Assert-MaxLines "apps\api\Services\ResumeTextSanitizer.cs" 400
Assert-MaxLines "apps\api\Services\ResumeTailoringAnalyzer.cs" 400
Assert-MaxLines "apps\api\Services\AiChatService.cs" 400
Assert-MaxLines "apps\api\Services\AiCompletionService.cs" 400
Assert-MaxLines "apps\api\Services\AiChatResponseParser.cs" 100
Assert-MaxLines "apps\web\src\App.tsx" 250
Assert-MaxLines "apps\web\src\app\useJobPilotApp.ts" 380
Assert-MaxLines "apps\web\src\app\dataLoaders.ts" 150
Assert-MaxLines "apps\web\src\app\useResumeWorkflow.ts" 250
Assert-MaxLines "apps\web\src\index.css" 20
Assert-MaxLines "apps\web\src\types.ts" 20
Assert-MaxLines "apps\agent\Application\Orchestration\ApplicationAgent.cs" 750
Assert-MaxLines "apps\agent\Application\Orchestration\ApplicationAgent.Extension.cs" 350
Assert-MaxLines "apps\agent\Application\Orchestration\ApplicationAgent.ObservationLoop.cs" 320
Assert-MaxLines "apps\agent\Application\Execution\ApplicationAgent.Execution.cs" 260
Assert-MaxLines "apps\agent\Application\Trace\ApplicationAgent.Trace.cs" 300

Get-ChildItem -Path (Join-Path $root "apps\web\src\features") -Filter "*Tab.tsx" -File -Recurse |
  ForEach-Object {
    $relativePath = $_.FullName.Substring($root.Length).TrimStart("\", "/")
    Assert-MaxLines $relativePath 500
  }

Get-ChildItem -Path (Join-Path $root "apps\web\src\styles") -Filter "*.css" -File -Recurse |
  ForEach-Object {
    $relativePath = $_.FullName.Substring($root.Length).TrimStart("\", "/")
    Assert-MaxLines $relativePath 500
  }

$controllerCount = Get-FileCount "apps\api\Controllers" "*Controller.cs"
if ($controllerCount -lt 23) {
  $violations.Add("The API controller migration baseline requires at least twenty-three controllers.")
}

if (-not (Test-Path -LiteralPath (Join-Path $root "apps\web\src\api\generated\types.gen.ts"))) {
  $violations.Add("Generated frontend OpenAPI types are missing. Run npm run generate:api-types with the local API available.")
}

$endpointGroupCount = Get-FileCount "apps\api\Endpoints" "*Endpoints.cs"
if ($endpointGroupCount -gt 0) {
  $violations.Add("Public HTTP routes must use controllers; Minimal API endpoint groups are not allowed.")
}

$httpCoupledServices = Get-ChildItem -Path (Join-Path $root "apps\api\Services") -Filter "*.cs" -File |
  Select-String -Pattern "\bIResult\b|\bResults\."
if ($httpCoupledServices) {
  $violations.Add("Application services must not depend on HTTP results:`n$httpCoupledServices")
}

$untypedApplicationResults = Get-ChildItem -Path (Join-Path $root "apps\api") -Filter "*.cs" -File -Recurse |
  Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" } |
  Select-String -Pattern "ServiceResult<object>|ActionResult<object>"
if ($untypedApplicationResults) {
  $violations.Add("HTTP and application results must use explicit contracts:`n$untypedApplicationResults")
}

$persistenceCoupledControllers = Get-ChildItem -Path (Join-Path $root "apps\api\Controllers") -Filter "*Controller.cs" -File |
  Select-String -Pattern "\bJobPilotDbContext\b"
if ($persistenceCoupledControllers) {
  $violations.Add("Controllers must delegate persistence to services:`n$persistenceCoupledControllers")
}

$genericRouteControllers = Get-ChildItem -Path (Join-Path $root "apps\api\Controllers") -Filter "*Controller.cs" -File |
  Select-String -Pattern '\[Route\("api"\)\]'
if ($genericRouteControllers) {
  $violations.Add("Controllers must declare a resource-specific route:`n$genericRouteControllers")
}

$controllerConventionViolations = Get-ChildItem -Path (Join-Path $root "apps\api\Controllers") -Filter "*Controller.cs" -File |
  Where-Object {
    $content = Get-Content -LiteralPath $_.FullName -Raw
    $content -notmatch "\[ApiController\]" -or $content -notmatch "\[(Authorize|AllowAnonymous)"
  }
if ($controllerConventionViolations) {
  $violations.Add("Controllers must declare ApiController and an explicit authorization policy:`n$controllerConventionViolations")
}

$entityNames = Get-ChildItem -Path (Join-Path $root "apps\api\Domain\Entities") -Filter "*.cs" -File |
  ForEach-Object { [regex]::Escape($_.BaseName) }
$entityPattern = $entityNames -join "|"
$entityBoundaryPattern = "ActionResult<.*\b($entityPattern)\b|Task<List<($entityPattern)>>|Task<($entityPattern)>|\b($entityPattern)\s+\w+\)"
$entityBoundaryViolations = Get-ChildItem -Path (Join-Path $root "apps\api\Controllers") -Filter "*Controller.cs" -File |
  Select-String -Pattern $entityBoundaryPattern
if ($entityBoundaryViolations) {
  $violations.Add("Controllers must expose explicit request/response contracts instead of domain entities:`n$entityBoundaryViolations")
}

$directWebCalls = Select-String -LiteralPath (Join-Path $root "apps\web\src\App.tsx") -Pattern "api\.(get|post|put|delete|patch)"
if ($directWebCalls) {
  $violations.Add("App.tsx contains direct API calls. Add or extend a domain client:`n$directWebCalls")
}

$untypedWebPayloads = Get-ChildItem -Path (Join-Path $root "apps\web\src\api") -Include "*.ts" -File -Recurse |
  Select-String -Pattern "payload:\s*unknown"
if ($untypedWebPayloads) {
  $violations.Add("Frontend API clients must use explicit request contracts:`n$untypedWebPayloads")
}

$webSourceFiles = Get-ChildItem -Path (Join-Path $root "apps\web\src") -Include "*.ts", "*.tsx" -File -Recurse |
  Where-Object { $_.FullName -notmatch "[\\/]api[\\/]" }
$directWebApiCalls = $webSourceFiles | Select-String -Pattern "\bapi\.(get|post|put|delete|patch)\b"
if ($directWebApiCalls) {
  $violations.Add("Frontend HTTP calls must stay inside src/api clients:`n$directWebApiCalls")
}

$unjustifiedAny = $webSourceFiles | Select-String -Pattern ":\s*any\b|<any>|as\s+any\b"
if ($unjustifiedAny) {
  $violations.Add("Frontend application code must not introduce explicit any types:`n$unjustifiedAny")
}

$hybridIdentifiers = Get-ChildItem -Path (Join-Path $root "apps") -Include "*.cs", "*.ts", "*.tsx", "*.js" -File -Recurse |
  Where-Object { $_.FullName -notmatch "[\\/](bin|obj|node_modules|dist)[\\/]" } |
  Select-String -Pattern "CarregadorApplicationAgentRuntime|ResultadoServico|ConfiguracaoSeguranca|ParaActionResult"
if ($hybridIdentifiers) {
  $violations.Add("Known hybrid or reverted identifiers were reintroduced:`n$hybridIdentifiers")
}

$layerNamedAgentTests = Get-ChildItem -Path (Join-Path $root "apps\agent.tests") -Filter "Camada*.cs" -File
if ($layerNamedAgentTests) {
  $violations.Add("Agent tests must use behavioral names instead of historical Camada* names:`n$layerNamedAgentTests")
}

$trackedGenerated = git -C $root ls-files | Select-String -Pattern '(^|/)(bin|obj|node_modules|dist|artifacts|logs)/'
if ($trackedGenerated) {
  $violations.Add("Generated files are tracked:`n$trackedGenerated")
}

$rootTemporaryFiles = Get-ChildItem -LiteralPath $root -File |
  Where-Object { $_.Name -match '\.(log|tmp|dump)$|^codex_.*\.json$|^debug-filter\.cs$|^dashboard-qa\.png$|^tmp-.*\.png$' }
if ($rootTemporaryFiles) {
  $violations.Add("Temporary files must not live in the repository root:`n$rootTemporaryFiles")
}

if ($violations.Count -gt 0) {
  throw ($violations -join "`n")
}

Write-Host "Architecture checks passed."
