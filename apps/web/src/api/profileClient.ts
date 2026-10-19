import { api } from './client'
import type { AtsProfileSkillAddition, Profile, ProfileValidationIssue } from '../types'

export type RetentionPolicy = {
  resumeOriginalsDays: number
  resumeExportsDays: number
  aiUsageLogsDays: number
  jobSourceFilesDays: number
}

export const profileClient = {
  get: () => api.get<Profile>('/profile'),
  save: (profile: Profile) => api.put<Profile>('/profile', profile),
  remove: () => api.delete('/profile'),
  addSkill: (payload: { skill: string; kind?: 'missing' | 'related'; reason?: string; jobTitle?: string; company?: string }) =>
    api.post<{ profile: Profile; alreadyPresent: boolean }>('/profile/skills', payload),
  skillRecommendations: () => api.get<AtsProfileSkillAddition[]>('/profile/skill-recommendations'),
  removeSkillRecommendation: (id: string) => api.delete(`/profile/skill-recommendations/${id}`),
  retention: () => api.get<RetentionPolicy>('/profile/retention'),
  validate: () => api.get<{ valid: boolean; critical: boolean; issues: ProfileValidationIssue[] }>('/profile/validate'),
  completion: () => api.get<{ percent: number; passed: number; total: number }>('/profile/completion'),
  importResumeFile: (payload: FormData, query = 'fetchPublicLinks=false&useAi=true') =>
    api.post(`/profile/import-resume-file?${query}`, payload),
}
