import { useMemo, useState } from 'react'
import type { Dispatch, SetStateAction } from 'react'
import {
  AlertCircle,
  ArrowLeftRight,
  ArrowRight,
  ExternalLink,
  Check,
  Copy,
  Download,
  Send,
  FileText,
  PencilLine,
  RefreshCw,
  Save,
  ShieldCheck,
} from 'lucide-react'
import { ResumeAiControl } from './ResumeAiControl'
import { ResumeAtsHeader } from './ResumeAtsHeader'
import { ResumeJobInput } from './ResumeJobInput'
import { ResumeAtsPreview } from './ResumeAtsPreview'
import { ResumeAtsInsights } from './ResumeAtsInsights'
import type { RequirementReviewItem, RequirementStatus } from './resumeAtsPresentation'
import { ResumeReviewCard } from './ResumeReviewCard'
import { SectionEditorPanel } from '../profile/ProfileSectionEditor'
import type {
  JobTargetDetail,
  Profile,
  ProfileValidationIssue,
  QuickTailorItem,
  ResumeDocumentSummary,
  ResumeLayoutOption,
  TailoredResume,
} from '../../types'

type Props = {
  profile: Profile
  validationIssues: ProfileValidationIssue[]
  documents: ResumeDocumentSummary[]
  selectedVersionId: string
  onSelectVersion: (id: string) => void
  jobDescription: string
  setJobDescription: Dispatch<SetStateAction<string>>
  jobUrl: string
  setJobUrl: (url: string) => void
  screenshotFile: File | null
  setScreenshotFile: Dispatch<SetStateAction<File | null>>
  draft: TailoredResume | null
  jobTarget: JobTargetDetail | null
  busy: boolean
  exportBusy: boolean
  notice: string
  useAi: boolean
  onToggleUseAi: (value: boolean) => void
  onBuild: () => void
  onBuildBase?: () => void
  onExport: (format: 'word' | 'pdf') => void
  setTab: (tab: string) => void
  layoutOptions: ResumeLayoutOption[]
  selectedLayout: string
  setSelectedLayout: Dispatch<SetStateAction<string>>
  onRegenerateAfterAnswers: () => Promise<void>
  onSaveReviewAnswer?: (requirementId: string, label: string, question: string, value: string, type: string) => Promise<void>
  onAddProfileSkill: (skill: string, context?: { kind?: 'missing' | 'related'; reason?: string }) => Promise<void>
  onUpdateTailoredSection: (sectionId: string, contentJson: string) => Promise<void>
  /** Fluxo rápido: lote gerado, candidaturas já marcadas e ações por versão. */
  batchItems?: QuickTailorItem[]
  appliedVersionIds?: string[]
  onDownloadVersion?: (versionId: string, format: 'word' | 'pdf') => void
  onMarkApplied?: (versionId: string, applied: boolean) => void
  onNextJob?: () => void
}

