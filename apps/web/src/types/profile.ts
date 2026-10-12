export type Profile = {
  id?: string
  fullName: string
  email: string
  phone: string
  city: string
  state: string
  country: string
  linkedinUrl: string
  githubUrl: string
  portfolioUrl: string
  professionalSummary: string
  skills: string[]
  targetRole: string
  seniority: string
  salaryExpectation: string
}

export type ProfileValidationIssue = {
  fieldName: string
  errorMessage: string
  isCritical: boolean
}

export type AtsProfileSkillAddition = {
  id: string
  skill: string
  kind: 'missing' | 'related'
  reason: string
  jobTitle: string
  company: string
  createdAt: string
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

