import { useState } from 'react'
import type { ComponentProps } from 'react'
import { AxiosHeaders } from 'axios'
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { resumeClient } from '../../api/resumeClient'
import type { JobTargetDetail, ResumeSection, TailoredResume } from '../../types'
import { ResumeAtsTab } from './ResumeAtsTab'

vi.mock('../../api/resumeClient', () => ({ resumeClient: { listSections: vi.fn() } }))

type Props = ComponentProps<typeof ResumeAtsTab>
const version = {
  id: 'base', title: 'Base', status: 'ready', targetRole: 'Analista', targetCompany: '',
  atsScore: 0, updatedAt: '2026-10-01T12:00:00Z',
}

function section(sectionType: string, content: object): ResumeSection {
  return { id: sectionType, sectionType, title: sectionType, sortOrder: 0, contentJson: JSON.stringify(content) }
}

function sectionsResponse(data: ResumeSection[]) {
  return { data, status: 200, statusText: 'OK', headers: {}, config: { headers: new AxiosHeaders() } }
}

function makeProps(overrides: Partial<Props> = {}): Props {
  return {
    profile: {
      fullName: 'Ana Silva', email: 'ana@example.com', phone: '', city: '', state: '', country: '',
      linkedinUrl: '', githubUrl: '', portfolioUrl: '', professionalSummary: 'Resumo da base',
      skills: ['SQL'], targetRole: '', seniority: '', salaryExpectation: '',
    },
    validationIssues: [],
    documents: [{ id: 'document', title: 'Importado', status: 'ready', sourceFileName: 'cv.pdf',
      sourceKind: 'pdf', updatedAt: version.updatedAt, versions: [version] }],
    selectedVersionId: 'base', onSelectVersion: vi.fn(), jobDescription: '', setJobDescription: vi.fn(),
    jobUrl: '', setJobUrl: vi.fn(), screenshotFile: null, setScreenshotFile: vi.fn(),
    draft: null, jobTarget: null, busy: false, exportBusy: false, notice: '', useAi: false,
    onToggleUseAi: vi.fn(), onBuild: vi.fn(), onBuildBase: vi.fn(), onExport: vi.fn(), setTab: vi.fn(),
    layoutOptions: [{ key: 'simple', label: 'Simples', description: 'Uma coluna', atsNotes: '', recommendedForAts: true }],
    selectedLayout: 'simple', setSelectedLayout: vi.fn(), onRegenerateAfterAnswers: vi.fn().mockResolvedValue(undefined),
    onAddProfileSkill: vi.fn().mockResolvedValue(undefined), onUpdateTailoredSection: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  }
}

function makeDraft(overrides: Partial<TailoredResume> = {}): TailoredResume {
  return {
    tailoredVersionId: 'tailored', baseResumeVersionId: 'base', jobTargetId: 'job', evidenceMapRunId: 'map',
    status: 'draft', atsScore: 76.4, scoreComponents: [], sectionDiffs: [], reviewQuestions: [],
    plainText: 'Texto gerado', includedKeywords: ['SQL'], adjacentKeywords: ['Docker'],
    excludedKeywords: ['Python', 'python', 'SQL'], checkpoints: [], warnings: [], usedAi: false,
    version: { ...version, id: 'tailored', resumeDocumentId: 'document', plainText: 'Texto gerado', sections: [
      section('summary', { summary: 'Resumo adaptado' }),
      section('experience', { items: [{ role: 'Engenheira', company: 'Empresa', period: '2024–2026' }] }),
      section('education', { items: [{ degree: 'Computação', institution: 'Universidade', year: '2023' }] }),
      section('skills', { skills: ['TypeScript'] }), section('plain_text', { text: 'Texto gerado' }),
    ] },
    ...overrides,
  }
}

function makeJob(): JobTargetDetail {
  return {
    id: 'job', sourceKind: 'text', sourceUrl: '', title: 'Engenharia', company: 'Empresa', location: '',
    seniority: '', workplace: '', summary: '', status: 'ready', confidence: 1, warnings: ['Confira a fonte'],
    updatedAt: version.updatedAt,
    requirements: ['SQL', 'Docker', 'Python'].map((label, sortOrder) => ({
      id: label, label, category: 'skill', importance: 'required', evidenceText: `${label} na vaga`,
      normalizedKeyword: label.toLowerCase(), exactPhrases: [], sortOrder,
    })),
  }
}

