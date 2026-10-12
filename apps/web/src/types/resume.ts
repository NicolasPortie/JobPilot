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
  usedAi?: boolean
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

