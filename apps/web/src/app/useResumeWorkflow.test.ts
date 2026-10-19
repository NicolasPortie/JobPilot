import { AxiosHeaders } from 'axios'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { applicationsClient } from '../api/applicationsClient'
import { jobTargetClient } from '../api/jobTargetClient'
import { resumeClient } from '../api/resumeClient'
import type { JobTargetDetail, ResumeVersionDetail, TailoredResume } from '../types'
import { emptyProfile } from './defaults'
import type { ResumeWorkflowState } from './resumeWorkflowModels'
import { useResumeWorkflow } from './useResumeWorkflow'

const version: ResumeVersionDetail = {
  id: 'tailored-version', resumeDocumentId: 'document', title: 'Adaptado', status: 'draft',
  targetRole: 'Engenheira', targetCompany: 'Empresa', atsScore: 75, updatedAt: '2026-10-01T12:00:00Z', kind: 'tailored',
  plainText: 'Resumo original\nExperiência preservada', sections: [
    { id: 'summary', sectionType: 'summary', title: 'Resumo', sortOrder: 0, contentJson: '{"text":"Resumo original"}' },
    { id: 'experience', sectionType: 'experience', title: 'Experiência', sortOrder: 1, contentJson: '{"text":"Experiência preservada"}' },
  ],
}
const draft: TailoredResume = {
  tailoredVersionId: version.id, baseResumeVersionId: 'base-version', jobTargetId: 'job', evidenceMapRunId: 'map',
  status: 'draft', atsScore: 75, scoreComponents: [], reviewQuestions: [],
  sectionDiffs: [
    { sectionType: 'summary', title: 'Resumo', status: 'unchanged', beforePreview: 'Resumo original', afterPreview: 'Resumo original', afterSectionId: 'summary' },
    { sectionType: 'experience', title: 'Experiência', status: 'unchanged', beforePreview: 'Experiência preservada', afterPreview: 'Experiência preservada', afterSectionId: 'experience' },
  ],
  plainText: version.plainText, includedKeywords: [], adjacentKeywords: [], excludedKeywords: [],
  checkpoints: [], warnings: [], version,
}
const job: JobTargetDetail = {
  id: 'job', sourceKind: 'manual_text', sourceUrl: '', title: 'Engenheira', company: 'Empresa',
  location: '', seniority: '', workplace: '', summary: '', status: 'ready', confidence: 1,
  warnings: [], requirements: [], updatedAt: version.updatedAt,
}

function makeState(overrides: Partial<ResumeWorkflowState> = {}) {
  const state: ResumeWorkflowState = {
    profile: emptyProfile, selectedResumeFile: null, selectedResumeVersionId: 'base-version',
    resumeDocuments: [{ id: 'document', title: 'Importado', status: 'ready', sourceFileName: 'cv.pdf',
      sourceKind: 'pdf', updatedAt: version.updatedAt,
      versions: [{ ...version, id: 'base-version', title: 'Base importada', kind: 'base' }] }],
    atsJobDescription: '', atsJobUrl: '', atsScreenshotFile: null, atsTailoredResume: structuredClone(draft),
    atsJobTarget: null, atsUseAi: false, selectedLayout: 'ats_standard', reviewAnswers: {},
    setProfile: vi.fn(), setProfileImportBusy: vi.fn(),
    setProfileImportResult: vi.fn(), setProfileImportNotice: vi.fn(), setSelectedResumeFile: vi.fn(),
    setAtsResumeBusy: vi.fn(), setAtsJobTarget: vi.fn(), setAtsScreenshotFile: vi.fn(), setAtsExportBusy: vi.fn(),
    setAtsResumeNotice: vi.fn(), refresh: vi.fn().mockResolvedValue(undefined),
    setReviewAnswers: (action) => { state.reviewAnswers = typeof action === 'function' ? action(state.reviewAnswers) : action },
    setAtsTailoredResume: (action) => { state.atsTailoredResume = typeof action === 'function' ? action(state.atsTailoredResume) : action },
    ...overrides,
  }
  return state
}

type Request = { operation: string; body: unknown }
let requests: Request[]

function response<T>(data: T) {
  return { data, status: 200, statusText: 'OK', headers: {}, config: { headers: new AxiosHeaders() } }
}

function record<T>(operation: string, body: unknown, data: T) {
  requests.push({ operation, body })
  return Promise.resolve(response(data))
}

