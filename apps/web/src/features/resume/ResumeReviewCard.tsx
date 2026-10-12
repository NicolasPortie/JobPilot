import { useState } from 'react'
import { AlertCircle, Check, RefreshCw } from 'lucide-react'
import type { ResumeReviewQuestion } from '../../types'

type Props = {
  questions: ResumeReviewQuestion[]
  busy: boolean
  onRegenerate: () => void
  onSave?: (requirementId: string, label: string, question: string, value: string, type: string) => Promise<void>
}

export function ResumeReviewCard({ questions, busy, onRegenerate, onSave }: Props) {
  if (questions.length === 0) return null

  return (
    <div className="jp-card border-[var(--jp-accent-100)] bg-[var(--jp-accent-50)] p-5">
      <div className="flex items-start gap-3">
        <AlertCircle className="mt-0.5 shrink-0 text-[var(--jp-accent-700)]" size={17} />
        <div className="min-w-0 flex-1">
          <h3 className="text-sm font-black text-[var(--jp-accent-700)]">Há pontos que precisam da sua confirmação</h3>
          <p className="mt-1 text-xs leading-5 text-[var(--jp-accent-700)]">
            {questions.length === 1
              ? '1 requisito sem evidência suficiente. Responda abaixo e clique em Regenerar.'
              : `${questions.length} requisitos sem evidência suficiente. Responda abaixo e clique em Regenerar.`}
          </p>
          <div className="mt-3 grid gap-3">
            {questions.slice(0, 8).map((question) => (
              <ReviewQuestionItem
                key={question.requirementId}
                requirementId={question.requirementId}
                requirementLabel={question.requirementLabel}
                questionText={question.questionText}
                disabled={busy}
                onSave={onSave}
              />
            ))}
          </div>
        </div>
        <button className="jp-btn jp-btn--compact jp-btn--secondary inline-flex shrink-0 items-center gap-1.5 px-3 text-xs disabled:opacity-50" onClick={() => void onRegenerate()} disabled={busy}>
          <RefreshCw size={13} /> Regenerar
        </button>
      </div>
    </div>
  )
}

function ReviewQuestionItem({ requirementId, requirementLabel, questionText, disabled, onSave }: {
  requirementId: string
  requirementLabel: string
  questionText: string
  disabled: boolean
  onSave?: (requirementId: string, label: string, question: string, value: string, type: string) => Promise<void>
}) {
  const [value, setValue] = useState('')
  const [saved, setSaved] = useState(false)
  const [saving, setSaving] = useState(false)
  const inputId = `review-${requirementId}`

  async function handleSave() {
    if (!onSave || !value.trim() || saving) return
    setSaving(true)
    try {
      await onSave(requirementId, requirementLabel, questionText, value.trim(), 'text')
      setSaved(true)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="rounded-lg border border-[var(--jp-border)] bg-white p-3">
      <label htmlFor={inputId} className="block text-xs font-black text-[var(--jp-text-primary)]">
        {requirementLabel}
      </label>
      <p className="mt-1 text-xs leading-5 text-[var(--jp-text-secondary)]">{questionText}</p>
      <textarea
        id={inputId}
        className="jp-input mt-2 min-h-16 text-xs"
        value={value}
        onChange={(event) => { setValue(event.target.value); setSaved(false) }}
        placeholder="Descreva sua experiência real com esse requisito…"
        disabled={disabled || saving}
      />
      <div className="mt-2 flex items-center gap-2">
        <button
          className="jp-btn jp-btn--compact jp-btn--secondary px-3 text-xs disabled:opacity-50"
          onClick={() => void handleSave()}
          disabled={disabled || saving || !value.trim() || !onSave}
        >
          {saving ? 'Salvando…' : saved ? (<span className="inline-flex items-center gap-1"><Check size={13} /> Salva</span>) : 'Salvar resposta'}
        </button>
        {!onSave && <span className="text-[11px] text-[var(--jp-text-tertiary)]">Salvamento indisponível.</span>}
      </div>
    </div>
  )
}
