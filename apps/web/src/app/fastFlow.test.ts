import { AxiosHeaders } from 'axios'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { applicationsClient } from '../api/applicationsClient'
import { resumeClient } from '../api/resumeClient'
import type { JobTargetDetail, QuickTailorItem, TailoredResume } from '../types'
import { emptyProfile } from './defaults'
import { downloadResumeVersion, fileNameFromDisposition } from './downloads'
import type { ResumeWorkflowState } from './resumeWorkflowModels'
import { useResumeWorkflow } from './useResumeWorkflow'

function ok<T>(data: T, headers: Record<string, string> = {}) {
  return { data, status: 200, statusText: 'OK', headers, config: { headers: new AxiosHeaders() } }
}

const target = (id: string, company: string): JobTargetDetail => ({
  id, sourceKind: 'url', sourceUrl: `https://vagas.example/${id}`, title: 'Dev .NET', company, location: '',
  seniority: '', workplace: '', summary: '', status: 'organized', confidence: 1, warnings: [], requirements: [], updatedAt: '',
})
const tailored = (id: string) => ({ tailoredVersionId: id, atsScore: 70, status: 'ready' }) as TailoredResume
const item = (id: string, succeeded = true): QuickTailorItem => succeeded
  ? { input: `https://vagas.example/${id}`, succeeded, target: target(id, `Empresa ${id}`), tailored: tailored(`v-${id}`) }
  : { input: `https://vagas.example/${id}`, succeeded, error: 'O site bloqueou a leitura.' }

function makeState(overrides: Partial<ResumeWorkflowState> = {}) {
  const calls: Record<string, unknown[]> = {}
  const record = (name: string) => vi.fn((value: unknown) => { (calls[name] ??= []).push(value) })
  const state: ResumeWorkflowState = {
    profile: emptyProfile, selectedResumeFile: null, selectedResumeVersionId: 'base',
    resumeDocuments: [{ id: 'd', title: 'Importado', status: 'ready', sourceFileName: 'cv.pdf', sourceKind: 'pdf', updatedAt: '',
      versions: [
        { id: 'adaptada', title: 'Adaptada', status: 'ready', targetRole: '', targetCompany: '', atsScore: 1, updatedAt: '2026-10-05T00:00:00Z', kind: 'tailored' },
        { id: 'base', title: 'Versão base importada', status: 'draft', targetRole: '', targetCompany: '', atsScore: 1, updatedAt: '2026-10-01T00:00:00Z', kind: 'base' },
      ] }],
    atsJobDescription: '', atsJobUrl: 'https://vagas.example/1\nhttps://vagas.example/2', atsScreenshotFile: null,
    atsTailoredResume: null, atsJobTarget: null, atsUseAi: false, selectedLayout: 'ats_standard', reviewAnswers: {},
    setRecommendedSkillAdditions: vi.fn(), setProfile: vi.fn(), setProfileImportBusy: vi.fn(), setProfileImportResult: vi.fn(),
    setProfileImportNotice: vi.fn(), setSelectedResumeFile: vi.fn(), setAtsResumeBusy: vi.fn(), setAtsJobTarget: record('target'),
    setAtsTailoredResume: record('tailored'), setAtsScreenshotFile: vi.fn(), setAtsExportBusy: vi.fn(),
    setAtsResumeNotice: record('notice'), setReviewAnswers: vi.fn(), refresh: vi.fn().mockResolvedValue(undefined),
    setAtsBatchItems: record('batch'), setAppliedVersionIds: record('applied'), clearJobInputs: vi.fn(),
    ...overrides,
  }
  return { state, calls }
}

beforeEach(() => { vi.spyOn(resumeClient, 'buildAts').mockResolvedValue(ok(tailored('ats'))) })
afterEach(() => vi.restoreAllMocks())