function InputHarness({ props }: { props: Props }) {
  const [jobDescription, setJobDescription] = useState(props.jobDescription)
  const [jobUrl, setJobUrl] = useState(props.jobUrl)
  const [screenshotFile, setScreenshotFile] = useState<File | null>(null)
  return <ResumeAtsTab {...props} {...{ jobDescription, setJobDescription, jobUrl, setJobUrl, screenshotFile, setScreenshotFile }} />
}

describe('ResumeAtsTab workflow', () => {
  beforeEach(() => vi.mocked(resumeClient.listSections).mockResolvedValue(sectionsResponse([])))
  afterEach(() => { cleanup(); vi.resetAllMocks(); vi.unstubAllGlobals() })

  it('keeps job inputs across tabs and enables adaptation from an attached file', async () => {
    const props = makeProps()
    render(<InputHarness props={props} />)
    const build = screen.getByRole('button', { name: 'Adaptar currículo para esta vaga' }) as HTMLButtonElement
    expect(build.disabled).toBe(true)
    fireEvent.change(screen.getByLabelText(/Descrição da vaga/), { target: { value: 'Descrição original' } })
    fireEvent.click(screen.getByRole('tab', { name: 'Link da vaga' }))
    fireEvent.change(screen.getByLabelText('Link da vaga'), { target: { value: 'https://empresa.com/vaga' } })
    fireEvent.click(screen.getByRole('tab', { name: 'Colar descrição da vaga' }))
    expect((screen.getByLabelText(/Descrição da vaga/) as HTMLTextAreaElement).value).toBe('Descrição original')
    fireEvent.change(screen.getByLabelText(/Descrição da vaga/), { target: { value: '' } })
    fireEvent.click(screen.getByRole('tab', { name: 'Link da vaga' }))
    expect((screen.getByLabelText('Link da vaga') as HTMLInputElement).value).toBe('https://empresa.com/vaga')
    fireEvent.change(screen.getByLabelText('Link da vaga'), { target: { value: '' } })
    fireEvent.click(screen.getByRole('tab', { name: 'Anexar PDF ou imagem' }))
    fireEvent.change(screen.getByLabelText(/Escolher arquivo/), { target: { files: [new File(['vaga'], 'vaga.pdf')] } })
    expect(build.disabled).toBe(false)
    fireEvent.click(build)
    expect(props.onBuild).toHaveBeenCalledOnce()
    fireEvent.click(screen.getByRole('button', { name: 'Remover arquivo vaga.pdf' }))
    expect(build.disabled).toBe(true)
  })

  it('selects the newest base as fallback and cycles versions without resetting the job tab', () => {
    const props = makeProps({ selectedVersionId: 'missing' })
    props.documents[0].versions = [version, { ...version, id: 'newest', updatedAt: '2026-10-03T12:00:00Z' }]
    const view = render(<ResumeAtsTab {...props} />)
    fireEvent.click(screen.getByRole('tab', { name: 'Link da vaga' }))
    fireEvent.click(screen.getByRole('button', { name: /Trocar/ }))
    expect(props.onSelectVersion).toHaveBeenCalledWith('base')
    view.rerender(<ResumeAtsTab {...props} selectedVersionId="base" />)
    expect(screen.getByRole('tab', { name: 'Link da vaga' }).getAttribute('aria-selected')).toBe('true')
    fireEvent.click(screen.getByRole('button', { name: /Trocar/ }))
    expect(props.onSelectVersion).toHaveBeenLastCalledWith('newest')
  })

  it('shows no preview before generation and uses backend validation for base readiness', () => {
    const props = makeProps({ validationIssues: [{ fieldName: 'email', errorMessage: 'Obrigatório', isCritical: true }] })
    render(<ResumeAtsTab {...props} />)
    const rail = screen.getByRole('complementary', { name: 'Prévia e aderência' })
    expect(within(rail).getByText('Nenhuma prévia ainda')).toBeTruthy()
    expect(within(rail).queryByText('Resumo da base')).toBeNull()
    expect(within(rail).getByText('Currículo base pendente')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /Trocar/ }))
    expect(props.setTab).toHaveBeenCalledWith('profile')
  })

  it('renders tailored sections, requirement verdicts and real base projects', async () => {
    vi.mocked(resumeClient.listSections).mockResolvedValue(sectionsResponse([section('projects', { items: [{ name: 'Projeto real' }] })]))
    const props = makeProps({ draft: makeDraft(), jobTarget: makeJob() })
    render(<ResumeAtsTab {...props} />)
    const rail = screen.getByRole('complementary', { name: 'Prévia e aderência' })
    expect(within(rail).getByText('Resumo adaptado')).toBeTruthy()
    expect(within(rail).queryByText('Resumo da base')).toBeNull()
    expect(within(rail).getByText('Engenheira')).toBeTruthy()
    expect(within(rail).getByText('TypeScript')).toBeTruthy()
    expect(within(rail).getByText('Computação')).toBeTruthy()
    expect(within(rail).getByRole('img', { name: 'Aderência de 76%' })).toBeTruthy()
    const requirements = screen.getByRole('region', { name: 'Requisitos em contexto' })
    for (const label of ['· Comprovado', '· Relacionado', '· Revisar']) expect(within(requirements).getByText(label)).toBeTruthy()
    expect(screen.getByText(/1 de 3 requisitos comprovados/)).toBeTruthy()
    expect(await screen.findByText(/Destaque projetos como Projeto real/)).toBeTruthy()
    expect(screen.getByText(/como Python e Docker/)).toBeTruthy()
    const gaps = screen.getByRole('region', { name: 'Lacunas para você decidir' })
    expect(within(gaps).getAllByRole('button')).toHaveLength(1)
    fireEvent.click(within(gaps).getByRole('button', { name: /Python/ }))
    expect(props.onAddProfileSkill).toHaveBeenCalledWith('Python', { kind: 'missing', reason: 'Requisito detectado na vaga.' })
  })

  it('saves edits through the diff section fallback and resets text when adapting again', async () => {
    const draft = makeDraft()
    draft.version.sections = draft.version.sections.filter((item) => item.sectionType !== 'plain_text')
    draft.sectionDiffs = [{ sectionType: 'plain_text', title: 'Texto', status: 'changed', beforePreview: '', afterPreview: '', afterSectionId: 'diff-text' }]
    let finishSave!: () => void
    const props = makeProps({ draft, jobDescription: 'Vaga', onUpdateTailoredSection: vi.fn(() => new Promise<void>((resolve) => { finishSave = resolve })) })
    render(<ResumeAtsTab {...props} />)
    const output = screen.getByRole('textbox', { name: 'Texto do currículo adaptado' }) as HTMLTextAreaElement
    fireEvent.change(output, { target: { value: 'Revisão pessoal' } })
    fireEvent.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(props.onUpdateTailoredSection).toHaveBeenCalledWith('diff-text', '{"text":"Revisão pessoal"}')
    expect((screen.getByRole('button', { name: 'Salvar rascunho' }) as HTMLButtonElement).disabled).toBe(true)
    await act(async () => finishSave())
    expect(output.value).toBe('Texto gerado')
    fireEvent.change(output, { target: { value: 'Outro texto' } })
    fireEvent.click(screen.getByRole('button', { name: 'Adaptar currículo para esta vaga' }))
    expect(output.value).toBe('Texto gerado')
    expect(props.onBuild).toHaveBeenCalledOnce()
    fireEvent.click(screen.getByRole('button', { name: 'Word' }))
    fireEvent.click(screen.getByRole('button', { name: 'Exportar PDF' }))
    expect(props.onExport).toHaveBeenNthCalledWith(1, 'word')
    expect(props.onExport).toHaveBeenNthCalledWith(2, 'pdf')
  })

  it('ignores late base section responses after selecting another version', async () => {
    let finishFirst!: (response: ReturnType<typeof sectionsResponse>) => void
    vi.mocked(resumeClient.listSections)
      .mockImplementationOnce(() => new Promise((resolve) => { finishFirst = resolve }))
      .mockResolvedValueOnce(sectionsResponse([section('projects', { items: [{ name: 'Projeto atual' }] })]))
    const props = makeProps()
    props.documents[0].versions.push({ ...version, id: 'second' })
    const view = render(<ResumeAtsTab {...props} />)
    view.rerender(<ResumeAtsTab {...props} selectedVersionId="second" />)
    expect(await screen.findByText(/Destaque projetos como Projeto atual/)).toBeTruthy()
    await act(async () => finishFirst(sectionsResponse([section('projects', { items: [{ name: 'Projeto antigo' }] })])))
    await waitFor(() => expect(screen.queryByText(/Destaque projetos como Projeto antigo/)).toBeNull())
    expect(screen.getByText(/Destaque projetos como Projeto atual/)).toBeTruthy()
  })
})
