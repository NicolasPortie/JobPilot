import { useEffect, useId, useState } from 'react'
import { aiClient } from '../../api/aiClient'
import type { AiStatus } from '../../api/aiClient'

type Props = { enabled: boolean; busy: boolean; onToggle: (enabled: boolean) => void }

export function ResumeAiControl({ enabled, busy, onToggle }: Props) {
  const statusId = useId()
  const [status, setStatus] = useState<AiStatus | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let active = true
    if (!busy) {
      void aiClient.status().then(({ data }) => {
        if (active) { setStatus(data); setFailed(false) }
      }).catch(() => {
        if (active) { setStatus(null); setFailed(true) }
      })
    }
    return () => { active = false }
  }, [busy, enabled])

  const text = !enabled ? 'IA desativada' : failed ? 'Não foi possível consultar a IA'
    : !status ? 'Consultando IA…' : !status.configured ? 'IA não configurada'
    : !status.available ? 'IA indisponível · modo sem IA'
    : status.lastCallSucceeded === true ? 'IA respondendo' : 'IA configurada · conexão não verificada'

  return (
    <div className="cv-ai-control">
      <label className="cv-ai-control__toggle">
        <input type="checkbox" checked={enabled} onChange={(event) => onToggle(event.target.checked)}
          aria-label="Usar IA quando disponível" aria-describedby={statusId} />
        Usar IA
      </label>
      <span id={statusId} role="status" className="cv-ai-control__status">
        {text}
        {enabled && status?.configured && <span className="cv-ai-control__provider">{status.provider} · {status.model}</span>}
      </span>
    </div>
  )
}
