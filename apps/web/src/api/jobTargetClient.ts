import { api } from './client'

export const jobTargetClient = {
  organize: (payload: unknown) => api.post('/job-targets/organize', payload),
  organizeScreenshot: (payload: FormData, query = 'useAi=true') =>
    api.post(`/job-targets/organize-screenshot?${query}`, payload),
}
