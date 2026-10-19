import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { aiClient, type AiSettings } from '../../api/aiClient'
import { AiSettingsSection } from './AiSettingsSection'

vi.mock('../../api/aiClient', () => ({
  aiClient: { settings: vi.fn(), saveSettings: vi.fn(), removeSettings: vi.fn(), testSettings: vi.fn() },
}))
afterEach(() => { cleanup(); vi.resetAllMocks() })

const presets = [
  { providerType: 'anthropic', label: 'Anthropic (Claude)', baseUrl: 'https://api.anthropic.com', model: 'claude-haiku-5-5', requiresApiKey: true, hint: 'Chave da Anthropic.' },
  { providerType: 'openai-compatible', label: 'Gateway compatível com OpenAI', baseUrl: '', model: '', requiresApiKey: true, hint: 'Termina em /v1.' },
  { providerType: 'ollama', label: 'Ollama local', baseUrl: 'http://localhost:11434', model: 'qwen3:4b-instruct', requiresApiKey: false, hint: 'Sem chave.' },
]

function settings(values: Partial<AiSettings> = {}): AiSettings {
  return {
    providerType: 'anthropic', name: 'Anthropic (Claude)', baseUrl: 'https://api.anthropic.com', model: 'claude-haiku-5-5',
    hasApiKey: false, enabled: true, saved: false, activeSource: 'ollama', activeProvider: 'Ollama Local', presets, ...values,
  }
}

function load(values: Partial<AiSettings> = {}) {
  vi.mocked(aiClient.settings).mockResolvedValue({ data: settings(values) } as Awaited<ReturnType<typeof aiClient.settings>>)
}

describe('AiSettingsSection', () => {
  it('shows which AI is in use and where it comes from', async () => {
    load({ activeSource: 'config', activeProvider: 'Kimi Code' })
    render(<AiSettingsSection />)
    const line = await screen.findByText(/Em uso agora: Kimi Code/)
    expect(line.textContent).toContain('configuração do servidor')
  })

  it('fills address and model from the provider chosen and sends the key typed', async () => {
    load()
    vi.mocked(aiClient.saveSettings).mockResolvedValue({ data: settings({ saved: true, hasApiKey: true, activeSource: 'saved', activeProvider: 'OmniRoute' }) } as Awaited<ReturnType<typeof aiClient.saveSettings>>)
    render(<AiSettingsSection />)
    fireEvent.change(await screen.findByLabelText('Provedor'), { target: { value: 'openai-compatible' } })
    fireEvent.change(screen.getByLabelText(/^Nome/), { target: { value: 'OmniRoute' } })
    fireEvent.change(screen.getByLabelText(/Endereço do serviço/), { target: { value: 'https://ia.example.com/v1' } })
    fireEvent.change(screen.getByLabelText(/^Modelo/), { target: { value: 'meu-modelo' } })
    fireEvent.change(screen.getByLabelText(/Chave de API/), { target: { value: 'sk-segredo' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))

    await waitFor(() => expect(aiClient.saveSettings).toHaveBeenCalledWith({
      providerType: 'openai-compatible', name: 'OmniRoute', baseUrl: 'https://ia.example.com/v1', model: 'meu-modelo', apiKey: 'sk-segredo', enabled: true,
    }))
    await waitFor(() => expect(screen.getByText(/Configuração salva/)).toBeTruthy())
    const key = screen.getByLabelText(/Chave de API/) as HTMLInputElement
    expect(key.value).toBe('')
    expect(key.placeholder).toContain('deixe em branco para manter')
    expect(document.body.textContent).not.toContain('sk-segredo')
  })

  it('hides the key field for providers that need none', async () => {
    load({ providerType: 'ollama', name: 'Ollama local', baseUrl: 'http://localhost:11434', model: 'qwen3:4b-instruct' })
    render(<AiSettingsSection />)
    await screen.findByLabelText('Provedor')
    expect(screen.queryByLabelText(/Chave de API/)).toBeNull()
  })

  it('reports the test result and the server explanation when it fails', async () => {
    load()
    vi.mocked(aiClient.testSettings)
      .mockResolvedValueOnce({ data: { ok: true, provider: 'x', model: 'claude-haiku-5-5', sample: 'ok', error: null } } as Awaited<ReturnType<typeof aiClient.testSettings>>)
      .mockResolvedValueOnce({ data: { ok: false, provider: 'x', model: 'm', sample: '', error: 'Este host de IA não está na lista de destinos confiáveis.' } } as Awaited<ReturnType<typeof aiClient.testSettings>>)
    render(<AiSettingsSection />)
    const test = await screen.findByRole('button', { name: 'Testar conexão' })
    fireEvent.click(test)
    await waitFor(() => expect(screen.getByText('Conexão funcionando com claude-haiku-5-5.')).toBeTruthy())
    fireEvent.click(screen.getByRole('button', { name: 'Testar conexão' }))
    await waitFor(() => expect(screen.getByText(/lista de destinos confiáveis/)).toBeTruthy())
  })

  it('shows the API message when saving is refused', async () => {
    load()
    vi.mocked(aiClient.saveSettings).mockRejectedValue({ response: { data: { detail: 'Informe a chave de API deste provedor.' }, status: 400 } })
    render(<AiSettingsSection />)
    fireEvent.click(await screen.findByRole('button', { name: 'Salvar' }))
    await waitFor(() => expect(screen.getByText('Informe a chave de API deste provedor.')).toBeTruthy())
  })

  it('removes the saved configuration', async () => {
    load({ saved: true, hasApiKey: true, activeSource: 'saved' })
    vi.mocked(aiClient.removeSettings).mockResolvedValue({} as Awaited<ReturnType<typeof aiClient.removeSettings>>)
    render(<AiSettingsSection />)
    fireEvent.click(await screen.findByRole('button', { name: 'Remover configuração' }))
    await waitFor(() => expect(aiClient.removeSettings).toHaveBeenCalled())
    await waitFor(() => expect(screen.getByText('Configuração removida.')).toBeTruthy())
  })
})
