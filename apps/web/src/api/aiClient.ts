import { api } from './client'

export type AiStatus = {
  available: boolean
  configured: boolean
  provider: string
  model: string
  lastCallSucceeded: boolean | null
  lastCheckedAt: string | null
  message: string
}

export const aiClient = {
  status: () => api.get<AiStatus>('/ai/status'),
}
