import { api } from './client'
import type { ResumeLayoutOption, ResumeSection } from '../types'

export const resumeClient = {
  documents: () => api.get('/resume/documents'),
  layouts: () => api.get<ResumeLayoutOption[]>('/resume/layouts'),
  buildAts: (payload: unknown) => api.post('/resume/ats-build', payload),
  tailor: (payload: unknown) => api.post('/resume/tailor', payload),
  exportVersion: (versionId: string, payload: unknown) => api.post(`/resume/versions/${versionId}/exports`, payload),
  downloadExport: (exportId: string) => api.get(`/resume/exports/${exportId}/download`, { responseType: 'blob' }),
  saveReviewAnswer: (payload: unknown) => api.post('/resume/review-answers', payload),
  regenerateAfterAnswers: (payload: unknown) => api.post('/resume/review-answers/regenerate', payload),
  updateSection: (sectionId: string, payload: unknown) => api.put(`/resume/sections/${sectionId}`, payload),
  listSections: (versionId: string) => api.get<ResumeSection[]>(`/resume/versions/${versionId}/sections`),
  createSection: (versionId: string, payload: unknown) => api.post(`/resume/versions/${versionId}/sections`, payload),
  deleteSection: (sectionId: string) => api.delete(`/resume/sections/${sectionId}`),
  reorderSections: (versionId: string, sectionIds: string[]) => api.post(`/resume/versions/${versionId}/sections/reorder`, { sectionIds }),
}
