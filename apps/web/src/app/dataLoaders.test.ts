import { AxiosHeaders } from 'axios'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { profileClient } from '../api/profileClient'
import { resumeClient } from '../api/resumeClient'
import type { ResumeDocumentSummary, ResumeVersionSummary } from '../types'
import { loadJobPilotTab, type JobPilotDataLoaderState } from './dataLoaders'
import { emptyProfile, fallbackResumeLayouts } from './defaults'

function response<T>(data: T) {
  return { data, status: 200, statusText: 'OK', headers: {}, config: { headers: new AxiosHeaders() } }
}

function version(id: string, title: string, updatedAt: string): ResumeVersionSummary {
  return { id, title, updatedAt, status: 'ready', targetRole: '', targetCompany: '', atsScore: 0, kind: title.includes('Base importada') ? 'base' : 'other' }
}

function document(id: string, versions: ResumeVersionSummary[]): ResumeDocumentSummary {
  return { id, versions, title: 'Currículo', status: 'ready', sourceFileName: 'cv.pdf', sourceKind: 'pdf',
    updatedAt: '2026-10-04T12:00:00Z' }
}

const documents = [
  document('first-document', [version('latest-import', 'Base importada', '2026-10-03T12:00:00Z')]),
  document('second-document', [
    version('older-selected', 'Versão escolhida', '2026-09-01T12:00:00Z'),
    version('latest-tailored', 'Adaptado', '2026-10-04T12:00:00Z'),
  ]),
]

function selectionState(initial: string) {
  let selection = initial
  const state: JobPilotDataLoaderState = {
    setProfile: vi.fn(), setResumeDocuments: vi.fn(), setResumeLayouts: vi.fn(),
    setValidationIssues: vi.fn(), setProfileCompletion: vi.fn(),
    setSelectedResumeVersionId: (action) => { selection = typeof action === 'function' ? action(selection) : action },
  }
  return { state, selected: () => selection }
}

beforeEach(() => {
  vi.spyOn(profileClient, 'get').mockResolvedValue(response(emptyProfile))
  vi.spyOn(profileClient, 'validate').mockResolvedValue(response({ valid: true, critical: false, issues: [] }))
  vi.spyOn(profileClient, 'completion').mockResolvedValue(response({ percent: 100, passed: 1, total: 1 }))
  vi.spyOn(resumeClient, 'documents').mockResolvedValue(response(documents))
  vi.spyOn(resumeClient, 'layouts').mockResolvedValue(response(fallbackResumeLayouts))
})
afterEach(() => vi.restoreAllMocks())

describe('resume selection across refreshes', () => {
  it('preserves an older selection in another document across repeated refreshes', async () => {
    const { state, selected } = selectionState('older-selected')
    await loadJobPilotTab(state)
    expect(selected()).toBe('older-selected')
    await loadJobPilotTab(state)
    expect(selected()).toBe('older-selected')
  })

  it.each(['missing', ''])('selects the latest imported base when selection is "%s"', async (initial) => {
    const { state, selected } = selectionState(initial)
    await loadJobPilotTab(state)
    expect(selected()).toBe('latest-import')
  })

  it('falls back to the latest version when there are no imported bases', async () => {
    vi.mocked(resumeClient.documents).mockResolvedValue(response([documents[1]]))
    const { state, selected } = selectionState('missing')
    await loadJobPilotTab(state)
    expect(selected()).toBe('latest-tailored')
  })

  it.each([
    { name: 'no documents', documents: [] },
    { name: 'a document without versions', documents: [document('empty-document', [])] },
  ])('clears a stale selection with $name', async (empty) => {
    vi.mocked(resumeClient.documents).mockResolvedValue(response(empty.documents))
    const { state, selected } = selectionState('previous-session-version')
    await loadJobPilotTab(state)
    expect(selected()).toBe('')
  })

  it('preserves the selection made while documents are loading', async () => {
    let release!: () => void
    const pending = new Promise<void>((resolve) => { release = resolve })
    vi.mocked(resumeClient.documents).mockImplementation(async () => {
      await pending
      return response(documents)
    })
    const { state, selected } = selectionState('latest-import')
    const refresh = loadJobPilotTab(state)
    state.setSelectedResumeVersionId('older-selected')
    release()
    await refresh
    expect(selected()).toBe('older-selected')
  })
})
