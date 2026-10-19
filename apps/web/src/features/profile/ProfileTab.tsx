import { useState, type Dispatch, type SetStateAction } from 'react'
import { clsx } from 'clsx'
import {
  AlertTriangle,
  BriefcaseBusiness,
  CheckCircle2,
  DollarSign,
  FileText,
  GraduationCap,
  History,
  Link2,
  Mail,
  MapPin,
  Phone,
  Save,
  Trash2,
  Upload,
  UserRound,
} from 'lucide-react'
import { SectionCard } from '../../components/forms/SectionCard'
import { FieldLabel } from '../../components/forms/FieldLabel'
import { EmptyState } from '../../components/data-display/EmptyState'
import { fieldNameLabels } from '../../constants/labels'
import type { AtsProfileSkillAddition, Profile, ProfileValidationIssue, ResumeImportResult } from '../../types'
import type { RetentionPolicy } from '../../api/profileClient'

type Props = {
  profile: Profile
  setProfile: Dispatch<SetStateAction<Profile>>
  recommendedSkillAdditions: AtsProfileSkillAddition[]
  onSave: () => void
  saveBusy: boolean
  notice: { tone: 'ok' | 'error'; text: string } | null
  onRemoveRecommendedSkillAddition: (id: string) => void
  onImportResumeFile: () => void
  onDeleteProfile: () => void
  onDeleteAccount: () => void
  onRetryDanger: () => void
  retention: RetentionPolicy | null
  dangerNotice: string
  dangerBusy: boolean
  selectedResumeFile: File | null
  setSelectedResumeFile: Dispatch<SetStateAction<File | null>>
  importBusy: boolean
  importResult: ResumeImportResult | null
  importNotice: string
  validationIssues: ProfileValidationIssue[]
}