beforeEach(() => {
  requests = []
  vi.spyOn(resumeClient, 'saveReviewAnswer').mockImplementation((payload) => record('saveReviewAnswer', payload, { id: 'saved-answer' }))
  vi.spyOn(resumeClient, 'regenerateAfterAnswers').mockImplementation((payload) => record('regenerateAfterAnswers', payload, {
      newEvidenceMapRunId: 'new-map', newTailoredVersionId: 'new-version', status: 'draft', atsScore: 80,
      updatedEvidences: [], tailoredResume: { ...draft, tailoredVersionId: 'new-version' },
    }))
  vi.spyOn(resumeClient, 'buildAts').mockImplementation((payload) => record('buildAts', payload, draft))
  vi.spyOn(resumeClient, 'tailor').mockImplementation((payload) => record('tailor', payload, draft))
  vi.spyOn(applicationsClient, 'quick').mockImplementation((payload) => record('quick', payload, {
    baseResumeVersionId: payload.baseResumeVersionId ?? '',
    items: [{ input: payload.input, succeeded: true, target: job, tailored: draft }],
  }))
  vi.spyOn(jobTargetClient, 'organize').mockImplementation((payload) => record('organize', payload, job))
  vi.spyOn(jobTargetClient, 'organizeScreenshot').mockImplementation((payload, query) => record('organizeScreenshot', { payload, query }, job))
  vi.spyOn(resumeClient, 'updateSection').mockResolvedValue(response(version.sections[0]))
  vi.spyOn(resumeClient, 'getVersion').mockResolvedValue(response(version))
})
afterEach(() => vi.restoreAllMocks())

describe('review answer persistence and regeneration', () => {
  it('keeps requirement keys for the UI but regenerates with the server answer ID', async () => {
    const state = makeState()
    const workflow = useResumeWorkflow(state)
    await workflow.saveReviewAnswer('requirement', 'SQL', 'Conhece SQL?', 'Sim', 'proven')
    expect(state.reviewAnswers).toEqual({ requirement: { id: 'saved-answer', value: 'Sim', type: 'proven', evidence: '' } })
    await workflow.regenerateAfterAnswers()
    expect(requests.at(-1)?.body).toEqual({ answerIds: ['saved-answer'], baseResumeVersionId: 'base-version',
      jobTargetId: 'job', layout: 'ats_standard', useAi: false })
    expect(state.atsTailoredResume?.tailoredVersionId).toBe('new-version')
    expect(state.refresh).toHaveBeenCalledOnce()
  })

  it('uses the latest successful save ID when a requirement is edited again', async () => {
    const state = makeState()
    const workflow = useResumeWorkflow(state)
    await workflow.saveReviewAnswer('requirement', 'SQL', 'SQL?', 'Primeira', 'adjacent')
    vi.mocked(resumeClient.saveReviewAnswer).mockResolvedValue(response({ id: 'updated-answer' }))
    await workflow.saveReviewAnswer('requirement', 'SQL', 'SQL?', 'Segunda', 'proven')
    await workflow.regenerateAfterAnswers()
    expect(requests.at(-1)?.body).toMatchObject({ answerIds: ['updated-answer'] })
    expect(state.reviewAnswers.requirement).toMatchObject({ value: 'Segunda', type: 'proven' })
  })

  it('excludes unsaved and blank answers from regeneration', async () => {
    const answers = {
      saved: { id: 'persisted', value: 'Sim', type: 'proven', evidence: '' },
      unsaved: { value: 'Rascunho', type: 'adjacent', evidence: '' },
      blank: { id: 'blank-answer', value: '  ', type: 'missing', evidence: '' },
    }
    const state = makeState({ reviewAnswers: answers })
    await useResumeWorkflow(state).regenerateAfterAnswers()
    expect(requests[0]?.body).toMatchObject({ answerIds: ['persisted'] })
  })

  it('does not regenerate when only unsaved answers exist and clears busy state', async () => {
    const state = makeState({ reviewAnswers: { requirement: { value: 'Sim', type: 'proven', evidence: '' } } })
    await useResumeWorkflow(state).regenerateAfterAnswers()
    expect(requests).toEqual([])
    expect(state.refresh).not.toHaveBeenCalled()
    expect(state.setAtsResumeNotice).toHaveBeenLastCalledWith(expect.stringContaining('Responda'))
    expect(state.setAtsResumeBusy).toHaveBeenLastCalledWith(false)
  })

  it('preserves the last saved answer when saving fails', async () => {
    const previous = { id: 'persisted', value: 'Anterior', type: 'proven', evidence: '' }
    const state = makeState({ reviewAnswers: { requirement: previous } })
    vi.mocked(resumeClient.saveReviewAnswer).mockRejectedValue({ response: { data: { message: 'Save failed' } } })
    await useResumeWorkflow(state).saveReviewAnswer('requirement', 'SQL', 'SQL?', 'Nova', 'adjacent')
    expect(state.reviewAnswers.requirement).toEqual(previous)
    expect(state.setAtsResumeNotice).toHaveBeenLastCalledWith('Save failed')
  })
})

