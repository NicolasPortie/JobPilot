import { useMemo } from 'react'
import type { CSSProperties } from 'react'
import { Check, ChevronRight, Clock, FileText, TrendingUp } from 'lucide-react'
import type { Profile, ResumeAnalysis, ResumeSection, TailoredResume } from '../../types'
import { initials } from '../../app/utils'
import { sectionItems } from './resumeAtsPresentation'

type Props = {
  profile: Pick<Profile, 'fullName' | 'professionalSummary' | 'skills'>
  draft: TailoredResume | null
  previewRole: string
  baseReady: boolean
  hasJobInput: boolean
  analysis: ResumeAnalysis | null
  onViewResume: () => void
  onViewRequirements: () => void
  onViewSuggestions: () => void
}

export function ResumeAtsPreview({ profile, draft, previewRole, baseReady, hasJobInput, analysis, onViewResume, onViewRequirements, onViewSuggestions }: Props) {
  const score = Math.round(Number(draft?.atsScore ?? 0))
  const adherence = analysis?.adherence ?? null
  const badgeTone = adherence?.level === 'high' ? 'cv-badge--green' : adherence?.level === 'good' ? 'cv-badge--peach' : 'cv-badge--amber'
  const provenLabels = analysis?.provenLabels ?? []
  const missingSuggestions = analysis?.missingSuggestions ?? []

  // Previa monta SOMENTE das secoes reais da versao adaptada (draft).
  // Sem draft, sem previa — nada de curriculo antes de gerar.
  const previewData = useMemo(() => {
    if (!draft) return null
    const sections = draft.version.sections ?? []
    const experiences = sectionItems(sections, 'experience').slice(0, 3).map((item) => ({
      title: item.role || item.company || '',
      meta: [item.role && item.company ? item.company : '', item.period].filter(Boolean).join(' • '),
    })).filter((item) => item.title || item.meta)
    const education = sectionItems(sections, 'education').slice(0, 2).map((item) => ({
      title: item.degree || item.institution || '',
      meta: [item.degree && item.institution ? item.institution : '', item.year].filter(Boolean).join(' • '),
    })).filter((item) => item.title || item.meta)
    const tailoredSummary = sectionItems(sections, 'summary')[0]?.summary?.trim() ?? ''
    const tailoredSkills = sectionSkills(sections)
    return {
      experiences,
      education,
      summary: tailoredSummary || profile.professionalSummary,
      skills: tailoredSkills.length > 0 ? tailoredSkills : profile.skills,
    }
  }, [draft, profile.professionalSummary, profile.skills])

  return (
    <aside className="cv-rail" aria-label="Prévia e aderência">
      <div className="cv-railhead">
        <div className="cv-railhead__row">
          <h2 className="cv-railhead__title">Prévia do currículo adaptado</h2>
          <button className="cv-btn cv-btn--secondary" style={{ minHeight: 36, fontSize: 12.5, whiteSpace: 'nowrap' }} onClick={onViewResume} disabled={!draft} title={draft ? 'Ir para a versão para revisar' : 'Gere uma versão para ver o currículo completo'}>
            <FileText size={14} /> Ver currículo completo
          </button>
        </div>
        <p className="cv-railhead__sub">Veja como seu currículo ficará e confira a análise de aderência com a vaga.</p>
      </div>

      <div className="cv-prev">
        {draft && previewData ? (
          <>
            <div className="cv-prev__head">
              <span className="cv-avatar" aria-hidden>{initials(profile.fullName)}</span>
              <div className="min-w-0">
                <p className="cv-prev__name truncate">{profile.fullName}</p>
                {previewRole && <p className="cv-prev__role truncate">{previewRole}</p>}
              </div>
              {adherence && <span className={`cv-badge cv-prev__badge ${badgeTone}`}>{adherence.badgeLabel}</span>}
            </div>

            {previewData.summary && (
              <div className="cv-prev__section">
                <h3 className="cv-prev__h">Resumo profissional</h3>
                <p className="cv-stepsub" style={{ display: '-webkit-box', WebkitLineClamp: 3, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>{previewData.summary}</p>
              </div>
            )}

            <div className="cv-prev__section">
              <h3 className="cv-prev__h">Experiência profissional</h3>
              {previewData.experiences.length > 0 ? (
                <div className="cv-timeline">
                  {previewData.experiences.map((exp, index) => (
                    <div key={`${exp.title}-${index}`} className="cv-timeline__item">
                      <p className="cv-timeline__title">{exp.title}</p>
                      {exp.meta && <p className="cv-timeline__meta">{exp.meta}</p>}
                    </div>
                  ))}
                </div>
              ) : (
                <p className="cv-hint">Sem experiências nesta versão.</p>
              )}
            </div>

            {previewData.skills.length > 0 && (
              <div className="cv-prev__section">
                <h3 className="cv-prev__h">Habilidades</h3>
                <div className="cv-chips">
                  {previewData.skills.slice(0, 8).map((skill) => <span key={skill} className="cv-chip">{skill}</span>)}
                </div>
              </div>
            )}

            {previewData.education.length > 0 && (
              <div className="cv-prev__section">
                <h3 className="cv-prev__h">Formação</h3>
                <div>
                  {previewData.education.map((edu, index) => (
                    <div key={`${edu.title}-${index}`} style={{ marginTop: index > 0 ? 8 : 0 }}>
                      <p className="cv-timeline__title">{edu.title}</p>
                      {edu.meta && <p className="cv-timeline__meta">{edu.meta}</p>}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        ) : (
          <div className="cv-prev__empty">
            <span className="cv-prev__empty-icon"><FileText size={22} /></span>
            <p className="cv-prev__empty-title">Nenhuma prévia ainda</p>
            <p className="cv-prev__empty-text">Cole a vaga e clique em Adaptar currículo para esta vaga para gerar sua primeira versão.</p>
          </div>
        )}
      </div>

      <div className="cv-rail__stack">
        <div className="cv-mini">
          <div className="cv-mini__head">
            <span className="cv-mini__icon"><TrendingUp size={17} /></span>
            <h3 className="cv-mini__title">Aderência da vaga</h3>
          </div>
          <div className="cv-meterrow">
            <div className="cv-donut" style={{ '--cv-score': score } as CSSProperties} role="img" aria-label={`Aderência de ${score}%`}>
              <span className="cv-donut__inner">{draft ? `${score}%` : '–'}</span>
            </div>
            <div className="cv-callout">
              <strong>{adherence ? adherence.headline : 'Gere sua versão'}</strong>
              {adherence ? adherence.message : 'Cole a vaga e gere uma versão para ver sua aderência estimada.'}
            </div>
          </div>
          <div className="cv-checklist" aria-label="Progresso da candidatura">
            <ChecklistRow done={Boolean(baseReady)} label={baseReady ? 'Currículo base pronto' : 'Currículo base pendente'} />
            <ChecklistRow done={hasJobInput} label={hasJobInput ? 'Dados da vaga informados' : 'Cole a descrição da vaga'} />
            <ChecklistRow done={Boolean(draft)} label={draft ? 'Versão adaptada gerada' : 'Versão adaptada aguardando'} />
          </div>
          {draft && draft.usedAi === false && (
            <p className="cv-hint" role="status">
              {draft.aiNotice ?? 'IA indisponível — resultado determinístico.'} Revise com atenção antes de exportar.
            </p>
          )}
        </div>

        <div className="cv-mini">
          <div className="cv-mini__head">
            <span className="cv-mini__icon cv-mini__icon--green"><Check size={17} /></span>
            <h3 className="cv-mini__title">Pontos comprovados</h3>
            {analysis && analysis.requirements.length > 0 && (
              <button className="cv-mini__chev" onClick={onViewRequirements} aria-label="Ver requisitos em contexto" title="Ver requisitos em contexto">
                <ChevronRight size={17} />
              </button>
            )}
          </div>
          <p className="cv-mini__sub">Experiências e habilidades que atendem aos requisitos da vaga.</p>
          {provenLabels.length > 0 ? (
            <div className="cv-chips">
              {provenLabels.slice(0, 6).map((label) => <span key={label} className="cv-chip">{label}</span>)}
            </div>
          ) : (
            <p className="cv-hint">Aparecem aqui após a análise da vaga.</p>
          )}
        </div>

        <div className="cv-mini">
          <div className="cv-mini__head">
            <span className="cv-mini__icon"><FileText size={17} /></span>
            <h3 className="cv-mini__title">Sugestões de melhoria</h3>
            {missingSuggestions.length > 0 && (
              <button className="cv-mini__chev" onClick={onViewSuggestions} aria-label="Ver lacunas para decidir" title="Ver lacunas para decidir">
                <ChevronRight size={17} />
              </button>
            )}
          </div>
          <p className="cv-mini__sub">Pontos da vaga sem evidência no seu perfil. Adicione só o que você realmente domina.</p>
          {missingSuggestions.length > 0 ? (
            <div className="cv-chips">
              {missingSuggestions.map((skill) => <span key={skill} className="cv-chip">{skill}</span>)}
            </div>
          ) : (
            <p className="cv-hint">Nenhuma pendência no momento.</p>
          )}
        </div>
      </div>
    </aside>
  )
}

function ChecklistRow({ done, label }: { done: boolean; label: string }) {
  return (
    <div className={`cv-checklist__row${done ? '' : ' cv-checklist__row--pending'}`}>
      <span className={`cv-req__dot ${done ? 'cv-req__dot--direct' : 'cv-req__dot--missing'}`}>
        {done ? <Check size={12} /> : <Clock size={12} />}
      </span>
      {label}
    </div>
  )
}

// Skills reais da secao (formato do editor: { skills: string[] }).
function sectionSkills(sections: ResumeSection[]): string[] {
  const section = sections.find((item) => item.sectionType === 'skills')
  if (!section) return []
  try {
    const parsed = JSON.parse(section.contentJson || '{}') as { skills?: unknown }
    if (Array.isArray(parsed.skills)) {
      return parsed.skills
        .filter((skill): skill is string => typeof skill === 'string')
        .map((skill) => skill.trim())
        .filter(Boolean)
    }
  } catch {
    // conteúdo ilegível: sem skills desta seção
  }
  return []
}

