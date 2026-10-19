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
    <section className="cv-card" aria-label="Pontos que precisam da sua confirmação">
      <div className="cv-sectionhead">
        <span className="cv-sectionhead__icon" aria-hidden><AlertCircle size={18} /></span>
        <div className="min-w-0 flex-1">
          <h2 className="cv-steptitle">Há pontos que precisam da sua confirmação</h2>
          <p className="cv-stepsub">
            {questions.length === 1
              ? '1 requisito sem evidência suficiente. Responda abaixo e clique em Regenerar.'
              : `${questions.length} requisitos sem evidência suficiente. Responda abaixo e clique em Regenerar.`}
          </p>
        </div>
        <button className="cv-btn cv-btn--secondary cv-btn--small cv-sectionhead__action" onClick={() => void onRegenerate()} disabled={busy}>
          <RefreshCw size={13} /> Regenerar
        </button>
      </div>
      <div className="cv-list cv-sectionbody">
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
    </section>
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
    <div className="cv-importbox">
      <label htmlFor={inputId} className="cv-label" style={{ marginBottom: 2 }}>
        {requirementLabel}
      </label>
      <p className="cv-hint" style={{ marginTop: 0 }}>{questionText}</p>
      <textarea
        id={inputId}
        className="cv-textarea cv-textarea--short"
        style={{ marginTop: 8 }}
        value={value}
        onChange={(event) => { setValue(event.target.value); setSaved(false) }}
        placeholder="Descreva sua experiência real com esse requisito…"
        disabled={disabled || saving}
      />
      <div className="cv-row" style={{ marginTop: 8 }}>
        <button
          className="cv-btn cv-btn--secondary cv-btn--small"
          onClick={() => void handleSave()}
          disabled={disabled || saving || !value.trim() || !onSave}
        >
          {saving ? 'Salvando…' : saved ? (<span className="inline-flex items-center gap-1"><Check size={13} /> Salva</span>) : 'Salvar resposta'}
        </button>
        {!onSave && <span className="cv-hint">Salvamento indisponível.</span>}
      </div>
    </div>
  )
}