describe('selected base version', () => {
  function stateWithVersions(selectedResumeVersionId: string) {
    const state = makeState({ selectedResumeVersionId, atsJobDescription: 'Vaga SQL' })
    state.resumeDocuments.push({ ...state.resumeDocuments[0], id: 'other-document', versions: [
      { ...version, id: 'older-selected', title: 'Versão escolhida', updatedAt: '2026-09-01T12:00:00Z' },
      { ...version, id: 'newest-import', title: 'Base importada recente', updatedAt: '2026-10-04T12:00:00Z', kind: 'base' },
      { ...version, id: 'newest-tailored', title: 'Adaptado', updatedAt: '2026-10-05T12:00:00Z' },
    ] })
    return state
  }

  it.each(['buildAtsResume', 'buildBaseResume'] as const)(
    '%s honors an older selected version in another document', async (operation) => {
      const state = stateWithVersions('older-selected')
      await useResumeWorkflow(state)[operation]()
      expect(requests.at(-1)?.body).toMatchObject({ baseResumeVersionId: 'older-selected' })
    })

  it.each([
    ['buildAtsResume', 'missing'], ['buildAtsResume', ''],
    ['buildBaseResume', 'missing'], ['buildBaseResume', ''],
  ] as const)('%s falls back to the newest imported base for selection "%s"', async (operation, selected) => {
    await useResumeWorkflow(stateWithVersions(selected))[operation]()
    expect(requests.at(-1)?.body).toMatchObject({ baseResumeVersionId: 'newest-import' })
  })

  it('falls back to the newest version when no imported bases exist', async () => {
    const state = stateWithVersions('missing')
    state.resumeDocuments.forEach((document) => { document.versions = document.versions.map((item) => ({ ...item, title: 'Adaptado', kind: 'tailored' as const })) })
    await useResumeWorkflow(state).buildBaseResume()
    expect(requests.at(-1)?.body).toMatchObject({ baseResumeVersionId: 'newest-tailored' })
  })
})

describe('AI preference', () => {
  it.each([false, true])('passes useAi=%s through every generation entry point', async (useAi) => {
    const state = makeState({ atsUseAi: useAi })
    await useResumeWorkflow(state).buildBaseResume()
    await useResumeWorkflow(state).buildAtsResume()
    const answers = { requirement: { id: 'saved-answer', value: 'Sim', type: 'proven', evidence: '' } }
    await useResumeWorkflow(makeState({ atsUseAi: useAi, reviewAnswers: answers })).regenerateAfterAnswers()
    expect(requests.map((request) => request.body)).toEqual([
      { baseResumeVersionId: 'base-version', layout: 'ats_standard', useAi },
      { baseResumeVersionId: 'base-version', layout: 'ats_standard', useAi },
      { answerIds: ['saved-answer'], baseResumeVersionId: 'base-version', jobTargetId: 'job', layout: 'ats_standard', useAi },
    ])
  })

  it.each([
    { atsJobDescription: 'Vaga SQL', atsJobUrl: '', atsScreenshotFile: null },
    { atsJobDescription: '', atsJobUrl: 'https://example.com/job', atsScreenshotFile: null },
    { atsJobDescription: '', atsJobUrl: '', atsScreenshotFile: new File(['vaga'], 'vaga.png') },
  ])('honors disabled AI when generating from $atsJobDescription $atsJobUrl', async (input) => {
    const state = makeState(input)
    await useResumeWorkflow(state).buildAtsResume()
    if (input.atsScreenshotFile) {
      expect(requests).toHaveLength(2)
      expect(jobTargetClient.organizeScreenshot).toHaveBeenCalledWith(expect.any(FormData), 'useAi=false')
      expect(vi.mocked(jobTargetClient.organizeScreenshot).mock.calls[0][0].get('file')).toBe(input.atsScreenshotFile)
      expect(requests[1]).toMatchObject({ operation: 'tailor', body: {
        baseResumeVersionId: 'base-version', jobTargetId: 'job', layout: 'ats_standard', useAi: false,
      } })
    } else {
      // Texto e links vão numa só chamada: o servidor organiza a vaga e adapta o currículo.
      expect(requests).toHaveLength(1)
      expect(requests[0]).toMatchObject({ operation: 'quick', body: {
        input: input.atsJobDescription || input.atsJobUrl, baseResumeVersionId: 'base-version',
        layout: 'ats_standard', useAi: false,
      } })
    }
    expect(state.refresh).toHaveBeenCalledOnce()
    expect(state.setAtsResumeBusy).toHaveBeenLastCalledWith(false)
  })
})