describe('file name of downloads', () => {
  it('reads the server file name, preferring the UTF-8 form', () => {
    expect(fileNameFromDisposition(`attachment; filename=Curriculo-Ana.pdf; filename*=UTF-8''Curr%C3%ADculo-Ana.pdf`, 'x.pdf'))
      .toBe('Currículo-Ana.pdf')
    expect(fileNameFromDisposition('attachment; filename="Curriculo-Ana-Beta.pdf"', 'x.pdf')).toBe('Curriculo-Ana-Beta.pdf')
    expect(fileNameFromDisposition(undefined, 'curriculo.pdf')).toBe('curriculo.pdf')
  })

  it('exports on the server and saves the file with the server name', async () => {
    vi.spyOn(resumeClient, 'exportVersion').mockResolvedValue(ok({ id: 'export-1', parseWarnings: ['Texto curto'] }))
    vi.spyOn(resumeClient, 'downloadExport').mockResolvedValue(ok(new Blob(['pdf']), { 'content-disposition': 'attachment; filename=Curriculo-Ana-Beta.pdf' }) as never)
    const created = vi.fn().mockReturnValue('blob:x')
    vi.stubGlobal('URL', { createObjectURL: created, revokeObjectURL: vi.fn() })
    const clicked: string[] = []
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { clicked.push(this.download) })

    const result = await downloadResumeVersion('v1', 'pdf', 'ats_standard')

    expect(resumeClient.exportVersion).toHaveBeenCalledWith('v1', { format: 'pdf', layout: 'ats_standard' })
    expect(clicked).toEqual(['Curriculo-Ana-Beta.pdf'])
    expect(result).toEqual({ fileName: 'Curriculo-Ana-Beta.pdf', warnings: ['Texto curto'] })
    vi.unstubAllGlobals()
  })
})

describe('quick flow in the workflow hook', () => {
  it('keeps several links as a batch and does not open a single result', async () => {
    vi.spyOn(applicationsClient, 'quick').mockResolvedValue(ok({ baseResumeVersionId: 'base', items: [item('1'), item('2', false)] }))
    const { state, calls } = makeState()

    await useResumeWorkflow(state).buildAtsResume()

    expect(applicationsClient.quick).toHaveBeenCalledWith(expect.objectContaining({
      input: 'https://vagas.example/1\nhttps://vagas.example/2', baseResumeVersionId: 'base', useAi: false,
    }))
    expect(calls.batch?.at(-1)).toHaveLength(2)
    expect(calls.tailored?.at(-1)).toBeNull()
    expect(calls.notice?.at(-1)).toBe('1 de 2 currículos gerados.')
  })

  it('shows the server reason when a single link cannot be read', async () => {
    vi.spyOn(applicationsClient, 'quick').mockResolvedValue(ok({ baseResumeVersionId: 'base', items: [item('1', false)] }))
    const { state, calls } = makeState({ atsJobUrl: 'https://vagas.example/1' })

    await useResumeWorkflow(state).buildAtsResume()

    expect(calls.notice?.at(-1)).toBe('O site bloqueou a leitura.')
    expect(calls.tailored).toBeUndefined()
  })

  it('sends pasted text with the link as the source to open later', async () => {
    vi.spyOn(applicationsClient, 'quick').mockResolvedValue(ok({ baseResumeVersionId: 'base', items: [item('1')] }))
    const { state } = makeState({ atsJobDescription: 'Texto da vaga', atsJobUrl: 'https://vagas.example/1' })

    await useResumeWorkflow(state).buildAtsResume()

    expect(applicationsClient.quick).toHaveBeenCalledWith(expect.objectContaining({ input: 'Texto da vaga', jobUrl: 'https://vagas.example/1' }))
  })

  it('starts the next job from a clean slate', () => {
    const { state, calls } = makeState()

    useResumeWorkflow(state).startNextJob()

    expect(calls.target?.at(-1)).toBeNull()
    expect(calls.tailored?.at(-1)).toBeNull()
    expect(calls.batch?.at(-1)).toEqual([])
    expect(state.clearJobInputs).toHaveBeenCalledOnce()
  })

  it('marks and unmarks an application as sent', async () => {
    vi.spyOn(applicationsClient, 'setApplied').mockResolvedValue(ok(undefined))
    const { state } = makeState()
    const applied: string[][] = []
    state.setAppliedVersionIds = (action) => { applied.push(typeof action === 'function' ? action(applied.at(-1) ?? []) : action) }

    await useResumeWorkflow(state).markApplied('v1', true)
    await useResumeWorkflow(state).markApplied('v1', false)

    expect(applicationsClient.setApplied).toHaveBeenNthCalledWith(1, 'v1', true)
    expect(applicationsClient.setApplied).toHaveBeenNthCalledWith(2, 'v1', false)
    expect(applied).toEqual([['v1'], []])
  })
})
