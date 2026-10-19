import { useCallback, useEffect, useState } from 'react'
import { ChevronDown, ChevronUp, Edit, FileText, Plus, Save, Trash2 } from 'lucide-react'
import { resumeClient } from '../../api/resumeClient'
import { responseMessage } from '../../app/utils'
import { FieldLabel } from '../../components/forms/FieldLabel'
import type { ResumeSection } from '../../types'

type SectionField = { key: string; label: string; multiline?: boolean; placeholder?: string }
type SectionItem = Record<string, string>

const sectionFields: Record<string, SectionField[]> = {
  experience: [
    { key: 'company', label: 'Empresa' },
    { key: 'role', label: 'Cargo' },
    { key: 'period', label: 'Período' },
    { key: 'description', label: 'Descrição', multiline: true },
  ],
  projects: [
    { key: 'name', label: 'Projeto' },
    { key: 'role', label: 'Papel' },
    { key: 'technologies', label: 'Tecnologias' },
    { key: 'description', label: 'Descrição', multiline: true },
  ],
  education: [
    { key: 'institution', label: 'Instituição' },
    { key: 'degree', label: 'Curso/Grau' },
    { key: 'year', label: 'Ano' },
  ],
  certifications: [
    { key: 'name', label: 'Certificação' },
    { key: 'issuer', label: 'Emissor' },
    { key: 'year', label: 'Ano' },
  ],
  languages: [
    { key: 'language', label: 'Idioma' },
    { key: 'level', label: 'Nível' },
  ],
  skills: [{ key: 'skills', label: 'Habilidades', multiline: true, placeholder: 'Separe por vírgulas ou linhas' }],
  summary: [{ key: 'summary', label: 'Resumo', multiline: true }],
  identity: [
    { key: 'name', label: 'Nome' },
    { key: 'email', label: 'Email' },
    { key: 'phone', label: 'Telefone' },
    { key: 'links', label: 'Links', multiline: true },
  ],
  raw_import: [{ key: 'text', label: 'Texto importado', multiline: true }],
}

function defaultSectionItem(type: string): SectionItem {
  return Object.fromEntries((sectionFields[type] ?? sectionFields.raw_import).map((field) => [field.key, '']))
}

function parseSectionItems(type: string, contentJson: string): SectionItem[] {
  const fields = sectionFields[type] ?? sectionFields.raw_import
  try {
    const parsed = JSON.parse(contentJson || '{}')
    if (Array.isArray(parsed?.items)) {
      return parsed.items.map((item: Record<string, unknown>) =>
        Object.fromEntries(fields.map((field) => [field.key, String(item?.[field.key] ?? '')]))
      )
    }
    if (Array.isArray(parsed?.skills) && type === 'skills') return [{ skills: parsed.skills.join('\n') }]
    return [Object.fromEntries(fields.map((field) => [field.key, String(parsed?.[field.key] ?? parsed?.text ?? '')]))]
  } catch {
    return [{ ...defaultSectionItem(type), [fields[0]?.key ?? 'text']: contentJson }]
  }
}

function sectionItemsToJson(type: string, items: SectionItem[]) {
  const cleaned = items
    .map((item) => Object.fromEntries(Object.entries(item).map(([key, value]) => [key, value.trim()])))
    .filter((item) => Object.values(item).some((value) => value.length > 0))
  if (type === 'summary') return JSON.stringify({ summary: cleaned[0]?.summary ?? '' })
  if (type === 'skills') {
    return JSON.stringify({
      skills: (cleaned[0]?.skills ?? '').split(/[\n,]/).map((value) => value.trim()).filter(Boolean),
    })
  }
  if (type === 'raw_import') return JSON.stringify({ text: cleaned[0]?.text ?? '' })
  return JSON.stringify({ items: cleaned })
}