describe('authoritative resume after section edits', () => {
  function serverVersion(contentJson: string): ResumeVersionDetail {
    return { ...version, updatedAt: '2026-10-04T12:00:00Z', plainText: 'Resumo normalizado pelo servidor\nExperiência preservada',
      sections: [{ ...version.sections[0], contentJson }, version.sections[1]] }
  }

  it.each(['{"text":"Resumo editado"}', '{"summary":"Resumo editado","skills":["SQL"]}'])(
    'projects the full server version after saving %s', async (contentJson) => {
      const authoritative = serverVersion(contentJson)
      vi.mocked(resumeClient.updateSection).mockResolvedValue(response(authoritative.sections[0]))
      vi.mocked(resumeClient.getVersion).mockResolvedValue(response(authoritative))
      const state = makeState()
      await useResumeWorkflow(state).updateTailoredSection('summary', contentJson)
      expect(resumeClient.updateSection).toHaveBeenCalledWith('summary', { contentJson })
      expect(resumeClient.getVersion).toHaveBeenCalledExactlyOnceWith('tailored-version')
      expect(vi.mocked(resumeClient.updateSection).mock.invocationCallOrder[0])
        .toBeLessThan(vi.mocked(resumeClient.getVersion).mock.invocationCallOrder[0])
      expect(state.atsTailoredResume?.plainText).toBe('Resumo normalizado pelo servidor\nExperiência preservada')
      expect(state.atsTailoredResume?.version).toEqual(authoritative)
      expect(state.atsTailoredResume?.sectionDiffs[0]).toMatchObject({ status: 'changed' })
      expect(state.atsTailoredResume?.sectionDiffs[1]).toEqual(draft.sectionDiffs[1])
      expect(state.setAtsResumeNotice).toHaveBeenLastCalledWith('Secao atualizada.')
    })

  it('keeps the complete preview if fetching the saved version fails', async () => {
    vi.mocked(resumeClient.getVersion).mockRejectedValue({ response: { data: { message: 'Reload failed' } } })
    const state = makeState()
    await useResumeWorkflow(state).updateTailoredSection('summary', '{"text":"Editado"}')
    expect(state.atsTailoredResume).toEqual(draft)
    expect(state.setAtsResumeNotice).toHaveBeenLastCalledWith('Reload failed')
  })

  it('does not load a version or modify the preview if saving the section fails', async () => {
    vi.mocked(resumeClient.updateSection).mockRejectedValue({ response: { data: { message: 'Update failed' } } })
    const state = makeState()
    await useResumeWorkflow(state).updateTailoredSection('summary', '{"text":"Editado"}')
    expect(resumeClient.getVersion).not.toHaveBeenCalled()
    expect(state.atsTailoredResume).toEqual(draft)
    expect(state.setAtsResumeNotice).toHaveBeenLastCalledWith('Update failed')
  })

  it('does not overwrite a new draft selected while the section save is in flight', async () => {
    let release!: () => void
    const pending = new Promise<void>((resolve) => { release = resolve })
    vi.mocked(resumeClient.updateSection).mockImplementation(async () => {
      await pending
      return response(version.sections[0])
    })
    vi.mocked(resumeClient.getVersion).mockResolvedValue(response(serverVersion('{"text":"Editado"}')))
    const state = makeState()
    const workflow = useResumeWorkflow(state)
    const update = workflow.updateTailoredSection('summary', '{"text":"Editado"}')
    const replacement = { ...draft, tailoredVersionId: 'replacement', plainText: 'Novo currículo',
      version: { ...version, id: 'replacement', plainText: 'Novo currículo' } }
    state.atsTailoredResume = replacement
    release()
    await update
    expect(state.atsTailoredResume).toEqual(replacement)
  })
})
