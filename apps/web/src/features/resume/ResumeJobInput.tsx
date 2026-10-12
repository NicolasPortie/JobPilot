import { useRef, useState } from 'react'
import { CircleHelp, FileText, Link2, Paperclip, Upload } from 'lucide-react'

type JobTab = 'desc' | 'link' | 'file'

type Props = {
  jobDescription: string
  setJobDescription: (value: string) => void
  jobUrl: string
  setJobUrl: (value: string) => void
  screenshotFile: File | null
  setScreenshotFile: (value: File | null) => void
}

const MAX_DESC = 2000

export function ResumeJobInput({ jobDescription, setJobDescription, jobUrl, setJobUrl, screenshotFile, setScreenshotFile }: Props) {
  const [jobTab, setJobTab] = useState<JobTab>('desc')
  const descRef = useRef<HTMLTextAreaElement>(null)

  return (
    <section className="cv-card cv-step" aria-labelledby="cv-passo2">
      <div className="cv-stephead">
        <span className="cv-stepnum" aria-hidden>2</span>
        <div>
          <h2 className="cv-steptitle" id="cv-passo2">Dados da vaga</h2>
          <p className="cv-stepsub">Informe os dados da vaga para que o JobPilot possa adaptar seu currículo.</p>
        </div>
        <span className="cv-stephead__side">
          <button className="cv-helplink" onClick={() => { setJobTab('desc'); descRef.current?.focus() }}>
            <CircleHelp size={15} /> Precisa de ajuda?
          </button>
        </span>
      </div>

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
            maxLength={MAX_DESC}
            onChange={(event) => setJobDescription(event.target.value)}
            placeholder="Cole aqui a descrição completa da vaga, incluindo responsabilidades, requisitos, tecnologias e benefícios. Quanto mais detalhes, melhor será a adaptação."
          />
          <p className="cv-counter">{jobDescription.length}/{MAX_DESC}</p>
        </div>
      )}
      {jobTab === 'link' && (
        <div className="cv-field">
          <label className="cv-label" htmlFor="cv-link">Link da vaga</label>
          <input
            id="cv-link"
            className="cv-input"
            value={jobUrl}
            onChange={(event) => setJobUrl(event.target.value)}
            placeholder="https://empresa.com/vaga"
            type="url"
          />
          <p className="cv-hint">O JobPilot lê o anúncio e extrai requisitos, tecnologias e senioridade.</p>
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