export function ResumeAtsTab(props: Props) {
  const {
    profile,
    validationIssues,
    documents,
    selectedVersionId,
    onSelectVersion,
    jobDescription,
    setJobDescription,
    jobUrl,
    setJobUrl,
    screenshotFile,
    setScreenshotFile,
    draft,
    jobTarget,
    busy,
    exportBusy,
    notice,
    useAi,
    onToggleUseAi,
    onBuild,
    onBuildBase,
    onExport,
    setTab,
    layoutOptions,
    selectedLayout,
    setSelectedLayout,
    onRegenerateAfterAnswers,
    onSaveReviewAnswer,
    onAddProfileSkill,
    onUpdateTailoredSection,
    batchItems = [],
    appliedVersionIds = [],
    onDownloadVersion,
    onMarkApplied,
    onNextJob,
  } = props
  const [showSectionEditor, setShowSectionEditor] = useState(false)
  const [textOverride, setTextOverride] = useState<string | null>(null)
  const [savingOutput, setSavingOutput] = useState(false)

  const versions = documents.flatMap((document) =>
    document.versions.map((version) => ({ ...version, documentTitle: document.title })),
  )
  // A adaptação parte sempre de um currículo importado (kind vem do backend), nunca de uma versão já adaptada.
  const baseCandidates = versions.filter((version) =>
    version.kind ? version.kind === 'base' : version.title.toLowerCase().includes('base importada'))
  const selectable = baseCandidates.length > 0 ? baseCandidates : versions
  const activeVersion = selectable.find((version) => version.id === selectedVersionId)
    ?? [...selectable].sort((left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt))[0]
  const activeVersionId = activeVersion?.id ?? ''

  const hasJobInput = Boolean(jobDescription.trim() || jobUrl.trim() || screenshotFile)
  const outputText = textOverride ?? draft?.plainText ?? ''
  const plainTextSectionId = draft?.version.sections.find((section) => section.sectionType === 'plain_text')?.id
    ?? draft?.sectionDiffs.find((diff) => diff.sectionType === 'plain_text')?.afterSectionId

  // Prontidão da base vem do veredito do backend (ProfileGuard), nunca de
  // regra inventada no frontend. O frontend só agrupa por seção para exibir.
  const criticalFields = new Set(
    validationIssues.filter((issue) => issue.isCritical).map((issue) => issue.fieldName),
  )
  const baseReady = criticalFields.size === 0

  const requirementReview = useMemo(() => {
    const included = new Set((draft?.includedKeywords ?? []).map(normalizeKeyword))
    const adjacent = new Set((draft?.adjacentKeywords ?? []).map(normalizeKeyword))
    const excluded = new Set((draft?.excludedKeywords ?? []).map(normalizeKeyword))

    return (jobTarget?.requirements ?? []).map((requirement): RequirementReviewItem => {
      const keys = [requirement.label, requirement.normalizedKeyword, ...requirement.exactPhrases]
        .map(normalizeKeyword)
        .filter(Boolean)
      const status = keys.some((key) => included.has(key))
        ? 'direct'
        : keys.some((key) => adjacent.has(key))
          ? 'adjacent'
          : keys.some((key) => excluded.has(key))
            ? 'missing'
            : 'missing'
      return { ...requirement, status }
    })
  }, [draft?.adjacentKeywords, draft?.excludedKeywords, draft?.includedKeywords, jobTarget?.requirements])

  const missingSuggestions = useMemo(() => {
    const profileSkills = new Set(profile.skills.map(normalizeKeyword))
    return (draft?.excludedKeywords ?? [])
      .filter((skill) => skill.trim() && !profileSkills.has(normalizeKeyword(skill)))
      .filter((skill, index, all) =>
        all.findIndex((candidate) => normalizeKeyword(candidate) === normalizeKeyword(skill)) === index,
      )
      .slice(0, 4)
  }, [draft?.excludedKeywords, profile.skills])

  const directItems = requirementReview.filter((item) => item.status === 'direct')
  // Sem fallback inventado: vazio e vazio.
  const previewRole = profile.targetRole || activeVersion?.targetRole || ''

  async function copyOutput() {
    if (outputText) await navigator.clipboard.writeText(outputText)
  }

  async function saveOutput() {
    if (!plainTextSectionId || !outputText.trim()) return
    setSavingOutput(true)
    try {
      await onUpdateTailoredSection(plainTextSectionId, JSON.stringify({ text: outputText }))
      setTextOverride(null)
    } finally {
      setSavingOutput(false)
    }
  }

  const canBuild = !busy && Boolean(activeVersion) && hasJobInput

  function buildResume() {
    if (!canBuild) return
    setTextOverride(null)
    onBuild()
  }

  const jobUrlToOpen = jobTarget?.sourceUrl || (/^https?:\/\/\S+$/i.test(jobUrl.trim()) ? jobUrl.trim() : '')
  const currentApplied = draft ? appliedVersionIds.includes(draft.tailoredVersionId) : false

  function cycleVersion() {
    if (selectable.length > 1 && activeVersion) {
      const index = selectable.findIndex((version) => version.id === activeVersion.id)
      onSelectVersion(selectable[(index + 1) % selectable.length].id)
    } else {
      setTab('profile')
    }
  }

  function scrollTo(id: string) {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  return (
    <div>
      <ResumeAtsHeader profile={profile} />
      <div className="cv-page">
        <div className="cv-main">

          {notice && <div className="cv-alert cv-alert--info" role="status"><AlertCircle size={16} className="shrink-0" />{notice}</div>}

          <section className="cv-card cv-step cv-step--first" aria-labelledby="cv-passo1">
            <div className="cv-stephead">
              <span className="cv-stepnum" aria-hidden>1</span>
              <div>
                <h2 className="cv-steptitle" id="cv-passo1">Currículo base</h2>
                <p className="cv-stepsub">Selecione o currículo que será adaptado para esta vaga.</p>
              </div>
            </div>
            <div className="cv-baserow">
              <span className="cv-baserow__icon"><FileText size={20} /></span>
              <div className="min-w-0 flex-1">
                <p className="cv-baserow__title truncate">{activeVersion ? `Currículo base – ${activeVersion.documentTitle}` : 'Nenhum currículo importado'}</p>
                <p className="cv-baserow__meta">
                  {activeVersion ? `Atualizado em ${formatDate(activeVersion.updatedAt)}` : 'Importe seu currículo no Perfil para começar.'}
                </p>
              </div>
              <span className={`cv-badge ${baseReady && activeVersion ? 'cv-badge--green' : 'cv-badge--amber'}`}>
                {activeVersion && baseReady && <Check size={13} />}
                {activeVersion && baseReady ? 'Base pronta' : 'Ação necessária'}
              </span>
              <button className="cv-btn cv-btn--secondary" style={{ minHeight: 38 }} onClick={cycleVersion} title={selectable.length > 1 ? 'Alternar entre currículos importados' : 'Importar currículo no Perfil'}>
                Trocar <ArrowLeftRight size={15} />
              </button>
            </div>
            {activeVersion && (
              <div style={{ marginTop: 12 }}>
                <button className="cv-helplink" onClick={() => setShowSectionEditor((value) => !value)}>
                  <PencilLine size={14} /> {showSectionEditor ? 'Fechar base editável' : 'Editar seções da base'}
                </button>
                {showSectionEditor && (
                  <div style={{ marginTop: 12 }}><SectionEditorPanel versionId={activeVersion.id} /></div>
                )}
              </div>
            )}
          </section>

          <ResumeJobInput
            jobDescription={jobDescription}
            setJobDescription={setJobDescription}
            jobUrl={jobUrl}
            setJobUrl={setJobUrl}
            screenshotFile={screenshotFile}
            setScreenshotFile={setScreenshotFile}
            onSubmit={buildResume}
          />

          <section className="cv-card cv-step cv-step--last" aria-labelledby="cv-passo3">
            <div className="cv-stephead">
              <span className="cv-stepnum" aria-hidden>3</span>
              <div>
                <h2 className="cv-steptitle" id="cv-passo3">Formato de saída</h2>
                <p className="cv-stepsub">Escolha o formato ideal para sua área e objetivo.</p>
              </div>
            </div>
            <div className="cv-field">
              <label className="cv-label sr-only" htmlFor="cv-layout">Formato de saída</label>
              <select id="cv-layout" className="cv-input" value={selectedLayout} onChange={(event) => setSelectedLayout(event.target.value)}>
                {layoutOptions.map((layout) => (
                  <option key={layout.key} value={layout.key}>{layout.label}</option>
                ))}
              </select>
              {layoutOptions.find((layout) => layout.key === selectedLayout)?.description && (
                <p className="cv-hint">{layoutOptions.find((layout) => layout.key === selectedLayout)?.description}</p>
              )}
            </div>
            {onBuildBase && (
              <p className="cv-hint">
                Sem vaga em mãos?{' '}
                <button className="cv-helplink" onClick={() => { setTextOverride(null); onBuildBase() }} disabled={busy || !activeVersion}>
                  Gerar base ATS (sem vaga)
                </button>
              </p>
            )}
          </section>

          {batchItems.length > 0 && (
            <section className="cv-card" aria-label="Currículos gerados">
              <h2 className="cv-steptitle">Currículos gerados</h2>
              <p className="cv-stepsub">Baixe cada PDF, abra a vaga e envie a candidatura. Marque como enviada para acompanhar depois.</p>
              <div className="cv-batch">
                {batchItems.map((item) => {
                  const versionId = item.tailored?.tailoredVersionId ?? ''
                  const applied = appliedVersionIds.includes(versionId)
                  const link = item.target?.sourceUrl ?? ''
                  return (
                    <div key={item.input} className={`cv-batch__item${item.succeeded ? '' : ' cv-batch__item--error'}`}>
                      <div className="cv-batch__body">
                        <p className="cv-batch__title">{item.target ? `${item.target.title}${item.target.company ? ` · ${item.target.company}` : ''}` : item.input}</p>
                        {item.succeeded && item.tailored && <p className="cv-batch__meta">Aderência {Math.round(item.tailored.atsScore)} · {item.tailored.status === 'ready' ? 'pronto' : 'revise antes de enviar'}</p>}
                        {!item.succeeded && <p className="cv-batch__error">{item.error ?? 'Não foi possível gerar este currículo.'}</p>}
                      </div>
                      {item.succeeded && versionId && (
                        <div className="cv-resultbar__actions">
                          <button className="cv-btn cv-btn--primary cv-btn--small" onClick={() => onDownloadVersion?.(versionId, 'pdf')} disabled={exportBusy}><Download size={14} /> PDF</button>
                          <button className="cv-btn cv-btn--secondary cv-btn--small" onClick={() => onDownloadVersion?.(versionId, 'word')} disabled={exportBusy}>Word</button>
                          {link && <a className="cv-btn cv-btn--secondary cv-btn--small" href={link} target="_blank" rel="noreferrer noopener"><ExternalLink size={14} /> Abrir vaga</a>}
                          <button className="cv-btn cv-btn--secondary cv-btn--small" aria-pressed={applied} onClick={() => onMarkApplied?.(versionId, !applied)}>
                            <Send size={14} /> {applied ? 'Enviada ✓' : 'Marcar enviada'}
                          </button>
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
              {onNextJob && <div className="cv-row" style={{ marginTop: 14 }}><button className="cv-btn cv-btn--secondary cv-btn--small" onClick={onNextJob}>Novas vagas</button></div>}
            </section>
          )}

          {!draft ? null : (
            <>
              <section className="cv-card cv-resultbar" aria-label="Ações do currículo gerado">
                <div className="cv-resultbar__info">
                  <p className="cv-resultbar__title">{jobTarget ? `${jobTarget.title}${jobTarget.company ? ` · ${jobTarget.company}` : ''}` : draft.version.title}</p>
                  <p className="cv-resultbar__meta">Aderência {Math.round(draft.atsScore)} · {draft.status === 'ready' ? 'pronto para enviar' : 'revise os pontos abaixo antes de enviar'}</p>
                </div>
                <div className="cv-resultbar__actions">
                  <button className="cv-btn cv-btn--primary cv-btn--small" onClick={() => onExport('pdf')} disabled={exportBusy}><Download size={14} /> {exportBusy ? 'Exportando…' : 'Baixar PDF'}</button>
                  <button className="cv-btn cv-btn--secondary cv-btn--small" onClick={() => onExport('word')} disabled={exportBusy}>Word</button>
                  {jobUrlToOpen && <a className="cv-btn cv-btn--secondary cv-btn--small" href={jobUrlToOpen} target="_blank" rel="noreferrer noopener"><ExternalLink size={14} /> Abrir vaga</a>}
                  {jobTarget && onMarkApplied && (
                    <button className="cv-btn cv-btn--secondary cv-btn--small" aria-pressed={currentApplied} onClick={() => onMarkApplied(draft.tailoredVersionId, !currentApplied)}>
                      <Send size={14} /> {currentApplied ? 'Enviada ✓' : 'Marcar enviada'}
                    </button>
                  )}
                  {onNextJob && <button className="cv-btn cv-btn--secondary cv-btn--small" onClick={onNextJob}>Próxima vaga</button>}
                </div>
              </section>
              {jobTarget && (
                <section className="cv-card" id="cv-requisitos" aria-label="Requisitos em contexto">
                  <h2 className="cv-steptitle">Requisitos em contexto</h2>
                  <p className="cv-stepsub">Use esta leitura para conferir se cada requisito está sustentado por uma experiência real antes de exportar.</p>
                  <div style={{ marginTop: 14 }}>
                    {requirementReview.slice(0, 12).map((requirement) => (
                      <div key={requirement.id} className="cv-req">
                        <span className={`cv-req__dot cv-req__dot--${requirement.status}`}>
                          {requirement.status === 'direct' ? <Check size={12} /> : requirement.status === 'adjacent' ? <ArrowRight size={12} /> : <AlertCircle size={12} />}
                        </span>
                        <div className="min-w-0 flex-1">
                          <p className="cv-req__label">{requirement.label} <span className="cv-req__tag">· {requirementStatusLabel(requirement.status)}</span></p>
                          {requirement.evidenceText && <p className="cv-req__ev">{requirement.evidenceText}</p>}
                        </div>
                      </div>
                    ))}
                  </div>
                  {jobTarget.requirements.length > 12 && <p className="cv-hint">Mostrando os 12 requisitos mais relevantes.</p>}
                  {jobTarget.warnings.length > 0 && (
                    <div className="cv-alert cv-alert--amber" style={{ marginTop: 12 }}>
                      <AlertCircle size={16} className="shrink-0" />
                      <span><strong>Revise a fonte:</strong> {jobTarget.warnings.slice(0, 2).join(' · ')}</span>
                    </div>
                  )}
                </section>
              )}

              {missingSuggestions.length > 0 && (
                <section className="cv-card" aria-label="Lacunas para você decidir">
                  <h2 className="cv-steptitle">Lacunas para você decidir</h2>
                  <p className="cv-stepsub">Só adicione uma habilidade se você puder sustentá-la. O produto não inventa experiência para preencher palavras-chave.</p>
                  <div className="cv-row" style={{ marginTop: 12 }}>
                    {missingSuggestions.map((skill) => (
                      <button
                        key={skill}
                        className="cv-btn cv-btn--secondary"
                        style={{ minHeight: 34, fontSize: 12.5 }}
                        onClick={() => onAddProfileSkill(skill, { kind: 'missing', reason: 'Requisito detectado na vaga.' })}
                      >
                        {skill} <span style={{ color: 'var(--cv-brand)' }}>+ perfil</span>
                      </button>
                    ))}
                  </div>
                </section>
              )}

              <section className="cv-card" id="cv-versao" aria-label="Versão para revisar">
                <div className="cv-row cv-row--between">
                  <h2 className="cv-steptitle">Versão para revisar</h2>
                  <span className="cv-badge cv-badge--peach">{draft.version.title || 'Rascunho adaptado'}</span>
                </div>
                <p className="cv-stepsub">Edite o texto final com suas palavras. A estrutura continua simples para leitura humana e sistemas ATS.</p>
                <div className="cv-row cv-row--between" style={{ marginTop: 12 }}>
                  <span />
                  <div className="cv-row">
                    <button className="cv-btn cv-btn--secondary" style={{ minHeight: 34, fontSize: 12.5 }} onClick={() => void copyOutput()} disabled={!outputText}>
                      <Copy size={13} /> Copiar
                    </button>
                    <button className="cv-btn cv-btn--secondary" style={{ minHeight: 34, fontSize: 12.5 }} onClick={() => void saveOutput()} disabled={!plainTextSectionId || !outputText.trim() || savingOutput}>
                      {savingOutput ? <RefreshCw className="animate-spin" size={13} /> : <Save size={13} />} Salvar
                    </button>
                  </div>
                </div>
                <div style={{ marginTop: 10 }}>
                  <textarea
                    className="cv-output"
                    value={outputText}
                    onChange={(event) => setTextOverride(event.target.value)}
                    aria-label="Texto do currículo adaptado"
                  />
                </div>
                <hr className="cv-divider" />
                <div className="cv-row cv-row--between">
                  <span className="cv-footer__note"><ShieldCheck size={14} /> Confira datas, métricas e responsabilidades antes de enviar.</span>
                  <div className="cv-row">
                    <button className="cv-btn cv-btn--secondary" style={{ minHeight: 38, fontSize: 12.5 }} onClick={() => onExport('word')} disabled={exportBusy}>
                      <Download size={14} /> {exportBusy ? 'Exportando…' : 'Word'}
                    </button>
                    <button className="cv-btn cv-btn--primary" style={{ minHeight: 38, fontSize: 12.5 }} onClick={() => onExport('pdf')} disabled={exportBusy}>
                      <Download size={14} /> {exportBusy ? 'Exportando…' : 'Exportar PDF'}
                    </button>
                  </div>
                </div>
              </section>

              <ResumeReviewCard
                questions={draft.reviewQuestions ?? []}
                busy={busy}
                onRegenerate={() => void onRegenerateAfterAnswers()}
                onSave={onSaveReviewAnswer}
              />
            </>
          )}

        </div>

        <ResumeAtsPreview
          profile={profile}
          draft={draft}
          previewRole={previewRole}
          baseReady={Boolean(activeVersion && baseReady)}
          hasJobInput={hasJobInput}
          directItems={directItems}
          missingSuggestions={missingSuggestions}
          onViewResume={() => scrollTo('cv-versao')}
        />
      </div>

      <ResumeAtsInsights
        activeVersionId={activeVersionId}
        hasAnalysis={Boolean(draft && jobTarget)}
        directCount={directItems.length}
        requirementReview={requirementReview}
        onViewAnalysis={() => scrollTo('cv-requisitos')}
      />

      <footer className="cv-footer">
        <span className="cv-footer__note">{hasJobInput ? 'Ctrl + Enter também gera o currículo.' : 'Cole a vaga para começar.'}</span>
        <div className="cv-footer__actions">
          <ResumeAiControl enabled={useAi} busy={busy} onToggle={onToggleUseAi} />
          <button className="cv-btn cv-btn--secondary" onClick={() => void saveOutput()} disabled={!plainTextSectionId || !outputText.trim() || savingOutput} title="Salvar o texto revisado">
            <Save size={15} /> Salvar rascunho
          </button>
          <button className="cv-btn cv-btn--primary" onClick={buildResume} disabled={!canBuild} title={!hasJobInput ? 'Cole a descrição ou adicione um link para adaptar' : 'Adaptar currículo para esta vaga'}>
            {busy ? <RefreshCw className="animate-spin" size={15} /> : null}
            {busy ? 'Comparando vaga…' : 'Adaptar currículo para esta vaga'} <ArrowRight size={15} />
          </button>
        </div>
      </footer>
    </div>
  )
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' }).replace(/\./g, '')
}

function normalizeKeyword(value: string) {
  return value.toLowerCase().normalize('NFD').replace(/[\\u0300-\\u036f]/g, '').replace(/[^a-z0-9+#.]+/g, ' ').trim()
}

function requirementStatusLabel(status: RequirementStatus) {
  if (status === 'direct') return 'Comprovado'
  if (status === 'adjacent') return 'Relacionado'
  return 'Revisar'
}
