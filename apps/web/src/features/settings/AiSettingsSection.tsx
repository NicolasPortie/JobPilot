import { useEffect, useId, useState } from 'react'
import { Bot } from 'lucide-react'
import { aiClient, type AiProviderPreset, type AiSettings, type AiSettingsInput } from '../../api/aiClient'
import { SectionCard } from '../../components/forms/SectionCard'
import { FieldLabel } from '../../components/forms/FieldLabel'
import { LOCAL_API_OFFLINE_MESSAGE, responseMessage } from '../../app/utils'

const sourceLabels: Record<AiSettings['activeSource'], string> = {
  saved: 'configurada nesta tela',
  config: 'configuração do servidor',
  ollama: 'Ollama local',
}

type Feedback = { tone: 'ok' | 'error'; text: string } | null

function formFrom(settings: AiSettings): AiSettingsInput {
  return {
    providerType: settings.providerType,
    name: settings.name,
    baseUrl: settings.baseUrl,
    model: settings.model,
    apiKey: '',
    enabled: settings.enabled,
  }
}

export function AiSettingsSection() {
  const ids = { type: useId(), name: useId(), url: useId(), model: useId(), key: useId() }
  const [settings, setSettings] = useState<AiSettings | null>(null)
  const [form, setForm] = useState<AiSettingsInput | null>(null)
  const [busy, setBusy] = useState<'save' | 'test' | 'remove' | null>(null)
  const [feedback, setFeedback] = useState<Feedback>(null)
  const [loadFailed, setLoadFailed] = useState(false)

  useEffect(() => {
    let active = true
    void aiClient.settings().then(({ data }) => {
      if (!active) return
      setSettings(data)
      setForm(formFrom(data))
    }).catch(() => { if (active) setLoadFailed(true) })
    return () => { active = false }
  }, [])

  if (loadFailed) return <SectionCard title="Inteligência artificial" icon={Bot}><p role="alert" className="cv-alert cv-alert--red">{LOCAL_API_OFFLINE_MESSAGE}</p></SectionCard>
  if (!settings || !form) return <SectionCard title="Inteligência artificial" icon={Bot}><p role="status" className="cv-prose">Carregando…</p></SectionCard>

  const preset = settings.presets.find((item) => item.providerType === form.providerType)
  // A chave salva só vale para o mesmo provedor e endereço; o servidor faz a checagem final.
  const keepsSavedKey = settings.saved && settings.hasApiKey && form.providerType === settings.providerType && form.baseUrl === settings.baseUrl
  const update = (patch: Partial<AiSettingsInput>) => setForm({ ...form, ...patch })

  function choosePreset(next: AiProviderPreset) {
    update({ providerType: next.providerType, name: next.providerType === 'openai-compatible' ? '' : next.label, baseUrl: next.baseUrl, model: next.model, apiKey: '' })
    setFeedback(null)
  }

  async function run(kind: 'save' | 'test' | 'remove') {
    if (!form) return
    setBusy(kind)
    setFeedback(null)
    try {
      if (kind === 'save') {
        const { data } = await aiClient.saveSettings(form)
        setSettings(data)
        setForm(formFrom(data))
        setFeedback({ tone: 'ok', text: 'Configuração salva. Ela já vale para os próximos currículos.' })
      } else if (kind === 'remove') {
        await aiClient.removeSettings()
        const { data } = await aiClient.settings()
        setSettings(data)
        setForm(formFrom(data))
        setFeedback({ tone: 'ok', text: 'Configuração removida.' })
      } else {
        const { data } = await aiClient.testSettings(form)
        setFeedback(data.ok
          ? { tone: 'ok', text: `Conexão funcionando com ${data.model}.` }
          : { tone: 'error', text: data.error ?? 'A conexão falhou.' })
      }
    } catch (error: unknown) {
      setFeedback({ tone: 'error', text: responseMessage(error, 'Não foi possível concluir a operação.') })
    } finally {
      setBusy(null)
    }
  }

  return (
    <SectionCard
      title="Inteligência artificial"
      description="A IA ajuda a escrever o currículo, mas só com o que já está no seu currículo. Sem IA, o JobPilot gera a versão sem inventar nada."
      icon={Bot}
      action={<span className="cv-badge cv-badge--peach" role="status">Em uso agora: {settings.activeProvider} ({sourceLabels[settings.activeSource]})</span>}
    >
      <form className="cv-form" onSubmit={(event) => { event.preventDefault(); void run('save') }}>
        <div>
          <label htmlFor={ids.type}><FieldLabel>Provedor</FieldLabel></label>
          <select id={ids.type} className="cv-input cv-select" value={form.providerType} aria-describedby={`${ids.type}-hint`}
            onChange={(event) => { const next = settings.presets.find((item) => item.providerType === event.target.value); if (next) choosePreset(next) }}>
            {settings.presets.map((item) => <option key={item.providerType} value={item.providerType}>{item.label}</option>)}
          </select>
          {preset?.hint && <span id={`${ids.type}-hint`} className="cv-hint">{preset.hint}</span>}
        </div>

        <label htmlFor={ids.name} className="block">
          <FieldLabel required>Nome</FieldLabel>
          <input id={ids.name} className="cv-input" value={form.name} onChange={(event) => update({ name: event.target.value })} placeholder="ex: OmniRoute" />
        </label>

        <label htmlFor={ids.url} className="block">
          <FieldLabel required>Endereço do serviço</FieldLabel>
          <input id={ids.url} className="cv-input" value={form.baseUrl} onChange={(event) => update({ baseUrl: event.target.value })} placeholder="https://…/v1" autoComplete="off" />
        </label>

        <label htmlFor={ids.model} className="block">
          <FieldLabel required>Modelo</FieldLabel>
          <input id={ids.model} className="cv-input" value={form.model} onChange={(event) => update({ model: event.target.value })} placeholder="ex: claude-haiku-5-5" autoComplete="off" />
        </label>

        {preset?.requiresApiKey && (
          <div>
            <label htmlFor={ids.key}><FieldLabel required={!keepsSavedKey}>Chave de API</FieldLabel></label>
            <input id={ids.key} className="cv-input" type="password" value={form.apiKey} autoComplete="new-password" spellCheck={false}
              aria-describedby={`${ids.key}-hint`} onChange={(event) => update({ apiKey: event.target.value })}
              placeholder={keepsSavedKey ? 'Chave salva — deixe em branco para manter' : 'Cole a chave aqui'} />
            <span id={`${ids.key}-hint`} className="cv-hint">A chave é guardada criptografada e nunca volta para esta tela.</span>
          </div>
        )}

        <label className="cv-check">
          <input type="checkbox" checked={form.enabled} onChange={(event) => update({ enabled: event.target.checked })} />
          Usar esta configuração
        </label>

        {feedback && (
          <p role="status" className={feedback.tone === 'ok' ? 'cv-alert cv-alert--green' : 'cv-alert cv-alert--red'}>{feedback.text}</p>
        )}

        <div className="cv-row">
          <button type="submit" className="cv-btn cv-btn--primary" disabled={busy !== null}>{busy === 'save' ? 'Salvando…' : 'Salvar'}</button>
          <button type="button" className="cv-btn cv-btn--secondary" disabled={busy !== null} onClick={() => void run('test')}>
            {busy === 'test' ? 'Testando…' : 'Testar conexão'}
          </button>
          {settings.saved && (
            <button type="button" className="cv-btn cv-btn--ghost" disabled={busy !== null} onClick={() => void run('remove')}>Remover configuração</button>
          )}
        </div>
      </form>
    </SectionCard>
  )
}
