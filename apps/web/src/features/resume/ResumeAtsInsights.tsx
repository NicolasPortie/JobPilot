import { useEffect, useMemo, useState } from 'react'
import { ArrowRight, ChevronRight, Lightbulb, Search, TrendingUp } from 'lucide-react'
import { resumeClient } from '../../api/resumeClient'
import type { ResumeSection } from '../../types'
import { sectionItems } from './resumeAtsPresentation'
import type { RequirementReviewItem } from './resumeAtsPresentation'

type Props = {
  activeVersionId: string
  hasAnalysis: boolean
  directCount: number
  requirementReview: RequirementReviewItem[]
  onViewAnalysis: () => void
}

export function ResumeAtsInsights({ activeVersionId, hasAnalysis, directCount, requirementReview, onViewAnalysis }: Props) {
  const [baseSections, setBaseSections] = useState<ResumeSection[]>([])

  // Secoes reais da versao base (backend e a fonte; aqui so exibicao).
  useEffect(() => {
    let cancelled = false
    async function loadSections() {
      if (!activeVersionId) {
        setBaseSections([])
        return
      }
      try {
        const { data } = await resumeClient.listSections(activeVersionId)
        if (!cancelled) setBaseSections(data ?? [])
      } catch {
        if (!cancelled) setBaseSections([])
      }
    }
    void loadSections()
    return () => { cancelled = true }
  }, [activeVersionId])

  // Projetos reais da base para o insight (independe de gerar versao).
  const baseProjects = useMemo(() => sectionItems(baseSections, 'projects')
    .map((item) => ({ name: item.name || item.role || '', tech: item.technologies || '' }))
    .filter((item) => item.name)
    .slice(0, 2), [baseSections])

  // Palavras da propria analise da vaga (vereditos do backend), para o insight.
  const focusKeywords = useMemo(() => {
    const pool = [
      ...requirementReview.filter((item) => item.status === 'missing'),
      ...requirementReview.filter((item) => item.status === 'adjacent'),
    ]
    return pool.map((item) => item.label).filter(Boolean).slice(0, 3)
  }, [requirementReview])

  return (
    <section className="cv-insights" style={{ marginTop: 16 }} aria-label="Insights da vaga">
      <div className="cv-insights__head">
        <h2 className="cv-insights__title">Insights da vaga para você</h2>
        <p className="cv-insights__sub">Com base na descrição e no seu perfil, aqui estão os principais pontos de atenção.</p>
        <button className="cv-insights__link" onClick={onViewAnalysis}>
          Ver análise completa <ArrowRight size={14} />
        </button>
      </div>
      <div className="cv-insights__grid">
        <div className="cv-insight">
          <span className="cv-insight__icon"><TrendingUp size={20} /></span>
          <span>
            <span className="cv-insight__title">Resultados concretos</span>
            <span className="cv-insight__text" style={{ display: 'block' }}>
              {hasAnalysis
                ? `${directCount} de ${requirementReview.length} requisitos comprovados. Vagas nesta área valorizam métricas e impacto — inclua números e resultados nas suas experiências.`
                : 'Vagas nesta área valorizam métricas e impacto. Inclua números e resultados nas suas experiências.'}
            </span>
          </span>
          <ChevronRight size={17} className="cv-insight__chev" />
        </div>
        <div className="cv-insight">
          <span className="cv-insight__icon"><Search size={20} /></span>
          <span>
            <span className="cv-insight__title">Palavras-chave importantes</span>
            <span className="cv-insight__text" style={{ display: 'block' }}>
              {focusKeywords.length > 0
                ? `Use os termos mais relevantes da vaga no seu currículo, como ${formatList(focusKeywords)}.`
                : 'Use os termos mais relevantes da vaga no seu currículo para passar na triagem.'}
            </span>
          </span>
          <ChevronRight size={17} className="cv-insight__chev" />
        </div>
        <div className="cv-insight">
          <span className="cv-insight__icon"><Lightbulb size={20} /></span>
          <span>
            <span className="cv-insight__title">Projetos relevantes</span>
            <span className="cv-insight__text" style={{ display: 'block' }}>
              {baseProjects.length > 0
                ? `Destaque projetos como ${formatList(baseProjects.map((project) => project.name))} para demonstrar experiência prática.`
                : 'Destaque projetos que demonstrem sua experiência prática com as tecnologias da vaga.'}
            </span>
          </span>
          <ChevronRight size={17} className="cv-insight__chev" />
        </div>
      </div>
    </section>
  )
}

function formatList(values: string[]) {
  if (values.length <= 2) return values.join(' e ')
  return `${values.slice(0, -1).join(', ')} e ${values[values.length - 1]}`
}