export function SectionEditorPanel({ versionId }: { versionId: string }) {
  const [sections, setSections] = useState<ResumeSection[]>([])
  const [loading, setLoading] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [draftType, setDraftType] = useState('experience')
  const [draftTitle, setDraftTitle] = useState('')
  const [draftItems, setDraftItems] = useState<SectionItem[]>([defaultSectionItem('experience')])
  const [error, setError] = useState('')

  const loadSections = useCallback(async () => {
    if (!versionId) return
    setLoading(true)
    try {
      const { data } = await resumeClient.listSections(versionId)
      setSections(data)
    } catch {
      setError('Não foi possível carregar as seções.')
    } finally {
      setLoading(false)
    }
  }, [versionId])

  useEffect(() => {
    queueMicrotask(() => void loadSections())
  }, [loadSections])

  function resetDraft() {
    setEditingId(null)
    setDraftType('experience')
    setDraftTitle('')
    setDraftItems([defaultSectionItem('experience')])
    setError('')
  }

  async function saveSection() {
    if (!draftTitle.trim()) return
    setLoading(true)
    try {
      const draftContent = sectionItemsToJson(draftType, draftItems)
      if (editingId) {
        await resumeClient.updateSection(editingId, { title: draftTitle, contentJson: draftContent })
      } else {
        await resumeClient.createSection(versionId, { sectionType: draftType, title: draftTitle, contentJson: draftContent })
      }
      await loadSections()
      resetDraft()
    } catch (error: unknown) {
      setError(responseMessage(error, 'Não foi possível salvar a seção.'))
    } finally {
      setLoading(false)
    }
  }

  async function deleteSection(id: string) {
    if (!confirm('Excluir esta seção?')) return
    setLoading(true)
    try {
      await resumeClient.deleteSection(id)
      await loadSections()
    } catch {
      setError('Não foi possível excluir a seção.')
    } finally {
      setLoading(false)
    }
  }

  async function moveSection(index: number, direction: -1 | 1) {
    const newIndex = index + direction
    if (newIndex < 0 || newIndex >= sections.length) return
    const reordered = [...sections]
    const [moved] = reordered.splice(index, 1)
    reordered.splice(newIndex, 0, moved)
    const ids = reordered.map((section) => section.id)
    setSections(reordered)
    try {
      await resumeClient.reorderSections(versionId, ids)
    } catch {
      setError('Não foi possível reordenar as seções.')
      await loadSections()
    }
  }

  function startEdit(section: ResumeSection) {
    setEditingId(section.id)
    setDraftType(section.sectionType)
    setDraftTitle(section.title)
    setDraftItems(parseSectionItems(section.sectionType, section.contentJson))
    setError('')
  }

  function changeDraftType(value: string) {
    setDraftType(value)
    setDraftItems([defaultSectionItem(value)])
  }

  function updateItem(index: number, key: string, value: string) {
    setDraftItems((items) => items.map((item, itemIndex) => (itemIndex === index ? { ...item, [key]: value } : item)))
  }

  function addItem() {
    setDraftItems((items) => [...items, defaultSectionItem(draftType)])
  }

  function removeItem(index: number) {
    setDraftItems((items) => (items.length <= 1 ? items : items.filter((_, itemIndex) => itemIndex !== index)))
  }

  const fields = sectionFields[draftType] ?? sectionFields.raw_import
  const allowsMultiple = !['summary', 'skills', 'raw_import', 'identity'].includes(draftType)

  return (
    <div className="cv-importbox">
      <div className="cv-sectionhead" style={{ alignItems: 'center' }}>
        <span className="cv-sectionhead__icon"><FileText size={17} /></span>
        <h3 className="cv-steptitle" style={{ fontSize: 15 }}>Editor de seções</h3>
      </div>
      <div className="cv-form" style={{ marginTop: 14 }}>
        {error && <div className="cv-alert cv-alert--red" role="alert">{error}</div>}
        <div className="cv-list">
          {sections.map((section, index) => (
            <div key={section.id} className="cv-listitem">
              <div className="cv-listitem__body">
                <div className="cv-listitem__title">{section.title}</div>
                <div className="cv-listitem__meta">{section.sectionType} · ordem {section.sortOrder}</div>
              </div>
              <div className="cv-row" style={{ gap: 2, flexWrap: 'nowrap' }}>
                <button className="cv-iconbtn" title="Mover para cima" aria-label={`Mover ${section.title} para cima`} onClick={() => void moveSection(index, -1)} disabled={index === 0 || loading}>
                  <ChevronUp size={14} />
                </button>
                <button className="cv-iconbtn" title="Mover para baixo" aria-label={`Mover ${section.title} para baixo`} onClick={() => void moveSection(index, 1)} disabled={index === sections.length - 1 || loading}>
                  <ChevronDown size={14} />
                </button>
                <button className="cv-iconbtn" title="Editar" aria-label={`Editar ${section.title}`} onClick={() => startEdit(section)}>
                  <Edit size={14} />
                </button>
                <button className="cv-iconbtn" title="Excluir" aria-label={`Excluir ${section.title}`} onClick={() => void deleteSection(section.id)}>
                  <Trash2 size={14} />
                </button>
              </div>
            </div>
          ))}
          {sections.length === 0 && <p className="cv-hint" style={{ textAlign: 'center' }}>Nenhuma seção nesta versão.</p>}
        </div>

        <div className="cv-card" style={{ boxShadow: 'none' }}>
          <div className="cv-steptitle" style={{ fontSize: 14 }}>{editingId ? 'Editar seção' : 'Nova seção'}</div>
          <div className="cv-form" style={{ marginTop: 12 }}>
            <div className="cv-formrow">
              <label className="block">
                <FieldLabel>Tipo</FieldLabel>
                <select className="cv-input cv-select" value={draftType} onChange={(event) => changeDraftType(event.target.value)} disabled={Boolean(editingId)}>
                  <option value="experience">Experiência</option>
                  <option value="projects">Projetos</option>
                  <option value="education">Educação</option>
                  <option value="certifications">Certificações</option>
                  <option value="languages">Idiomas</option>
                  <option value="skills">Habilidades</option>
                  <option value="summary">Resumo</option>
                  <option value="identity">Identidade e contato</option>
                  <option value="raw_import">Importação bruta</option>
                </select>
              </label>
              <label className="block">
                <FieldLabel>Título</FieldLabel>
                <input className="cv-input" value={draftTitle} onChange={(event) => setDraftTitle(event.target.value)} placeholder="ex: Experiência profissional" />
              </label>
            </div>
            {draftItems.map((item, index) => (
              <div key={index} className="cv-importbox">
                {allowsMultiple && (
                  <div className="cv-row cv-row--between" style={{ marginBottom: 8 }}>
                    <span style={{ fontSize: 13, fontWeight: 800 }}>Item {index + 1}</span>
                    <button className="cv-iconbtn" title="Remover item" aria-label={`Remover item ${index + 1}`} onClick={() => removeItem(index)} disabled={draftItems.length <= 1}>
                      <Trash2 size={14} />
                    </button>
                  </div>
                )}
                <div className="cv-formrow">
                  {fields.map((field) => (
                    <div key={field.key} style={field.multiline ? { gridColumn: '1 / -1' } : undefined}>
                      <FieldLabel>{field.label}</FieldLabel>
                      {field.multiline ? (
                        <textarea className="cv-textarea cv-textarea--short" value={item[field.key] ?? ''} onChange={(event) => updateItem(index, field.key, event.target.value)} rows={3} placeholder={field.placeholder} />
                      ) : (
                        <input className="cv-input" value={item[field.key] ?? ''} onChange={(event) => updateItem(index, field.key, event.target.value)} placeholder={field.placeholder} />
                      )}
                    </div>
                  ))}
                </div>
              </div>
            ))}
            {allowsMultiple && (
              <div>
                <button className="cv-btn cv-btn--secondary cv-btn--small" onClick={addItem} type="button">
                  <Plus size={14} /> Adicionar item
                </button>
              </div>
            )}
            <div className="cv-row">
              <button className="cv-btn cv-btn--primary cv-btn--small" onClick={() => void saveSection()} disabled={loading || !draftTitle.trim()}>
                <Save size={14} /> {editingId ? 'Salvar alterações' : 'Adicionar'}
              </button>
              {editingId && (
                <button className="cv-btn cv-btn--secondary cv-btn--small" onClick={resetDraft}>
                  Cancelar
                </button>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
