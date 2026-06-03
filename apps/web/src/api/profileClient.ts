import { api } from './client'
import type { Profile } from '../types'

export const profileClient = {
  get: () => api.get('/profile'),
  save: (profile: Profile) => api.put('/profile', profile),
  validate: () => api.get('/profile/validate'),
  importResume: (payload: unknown) => api.post('/profile/import-resume', payload),
  importResumeFile: (payload: FormData, query = 'fetchPublicLinks=true&useAi=true') =>
    api.post(`/profile/import-resume-file?${query}`, payload),
}