export function ProfileTab({
  profile,
  setProfile,
  recommendedSkillAdditions,
  onSave,
  saveBusy,
  notice,
  onRemoveRecommendedSkillAddition,
  onImportResumeFile,
  onDeleteProfile,
  onDeleteAccount,
  onRetryDanger,
  retention,
  dangerNotice,
  dangerBusy,
  selectedResumeFile,
  setSelectedResumeFile,
  importBusy,
  importResult,
  importNotice,
  validationIssues,
}: Props) {
  const criticalCount = validationIssues.filter((issue) => issue.isCritical).length
  const warningCount = validationIssues.length - criticalCount
  const update = (patch: Partial<Profile>) => setProfile((current) => ({ ...current, ...patch }))

  return (
    <div className="cv-pagebody">
      <section className="cv-card" aria-labelledby="cv-perfil-base">
        <div className="cv-summarybar">
          <div>
            <h2 className="cv-steptitle" id="cv-perfil-base">Sua base para cada candidatura.</h2>
            <p className="cv-statusline" style={{ marginTop: 6 }}>
              {validationIssues.length > 0 ? (
                <>
                  <AlertTriangle size={14} /> {criticalCount} crítico{criticalCount === 1 ? '' : 's'} e {warningCount} aviso{warningCount === 1 ? '' : 's'}
                </>
              ) : (
                <>
                  <CheckCircle2 size={14} /> Base pronta para adaptar
                </>
              )}
            </p>
          </div>
          <button className="cv-btn cv-btn--primary" onClick={onSave} disabled={saveBusy} aria-busy={saveBusy}>
            <Save size={16} /> {saveBusy ? 'Salvando…' : 'Salvar perfil'}
          </button>
        </div>
        {notice && (
          <div className={clsx('cv-alert', notice.tone === 'ok' ? 'cv-alert--green' : 'cv-alert--red')} role="status" style={{ marginTop: 14 }}>
            {notice.tone === 'ok' ? <CheckCircle2 size={16} className="mt-0.5 shrink-0" /> : <AlertTriangle size={16} className="mt-0.5 shrink-0" />}
            {notice.text}
          </div>
        )}
      </section>

      {validationIssues.length > 0 && (
        <section className={clsx('cv-alert', criticalCount > 0 ? 'cv-alert--red' : 'cv-alert--amber')} aria-label="Pontos a conferir">
          <AlertTriangle size={18} className="mt-0.5 shrink-0" />
          <div className="min-w-0 flex-1">
            <div style={{ fontWeight: 800 }}>Antes de adaptar, confira estes pontos</div>
            <div className="cv-issues">
              {validationIssues.map((issue) => (
                <div key={issue.fieldName} className={clsx('cv-issue', issue.isCritical && 'cv-issue--critical')}>
                  <div className="cv-issue__row">
                    <span>{fieldNameLabels[issue.fieldName] ?? issue.fieldName}</span>
                    <span className={clsx('cv-badge', issue.isCritical ? 'cv-badge--peach' : 'cv-badge--amber')}>{issue.isCritical ? 'Crítico' : 'Aviso'}</span>
                  </div>
                  <span className="cv-issue__text">{issue.errorMessage}</span>
                </div>
              ))}
            </div>
          </div>
        </section>
      )}

      <div className="cv-grid2">
        <div className="cv-col">
        <SectionCard title="Como a pessoa te encontra" icon={UserRound}>
          <div className="cv-form">
            <label className="block">
              <FieldLabel>Nome completo</FieldLabel>
              <input className="cv-input" value={profile.fullName} onChange={(event) => update({ fullName: event.target.value })} placeholder="Seu nome completo" autoComplete="name" />
            </label>
            <label className="block">
              <FieldLabel icon={Mail}>E-mail</FieldLabel>
              <input className="cv-input" type="email" value={profile.email} onChange={(event) => update({ email: event.target.value })} placeholder="seu@email.com" autoComplete="email" />
            </label>
            <label className="block">
              <FieldLabel icon={Phone}>Telefone</FieldLabel>
              <input className="cv-input" type="tel" value={profile.phone} onChange={(event) => update({ phone: event.target.value })} placeholder="(11) 99999-9999" autoComplete="tel" />
            </label>
            <div className="cv-formrow cv-formrow--uf">
              <label className="block">
                <FieldLabel icon={MapPin}>Cidade</FieldLabel>
                <input className="cv-input" value={profile.city} onChange={(event) => update({ city: event.target.value })} placeholder="São Paulo" autoComplete="address-level2" />
              </label>
              <label className="block">
                <FieldLabel>UF</FieldLabel>
                <input className="cv-input" value={profile.state} maxLength={2} onChange={(event) => update({ state: event.target.value })} placeholder="SP" autoComplete="address-level1" />
              </label>
            </div>
          </div>
        </SectionCard>

        <SectionCard title="Pontos de contato" icon={Link2}>
          <div className="cv-form">
            <p className="cv-prose">Links ajudam a validar seu repertório. Só inclua páginas que você quer compartilhar.</p>
            <label className="block">
              <FieldLabel>LinkedIn</FieldLabel>
              <input className="cv-input" type="url" value={profile.linkedinUrl} onChange={(event) => update({ linkedinUrl: event.target.value })} placeholder="https://linkedin.com/in/..." />
            </label>
            <label className="block">
              <FieldLabel>GitHub</FieldLabel>
              <input className="cv-input" type="url" value={profile.githubUrl} onChange={(event) => update({ githubUrl: event.target.value })} placeholder="https://github.com/..." />
            </label>
            <label className="block">
              <FieldLabel>Portfólio</FieldLabel>
              <input className="cv-input" type="url" value={profile.portfolioUrl} onChange={(event) => update({ portfolioUrl: event.target.value })} placeholder="https://..." />
            </label>
          </div>
        </SectionCard>
        </div>

        <div className="cv-col">
        <SectionCard title="O que você quer construir" icon={BriefcaseBusiness}>
          <div className="cv-form">
            <label className="block">
              <FieldLabel>Cargo alvo</FieldLabel>
              <input className="cv-input" value={profile.targetRole} onChange={(event) => update({ targetRole: event.target.value })} placeholder="ex: Software Engineer" />
            </label>
            <div className="cv-formrow">
              <label className="block">
                <FieldLabel icon={GraduationCap}>Senioridade</FieldLabel>
                <select className="cv-input cv-select" value={profile.seniority} onChange={(event) => update({ seniority: event.target.value })}>
                  <option value="unknown">Ainda não defini</option>
                  <option value="junior">Júnior</option>
                  <option value="pleno">Pleno</option>
                  <option value="senior">Sênior</option>
                </select>
              </label>
              <label className="block">
                <FieldLabel icon={DollarSign}>Pretensão salarial</FieldLabel>
                <input className="cv-input" value={profile.salaryExpectation} onChange={(event) => update({ salaryExpectation: event.target.value })} placeholder="Opcional" />
              </label>
            </div>
            <label className="block">
              <FieldLabel auxiliary="separadas por vírgula">Habilidades que você realmente domina</FieldLabel>
              <SkillsInput skills={profile.skills} onChange={(skills) => update({ skills })} />
            </label>
            <label className="block">
              <FieldLabel>Resumo profissional</FieldLabel>
              <textarea className="cv-textarea cv-textarea--tall" value={profile.professionalSummary} onChange={(event) => update({ professionalSummary: event.target.value })} rows={5} placeholder="Conte, em poucas linhas, que tipo de problema você resolve." />
            </label>
          </div>
        </SectionCard>

        <SectionCard title="Trazer currículo para cá" icon={Upload}>
          <div className="cv-form">
            <p className="cv-prose">O arquivo vira sua base de trabalho. O JobPilot extrai o texto e você revisa antes de usar qualquer adaptação.</p>
            <label className="cv-filebtn">
              <input type="file" className="sr-only" accept=".pdf,.docx,.txt,.md,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/plain" onChange={(event) => setSelectedResumeFile(event.target.files?.[0] ?? null)} />
              <FileText size={18} className="shrink-0" />
              <span className="truncate">{selectedResumeFile ? selectedResumeFile.name : 'Selecionar PDF, DOCX, TXT ou MD'}</span>
            </label>
            <button className="cv-btn cv-btn--primary cv-btn--block" onClick={onImportResumeFile} disabled={importBusy || !selectedResumeFile}>
              <Upload size={15} /> {importBusy ? 'Lendo arquivo…' : 'Ler currículo'}
            </button>
            {importNotice && <div className="cv-alert cv-alert--red" role="alert"><AlertTriangle size={16} className="mt-0.5 shrink-0" /> {importNotice}</div>}
            {importResult && (
              <div className="cv-importbox">
                <div className="cv-row cv-row--between">
                  <span style={{ fontSize: 13.5, fontWeight: 800 }}>Importação concluída</span>
                  <span className={clsx('cv-badge', importResult.extraction.requiresOcr || importResult.extraction.requiresReview ? 'cv-badge--amber' : 'cv-badge--green')}>
                    {importResult.extraction.requiresOcr ? 'OCR necessário' : importResult.extraction.requiresReview ? 'Revisar' : 'Confiável'}
                  </span>
                </div>
                <p className="cv-hint">
                  {importResult.extraction.wordCount} palavras · {importResult.skillsAdded.length} habilidades reconhecidas
                </p>
                {importResult.warnings.length > 0 && <p className="cv-hint" style={{ color: '#9a5b12' }}>{importResult.warnings.join(' | ')}</p>}
                <textarea className="cv-textarea" style={{ marginTop: 10 }} readOnly rows={5} value={importResult.extractedText || 'Nenhum texto extraído.'} aria-label="Texto extraído do currículo" />
              </div>
            )}
          </div>
        </SectionCard>
        </div>
      </div>

      <SectionCard
        title="Histórico de recomendações"
        description="Habilidades que você levou para o perfil a partir das análises de vaga."
        icon={History}
        action={recommendedSkillAdditions.length > 0 && <span className="cv-badge cv-badge--peach">{recommendedSkillAdditions.length}</span>}
      >
        {recommendedSkillAdditions.length === 0 ? (
          <EmptyState icon={History} title="Nenhuma recomendação ainda" description="Quando você adicionar uma lacuna da análise ao perfil, ela fica registrada aqui." />
        ) : (
          <div className="cv-grid2">
            {recommendedSkillAdditions.map((item) => (
              <div key={item.id} className="cv-listitem">
                <div className="cv-listitem__body">
                  <div className="cv-listitem__title">
                    {item.skill}
                    <span className={clsx('cv-badge', item.kind === 'missing' ? 'cv-badge--peach' : 'cv-badge--amber')}>{item.kind === 'missing' ? 'Lacuna' : 'Relacionado'}</span>
                  </div>
                  <p className="cv-listitem__text">{item.reason}</p>
                  <p className="cv-listitem__meta">{[item.jobTitle, item.company].filter(Boolean).join(' · ') || 'Recomendação de uma análise'} · {formatDate(item.createdAt)}</p>
                </div>
                <button className="cv-iconbtn" title="Remover do histórico" aria-label={`Remover ${item.skill} do histórico`} onClick={() => onRemoveRecommendedSkillAddition(item.id)}>
                  <Trash2 size={15} />
                </button>
              </div>
            ))}
          </div>
        )}
      </SectionCard>

      <SectionCard title="Seus dados e prazos" icon={Trash2}>
        <div className="cv-form">
          <p className="cv-prose">
            {retention
              ? `Originais por ${retention.resumeOriginalsDays} dias · exportações por ${retention.resumeExportsDays} dias · logs de IA por ${retention.aiUsageLogsDays} dias · fontes de vaga por ${retention.jobSourceFilesDays} dias.`
              : 'Carregando prazos de guarda…'}
          </p>
          {dangerNotice && (
            <div className="cv-alert cv-alert--amber" role="status">
              <AlertTriangle size={16} className="mt-0.5 shrink-0" />
              <span className="flex-1">{dangerNotice}</span>
              <button className="cv-btn cv-btn--secondary cv-btn--small" onClick={onRetryDanger} disabled={dangerBusy}>
                Tentar novamente
              </button>
            </div>
          )}
          <div className="cv-row">
            <button
              className="cv-btn cv-btn--secondary"
              disabled={dangerBusy}
              onClick={() => { if (window.confirm('Excluir a base profissional? O login continua, mas perfil e base saem.')) void onDeleteProfile() }}
            >
              <Trash2 size={15} /> {dangerBusy ? 'Excluindo…' : 'Excluir base'}
            </button>
            <button
              className="cv-btn cv-btn--danger"
              disabled={dangerBusy}
              onClick={() => { if (window.confirm('Excluir a conta e TODOS os dados? Essa ação não pode ser desfeita.')) void onDeleteAccount() }}
              aria-label="Excluir conta e todos os dados"
            >
              <Trash2 size={15} /> {dangerBusy ? 'Excluindo…' : 'Excluir conta e dados'}
            </button>
          </div>
        </div>
      </SectionCard>
    </div>
  )
}

/** Campo de texto livre que entrega a lista; o servidor limpa espaços e repetidos ao salvar. */
function SkillsInput({ skills, onChange }: { skills: string[]; onChange: (skills: string[]) => void }) {
  const [text, setText] = useState(skills.join(', '))
  const [seen, setSeen] = useState(skills)

  // Quando o perfil muda por fora (importação, atualização), o texto acompanha; digitar não o sobrescreve.
  if (skills !== seen) {
    setSeen(skills)
    if (toList(text).join('\u0000') !== skills.join('\u0000')) setText(skills.join(', '))
  }

  return (
    <input
      className="cv-input"
      value={text}
      onChange={(event) => {
        setText(event.target.value)
        onChange(toList(event.target.value))
      }}
      placeholder="React, TypeScript, Node.js..."
    />
  )
}

function toList(text: string) {
  return text.split(',').map((item) => item.trim()).filter(Boolean)
}

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('pt-BR')
}
