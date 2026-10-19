import { useCallback, useEffect, useState } from 'react'
import { Download, ExternalLink, FileText, ListChecks, RefreshCw, Send } from 'lucide-react'
import { applicationsClient } from '../../api/applicationsClient'
import { responseMessage, safeHttpUrl } from '../../app/utils'
import { SectionCard } from '../../components/forms/SectionCard'
import { EmptyState } from '../../components/data-display/EmptyState'
import type { ApplicationHistoryItem } from '../../types'

type Filter = 'all' | 'pending' | 'applied'

type Props = {
  exportBusy: boolean
  onDownload: (versionId: string, format: 'word' | 'pdf') => void
  onMarkApplied: (versionId: string, applied: boolean) => Promise<boolean | void> | boolean | void
  onGoToResume: () => void
}

export function ApplicationsTab({ exportBusy, onDownload, onMarkApplied, onGoToResume }: Props) {
  const [items, setItems] = useState<ApplicationHistoryItem[]>([])
  const [filter, setFilter] = useState<Filter>('all')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const { data } = await applicationsClient.history()
      setItems(data)
    } catch (failure: unknown) {
      setError(responseMessage(failure, 'Não foi possível carregar as candidaturas.'))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { queueMicrotask(() => void load()) }, [load])

  async function toggleApplied(item: ApplicationHistoryItem) {
    const saved = await onMarkApplied(item.resumeVersionId, !item.applied)
    if (saved === false) {
      setError('Não foi possível atualizar o status da candidatura. Tente de novo.')
      return
    }
    setError('')
    setItems((current) => current.map((entry) =>
      entry.resumeVersionId === item.resumeVersionId ? { ...entry, applied: !item.applied } : entry))
  }

  const visible = items.filter((item) => filter === 'all' || (filter === 'applied' ? item.applied : !item.applied))
  const appliedCount = items.filter((item) => item.applied).length

  return (
    <div className="cv-pagebody">
      <SectionCard
        title="Histórico de candidaturas"
        description={items.length === 0 ? 'Nenhum currículo adaptado ainda.' : `${items.length} ${items.length === 1 ? 'currículo adaptado' : 'currículos adaptados'} · ${appliedCount} ${appliedCount === 1 ? 'enviado' : 'enviados'}`}
        icon={ListChecks}
        action={
          <button className="cv-btn cv-btn--secondary cv-btn--small" onClick={() => void load()} disabled={loading} aria-busy={loading}>
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Atualizar
          </button>
        }
      >
        {items.length > 0 && (
          <div className="cv-history__filters" role="group" aria-label="Filtrar candidaturas">
            {([['all', 'Todas'], ['pending', 'A enviar'], ['applied', 'Enviadas']] as const).map(([key, label]) => (
              <button key={key} className="cv-btn cv-btn--secondary cv-btn--small cv-history__filter" aria-pressed={filter === key} onClick={() => setFilter(key)}>
                {label}
              </button>
            ))}
          </div>
        )}

        {error && <div className="cv-alert cv-alert--amber" role="alert">{error}</div>}
        {!error && loading && items.length === 0 && <p className="cv-prose" role="status">Carregando…</p>}
        {!error && !loading && items.length === 0 && (
          <EmptyState
            icon={FileText}
            title="Nenhuma candidatura preparada"
            description="Gere um currículo adaptado a uma vaga e ele aparece aqui, pronto para baixar e enviar."
            action={<button className="cv-btn cv-btn--primary cv-btn--small" onClick={onGoToResume}>Adaptar um currículo</button>}
          />
        )}
        {!error && !loading && items.length > 0 && visible.length === 0 && <p className="cv-history__empty">Nada neste filtro.</p>}

        <div className="cv-batch" style={{ marginTop: 0 }}>
          {visible.map((item) => (
            <div key={item.resumeVersionId} className="cv-batch__item">
              <div className="cv-batch__body">
                <p className="cv-batch__title">{item.role || 'Vaga sem título'}{item.company ? ` · ${item.company}` : ''}</p>
                <p className="cv-batch__meta">
                  {formatDate(item.createdAt)}{item.location ? ` · ${item.location}` : ''} · aderência {Math.round(item.atsScore)}
                  {item.applied && <span className="cv-history__applied"> · enviada</span>}
                </p>
              </div>
              <div className="cv-resultbar__actions">
                <button className="cv-btn cv-btn--primary cv-btn--small" onClick={() => onDownload(item.resumeVersionId, 'pdf')} disabled={exportBusy}><Download size={14} /> PDF</button>
                <button className="cv-btn cv-btn--secondary cv-btn--small" onClick={() => onDownload(item.resumeVersionId, 'word')} disabled={exportBusy}>Word</button>
                {safeHttpUrl(item.sourceUrl) && (
                  <a className="cv-btn cv-btn--secondary cv-btn--small" href={safeHttpUrl(item.sourceUrl)} target="_blank" rel="noreferrer noopener"><ExternalLink size={14} /> Abrir vaga</a>
                )}
                <button className="cv-btn cv-btn--secondary cv-btn--small" aria-pressed={item.applied} onClick={() => void toggleApplied(item)}>
                  <Send size={14} /> {item.applied ? 'Enviada ✓' : 'Marcar enviada'}
                </button>
              </div>
            </div>
          ))}
        </div>
      </SectionCard>
    </div>
  )
}

function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' }).replace(/\./g, '')
}
