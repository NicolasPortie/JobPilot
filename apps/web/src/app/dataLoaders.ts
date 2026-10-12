import type { Dispatch, SetStateAction } from 'react'
import { profileClient } from '../api/profileClient'
import { resumeClient } from '../api/resumeClient'
import type { Profile, ProfileValidationIssue, ResumeDocumentSummary, ResumeLayoutOption } from '../types'
import { emptyProfile, fallbackResumeLayouts } from './defaults'

export type JobPilotDataLoaderState = {
  setProfile: Dispatch<SetStateAction<Profile>>
  setResumeDocuments: Dispatch<SetStateAction<ResumeDocumentSummary[]>>
  setSelectedResumeVersionId: Dispatch<SetStateAction<string>>
  setResumeLayouts: Dispatch<SetStateAction<ResumeLayoutOption[]>>
  setValidationIssues: Dispatch<SetStateAction<ProfileValidationIssue[]>>
  setProfileCompletion: Dispatch<SetStateAction<number>>
}

export async function loadJobPilotTab(state: JobPilotDataLoaderState) {
  const [profileRes, validationRes] = await Promise.all([profileClient.get(), profileClient.validate()])
  state.setProfile({ ...emptyProfile, ...profileRes.data })
  state.setValidationIssues(validationRes.data?.issues ?? [])
  // Apos o validate (que semeia as regras no backend), a completude vem do servidor.
  const completionRes = await profileClient.completion().catch(() => ({ data: { percent: 0 } }))
  state.setProfileCompletion(completionRes.data?.percent ?? 0)

  const [resumeDocsRes, resumeLayoutsRes] = await Promise.all([
    resumeClient.documents(),
    resumeClient.layouts().catch(() => ({ data: fallbackResumeLayouts })),
  ])
  const nextResumeDocuments = resumeDocsRes.data ?? []
  state.setResumeDocuments(nextResumeDocuments)
  state.setSelectedResumeVersionId((current) => {
    const versions = nextResumeDocuments.flatMap((document) => document.versions)
    if (versions.some((version) => version.id === current)) return current
    const baseVersions = versions.filter((version) => version.title.toLowerCase().includes('base importada'))
    const latestVersion = (baseVersions.length > 0 ? baseVersions : versions)
      .sort((left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt))[0]
    return latestVersion?.id ?? ''
  })
  state.setResumeLayouts(resumeLayoutsRes.data?.length ? resumeLayoutsRes.data : fallbackResumeLayouts)
}
