import { AxiosHeaders } from 'axios'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { applicationsClient } from '../../api/applicationsClient'
import type { ApplicationHistoryItem } from '../../types'
import { ApplicationsTab } from './ApplicationsTab'

const history: ApplicationHistoryItem[] = [
  { resumeVersionId: 'v1', jobTargetId: 't1', role: 'Dev .NET', company: 'Alfa', location: 'Remoto', sourceUrl: 'https://vagas.example/1', atsScore: 81, applied: false, createdAt: '2026-10-12T10:00:00Z' },
  { resumeVersionId: 'v2', jobTargetId: 't2', role: 'Backend', company: 'Beta', location: '', sourceUrl: '', atsScore: 64, applied: true, createdAt: '2026-10-11T10:00:00Z' },
]

function ok<T>(data: T) {
  return { data, status: 200, statusText: 'OK', headers: {}, config: { headers: new AxiosHeaders() } }
}

afterEach(() => { cleanup(); vi.restoreAllMocks() })

describe('ApplicationsTab', () => {
  it('lists prepared applications with the actions to send each one', async () => {
    vi.spyOn(applicationsClient, 'history').mockResolvedValue(ok(history))
    const onDownload = vi.fn()
    render(<ApplicationsTab exportBusy={false} onDownload={onDownload} onMarkApplied={vi.fn()} />)

    expect(await screen.findByText(/Dev \.NET · Alfa/)).toBeTruthy()
    expect(screen.getByText('2 currículos adaptados · 1 enviados')).toBeTruthy()
    expect(screen.getAllByRole('link', { name: /Abrir vaga/ })).toHaveLength(1)
    fireEvent.click(screen.getAllByRole('button', { name: /PDF/ })[0])
    expect(onDownload).toHaveBeenCalledWith('v1', 'pdf')
  })

  it('filters by sent state and toggles the manual status', async () => {
    vi.spyOn(applicationsClient, 'history').mockResolvedValue(ok(history))
    const onMarkApplied = vi.fn().mockResolvedValue(undefined)
    render(<ApplicationsTab exportBusy={false} onDownload={vi.fn()} onMarkApplied={onMarkApplied} />)
    await screen.findByText(/Dev \.NET · Alfa/)

    fireEvent.click(screen.getByRole('button', { name: 'A enviar' }))
    expect(screen.queryByText(/Backend · Beta/)).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: /Marcar enviada/ }))

    await waitFor(() => expect(onMarkApplied).toHaveBeenCalledWith('v1', true))
    // Marcada como enviada, a vaga sai de "A enviar" e passa a aparecer em "Enviadas".
    await waitFor(() => expect(screen.queryByText(/Dev \.NET · Alfa/)).toBeNull())
    fireEvent.click(screen.getByRole('button', { name: 'Enviadas' }))
    expect(screen.getByText(/Dev \.NET · Alfa/)).toBeTruthy()
    expect(screen.getAllByText('Enviada ✓')).toHaveLength(2)
  })

  it('does not show a sent status the server failed to save', async () => {
    vi.spyOn(applicationsClient, 'history').mockResolvedValue(ok(history))
    const onMarkApplied = vi.fn().mockResolvedValue(false)
    render(<ApplicationsTab exportBusy={false} onDownload={vi.fn()} onMarkApplied={onMarkApplied} />)
    await screen.findByText(/Dev \.NET · Alfa/)

    fireEvent.click(screen.getAllByRole('button', { name: /Marcar enviada/ })[0])

    expect((await screen.findByRole('alert')).textContent).toContain('Não foi possível atualizar')
    expect(screen.getAllByText('Enviada ✓')).toHaveLength(1)
  })

  it('never links a job URL that is not http(s)', async () => {
    vi.spyOn(applicationsClient, 'history').mockResolvedValue(ok([{ ...history[0], sourceUrl: 'javascript:alert(1)' }]))
    render(<ApplicationsTab exportBusy={false} onDownload={vi.fn()} onMarkApplied={vi.fn()} />)
    await screen.findByText(/Dev \.NET · Alfa/)

    expect(screen.queryByRole('link', { name: /Abrir vaga/ })).toBeNull()
  })

  it('explains an empty history and surfaces load errors', async () => {
    vi.spyOn(applicationsClient, 'history').mockResolvedValue(ok([]))
    const view = render(<ApplicationsTab exportBusy={false} onDownload={vi.fn()} onMarkApplied={vi.fn()} />)
    expect(await screen.findByText(/Gere um currículo na aba Currículo/)).toBeTruthy()
    view.unmount()

    vi.spyOn(applicationsClient, 'history').mockRejectedValue({ response: { data: { message: 'Falhou' } } })
    render(<ApplicationsTab exportBusy={false} onDownload={vi.fn()} onMarkApplied={vi.fn()} />)
    expect((await screen.findByRole('alert')).textContent).toBe('Falhou')
  })
})
