import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { aiClient } from '../../api/aiClient'
import { ResumeAiControl } from './ResumeAiControl'

vi.mock('../../api/aiClient', () => ({ aiClient: { status: vi.fn() } }))
afterEach(() => { cleanup(); vi.resetAllMocks() })

function respond(values = {}) {
  vi.mocked(aiClient.status).mockResolvedValue({ data: {
    available: true, configured: true, provider: 'Kimi Code', model: 'kimi-for-coding',
    lastCallSucceeded: null, lastCheckedAt: null, message: '', ...values,
  } } as Awaited<ReturnType<typeof aiClient.status>>)
}

describe('ResumeAiControl', () => {
  it.each([
    [{}, 'IA configurada · conexão não verificada'],
    [{ lastCallSucceeded: true }, 'IA respondendo'],
    [{ available: false, lastCallSucceeded: false }, 'IA indisponível · modo sem IA'],
    [{ available: false, configured: false }, 'IA não configurada'],
  ])('shows the API state beside the toggle', async (values, text) => {
    respond(values)
    const toggle = vi.fn()
    render(<ResumeAiControl enabled busy={false} onToggle={toggle} />)
    await waitFor(() => expect(screen.getByRole('status').textContent).toContain(text))
    const checkbox = screen.getByRole('checkbox', { name: 'Usar IA quando disponível' })
    expect(checkbox.getAttribute('aria-describedby')).toBe(screen.getByRole('status').id)
    fireEvent.click(checkbox)
    expect(toggle).toHaveBeenCalledWith(false)
    if (!('configured' in values) || values.configured !== false) expect(screen.getByText('Kimi Code · kimi-for-coding')).toBeTruthy()
  })

  it('shows loading, then a neutral error without the raw API error', async () => {
    let reject!: (reason: unknown) => void
    vi.mocked(aiClient.status).mockReturnValue(new Promise((_, rejectPromise) => { reject = rejectPromise }))
    render(<ResumeAiControl enabled busy={false} onToggle={vi.fn()} />)
    expect(screen.getByRole('status').textContent).toBe('Consultando IA…')
    await act(async () => reject(new Error('sensitive-provider-text')))
    expect(screen.getByRole('status').textContent).toBe('Não foi possível consultar a IA')
  })

  it('refreshes after generation and shows the disabled choice', async () => {
    respond()
    const { rerender } = render(<ResumeAiControl enabled busy={false} onToggle={vi.fn()} />)
    await waitFor(() => expect(aiClient.status).toHaveBeenCalledTimes(1))
    rerender(<ResumeAiControl enabled busy onToggle={vi.fn()} />)
    expect(aiClient.status).toHaveBeenCalledTimes(1)
    respond({ lastCallSucceeded: false, available: false })
    rerender(<ResumeAiControl enabled busy={false} onToggle={vi.fn()} />)
    await waitFor(() => expect(screen.getByRole('status').textContent).toContain('IA indisponível'))
    rerender(<ResumeAiControl enabled={false} busy={false} onToggle={vi.fn()} />)
    expect(screen.getByRole('status').textContent).toBe('IA desativada')
  })
})
