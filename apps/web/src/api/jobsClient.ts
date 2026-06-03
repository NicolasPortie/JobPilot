import { api } from './client'

export const jobsClient = {
  searches: () => api.get('/searches'),
  jobs: () => api.get('/jobs'),
  runs: () => api.get('/runs'),
  checkpoints: () => api.get('/checkpoints'),
  events: () => api.get('/events'),
  updateStatus: (id: string, status: string) => api.patch(`/jobs/${id}/status`, { status }),
  attachResume: (jobId: string, resumeVersionId: string) => api.patch(`/jobs/${jobId}/resume`, { resumeVersionId }),
  createRun: (payload: unknown) => api.post('/runs', payload),
  resolveCheckpoint: (id: string, payload: unknown) => api.post(`/checkpoints/${id}/resolve`, payload),
}
