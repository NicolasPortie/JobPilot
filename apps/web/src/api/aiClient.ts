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

export type AiProviderPreset = {
  providerType: string
  label: string
  baseUrl: string
  model: string
  requiresApiKey: boolean
  hint: string
}

export type AiSettings = {
  providerType: string
  name: string
  baseUrl: string
  model: string
  hasApiKey: boolean
  enabled: boolean
  saved: boolean
  activeSource: 'saved' | 'config' | 'ollama'
  activeProvider: string
  presets: AiProviderPreset[]
}

/** `apiKey` vazio mantém a chave salva; a API nunca devolve a chave. */
export type AiSettingsInput = {
  providerType: string
  name: string
  baseUrl: string
  model: string
  apiKey: string
  enabled: boolean
}

export type AiConnectionTest = { ok: boolean; provider: string; model: string; sample: string; error: string | null }

export const aiClient = {
  status: () => api.get<AiStatus>('/ai/status'),
  settings: () => api.get<AiSettings>('/ai/settings'),
  saveSettings: (input: AiSettingsInput) => api.put<AiSettings>('/ai/settings', input),
  removeSettings: () => api.delete('/ai/settings'),
  testSettings: (input: AiSettingsInput) => api.post<AiConnectionTest>('/ai/settings/test', input),
}
