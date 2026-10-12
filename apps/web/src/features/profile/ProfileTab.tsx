import type { Dispatch, SetStateAction } from 'react'
import { clsx } from 'clsx'
import {
  AlertTriangle,
  BriefcaseBusiness,
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
    <div className="grid gap-6">
      <section className="jp-card p-5 sm:p-6">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <p className="jp-eyebrow">Base profissional</p>
            <h2 className="mt-2 max-w-2xl font-serif text-3xl font-medium leading-tight tracking-[-0.04em] text-[var(--jp-ink)] sm:text-4xl">
              Sua base para cada candidatura.
            </h2>
            <p className="mt-3 flex items-center gap-2 text-sm font-semibold text-[var(--jp-text-secondary)]">
              {validationIssues.length > 0 ? (
                <>
                  <AlertTriangle size={14} /> {criticalCount} crítico{criticalCount === 1 ? '' : 's'} e {warningCount} aviso{warningCount === 1 ? '' : 's'}
                </>
              ) : (
                <>
                  <Save size={14} /> Base pronta para adaptar
                </>
              )}
            </p>
          </div>
          <button className="jp-btn jp-btn--primary h-11 w-full px-5 sm:w-auto" onClick={onSave}>
            <Save size={16} /> Salvar perfil
          </button>
        </div>
      </section>

      {validationIssues.length > 0 && (
        <section className={clsx('jp-alert', criticalCount > 0 ? 'jp-alert--red' : 'jp-alert--amber')}>
          <AlertTriangle size={18} className="mt-0.5 shrink-0" />
          <div className="min-w-0 flex-1">
            <div className="font-black">Antes de adaptar, confira estes pontos</div>
            <div className="mt-3 grid gap-2">
              {validationIssues.map((issue) => (
                <div key={issue.fieldName} className={clsx('rounded-xl border px-3 py-2.5', issue.isCritical ? 'border-red-200 bg-white/70' : 'border-amber-200 bg-white/70')}>
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="font-bold">{fieldNameLabels[issue.fieldName] ?? issue.fieldName}</span>
                    <span className={clsx('jp-badge', issue.isCritical ? 'jp-badge--red' : 'jp-badge--amber')}>{issue.isCritical ? 'Crítico' : 'Aviso'}</span>
                  </div>
                  <span className="mt-1 block text-xs opacity-80">{issue.errorMessage}</span>
                </div>
              ))}
            </div>
          </div>
        </section>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        <SectionCard title="Como a pessoa te encontra" icon={UserRound}>
          <div className="space-y-4">
            <label className="block">
              <FieldLabel>Nome completo</FieldLabel>
              <input className="jp-input" value={profile.fullName} onChange={(event) => update({ fullName: event.target.value })} placeholder="Seu nome completo" />
            </label>
            <label className="block">
              <FieldLabel icon={Mail}>E-mail</FieldLabel>
              <input className="jp-input" type="email" value={profile.email} onChange={(event) => update({ email: event.target.value })} placeholder="seu@email.com" />
            </label>
            <label className="block">
              <FieldLabel icon={Phone}>Telefone</FieldLabel>
              <input className="jp-input" value={profile.phone} onChange={(event) => update({ phone: event.target.value })} placeholder="(11) 99999-9999" />
            </label>
            <div className="grid gap-4 sm:grid-cols-3">
              <label className="block sm:col-span-2">
                <FieldLabel icon={MapPin}>Cidade</FieldLabel>
                <input className="jp-input" value={profile.city} onChange={(event) => update({ city: event.target.value })} placeholder="São Paulo" />
              </label>
              <label className="block">
                <FieldLabel>UF</FieldLabel>
                <input className="jp-input" value={profile.state} onChange={(event) => update({ state: event.target.value.toUpperCase().replace(/[^A-Z]/g, '').slice(0, 2) })} placeholder="SP" />
              </label>
            </div>
          </div>
        </SectionCard>

        <SectionCard title="O que você quer construir" icon={BriefcaseBusiness}>
          <div className="space-y-4">
            <label className="block">
              <FieldLabel>Cargo alvo</FieldLabel>
              <input className="jp-input" value={profile.targetRole} onChange={(event) => update({ targetRole: event.target.value })} placeholder="ex: Software Engineer" />
            </label>
            <div className="grid gap-4 sm:grid-cols-2">
              <label className="block">
                <FieldLabel icon={GraduationCap}>Senioridade</FieldLabel>
                <select className="jp-input" value={profile.seniority} onChange={(event) => update({ seniority: event.target.value })}>
                  <option value="unknown">Ainda não defini</option>
                  <option value="junior">Júnior</option>
                  <option value="pleno">Pleno</option>
                  <option value="senior">Sênior</option>
                </select>
              </label>
              <label className="block">
                <FieldLabel icon={DollarSign}>Pretensão salarial</FieldLabel>
                <input className="jp-input" value={profile.salaryExpectation} onChange={(event) => update({ salaryExpectation: event.target.value })} placeholder="Opcional" />
              </label>
            </div>
            <label className="block">
              <FieldLabel auxiliary="separadas por vírgula">Habilidades que você realmente domina</FieldLabel>
              <input className="jp-input" value={profile.skills.join(', ')} onChange={(event) => update({ skills: event.target.value.split(',').map((item) => item.trim()).filter(Boolean) })} placeholder="React, TypeScript, Node.js..." />
            </label>
            <label className="block">
              <FieldLabel>Resumo profissional</FieldLabel>
              <textarea className="jp-input min-h-32" value={profile.professionalSummary} onChange={(event) => update({ professionalSummary: event.target.value })} rows={5} placeholder="Conte, em poucas linhas, que tipo de problema você resolve." />
            </label>
          </div>
        </SectionCard>

        <SectionCard title="Pontos de contato" icon={Link2}>
          <div className="space-y-4">
            <p className="text-sm leading-6 text-[var(--jp-text-secondary)]">Links ajudam a validar seu repertório. Só inclua páginas que você quer compartilhar.</p>
            <label className="block">
              <FieldLabel>LinkedIn</FieldLabel>
              <input className="jp-input" type="url" value={profile.linkedinUrl} onChange={(event) => update({ linkedinUrl: event.target.value })} placeholder="https://linkedin.com/in/..." />
            </label>
            <label className="block">
              <FieldLabel>GitHub</FieldLabel>
              <input className="jp-input" type="url" value={profile.githubUrl} onChange={(event) => update({ githubUrl: event.target.value })} placeholder="https://github.com/..." />
            </label>
            <label className="block">
              <FieldLabel>Portfólio</FieldLabel>
              <input className="jp-input" type="url" value={profile.portfolioUrl} onChange={(event) => update({ portfolioUrl: event.target.value })} placeholder="https://..." />
            </label>
          </div>
        </SectionCard>

        <SectionCard title="Trazer currículo para cá" icon={Upload}>
          <div className="space-y-4">
            <p className="text-sm leading-6 text-[var(--jp-text-secondary)]">O arquivo vira sua base de trabalho. O JobPilot extrai o texto e você revisa antes de usar qualquer adaptação.</p>
            <label className="jp-input--file flex w-full items-center gap-3">
              <input type="file" className="sr-only" accept=".pdf,.docx,.txt,.md,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/plain" onChange={(event) => setSelectedResumeFile(event.target.files?.[0] ?? null)} />
              <FileText size={18} className="shrink-0" />
              <span className="truncate">{selectedResumeFile ? selectedResumeFile.name : 'Selecionar PDF, DOCX, TXT ou MD'}</span>
            </label>
            <button className="jp-btn jp-btn--primary h-10 w-full justify-center disabled:opacity-50" onClick={onImportResumeFile} disabled={importBusy || !selectedResumeFile}>
              <Upload size={15} /> {importBusy ? 'Lendo arquivo...' : 'Ler currículo'}
            </button>
            {importNotice && <div className="jp-alert jp-alert--red"><AlertTriangle size={17} className="shrink-0" /> {importNotice}</div>}
            {importResult && (
              <div className="rounded-2xl border border-[var(--jp-border)] bg-[var(--jp-bg)] p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="text-sm font-black text-[var(--jp-ink)]">Importação concluída</span>
                  <span className={clsx('jp-badge', importResult.extraction.requiresOcr || importResult.extraction.requiresReview ? 'jp-badge--amber' : 'jp-badge--green')}>
                    {importResult.extraction.requiresOcr ? 'OCR necessário' : importResult.extraction.requiresReview ? 'Revisar' : 'Confiável'}
                  </span>
                </div>
                <p className="mt-2 text-xs leading-5 text-[var(--jp-text-secondary)]">
                  {importResult.extraction.wordCount} palavras · {importResult.skillsAdded.length} habilidades reconhecidas
                </p>
                {importResult.warnings.length > 0 && <p className="mt-2 text-xs leading-5 text-amber-700">{importResult.warnings.join(' | ')}</p>}
                <textarea className="jp-input mt-3 min-h-28" readOnly rows={5} value={importResult.extractedText || 'Nenhum texto extraído.'} aria-label="Texto extraído do currículo" />
              </div>
            )}
          </div>
        </SectionCard>
      </div>

      <SectionCard
        title="Histórico de recomendações"
        icon={History}
        action={recommendedSkillAdditions.length > 0 && <span className="jp-badge jp-badge--teal">{recommendedSkillAdditions.length}</span>}
      >
        {recommendedSkillAdditions.length === 0 ? (
          <EmptyState icon={History} title="Nenhuma recomendação ainda" description="Lacunas e habilidades relacionadas encontradas nas análises aparecem aqui para você decidir." />
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {recommendedSkillAdditions.map((item) => (
              <div key={item.id} className="rounded-2xl border border-[var(--jp-border)] bg-[var(--jp-bg)] p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <strong className="text-sm font-black text-[var(--jp-ink)]">{item.skill}</strong>
                      <span className={clsx('jp-badge', item.kind === 'missing' ? 'jp-badge--red' : 'jp-badge--amber')}>{item.kind === 'missing' ? 'Lacuna' : 'Relacionado'}</span>
                    </div>
                    <p className="mt-2 text-sm leading-6 text-[var(--jp-text-secondary)]">{item.reason}</p>
                    <small className="mt-2 block text-xs text-[var(--jp-text-tertiary)]">{[item.jobTitle, item.company].filter(Boolean).join(' · ') || 'Recomendação de uma análise'} · {new Date(item.createdAt).toLocaleDateString()}</small>
                  </div>
                  <button className="grid size-8 shrink-0 place-items-center rounded-lg text-[var(--jp-text-tertiary)] transition hover:bg-white hover:text-red-600" title="Remover do histórico" onClick={() => onRemoveRecommendedSkillAddition(item.id)}>
                    <Trash2 size={15} />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </SectionCard>

      <SectionCard title="Seus dados e prazos" icon={Trash2}>
        <div className="space-y-4">
          <p className="text-sm leading-6 text-[var(--jp-text-secondary)]">
            {retention
              ? `Originais por ${retention.resumeOriginalsDays} dias · exportações por ${retention.resumeExportsDays} dias · logs de IA por ${retention.aiUsageLogsDays} dias · fontes de vaga por ${retention.jobSourceFilesDays} dias.`
              : 'Carregando prazos de guarda...'}
          </p>
          {dangerNotice && (
            <div className="jp-alert jp-alert--amber" role="status">
              <AlertTriangle size={17} className="shrink-0" /> {dangerNotice}
              <button className="jp-btn h-8 px-3" onClick={onRetryDanger} disabled={dangerBusy}>
                Tentar novamente
              </button>
            </div>
          )}
          <div className="flex flex-col gap-3 sm:flex-row">
            <button
              className="jp-btn h-10 px-4 disabled:opacity-50"
              disabled={dangerBusy}
              onClick={() => { if (window.confirm('Excluir a base profissional? O login continua, mas perfil e base saem.')) void onDeleteProfile() }}
            >
              <Trash2 size={15} /> {dangerBusy ? 'Excluindo...' : 'Excluir base'}
            </button>
            <button
              className="jp-btn jp-btn--primary h-10 px-4 disabled:opacity-50"
              disabled={dangerBusy}
              onClick={() => { if (window.confirm('Excluir a conta e TODOS os dados? Essa ação não pode ser desfeita.')) void onDeleteAccount() }}
              aria-label="Excluir conta e todos os dados"
            >
              <Trash2 size={15} /> {dangerBusy ? 'Excluindo...' : 'Excluir conta e dados'}
            </button>
          </div>
        </div>
      </SectionCard>
    </div>
  )
}
