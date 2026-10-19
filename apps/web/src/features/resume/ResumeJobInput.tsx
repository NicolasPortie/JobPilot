import { useRef, useState, type KeyboardEvent } from 'react'
import { CircleHelp, FileText, Link2, Paperclip, Upload } from 'lucide-react'

type JobTab = 'desc' | 'link' | 'file'

type Props = {
  jobDescription: string
  setJobDescription: (value: string) => void
  jobUrl: string
  setJobUrl: (value: string) => void
  screenshotFile: File | null
  setScreenshotFile: (value: File | null) => void
  /** Ctrl/Cmd + Enter nos campos de texto gera o currículo. */
  onSubmit?: () => void
}

export function ResumeJobInput({ jobDescription, setJobDescription, jobUrl, setJobUrl, screenshotFile, setScreenshotFile, onSubmit }: Props) {
  const [jobTab, setJobTab] = useState<JobTab>('desc')
  const [showHelp, setShowHelp] = useState(false)
  const descRef = useRef<HTMLTextAreaElement>(null)

  function submitOnShortcut(event: KeyboardEvent) {
    if (onSubmit && event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
      event.preventDefault()
      onSubmit()
    }
  }

  return (
    <section className="cv-card cv-step" aria-labelledby="cv-passo2">
      <div className="cv-stephead">
        <span className="cv-stepnum" aria-hidden>2</span>
        <div>
          <h2 className="cv-steptitle" id="cv-passo2">Dados da vaga</h2>
          <p className="cv-stepsub">Cole a descrição ou o link (ou vários links, um por linha). Ctrl + Enter gera o currículo.</p>
        </div>
        <span className="cv-stephead__side">
          <button className="cv-helplink" onClick={() => setShowHelp((value) => !value)} aria-expanded={showHelp} aria-controls="cv-ajuda-vaga">
            <CircleHelp size={15} /> Precisa de ajuda?
          </button>
        </span>
      </div>

      {showHelp && (
        <div className="cv-helptext" id="cv-ajuda-vaga">
          <strong>Como informar a vaga</strong>
          <ul>
            <li><strong>Descrição:</strong> cole o texto completo do anúncio, com requisitos e responsabilidades.</li>
            <li><strong>Link:</strong> cole o endereço do anúncio. Com vários links (um por linha, até 15) o JobPilot gera um currículo para cada vaga.</li>
            <li><strong>PDF ou imagem:</strong> use quando a vaga só existe como arquivo ou captura de tela.</li>
          </ul>
          <button className="cv-helplink" style={{ marginTop: 8 }} onClick={() => { setJobTab('desc'); setShowHelp(false); window.setTimeout(() => descRef.current?.focus(), 0) }}>
            Entendi, colar a descrição
          </button>
        </div>
      )}

      <div className="cv-tabs" role="tablist" aria-label="Origem dos dados da vaga">
        <button role="tab" aria-selected={jobTab === 'desc'} className={`cv-tab${jobTab === 'desc' ? ' cv-tab--active' : ''}`} onClick={() => setJobTab('desc')}>
          <FileText size={15} /> Colar descrição da vaga
        </button>
        <button role="tab" aria-selected={jobTab === 'link'} className={`cv-tab${jobTab === 'link' ? ' cv-tab--active' : ''}`} onClick={() => setJobTab('link')}>
          <Link2 size={15} /> Link da vaga
        </button>
        <button role="tab" aria-selected={jobTab === 'file'} className={`cv-tab${jobTab === 'file' ? ' cv-tab--active' : ''}`} onClick={() => setJobTab('file')}>
          <Paperclip size={15} /> Anexar PDF ou imagem
        </button>
      </div>

      {jobTab === 'desc' && (
        <div className="cv-field">
          <label className="cv-label" htmlFor="cv-desc">Descrição da vaga <span className="cv-label__req">*</span></label>
          <textarea
            id="cv-desc"
            ref={descRef}
            className="cv-textarea"
            value={jobDescription}
            maxLength={60000}
            onChange={(event) => setJobDescription(event.target.value)}
            onKeyDown={submitOnShortcut}
            placeholder="Cole aqui a descrição completa da vaga, incluindo responsabilidades, requisitos, tecnologias e benefícios. Quanto mais detalhes, melhor será a adaptação."
          />
        </div>
      )}
      {jobTab === 'link' && (
        <div className="cv-field">
          <label className="cv-label" htmlFor="cv-link">Link da vaga</label>
          <textarea
            id="cv-link"
            className="cv-textarea cv-textarea--short"
            value={jobUrl}
            rows={3}
            onChange={(event) => setJobUrl(event.target.value)}
            onKeyDown={submitOnShortcut}
            placeholder="https://empresa.com/vaga"
          />
          <p className="cv-hint">O JobPilot lê o anúncio e extrai requisitos, tecnologias e senioridade. Cole um link por linha para gerar vários currículos de uma vez (até 15).</p>
        </div>
      )}
      {jobTab === 'file' && (
        <div className="cv-field">
          <span className="cv-label">Screenshot ou PDF da vaga</span>
          <div className="cv-filepill">
            <label className="cv-btn cv-btn--secondary" style={{ minHeight: 38 }}>
              <Upload size={15} /> Escolher arquivo
              <input
                className="sr-only"
                type="file"
                accept="image/*,.pdf"
                onChange={(event) => setScreenshotFile(event.target.files?.[0] ?? null)}
              />
            </label>
            {screenshotFile ? (
              <>
                <span className="min-w-0 truncate font-semibold">{screenshotFile.name}</span>
                <button
                  className="cv-helplink"
                  onClick={() => setScreenshotFile(null)}
                  aria-label={`Remover arquivo ${screenshotFile.name}`}
                >
                  × Remover
                </button>
              </>
            ) : (
              <span>Nenhum arquivo escolhido.</span>
            )}
          </div>
        </div>
      )}
    </section>
  )
}
