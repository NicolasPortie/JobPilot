import type { Dispatch, SetStateAction } from 'react'
import type { AtsProfileSkillAddition, JobTargetDetail, Profile, ResumeDocumentSummary, ResumeImportResult, TailoredResume } from '../types'

export type ReviewAnswers = Record<string, { id?: string; value: string; type: string; evidence: string }>

export type ResumeWorkflowState = {
  profile: Profile
  selectedResumeFile: File | null
  selectedResumeVersionId: string
  resumeDocuments: ResumeDocumentSummary[]
  atsJobDescription: string
  atsJobUrl: string
  atsScreenshotFile: File | null
  atsTailoredResume: TailoredResume | null
  atsJobTarget: JobTargetDetail | null
  atsUseAi: boolean
  selectedLayout: string
  reviewAnswers: ReviewAnswers
  setRecommendedSkillAdditions: Dispatch<SetStateAction<AtsProfileSkillAddition[]>>
  setProfile: Dispatch<SetStateAction<Profile>>
  setProfileImportBusy: Dispatch<SetStateAction<boolean>>
  setProfileImportResult: Dispatch<SetStateAction<ResumeImportResult | null>>
  setProfileImportNotice: Dispatch<SetStateAction<string>>
  setSelectedResumeFile: Dispatch<SetStateAction<File | null>>
  setAtsResumeBusy: Dispatch<SetStateAction<boolean>>
  setAtsJobTarget: Dispatch<SetStateAction<JobTargetDetail | null>>
  setAtsTailoredResume: Dispatch<SetStateAction<TailoredResume | null>>
  setAtsScreenshotFile: Dispatch<SetStateAction<File | null>>
  setAtsExportBusy: Dispatch<SetStateAction<boolean>>
  setAtsResumeNotice: Dispatch<SetStateAction<string>>
  setReviewAnswers: Dispatch<SetStateAction<ReviewAnswers>>
  refresh: (targetTab?: string) => Promise<void>
}
