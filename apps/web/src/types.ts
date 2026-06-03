export type Profile = {
  id?: string
  fullName: string
  cpf: string
  email: string
  phone: string
  city: string
  country: string
  linkedinUrl: string
  githubUrl: string
  portfolioUrl: string
  professionalSummary: string
  skills: string[]
  targetRole: string
  seniority: string
  salaryExpectation: string
  resumePath: string
}

export type ResumeImportResult = {
  profile: Profile
  answersUpserted: number
  skillsAdded: string[]
  linksFetched: string[]
  warnings: string[]
  extraction: {
    sourceKind: string
    extractionMethod: string
    classification: string
    textQualityScore: number
    confidence: number
    requiresOcr: boolean
    requiresReview: boolean
    pages: number
    pagesWithNativeText: number
    characterCount: number
    wordCount: number
    warnings: string[]
  }
  extractedText: string
  importData: {
    skills: string[]
    answers: { questionKey: string; questionText: string; answerValue: string; category: string }[]
  }
}

export type ResumeDocumentSummary = {
  id: string
  title: string
  status: string
  sourceFileName: string
  sourceKind: string
  updatedAt: string
  versions: ResumeVersionSummary[]
}

export type ResumeLayoutOption = {
  key: string
  label: string
  description: string
  atsNotes: string
  recommendedForAts: boolean
}

export type ResumeVersionSummary = {
  id: string
  title: string
  status: string
  targetRole: string
  targetCompany: string
  atsScore: number
  updatedAt: string
}

export type ResumeSection = {
  id: string
  sectionType: string
  title: string
  sortOrder: number
  contentJson: string
}

export type ResumeVersionDetail = ResumeVersionSummary & {
  resumeDocumentId: string
  plainText: string
  sections: ResumeSection[]
}

export type JobTargetDetail = {
  id: string
  jobLeadId?: string
  sourceKind: string
  sourceUrl: string
  title: string
  company: string
  location: string
  seniority: string
  workplace: string
  summary: string
  status: string
  confidence: number
  warnings: string[]
  requirements: JobRequirementDto[]
  updatedAt: string
}

export type JobRequirementDto = {
  id: string
  category: string
  label: string
  importance: string
  evidenceText: string
  normalizedKeyword: string
  exactPhrases: string[]
  sortOrder: number
}

export type TailoredResume = {
  tailoredVersionId: string
  baseResumeVersionId: string
  jobTargetId: string
  evidenceMapRunId: string
  status: string
  atsScore: number
  scoreComponents: ResumeScoreComponent[]
  sectionDiffs: ResumeSectionDiff[]
  reviewQuestions: ResumeReviewQuestion[]
  plainText: string
  includedKeywords: string[]
  adjacentKeywords: string[]
  excludedKeywords: string[]
  checkpoints: string[]
  warnings: string[]
  version: ResumeVersionDetail
}

export type ResumeScoreComponent = {
  key: string
  label: string
  score: number
  maxScore: number
  status: string
  reason: string
}

export type ResumeSectionDiff = {
  sectionType: string
  title: string
  status: string
  beforePreview: string
  afterPreview: string
  afterSectionId?: string
}

export type ResumeReviewQuestion = {
  requirementId: string
  requirementLabel: string
  category: string
  importance: string
  questionText: string
  reason: string
}

export type SavedSearch = { id: string; name: string; keywords: string; location: string; workplace: string; seniority: string; postedWithinDays: number; limitPerRun: number; isActive: boolean }
export type JobLeadResume = {
  resumeVersionId?: string
  resumeExportId?: string
  resumeFilePath?: string
}

