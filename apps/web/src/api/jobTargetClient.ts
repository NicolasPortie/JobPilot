import { api } from './client'
import type { JobTargetDetail } from '../types'
import type { OrganizeJobTargetRequest } from './contracts'

export const jobTargetClient = {
  organize: (payload: OrganizeJobTargetRequest) => api.post<JobTargetDetail>('/job-targets/organize', payload),
  organizeScreenshot: (payload: FormData, query = 'useAi=true') =>
    api.post<JobTargetDetail>(`/job-targets/organize-screenshot?${query}`, payload),
}
