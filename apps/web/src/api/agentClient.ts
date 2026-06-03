import { api } from './client'
import type { AgentPolicy } from '../types'

export const agentClient = {
  policy: () => api.get('/agent/policy'),
  savePolicy: (policy: AgentPolicy) => api.put('/agent/policy', policy),
  history: () => api.get('/agent/history'),
  learnings: () => api.get('/agent/learnings'),
  reviewLearning: (id: string, action: 'approve' | 'reject' | 'disable', note = '') =>
    api.post(`/agent/learnings/${id}/${action}`, { note }),
  pair: (payload: unknown) => api.post('/agent/pair', payload),
  extensionStatus: () => api.get('/extension/status'),
  health: () => api.get('/health/operational'),
  recoverHealth: () => api.post('/health/operational/recover'),
  discover: (payload: unknown) => api.post('/automation/discover', payload),
  apply: (payload: unknown) => api.post('/automation/apply', payload),
}
