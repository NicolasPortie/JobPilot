import { api } from './client'
import type { ApplicationHistoryItem, QuickTailorResponse } from '../types'
import type { QuickTailorRequest } from './contracts'

export const applicationsClient = {
  quick: (payload: QuickTailorRequest) => api.post<QuickTailorResponse>('/applications/quick', payload),
  history: () => api.get<ApplicationHistoryItem[]>('/applications/history'),
  setApplied: (resumeVersionId: string, applied: boolean) =>
    api.put(`/applications/${resumeVersionId}/applied`, { applied }),
}
