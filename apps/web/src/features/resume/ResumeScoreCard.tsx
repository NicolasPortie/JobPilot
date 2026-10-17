import { clsx } from 'clsx'
import { ShieldCheck } from 'lucide-react'
import { SectionCard } from '../../components/forms/SectionCard'
import type { TailoredResume } from '../../types'

type ScoreTone = 'good' | 'warn' | 'soft'

type Props = {
  draft: TailoredResume
  targetLabel: string
  targetMeta: string
  summary: string
  score: number
  scoreTone: ScoreTone
  directCount: number
  adjacentCount: number
  missingCount: number
}

export function ResumeScoreCard({ draft, targetLabel, targetMeta, summary, score, scoreTone, directCount, adjacentCount, missingCount }: Props) {
  return (
    <>
      <section className="jp-card overflow-hidden">
        <div className="flex flex-col gap-5 border-b border-[var(--jp-border)] p-5 sm:flex-row sm:items-start sm:justify-between sm:p-6">
          <div className="min-w-0">
            <p className="jp-eyebrow">Leitura da oportunidade</p>
            <h3 className="mt-2 truncate text-xl font-black tracking-[-0.03em] text-[var(--jp-text-primary)]">{targetLabel}</h3>
            {targetMeta && <p className="mt-1 text-sm font-semibold text-[var(--jp-text-secondary)]">{targetMeta}</p>}
            {summary && <p className="mt-3 max-w-2xl text-sm leading-6 text-[var(--jp-text-secondary)]">{summary}</p>}
          </div>
          <div className="flex shrink-0 items-center gap-3">
            <div className={clsx('jp-score h-16 w-16 text-xl', scoreTone === 'good' ? 'jp-score--strong' : scoreTone === 'warn' ? 'jp-score--mid' : 'jp-score--soft')}>
              {score}
            </div>
            <div>
              <p className="text-sm font-black text-[var(--jp-text-primary)]">Aderência estimada</p>
              <p className="mt-1 max-w-[150px] text-xs leading-5 text-[var(--jp-text-secondary)]">Ponto de partida para sua revisão, não uma garantia de entrevista.</p>
              {draft.usedAi === false && (
                <p className="jp-badge jp-badge--amber mt-2" role="status" title={draft.aiNotice ?? 'IA indisponível — resultado determinístico'}>
                  Sem IA — análise determinística
                </p>
              )}
              {draft.usedAi === true && (
                <p className="jp-badge jp-badge--teal mt-2" role="status">
                  Com IA
                </p>
              )}
            </div>
          </div>
        </div>
        <div className="grid grid-cols-3 divide-x divide-[var(--jp-border)]">
          <Metric value={directCount} label="Comprovados" tone="good" />
          <Metric value={adjacentCount} label="Relacionados" tone="warn" />
          <Metric value={missingCount} label="Para revisar" tone="soft" />
        </div>
      </section>

      <SectionCard title="Por que essa nota?" icon={ShieldCheck}>
        <div className="space-y-4">
          {(draft.scoreComponents ?? []).map((component) => (
            <div key={component.key}>
              <div className="mb-1.5 flex items-center justify-between gap-3">
                <span className="text-sm font-bold text-[var(--jp-text-primary)]">{component.label}</span>
                <span className="text-xs font-black text-[var(--jp-text-secondary)]">{Math.round(component.score)}/{Math.round(component.maxScore)}</span>
              </div>
              <div className="h-1.5 overflow-hidden rounded-full bg-[var(--jp-surface-muted)]">
                <div className="h-full rounded-full bg-[var(--jp-primary-500)] transition-[width] duration-500" style={{ width: (Math.min(100, Math.max(0, (component.score / Math.max(component.maxScore, 1)) * 100))).toString() + '%' }} />
              </div>
              {component.reason && <p className="mt-1.5 text-xs leading-5 text-[var(--jp-text-tertiary)]">{component.reason}</p>}
            </div>
          ))}
          {!draft.scoreComponents?.length && <p className="text-sm text-[var(--jp-text-secondary)]">Sem componentes suficientes para detalhar a nota.</p>}
        </div>
      </SectionCard>
    </>
  )
}

function Metric({ value, label, tone }: { value: number; label: string; tone: 'good' | 'warn' | 'soft' }) {
  return (
    <div className="px-3 py-4 text-center sm:px-5">
      <p className={clsx('text-2xl font-black tracking-[-0.04em]', tone === 'good' ? 'text-[var(--jp-primary-700)]' : tone === 'warn' ? 'text-[var(--jp-accent-700)]' : 'text-[var(--jp-text-secondary)]')}>{value}</p>
      <p className="mt-1 text-[10px] font-black uppercase tracking-[0.12em] text-[var(--jp-text-tertiary)]">{label}</p>
    </div>
  )
}
