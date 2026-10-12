import { api } from './client'
import type { ResumeDocumentSummary, ResumeLayoutOption, ResumeSection, ResumeVersionDetail, TailoredResume } from '../types'
import type { BuildAtsResumeRequest, CreateResumeExportRequest, CreateResumeSectionRequest, RegenerateAfterAnswersRequest, RegenerateAfterAnswersResponse, ResumePreviewResponse, SaveReviewAnswerRequest, TailorResumeRequest, UpdateResumeSectionRequest } from './contracts'

export const resumeClient = {
  documents: () => api.get<ResumeDocumentSummary[]>('/resume/documents'),
  getVersion: (versionId: string) => api.get<ResumeVersionDetail>(`/resume/versions/${versionId}`),
  layouts: () => api.get<ResumeLayoutOption[]>('/resume/layouts'),
  buildAts: (payload: BuildAtsResumeRequest) => api.post<TailoredResume>('/resume/ats-build', payload),
  tailor: (payload: TailorResumeRequest) => api.post<TailoredResume>('/resume/tailor', payload),
  exportVersion: (versionId: string, payload: CreateResumeExportRequest) => api.post<{ id: string; parseWarnings?: string[] }>(`/resume/versions/${versionId}/exports`, payload),
  previewVersion: (versionId: string, payload: CreateResumeExportRequest) => api.post<ResumePreviewResponse>(`/resume/versions/${versionId}/preview`, payload),
  downloadExport: (exportId: string) => api.get(`/resume/exports/${exportId}/download`, { responseType: 'blob' }),
  saveReviewAnswer: (payload: SaveReviewAnswerRequest) => api.post<{ id: string }>('/resume/review-answers', payload),
  regenerateAfterAnswers: (payload: RegenerateAfterAnswersRequest) => api.post<RegenerateAfterAnswersResponse>('/resume/review-answers/regenerate', payload),
  updateSection: (sectionId: string, payload: UpdateResumeSectionRequest) => api.put<ResumeSection>(`/resume/sections/${sectionId}`, payload),
  listSections: (versionId: string) => api.get<ResumeSection[]>(`/resume/versions/${versionId}/sections`),
  createSection: (versionId: string, payload: CreateResumeSectionRequest) => api.post<ResumeSection>(`/resume/versions/${versionId}/sections`, payload),
  deleteSection: (sectionId: string) => api.delete(`/resume/sections/${sectionId}`),
  reorderSections: (versionId: string, sectionIds: string[]) => api.post(`/resume/versions/${versionId}/sections/reorder`, { sectionIds }),
}