export type JobLead = {
  id: string
  title: string
  company: string
  location: string
  description: string
  url: string
  platform: string
  applyPlatform: string
  applySurface: string
  applyUrl: string
  automationSupport: string
  automationReason: string
  score: number
  scoreReason: string
  status: string
  isRemote: boolean
  filteredReason?: string
  foundAt: string
  resume?: JobLeadResume | null
}
export type Answer = { id: string; questionKey: string; questionText: string; answerValue: string; category: string }
export type Run = { id: string; name: string; state: string; totalJobs: number; createdAt: string; jobs: { id: string; state: string; reason?: string; resumeVersionId?: string; resumeExportId?: string; resumeFilePath?: string }[] }
export type Checkpoint = { id: string; reasonCode: string; stage: string; fieldLabel: string; detail: string; resolutionStatus?: string; resolutionValue?: string; suggestedUserAction?: string; resumeMode?: string; agentRunId?: string; traceStepId?: string; createdAt: string; resolvedAt?: string }
export type EventLog = { id: string; eventType: string; detail: string; occurredAt: string }
export type AiProvider = { id: string; name: string; providerType: string; baseUrl: string; model: string; apiFormat: string; hasApiKey: boolean; isActive: boolean; isDefault: boolean; updatedAt: string }
export type PlatformCredential = { id: string; platform: string; accountEmail: string; hasPassword: boolean; isActive: boolean; updatedAt: string }
export type ExecutionTarget = 'extension' | 'local_dedicated_browser' | 'remote_browser' | 'server_connector'
export type AgentPolicy = { autoSubmitMode: string; executionTarget: ExecutionTarget; maxApplicationsPerRun: number; maxApplicationsPerDay: number; allowedDomains: string[]; blockedDomains: string[]; isPaused: boolean }
export type ExtensionStatus = {
  sessionId?: string
  status: string
  currentUrl: string
  isSupportedPage: boolean
  browser: string
  extensionVersion: string
  lastError: string
  lastHeartbeatAt?: string
  lastObservedAt?: string
}
export type HealthSignal = { key: string; status: string; label: string; detail: string }
export type OperationalHealth = {
  status: string
  checkedAt: string
  signals: HealthSignal[]
  metrics: {
    queuedApplications: number
    openCheckpoints: number
    runningAgentRuns: number
    pendingExtensionCommands: number
    staleExtensionCommands: number
    staleAgentRuns: number
    staleApplicationJobs: number
    dailyLimit: number
    appliedToday: number
    remainingToday: number
  }
}
export type AgentLearning = {
  id: string
  applicationJobId?: string
  domain: string
  learningType: string
  patternKey: string
  title: string
  summary: string
  triggerJson: string
  actionJson: string
  evidenceJson: string
  status: string
  riskLevel: string
  successCount: number
  failureCount: number
  createdAt: string
  reviewedAt?: string
}

export type AgentHistory = {
  runs: { id: string; agentName: string; mode: string; state: string; policyMode: string; startedAt: string; completedAt?: string }[]
  steps: { id: string; stepNumber: number; agentName: string; pageState: string; actionType: string; targetLabel: string; result: string; confidence: number; createdAt: string }[]
  fields: { id: string; label: string; fieldType: string; valueSource: string; confidence: number; createdAt: string }[]
  answers: { id: string; questionText: string; answerValue: string; source: string; confidence: number; createdAt: string }[]
  memories: { id: string; domain: string; patternKey: string; successCount: number; updatedAt: string }[]
}

export type QuestionPattern = { id: string; patternKey: string; category: string; tags: string[]; questionTemplates: string[]; answerTemplate: string; valueSource: string; profileFieldMapping: string; confidenceThreshold: number; useCount: number }
export type JobFilter = { id: string; allowedSeniorities: string[]; blockedSeniorities: string[]; allowedRoleKeywords: string[]; blockedRoleKeywords: string[]; allowedWorkplaceTypes: string[]; blockedWorkplaceTypes: string[]; allowedApplyTypes: string[]; minScore: number; autoIgnoreLowScore: boolean }
export type ProfileValidationIssue = { fieldName: string; errorMessage: string; isCritical: boolean }
export type AuditTab = 'timeline' | 'fields' | 'answers' | 'steps' | 'checkpoints' | 'events' | 'learnings'

export type AuditField = { id: string; fieldKey: string; label: string; fieldType: string; valueSource: string; confidence: number; createdAt: string }
export type AuditAnswer = { id: string; questionText: string; answerValue: string; source: string; providerModel: string; confidence: number; createdAt: string }
export type AuditStep = { id: string; stepNumber: number; agentName: string; pageState: string; actionType: string; targetLabel: string; result: string; confidence: number; createdAt: string }
export type AgentTraceStep = {
  id: string
  agentRunId?: string
  applicationJobId?: string
  stepNumber: number
  objectiveKind: string
  objectiveJson: string
  platform: string
  module: string
  phase: string
  beforeFingerprint: string
  afterFingerprint: string
  planner: string
  proposalJson: string
  riskJson: string
  guardVerdictJson: string
  executionJson: string
  outcomeJson: string
  recoveryJson: string
  contentSafetyJson: string
  evidencePath: string
  startedAt: string
  completedAt: string
  createdAt: string
}
export type AuditCheckpoint = { id: string; reasonCode: string; stage: string; fieldLabel: string; detail: string; resolutionStatus: string; resolutionValue?: string; suggestedUserAction?: string; resumeMode?: string; agentRunId?: string; traceStepId?: string; createdAt: string; resolvedAt?: string }
export type AuditEvent = { id: string; eventType: string; detail: string; occurredAt: string }
export type JobAudit = { fields: AuditField[]; answers: AuditAnswer[]; steps: AuditStep[]; traceSteps: AgentTraceStep[]; checkpoints: AuditCheckpoint[]; events: AuditEvent[]; learnings: AgentLearning[] }
