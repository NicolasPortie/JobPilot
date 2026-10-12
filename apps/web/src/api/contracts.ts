export type OrganizeJobTargetRequest = {
  sourceKind: string
  text?: string
  url?: string
  useAi: boolean
}

export type BuildAtsResumeRequest = { baseResumeVersionId: string; layout: string; useAi: boolean }
export type TailorResumeRequest = { baseResumeVersionId: string; jobTargetId: string; layout: string; useAi: boolean }
export type CreateResumeExportRequest = { format: string; layout: string }
export type ResumePreviewResponse = { resumeVersionId: string; layout: string; html: string }
export type SaveReviewAnswerRequest = {
  evidenceMapRunId?: string
  jobRequirementId: string
  requirementLabel: string
  questionText: string
  answerValue: string
  answerType: string
  evidenceText?: string
}
export type RegenerateAfterAnswersRequest = {
  answerIds: string[]
  baseResumeVersionId: string
  jobTargetId: string
  layout: string
  useAi: boolean
}
export type RegenerateAfterAnswersResponse = {
  newEvidenceMapRunId: string
  newTailoredVersionId: string
  status: string
  atsScore: number
  updatedEvidences: unknown[]
  tailoredResume: import('../types').TailoredResume
}
export type UpdateResumeSectionRequest = { title?: string; contentJson: string }
export type CreateResumeSectionRequest = { sectionType: string; title: string; contentJson: string }
