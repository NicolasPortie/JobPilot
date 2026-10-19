import { ArrowRight, Lightbulb, Search, TrendingUp } from 'lucide-react'
import type { ComponentType } from 'react'
import type { ResumeAnalysis } from '../../types'

type Props = {
  analysis: ResumeAnalysis | null
  onViewAnalysis: () => void
}

const icons: Record<string, ComponentType<{ size?: number }>> = {
  results: TrendingUp,
  keywords: Search,
  projects: Lightbulb,
}

// Orientação geral, mostrada antes de existir uma análise. Depois dela, o texto vem do backend.
const generalTips = [
  { key: 'results', title: 'Resultados concretos', text: 'Vagas nesta área valorizam métricas e impacto: inclua números e resultados nas suas experiências.' },
  { key: 'keywords', title: 'Palavras-chave importantes', text: 'Use os termos mais relevantes da vaga no seu currículo para passar na triagem.' },
  { key: 'projects', title: 'Projetos relevantes', text: 'Destaque projetos que demonstrem sua experiência prática com as tecnologias da vaga.' },
]

export function ResumeAtsInsights({ analysis, onViewAnalysis }: Props) {
  const hasRequirements = Boolean(analysis && analysis.requirements.length > 0)
  const insights = analysis && analysis.insights.length > 0 ? analysis.insights : generalTips

  return (
    <section className="cv-insights" style={{ marginTop: 16 }} aria-label="Insights da vaga">
      <div className="cv-insights__head">
        <h2 className="cv-insights__title">Insights da vaga para você</h2>
        <p className="cv-insights__sub">
          {hasRequirements
            ? 'Com base na descrição e no seu perfil, aqui estão os principais pontos de atenção.'
            : 'Boas práticas para começar. Depois de analisar uma vaga, os pontos de atenção aparecem aqui.'}
        </p>
        {hasRequirements && (
          <button className="cv-insights__link" onClick={onViewAnalysis}>
            Ver análise completa <ArrowRight size={14} />
          </button>
        )}
      </div>
      <div className="cv-insights__grid">
        {insights.map((insight) => {
          const Icon = icons[insight.key] ?? Lightbulb
          return (
            <div key={insight.key} className="cv-insight">
              <span className="cv-insight__icon"><Icon size={20} /></span>
              <span>
                <span className="cv-insight__title">{insight.title}</span>
                <span className="cv-insight__text" style={{ display: 'block' }}>{insight.text}</span>
              </span>
            </div>
          )
        })}
      </div>
    </section>
  )
}
