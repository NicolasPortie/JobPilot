import { useCallback, useEffect, useState } from 'react'
import { clsx } from 'clsx'
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
    { key: 'period', label: 'Periodo' },
    { key: 'description', label: 'Descricao', multiline: true },
  ],
  projects: [
    { key: 'name', label: 'Projeto' },
    { key: 'role', label: 'Papel' },
    { key: 'technologies', label: 'Tecnologias' },
    { key: 'description', label: 'Descricao', multiline: true },
  ],
  education: [
    { key: 'institution', label: 'Instituicao' },
    { key: 'degree', label: 'Curso/Grau' },
    { key: 'year', label: 'Ano' },
  ],
  certifications: [
    { key: 'name', label: 'Certificacao' },
    { key: 'issuer', label: 'Emissor' },
    { key: 'year', label: 'Ano' },
  ],
  languages: [
    { key: 'language', label: 'Idioma' },
    { key: 'level', label: 'Nivel' },
  ],
  skills: [{ key: 'skills', label: 'Skills', multiline: true, placeholder: 'Separe por virgulas ou linhas' }],
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
      setError('Erro ao carregar secoes.')
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
      setError(responseMessage(error, 'Erro ao salvar secao.'))
    } finally {
      setLoading(false)
    }
  }

  async function deleteSection(id: string) {
    if (!confirm('Delete esta secao?')) return
    setLoading(true)
    try {
      await resumeClient.deleteSection(id)
      await loadSections()
    } catch {
      setError('Erro ao excluir secao.')
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
      setError('Erro ao reordenar.')
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
    <div className="jp-section">
      <div className="jp-section-header">
        <FileText size={16} className="text-teal-700" />
        <h2 className="flex-1 text-sm font-black text-slate-900">Editor de secoes</h2>
      </div>
      <div className="space-y-4 p-5">
        {error && <div className="jp-alert jp-alert--red">{error}</div>}
        <div className="space-y-2">
          {sections.map((section, index) => (
            <div key={section.id} className="jp-card flex items-start gap-3 p-3">
              <div className="min-w-0 flex-1">
                <div className="text-sm font-black text-slate-900">{section.title}</div>
                <div className="text-xs text-slate-500">{section.sectionType} · ordem {section.sortOrder}</div>
              </div>
              <div className="flex items-center gap-1">
                <button className="grid size-8 place-items-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-600 disabled:opacity-50" title="Mover para cima" onClick={() => void moveSection(index, -1)} disabled={index === 0 || loading}>
                  <ChevronUp size={14} />
                </button>
                <button className="grid size-8 place-items-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-600 disabled:opacity-50" title="Mover para baixo" onClick={() => void moveSection(index, 1)} disabled={index === sections.length - 1 || loading}>
                  <ChevronDown size={14} />
                </button>
                <button className="grid size-8 place-items-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-slate-600" title="Editar" onClick={() => startEdit(section)}>
                  <Edit size={14} />
                </button>
                <button className="grid size-8 place-items-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-rose-600" title="Delete" onClick={() => void deleteSection(section.id)}>
                  <Trash2 size={14} />
                </button>
              </div>
            </div>
          ))}
          {sections.length === 0 && <span className="block text-center text-sm text-slate-500">Nenhuma seção nesta versão.</span>}
        </div>

        <div className="jp-card space-y-4 p-4">
          <div className="text-sm font-black text-slate-900">{editingId ? 'Editar seção' : 'Nova seção'}</div>
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="block">
              <FieldLabel>Tipo</FieldLabel>
              <select className="jp-input" value={draftType} onChange={(event) => changeDraftType(event.target.value)} disabled={Boolean(editingId)}>
                <option value="experience">Experiencia</option>
                <option value="projects">Projetos</option>
                <option value="education">Educacao</option>
                <option value="certifications">Certificacoes</option>
                <option value="languages">Idiomas</option>
                <option value="skills">Skills</option>
                <option value="summary">Resumo</option>
                <option value="identity">Identidade/Contato</option>
                <option value="raw_import">Importacao bruta</option>
              </select>
            </label>
            <label className="block">
              <FieldLabel>Titulo</FieldLabel>
              <input className="jp-input" value={draftTitle} onChange={(event) => setDraftTitle(event.target.value)} placeholder="ex: Experiencia Profissional" />
            </label>
          </div>
          <div className="space-y-3">
            {draftItems.map((item, index) => (
              <div key={index} className="jp-card space-y-3 p-3">
                {allowsMultiple && (
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-bold text-slate-700">Item {index + 1}</span>
                    <button className="grid size-8 place-items-center rounded-lg text-slate-400 transition hover:bg-slate-100 hover:text-rose-600 disabled:opacity-50" title="Remover item" onClick={() => removeItem(index)} disabled={draftItems.length <= 1}>
                      <Trash2 size={14} />
                    </button>
                  </div>
                )}
                <div className="grid gap-4 sm:grid-cols-2">
                  {fields.map((field) => (
                    <div className={clsx(field.multiline && 'sm:col-span-2')} key={field.key}>
                      <FieldLabel>{field.label}</FieldLabel>
                      {field.multiline ? (
                        <textarea className="jp-input" value={item[field.key] ?? ''} onChange={(event) => updateItem(index, field.key, event.target.value)} rows={3} placeholder={field.placeholder} />
                      ) : (
                        <input className="jp-input" value={item[field.key] ?? ''} onChange={(event) => updateItem(index, field.key, event.target.value)} placeholder={field.placeholder} />
                      )}
                    </div>
                  ))}
                </div>
              </div>
            ))}
            {allowsMultiple && (
              <button className="jp-btn jp-btn--secondary h-9 px-3 text-xs" onClick={addItem} type="button">
                <Plus size={14} /> Adicionar item
              </button>
            )}
            <div className="text-xs text-slate-400">Os campos sao salvos como JSON estruturado no backend.</div>
          </div>
          <div className="flex items-center gap-2">
            <button className="jp-btn jp-btn--primary h-10 px-4 disabled:opacity-50" onClick={() => void saveSection()} disabled={loading || !draftTitle.trim()}>
              <Save size={14} /> {editingId ? 'Save alteracoes' : 'Adicionar'}
            </button>
            {editingId && (
              <button className="jp-btn jp-btn--secondary h-10 px-3 text-xs" onClick={resetDraft}>
                Cancelar
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
