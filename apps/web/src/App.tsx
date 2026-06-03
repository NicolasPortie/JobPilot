import { useCallback, useEffect, useMemo, useState } from 'react'
import type { Dispatch, SetStateAction } from 'react'
import {
  Bot,
  BriefcaseBusiness,
  CheckCircle2,
  CircleAlert,
  ClipboardList,
  ExternalLink,
  FileText,
  Filter,
  Footprints,
  History,
  KeyRound,
  Play,
  RefreshCw,
  Search,
  ShieldCheck,
  Trash2,
  UserRound,
  WandSparkles,
  Clock,
  Award,
  AlertCircle,
  AlertTriangle,
  MessageSquare,
  Zap,
  Plus,
  Save,
  Terminal,
  Eye,
  EyeOff,
  Lightbulb,
  ChevronUp,
  ChevronDown,
  Edit,
} from 'lucide-react'
import { api } from './api/client'
import { agentClient } from './api/agentClient'
import { jobTargetClient } from './api/jobTargetClient'
import { jobsClient } from './api/jobsClient'
import { profileClient } from './api/profileClient'
import { resumeClient } from './api/resumeClient'
import { MetricCard, Sidebar, Topbar } from './components/layout'
import { actionTypeLabels, checkpointReasonLabels, eventTypeLabels, fieldNameLabels, fieldTypeLabels, guardVerdictLabels, jobStatusLabels, jobStatuses, pageStateLabels, plannerLabels, recoveryModeLabels, riskLevelLabels, runStateLabels, stageLabels, statusBadge, valueSourceLabels } from './constants/labels'
import { useJobAudit } from './hooks/useJobAudit'
import type { AgentHistory, AgentLearning, AgentPolicy, AgentTraceStep, AiProvider, Answer, AuditTab, Checkpoint, EventLog, ExecutionTarget, ExtensionStatus, HealthSignal, JobAudit, JobFilter, JobLead, JobTargetDetail, OperationalHealth, PlatformCredential, Profile, ProfileValidationIssue, QuestionPattern, ResumeDocumentSummary, ResumeImportResult, ResumeLayoutOption, ResumeSection, ResumeSectionDiff, Run, SavedSearch, TailoredResume } from './types'
import './index.css'


const emptyProfile: Profile = {
  fullName: '', cpf: '', email: '', phone: '', city: '', country: 'Brazil', linkedinUrl: '', githubUrl: '', portfolioUrl: '',
  professionalSummary: '', skills: [], targetRole: 'ASP.NET Developer', seniority: 'junior', salaryExpectation: '', resumePath: '',
}
const emptyProvider = { name: 'Ollama Local', providerType: 'ollama', baseUrl: 'http://localhost:11434', model: 'qwen3:4b-instruct', apiFormat: 'ollama-chat', apiKey: '', isActive: true, isDefault: true }
const emptyExtensionStatus: ExtensionStatus = { status: 'not_paired', currentUrl: '', isSupportedPage: false, browser: '', extensionVersion: '', lastError: '' }
const emptyOperationalHealth: OperationalHealth = { status: 'unknown', checkedAt: '', signals: [], metrics: { queuedApplications: 0, openCheckpoints: 0, runningAgentRuns: 0, pendingExtensionCommands: 0, staleExtensionCommands: 0, staleAgentRuns: 0, staleApplicationJobs: 0, dailyLimit: 0, appliedToday: 0, remainingToday: 0 } }
const fallbackResumeLayouts: ResumeLayoutOption[] = [
  { key: 'ats_reverse_chronological', label: 'ATS cronologico reverso', description: 'Modelo ATS geral recomendado para gerar curriculo sem vaga especifica.', atsNotes: 'Uma coluna, secoes claras e sem tabelas, imagens ou icones.', recommendedForAts: true },
  { key: 'ats_classic', label: 'ATS Classic', description: 'Modelo simples para tailoring por vaga.', atsNotes: 'Uma coluna e headings previsiveis.', recommendedForAts: true },
  { key: 'ats_compact', label: 'ATS Compact', description: 'Modelo mais curto para conteudo enxuto.', atsNotes: 'Evita colunas e elementos visuais pesados.', recommendedForAts: true },
  { key: 'ats_modern_minimal', label: 'ATS Modern Minimal', description: 'Modelo limpo com acabamento discreto.', atsNotes: 'Mantem estrutura textual simples.', recommendedForAts: true },
  { key: 'human_review', label: 'Human Review', description: 'Modelo visual para revisao humana.', atsNotes: 'Use quando a apresentacao visual for prioridade.', recommendedForAts: false },
]
const defaultJobFilterConfig: JobFilter = { id: '', allowedSeniorities: ['junior'], blockedSeniorities: ['senior', 'sr.', 'staff', 'principal', 'lead', 'manager'], allowedRoleKeywords: ['asp.net', '.net', 'c#', 'full stack', 'fullstack', 'qa', 'tester', 'quality assurance', 'arquiteto asp.net', 'asp.net architect'], blockedRoleKeywords: ['suporte', 'support', 'infra', 'sre', 'devops', 'security', 'seguranca', 'data engineer', 'dados'], allowedWorkplaceTypes: ['remote'], blockedWorkplaceTypes: ['hybrid', 'onsite'], allowedApplyTypes: ['linkedin_easy_apply', 'inhire'], minScore: 30, autoIgnoreLowScore: true }

type Metrics = { jobs: number; shortlisted: number; queued: number; checkpoints: number; applied: number }
type DraftSearch = { name: string; keywords: string; location: string }
type DraftAnswer = { questionKey: string; questionText: string; answerValue: string; category: string }
type DraftProvider = Omit<AiProvider, 'id' | 'hasApiKey' | 'updatedAt'> & { apiKey: string }
type DraftPattern = { patternKey: string; category: string; tags: string; questionTemplates: string; answerTemplate: string; valueSource: string; profileFieldMapping: string; confidenceThreshold: number }
type PatternTest = { question: string; result: string }
type DraftPlatformCredential = { platform: string; accountEmail: string; password: string; isActive: boolean }
const requiredExtensionVersion = '0.1.4'
const defaultExecutionTarget: ExecutionTarget = 'extension'

function responseMessage(error: unknown, fallback: string) {
  if (typeof error === 'object' && error && 'response' in error) {
    const response = (error as { response?: { data?: { message?: string } } }).response
    return response?.data?.message ?? fallback
  }
  return fallback
}

function extensionVersionReady(version: string) {
  const current = versionParts(version)
  const required = versionParts(requiredExtensionVersion)
  for (let index = 0; index < Math.max(current.length, required.length); index += 1) {
    const currentPart = current[index] ?? 0
    const requiredPart = required[index] ?? 0
    if (currentPart > requiredPart) return true
    if (currentPart < requiredPart) return false
  }
  return true
}

function versionParts(version: string) {
  return version.split('.').map((part) => Number.parseInt(part, 10)).map((part) => Number.isFinite(part) ? part : 0)
}

function normalizeExecutionTarget(value?: string): ExecutionTarget {
  const normalized = (value ?? '').trim().toLowerCase().replaceAll('-', '_')
  if (normalized === 'local_dedicated_browser' || normalized === 'playwright' || normalized === 'local_browser') return 'local_dedicated_browser'
  if (normalized === 'remote_browser' || normalized === 'remote') return 'remote_browser'
  if (normalized === 'server_connector' || normalized === 'server') return 'server_connector'
  return 'extension'
}

function executionTargetLabel(target?: string) {
  const normalized = normalizeExecutionTarget(target)
  return ({
    extension: 'Extensao',
    local_dedicated_browser: 'Navegador local',
    remote_browser: 'Worker remoto',
    server_connector: 'Conector server-side',
  } as Record<ExecutionTarget, string>)[normalized]
}

/* ── App ── */
function App() {
  const [token, setToken] = useState(localStorage.getItem('jobpilot_token') ?? '')
  const [email, setEmail] = useState('admin@jobpilot.local')
  const [password, setPassword] = useState('admin123')
  const [showPassword, setShowPassword] = useState(false)
  const [loginError, setLoginError] = useState('')
  const [tab, setTab] = useState('dashboard')
  const [profile, setProfile] = useState<Profile>(emptyProfile)
  const [searches, setSearches] = useState<SavedSearch[]>([])
  const [jobs, setJobs] = useState<JobLead[]>([])
  const [answers, setAnswers] = useState<Answer[]>([])
  const [runs, setRuns] = useState<Run[]>([])
  const [checkpoints, setCheckpoints] = useState<Checkpoint[]>([])
  const [events, setEvents] = useState<EventLog[]>([])
  const [providers, setProviders] = useState<AiProvider[]>([])
  const [policy, setPolicy] = useState<AgentPolicy>({ autoSubmitMode: 'conservative', executionTarget: defaultExecutionTarget, maxApplicationsPerRun: 10, maxApplicationsPerDay: 40, allowedDomains: [], blockedDomains: [], isPaused: false })
  const [extensionStatus, setExtensionStatus] = useState<ExtensionStatus>(emptyExtensionStatus)
  const [operationalHealth, setOperationalHealth] = useState<OperationalHealth>(emptyOperationalHealth)
  const [agentHistory, setAgentHistory] = useState<AgentHistory>({ runs: [], steps: [], fields: [], answers: [], memories: [] })
  const [learnings, setLearnings] = useState<AgentLearning[]>([])
  const [learningFilter, setLearningFilter] = useState('all')
  const [learningTypeFilter, setLearningTypeFilter] = useState('all')
  const [learningRiskFilter, setLearningRiskFilter] = useState('all')
  const [learningDomainFilter, setLearningDomainFilter] = useState('')
  const [activeLearningId, setActiveLearningId] = useState('')
  const [selectedJobs, setSelectedJobs] = useState<string[]>([])
  const [activeJobId, setActiveJobId] = useState('')
  const [jobFilter, setJobFilter] = useState('all')
  const [agentToken, setAgentToken] = useState('')
  const [aiBusy, setAiBusy] = useState(false)
  const [automationBusy, setAutomationBusy] = useState('')
  const [maintenanceBusy, setMaintenanceBusy] = useState(false)
  const [automationNotice, setAutomationNotice] = useState('')
  const [profileImportBusy, setProfileImportBusy] = useState(false)
  const [profileImportResult, setProfileImportResult] = useState<ResumeImportResult | null>(null)
  const [selectedResumeFile, setSelectedResumeFile] = useState<File | null>(null)
  const [resumeDocuments, setResumeDocuments] = useState<ResumeDocumentSummary[]>([])
  const [selectedResumeVersionId, setSelectedResumeVersionId] = useState('')
  const [atsJobDescription, setAtsJobDescription] = useState('')
  const [atsJobUrl, setAtsJobUrl] = useState('')
  const [atsScreenshotFile, setAtsScreenshotFile] = useState<File | null>(null)
  const [atsResumeBusy, setAtsResumeBusy] = useState(false)
  const [atsTailoredResume, setAtsTailoredResume] = useState<TailoredResume | null>(null)
  const [atsJobTarget, setAtsJobTarget] = useState<JobTargetDetail | null>(null)
  const [atsExportBusy, setAtsExportBusy] = useState(false)
  const [atsResumeNotice, setAtsResumeNotice] = useState('')
  const [resumeLayouts, setResumeLayouts] = useState<ResumeLayoutOption[]>(fallbackResumeLayouts)
  const [selectedLayout, setSelectedLayout] = useState('ats_reverse_chronological')
  const [reviewAnswers, setReviewAnswers] = useState<Record<string, { value: string; type: string; evidence: string }>>({})
  const [providerStatus, setProviderStatus] = useState('')
  const [draftSearch, setDraftSearch] = useState({ name: '.NET', keywords: '.NET', location: 'Brazil' })
  const [draftAnswer, setDraftAnswer] = useState({ questionKey: 'salary', questionText: 'Pretensão salarial', answerValue: '', category: 'screening' })
  const [draftProvider, setDraftProvider] = useState<DraftProvider>(emptyProvider)
  const [patterns, setPatterns] = useState<QuestionPattern[]>([])
  const [jobFilterConfig, setJobFilterConfig] = useState<JobFilter>(defaultJobFilterConfig)
  const [validationIssues, setValidationIssues] = useState<ProfileValidationIssue[]>([])
  const [draftPattern, setDraftPattern] = useState({ patternKey: '', category: 'screening', tags: '', questionTemplates: '', answerTemplate: '', valueSource: 'profile_field', profileFieldMapping: '', confidenceThreshold: 0.75 })
  const [patternTest, setPatternTest] = useState({ question: '', result: '' })
  const [agentTab, setAgentTab] = useState<'events' | 'history'>('events')
  const [editingProviderId, setEditingProviderId] = useState('')
  const [editingAnswerId, setEditingAnswerId] = useState('')
  const [editingPatternId, setEditingPatternId] = useState('')
  const [editingSearchId, setEditingSearchId] = useState('')
  const [platformCreds, setPlatformCreds] = useState<PlatformCredential[]>([])
  const [draftPlatformCred, setDraftPlatformCred] = useState({ platform: 'linkedin', accountEmail: '', password: '', isActive: true })
  const [editingPlatformCredId, setEditingPlatformCredId] = useState('')
  const { jobAudit, auditTab, setAuditTab } = useJobAudit(token, activeJobId)

  async function login() {
    setLoginError('')
    try {
      const { data } = await api.post('/auth/login', { email, password })
      localStorage.setItem('jobpilot_token', data.token)
      setToken(data.token)
    } catch (error: unknown) {
      setLoginError(responseMessage(error, 'API local indisponivel. Inicie o JobPilot local e tente novamente.'))
    }
  }
  function logout() { localStorage.removeItem('jobpilot_token'); setToken('') }

  const refresh = useCallback(async () => {
    try {
      const [profileRes, searchesRes, jobsRes, answersRes, runsRes, checkpointsRes, eventsRes, providersRes, policyRes, historyRes, patternsRes, filterRes, validationRes, platformCredsRes, learningsRes, extensionRes, healthRes, resumeDocsRes, resumeLayoutsRes] = await Promise.all([
        profileClient.get(), jobsClient.searches(), jobsClient.jobs(), api.get('/answers'), jobsClient.runs(), jobsClient.checkpoints(),
        jobsClient.events(), api.get('/ai/providers'), agentClient.policy(), agentClient.history(),
        api.get('/question-patterns'), api.get('/job-filters'), profileClient.validate(),
        api.get('/platform-credentials'), agentClient.learnings(), agentClient.extensionStatus(), agentClient.health().catch(() => ({ data: emptyOperationalHealth })), resumeClient.documents(), resumeClient.layouts().catch(() => ({ data: fallbackResumeLayouts })),
      ])
      setProfile({ ...emptyProfile, ...profileRes.data })
      setSearches(searchesRes.data); setJobs(jobsRes.data); setAnswers(answersRes.data); setRuns(runsRes.data)
      setCheckpoints(checkpointsRes.data); setEvents(eventsRes.data); setProviders(providersRes.data)
      setAgentHistory(historyRes.data); setPatterns(patternsRes.data ?? [])
      setJobFilterConfig(filterRes.data ?? defaultJobFilterConfig)
      setValidationIssues(validationRes.data?.issues ?? [])
      setPlatformCreds(platformCredsRes.data ?? [])
      setLearnings(learningsRes.data ?? [])
      setExtensionStatus(extensionRes.data ?? emptyExtensionStatus)
      setOperationalHealth(healthRes.data ?? emptyOperationalHealth)
      const nextResumeDocuments: ResumeDocumentSummary[] = resumeDocsRes.data ?? []
      setResumeDocuments(nextResumeDocuments)
      setSelectedResumeVersionId((current) => {
        if (current) return current
        const latestVersion = nextResumeDocuments.flatMap((document) => document.versions).sort((left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt))[0]
        return latestVersion?.id ?? current
      })
      setResumeLayouts(resumeLayoutsRes.data?.length ? resumeLayoutsRes.data : fallbackResumeLayouts)
      setPolicy({ autoSubmitMode: policyRes.data.autoSubmitMode ?? 'conservative', executionTarget: normalizeExecutionTarget(policyRes.data.executionTarget), maxApplicationsPerRun: policyRes.data.maxApplicationsPerRun ?? 10, maxApplicationsPerDay: policyRes.data.maxApplicationsPerDay ?? 40, allowedDomains: policyRes.data.allowedDomains ?? [], blockedDomains: policyRes.data.blockedDomains ?? [], isPaused: policyRes.data.isPaused ?? false })
    } catch (error: unknown) {
      setAutomationNotice(responseMessage(error, 'API local indisponivel. Abra o JobPilot local para sincronizar dados, extensao e fila.'))
    }
  }, [])

  useEffect(() => {
    api.defaults.headers.common.Authorization = token ? `Bearer ${token}` : ''
    if (token) queueMicrotask(() => void refresh())
  }, [refresh, token])

  async function saveProfile() { await profileClient.save(profile); await refresh() }
  async function importResumeProfile() {
    if (!profile.resumePath) return
    setProfileImportBusy(true)
    setProfileImportResult(null)
    try {
      const { data } = await profileClient.importResume({ resumePath: profile.resumePath, fetchPublicLinks: true, useAi: true })
      setProfileImportResult(data)
      setProfile({ ...emptyProfile, ...data.profile })
      await refresh()
    } finally {
      setProfileImportBusy(false)
    }
  }
  async function importResumeFile() {
    if (!selectedResumeFile) return
    setProfileImportBusy(true)
    setProfileImportResult(null)
    try {
      const payload = new FormData()
      payload.append('file', selectedResumeFile)
      const { data } = await profileClient.importResumeFile(payload)
      setProfileImportResult(data)
      setProfile({ ...emptyProfile, ...data.profile })
      setSelectedResumeFile(null)
      await refresh()
    } finally {
      setProfileImportBusy(false)
    }
  }
  async function buildAtsResume() {
    setAtsResumeBusy(true)
    setAtsResumeNotice('')
    try {
      const versionId = selectedResumeVersionId || resumeDocuments.flatMap((document) => document.versions).sort((left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt))[0]?.id
      if (!versionId) {
        setAtsResumeNotice('Importe um curriculo no Perfil antes de gerar um curriculo ATS.')
        return
      }
      const hasUrl = atsJobUrl.trim().length > 0
      const hasJobInput = atsJobDescription.trim().length > 0 || hasUrl || Boolean(atsScreenshotFile)
      if (!hasJobInput) {
        const { data } = await resumeClient.buildAts({
          baseResumeVersionId: versionId,
          layout: selectedLayout,
          useAi: false,
        })
        setAtsJobTarget(null)
        setAtsTailoredResume(data)
        await refresh()
        return
      }
      const organized = atsScreenshotFile
        ? await jobTargetClient.organizeScreenshot((() => {
            const payload = new FormData()
            payload.append('file', atsScreenshotFile)
            return payload
          })())
        : await jobTargetClient.organize({
            sourceKind: hasUrl ? 'url' : 'manual_text',
            text: atsJobDescription,
            url: hasUrl ? atsJobUrl : '',
            useAi: true,
          })
      const tailored = await resumeClient.tailor({
        baseResumeVersionId: versionId,
        jobTargetId: organized.data.id,
        layout: selectedLayout,
        useAi: true,
      })
      setAtsJobTarget(organized.data)
      setAtsTailoredResume(tailored.data)
      setAtsScreenshotFile(null)
      await refresh()
    } catch (error: unknown) {
      setAtsResumeNotice(responseMessage(error, 'Nao foi possivel gerar o curriculo ATS agora.'))
    } finally {
      setAtsResumeBusy(false)
    }
  }
  async function exportAtsResume(format: 'txt' | 'docx' | 'pdf') {
    if (!atsTailoredResume) return
    setAtsExportBusy(true)
    setAtsResumeNotice('')
    try {
      const { data } = await resumeClient.exportVersion(atsTailoredResume.tailoredVersionId, { format, layout: selectedLayout })
      const download = await resumeClient.downloadExport(data.id)
      const url = URL.createObjectURL(download.data)
      const link = document.createElement('a')
      link.href = url
      link.download = `jobpilot-curriculo-${atsTailoredResume.tailoredVersionId}.${format}`
      document.body.appendChild(link)
      link.click()
      link.remove()
      URL.revokeObjectURL(url)
    } catch (error: unknown) {
      setAtsResumeNotice(responseMessage(error, `Nao foi possivel exportar o curriculo ${format.toUpperCase()}.`))
    } finally {
      setAtsExportBusy(false)
    }
  }
  async function saveReviewAnswer(reqId: string, label: string, question: string, value: string, type: string) {
    try {
      await resumeClient.saveReviewAnswer({
        evidenceMapRunId: atsTailoredResume?.evidenceMapRunId,
        jobRequirementId: reqId,
        requirementLabel: label,
        questionText: question,
        answerValue: value,
        answerType: type,
      })
      setReviewAnswers(prev => ({ ...prev, [reqId]: { value, type, evidence: '' } }))
      setAtsResumeNotice('Resposta salva. Clique em "Regenerar com respostas" para aplicar.')
    } catch (error: unknown) {
      setAtsResumeNotice(responseMessage(error, 'Nao foi possivel salvar a resposta.'))
    }
  }
  async function regenerateAfterAnswers() {
    if (!atsTailoredResume) return
    setAtsResumeBusy(true)
    setAtsResumeNotice('')
    try {
      const answerIds = Object.entries(reviewAnswers)
        .filter(([, v]) => v.value.trim().length > 0)
        .map(([id]) => id)
      if (answerIds.length === 0) {
        setAtsResumeNotice('Responda pelo menos uma pergunta antes de regenerar.')
        return
      }
      const { data } = await resumeClient.regenerateAfterAnswers({
        answerIds,
        baseResumeVersionId: atsTailoredResume.baseResumeVersionId,
        jobTargetId: atsTailoredResume.jobTargetId,
        layout: selectedLayout,
        useAi: true,
      })
      setAtsTailoredResume(prev => prev ? { ...prev, tailoredVersionId: data.newTailoredVersionId, status: data.status, atsScore: data.atsScore } : null)
      setAtsResumeNotice('Curriculo regenerado com respostas aplicadas.')
      await refresh()
    } catch (error: unknown) {
      setAtsResumeNotice(responseMessage(error, 'Nao foi possivel regenerar o curriculo.'))
    } finally {
      setAtsResumeBusy(false)
    }
  }
  async function updateTailoredSection(sectionId: string, contentJson: string) {
    try {
      await resumeClient.updateSection(sectionId, { contentJson })
      setAtsTailoredResume(prev => {
        if (!prev) return prev
        const newDiffs = prev.sectionDiffs.map(d =>
          d.afterSectionId === sectionId ? { ...d, afterPreview: contentJson.slice(0, 200), status: 'changed' as const } : d
        )
        return { ...prev, sectionDiffs: newDiffs }
      })
      setAtsResumeNotice('Secao atualizada.')
    } catch (error: unknown) {
      setAtsResumeNotice(responseMessage(error, 'Erro ao atualizar secao.'))
    }
  }
  async function savePlatformCred() {
    if (!draftPlatformCred.accountEmail) return
    if (editingPlatformCredId) {
      await api.put(`/platform-credentials/${editingPlatformCredId}`, draftPlatformCred)
      setEditingPlatformCredId('')
    } else {
      await api.post('/platform-credentials', draftPlatformCred)
    }
    setDraftPlatformCred({ platform: 'linkedin', accountEmail: '', password: '', isActive: true })
    await refresh()
  }
  async function deletePlatformCred(id: string) { await api.delete(`/platform-credentials/${id}`); await refresh() }
  async function createSearch() { if (editingSearchId) { await api.put(`/searches/${editingSearchId}`, { ...draftSearch, workplace: 'remote', seniority: 'junior', postedWithinDays: 7, limitPerRun: 25, isActive: true }); setEditingSearchId('') } else { await api.post('/searches', { ...draftSearch, workplace: 'remote', seniority: 'junior', postedWithinDays: 7, limitPerRun: 25, isActive: true }) }; setDraftSearch({ name: '.NET', keywords: '.NET', location: 'Brazil' }); await refresh() }
  async function deleteSearch(id: string) { await api.delete(`/searches/${id}`); await refresh() }
  async function saveAnswer() { if (!draftAnswer.questionKey || !draftAnswer.answerValue) return; if (editingAnswerId) { await api.put(`/answers/${editingAnswerId}`, draftAnswer); setEditingAnswerId('') } else { await api.post('/answers', draftAnswer) }; setDraftAnswer({ questionKey: '', questionText: '', answerValue: '', category: 'screening' }); await refresh() }
  async function deleteAnswer(id: string) { await api.delete(`/answers/${id}`); await refresh() }
  async function draftWithAi() {
    if (!draftAnswer.questionText) return; setAiBusy(true)
    try { const job = jobs.find((item) => item.id === activeJobId); const { data } = await api.post('/ai/answer-draft', { questionText: draftAnswer.questionText, jobContext: job ? `${job.title} na ${job.company}. ${job.description || job.scoreReason}` : '' }); setDraftAnswer((c) => ({ ...c, answerValue: data.text })) } finally { setAiBusy(false) }
  }
  async function updateJobStatus(id: string, status: string) { await jobsClient.updateStatus(id, status); await refresh() }
  async function assignResumeToJob(jobId: string, resumeVersionId: string) {
    if (!resumeVersionId) return
    await jobsClient.attachResume(jobId, resumeVersionId)
    await refresh()
  }
  async function createRun() { if (selectedJobs.length === 0) return; await jobsClient.createRun({ name: `Execução ${new Date().toLocaleString()}`, jobLeadIds: selectedJobs }); setSelectedJobs([]); await refresh() }
  async function pairAgent() { const { data } = await agentClient.pair({ name: navigator.userAgent.includes('Windows') ? 'Windows local agent' : 'Local agent' }); setAgentToken(data.token) }
  async function resolveCheckpoint(id: string, action = 'manual_done', value = 'Feito pelo usuario no navegador') { await jobsClient.resolveCheckpoint(id, { action, value }); await refresh() }
  async function startDiscover() {
    setAutomationBusy('discover')
    setAutomationNotice('')
    try {
      const { data } = await agentClient.discover({ keywords: draftSearch.keywords, location: draftSearch.location, limit: 75 })
      const prefix = data.alreadyRunning ? 'Busca continua ja esta rodando' : data.continuous ? 'Busca continua iniciada' : 'Busca iniciada'
      setAutomationNotice(`${prefix} para "${draftSearch.keywords}". Acompanhe em Eventos${data.logPath ? `; log: ${data.logPath}` : ''}.`)
      await refresh()
    } catch (error: unknown) {
      setAutomationNotice(responseMessage(error, 'Nao foi possivel iniciar a busca.'))
    } finally { setAutomationBusy('') }
  }
  async function startAutopilot() {
    setAutomationBusy('apply')
    setAutomationNotice('')
    try {
      const { data } = await agentClient.apply({ autoSubmit: policy.autoSubmitMode !== 'review_only', maxApplications: policy.maxApplicationsPerRun })
      if (data.started) {
        const queued = Number(data.queuedNow ?? 0)
        setAutomationNotice(`Piloto automatico iniciado${queued > 0 ? ` com ${queued} vaga(s) nova(s) na fila` : ''}. Acompanhe em Eventos${data.logPath ? `; log: ${data.logPath}` : ''}.`)
      } else {
        setAutomationNotice(data.message ?? 'Nenhuma vaga elegivel esta na fila agora. Execute uma busca ou revise os filtros.')
      }
      await refresh()
    } catch (error: unknown) {
      setAutomationNotice(responseMessage(error, 'Nao foi possivel iniciar o piloto automatico.'))
    } finally { setAutomationBusy('') }
  }
  async function recoverMaintenanceNow() {
    setMaintenanceBusy(true)
    setAutomationNotice('')
    try {
      const { data } = await agentClient.recoverHealth()
      const recovered = Number(data.staleAgentRuns ?? 0) + Number(data.staleApplicationJobs ?? 0)
      setAutomationNotice(recovered > 0
        ? `Manutencao executada: ${recovered} item(ns) recuperado(s) para revisao/auditoria.`
        : 'Manutencao executada: nenhum trabalho preso encontrado.')
      await refresh()
    } catch (error: unknown) {
      setAutomationNotice(responseMessage(error, 'Nao foi possivel executar a manutencao agora.'))
    } finally {
      setMaintenanceBusy(false)
    }
  }
  async function saveProvider() { if (!draftProvider.name || !draftProvider.model) return; if (editingProviderId) { await api.put(`/ai/providers/${editingProviderId}`, draftProvider); setEditingProviderId('') } else { await api.post('/ai/providers', draftProvider) }; setDraftProvider({ ...emptyProvider, apiKey: '' }); await refresh() }
  async function deleteProvider(id: string) { await api.delete(`/ai/providers/${id}`); await refresh() }
  async function makeDefaultProvider(provider: AiProvider) { await api.put(`/ai/providers/${provider.id}`, { ...provider, apiKey: null, isDefault: true, isActive: true }); await refresh() }
  async function testProvider(id: string) { setProviderStatus('Testando...'); try { const { data } = await api.post(`/ai/providers/${id}/test`); setProviderStatus(data.ok ? `Provedor OK: ${data.model}` : `Falhou: ${data.sample ?? 'sem resposta'}`) } catch { setProviderStatus('Teste falhou') } }
  async function savePolicy() { await agentClient.savePolicy(policy); await refresh() }
  async function saveJobFilter() { await api.put('/job-filters', jobFilterConfig); await refresh() }
  async function savePattern() {
    if (!draftPattern.patternKey || !draftPattern.answerTemplate) return
    const payload = { ...draftPattern, tags: draftPattern.tags.split(',').map((x) => x.trim()).filter(Boolean), questionTemplates: draftPattern.questionTemplates.split('\n').map((x) => x.trim()).filter(Boolean), confidenceThreshold: draftPattern.confidenceThreshold }
    if (editingPatternId) { await api.put(`/question-patterns/${editingPatternId}`, payload); setEditingPatternId('') } else { await api.post('/question-patterns', payload) }
    setDraftPattern({ patternKey: '', category: 'screening', tags: '', questionTemplates: '', answerTemplate: '', valueSource: 'profile_field', profileFieldMapping: '', confidenceThreshold: 0.75 })
    await refresh()
  }
  async function deletePattern(id: string) { await api.delete(`/question-patterns/${id}`); await refresh() }
  async function testPattern() { if (!patternTest.question) return; try { const { data } = await api.post('/question-patterns/resolve', { questionText: patternTest.question, jobContext: '' }); setPatternTest((p) => ({ ...p, result: `${data.source} | conf: ${Number(data.confidence).toFixed(2)}\n${data.answerValue || data.reason}` })) } catch { setPatternTest((p) => ({ ...p, result: 'Erro ao testar' })) } }
  async function reviewLearning(id: string, action: 'approve' | 'reject' | 'disable') { await agentClient.reviewLearning(id, action); await refresh() }
  const filteredJobs = useMemo(() => jobs.filter((job) => jobFilter === 'all' || job.status === jobFilter), [jobs, jobFilter])
  const filteredLearnings = useMemo(() => learnings.filter((learning) => {
    const statusOk = learningFilter === 'all' || learning.status === learningFilter
    const typeOk = learningTypeFilter === 'all' || learning.learningType === learningTypeFilter
    const riskOk = learningRiskFilter === 'all' || learning.riskLevel === learningRiskFilter
    const domainOk = !learningDomainFilter.trim() || learning.domain.toLowerCase().includes(learningDomainFilter.trim().toLowerCase())
    return statusOk && typeOk && riskOk && domainOk
  }), [learnings, learningFilter, learningTypeFilter, learningRiskFilter, learningDomainFilter])
  const activeJob = jobs.find((job) => job.id === activeJobId) ?? filteredJobs[0]

  const metrics = useMemo(() => ({
    jobs: jobs.length, shortlisted: jobs.filter((j) => j.status === 'shortlisted').length,
    queued: jobs.filter((j) => j.status === 'queued_to_apply').length, checkpoints: checkpoints.length,
    applied: jobs.filter((j) => j.status === 'applied').length,
  }), [jobs, checkpoints])

  /* ── Login ── */
  if (!token) {
    return (
      <main className="login-shell">
        <section className="login-card">
          <div className="login-brand">
            <div className="brand-icon"><Bot size={24} /></div>
            <h1>JobPilot</h1>
          </div>
          <p className="subtitle">Assistente local para descoberta de vagas e candidaturas automatizadas no LinkedIn.</p>
          <label>E-mail<input value={email} onChange={(e) => setEmail(e.target.value)} /></label>
          <label>Senha
            <div style={{ position: 'relative' }}>
              <input type={showPassword ? 'text' : 'password'} value={password} onChange={(e) => setPassword(e.target.value)} style={{ paddingRight: 40 }} />
              <button onClick={() => setShowPassword(!showPassword)} style={{ position: 'absolute', right: 8, top: '50%', transform: 'translateY(-50%)', background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer' }}>
                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </div>
          </label>
          {loginError && <div className="notice error">{loginError}</div>}
          <button className="btn-primary" onClick={login}><KeyRound size={16} /> Entrar</button>
        </section>
      </main>
    )
  }

  /* ── Main app ── */
  return (
    <main className="app-shell">
      <Sidebar tab={tab} setTab={setTab} onLogout={logout} />
      <section className="workspace">
        <Topbar title={titleFor(tab)} onRefresh={refresh} onLogout={logout} />

        {tab === 'dashboard' && (
          <DashboardTab
            metrics={metrics}
            jobs={jobs.slice(0, 6)}
            checkpoints={checkpoints.slice(0, 5)}
            activeJobId={activeJobId}
            setActiveJobId={setActiveJobId}
            draftSearch={draftSearch}
            setDraftSearch={setDraftSearch}
            onDiscover={startDiscover}
            onAutopilot={startAutopilot}
            automationBusy={automationBusy}
            automationNotice={automationNotice}
            policy={policy}
            validationIssues={validationIssues}
            extensionStatus={extensionStatus}
            operationalHealth={operationalHealth}
            setTab={setTab}
            onResolveCheckpoint={resolveCheckpoint}
            onRecoverMaintenance={recoverMaintenanceNow}
            maintenanceBusy={maintenanceBusy}
          />
        )}
        {tab === 'profile' && <ProfileTab profile={profile} setProfile={setProfile} onSave={saveProfile} onImportResume={importResumeProfile} onImportResumeFile={importResumeFile} selectedResumeFile={selectedResumeFile} setSelectedResumeFile={setSelectedResumeFile} importBusy={profileImportBusy} importResult={profileImportResult} validationIssues={validationIssues} />}
        {tab === 'resume' && <ResumeAtsTab profile={profile} documents={resumeDocuments} selectedVersionId={selectedResumeVersionId} setSelectedVersionId={setSelectedResumeVersionId} jobDescription={atsJobDescription} setJobDescription={setAtsJobDescription} jobUrl={atsJobUrl} setJobUrl={setAtsJobUrl} screenshotFile={atsScreenshotFile} setScreenshotFile={setAtsScreenshotFile} draft={atsTailoredResume} jobTarget={atsJobTarget} busy={atsResumeBusy} exportBusy={atsExportBusy} notice={atsResumeNotice} onBuild={buildAtsResume} onExport={exportAtsResume} setTab={setTab} layoutOptions={resumeLayouts} selectedLayout={selectedLayout} setSelectedLayout={setSelectedLayout} onSaveReviewAnswer={saveReviewAnswer} onRegenerateAfterAnswers={regenerateAfterAnswers} onUpdateTailoredSection={updateTailoredSection} />}
        {tab === 'searches' && <SearchesTab searches={searches} draftSearch={draftSearch} setDraftSearch={setDraftSearch} onCreate={createSearch} onDelete={deleteSearch} editingSearchId={editingSearchId} setEditingSearchId={setEditingSearchId} />}
        {tab === 'jobs' && (
          <JobsTab
            jobs={filteredJobs}
            documents={resumeDocuments}
            selectedJobs={selectedJobs}
            setSelectedJobs={setSelectedJobs}
            updateJobStatus={updateJobStatus}
            activeJob={activeJob}
            setActiveJobId={setActiveJobId}
            jobFilter={jobFilter}
            setJobFilter={setJobFilter}
            onQueue={createRun}
            jobAudit={jobAudit}
            auditTab={auditTab}
            setAuditTab={setAuditTab}
            onAssignResume={assignResumeToJob}
          />
        )}
        {tab === 'runs' && <RunsTab runs={runs} />}
        {tab === 'answers' && <AnswersTab answers={answers} draftAnswer={draftAnswer} setDraftAnswer={setDraftAnswer} onSave={saveAnswer} onDelete={deleteAnswer} onDraftAi={draftWithAi} aiBusy={aiBusy} editingAnswerId={editingAnswerId} setEditingAnswerId={setEditingAnswerId} />}
        {tab === 'filters' && <FiltersTab jobFilterConfig={jobFilterConfig} setJobFilterConfig={setJobFilterConfig} validationIssues={validationIssues} patterns={patterns} draftPattern={draftPattern} setDraftPattern={setDraftPattern} onSaveFilter={saveJobFilter} onSavePattern={savePattern} onDeletePattern={deletePattern} onTestPattern={testPattern} patternTest={patternTest} setPatternTest={setPatternTest} onRevalidate={refresh} editingPatternId={editingPatternId} setEditingPatternId={setEditingPatternId} />}
        {tab === 'ai' && <AiTab providers={providers} draftProvider={draftProvider} setDraftProvider={setDraftProvider} policy={policy} setPolicy={setPolicy} onSaveProvider={saveProvider} onDeleteProvider={deleteProvider} onMakeDefault={makeDefaultProvider} onTestProvider={testProvider} providerStatus={providerStatus} onSavePolicy={savePolicy} editingProviderId={editingProviderId} setEditingProviderId={setEditingProviderId} />}
        {tab === 'credentials' && <CredentialsTab platformCreds={platformCreds} draft={draftPlatformCred} setDraft={setDraftPlatformCred} onSave={savePlatformCred} onDelete={deletePlatformCred} editingId={editingPlatformCredId} setEditingId={setEditingPlatformCredId} />}
        {tab === 'agent' && <AgentTab events={events} checkpoints={checkpoints} agentHistory={agentHistory} agentToken={agentToken} onPair={pairAgent} onDiscover={startDiscover} onAutopilot={startAutopilot} onResolveCheckpoint={resolveCheckpoint} automationBusy={automationBusy} automationNotice={automationNotice} agentTab={agentTab} setAgentTab={setAgentTab} />}
        {tab === 'learnings' && <LearningsTab learnings={filteredLearnings} allLearnings={learnings} filter={learningFilter} setFilter={setLearningFilter} typeFilter={learningTypeFilter} setTypeFilter={setLearningTypeFilter} riskFilter={learningRiskFilter} setRiskFilter={setLearningRiskFilter} domainFilter={learningDomainFilter} setDomainFilter={setLearningDomainFilter} activeId={activeLearningId} setActiveId={setActiveLearningId} onReview={reviewLearning} />}
      </section>
    </main>
  )
}

/* ── Sidebar ── */
function DashboardTab({ metrics, jobs, checkpoints, activeJobId, setActiveJobId, draftSearch, setDraftSearch, onDiscover, onAutopilot, automationBusy, automationNotice, policy, validationIssues, extensionStatus, operationalHealth, setTab, onResolveCheckpoint, onRecoverMaintenance, maintenanceBusy }: {
  metrics: Metrics
  jobs: JobLead[]
  checkpoints: Checkpoint[]
  activeJobId: string
  setActiveJobId: (id: string) => void
  draftSearch: DraftSearch
  setDraftSearch: Dispatch<SetStateAction<DraftSearch>>
  onDiscover: () => void
  onAutopilot: () => void
  automationBusy: string
  automationNotice: string
  policy: AgentPolicy
  validationIssues: ProfileValidationIssue[]
  extensionStatus: ExtensionStatus
  operationalHealth: OperationalHealth
  setTab: (tab: string) => void
  onResolveCheckpoint: (id: string) => void
  onRecoverMaintenance: () => void
  maintenanceBusy: boolean
}) {
  const criticalIssues = validationIssues.filter((issue) => issue.isCritical)
  const queuedLabel = metrics.queued === 1 ? '1 vaga pronta' : `${metrics.queued} vagas prontas`
  const isPaused = Boolean(policy?.isPaused)
  const executionTarget = normalizeExecutionTarget(policy?.executionTarget)
  const needsExtension = executionTarget === 'extension'
  const extensionConnected = extensionStatus?.status === 'connected'
  const extensionReady = extensionConnected && extensionVersionReady(extensionStatus?.extensionVersion ?? '')
  const targetImplemented = executionTarget === 'extension' || executionTarget === 'local_dedicated_browser'
  const canRunAutomation = targetImplemented && (!needsExtension || extensionReady) && !isPaused && automationBusy === ''
  const staleMaintenanceItems = (operationalHealth.metrics.staleAgentRuns ?? 0) + (operationalHealth.metrics.staleApplicationJobs ?? 0)
  const checkpointAction = (cp: Checkpoint) => cp.suggestedUserAction || (
    cp.reasonCode === 'resume_upload_requires_manual_selection'
      ? 'Anexe o curriculo no navegador e depois retome.'
      : cp.reasonCode === 'login_or_security'
        ? 'Finalize login, 2FA ou verificacao no navegador.'
        : ''
  )
  const checkpointResumeLabel = (mode?: string) => {
    if (mode === 'retry_action') return 'Retoma tentando a mesma acao'
    if (mode === 'manual_only') return 'Continua manualmente'
    return 'Retoma reobservando a pagina'
  }
  const extensionLabel = extensionReady ? 'Extensao conectada' : extensionConnected ? 'Extensao desatualizada' : extensionStatus?.status === 'stale' ? 'Extensao sem heartbeat' : 'Extensao nao pareada'

  return (
    <div className="page-stack cockpit">
      <section className="cockpit-hero">
        <div className="cockpit-copy">
          <div className="cockpit-status-row">
            <span className={`status-pill ${isPaused ? 'status-pill-amber' : 'status-pill-green'}`}>
              <span className="status-dot" /> {isPaused ? 'Pausado' : 'Pronto para operar'}
            </span>
            <span className="status-pill status-pill-blue">{executionTargetLabel(executionTarget)}</span>
            <span className="status-pill status-pill-slate">LinkedIn primeiro</span>
          </div>
          <h2>Buscar, escolher e aplicar sem caçar botões.</h2>
          <p>
            Comece pela busca, revise a fila e rode o piloto com limites claros. Checkpoints aparecem aqui quando o JobPilot precisa de decisão humana.
          </p>
        </div>

        <div className="launch-panel">
          <div className="launch-panel-header">
            <div>
              <span className="eyebrow-text">Comando principal</span>
              <h3>Nova rodada</h3>
            </div>
            <button className="btn btn-secondary btn-sm" onClick={() => setTab('filters')}><ShieldCheck size={14} /> Regras</button>
          </div>
          <div className="launch-fields">
            <label>
              Palavras-chave
              <input className="form-input" value={draftSearch.keywords} onChange={(e) => setDraftSearch({ ...draftSearch, keywords: e.target.value })} />
            </label>
            <label>
              Local
              <input className="form-input" value={draftSearch.location} onChange={(e) => setDraftSearch({ ...draftSearch, location: e.target.value })} />
            </label>
          </div>
          <div className="launch-actions">
            <button className="btn btn-primary btn-lg" onClick={onDiscover} disabled={!canRunAutomation}>
              <Search size={17} /> {automationBusy === 'discover' ? 'Buscando...' : 'Buscar vagas'}
            </button>
            <button className="btn btn-dark btn-lg" onClick={onAutopilot} disabled={!canRunAutomation || metrics.queued === 0}>
              <Play size={17} /> {automationBusy === 'apply' ? 'Aplicando...' : 'Aplicar na fila'}
            </button>
          </div>
          <div className="launch-foot">
            <span>{queuedLabel}</span>
            <span>Limite: {policy?.maxApplicationsPerRun ?? 0}/execucao</span>
            <span>{policy?.autoSubmitMode === 'review_only' ? 'Somente revisão' : 'Auto-submit controlado'}</span>
            <span>{executionTargetLabel(executionTarget)}</span>
            {needsExtension && <span>{extensionReady ? 'Extensao pronta' : extensionConnected ? 'Recarregue a extensao' : 'Extensao necessaria'}</span>}
          </div>
          {automationNotice && <div className="notice-inline">{automationNotice}</div>}
          {needsExtension && !extensionConnected && (
            <button className="readiness-alert readiness-alert-action" onClick={() => window.open('chrome://extensions', '_blank')}>
              <KeyRound size={15} /> Conecte a extensao do JobPilot para buscar e aplicar.
            </button>
          )}
          {needsExtension && extensionConnected && !extensionReady && (
            <button className="readiness-alert readiness-alert-action" onClick={() => window.open('chrome://extensions', '_blank')}>
              <AlertTriangle size={15} /> Extensao {extensionStatus.extensionVersion || 'desconhecida'} carregada. Recarregue a versao {requiredExtensionVersion}.
            </button>
          )}
          {!targetImplemented && (
            <button className="readiness-alert" onClick={() => setTab('ai')}>
              <AlertTriangle size={15} /> {executionTargetLabel(executionTarget)} ainda nao esta habilitado neste MVP.
            </button>
          )}
          {criticalIssues.length > 0 && (
            <button className="readiness-alert" onClick={() => setTab('profile')}>
              <AlertTriangle size={15} /> {criticalIssues.length} pendência crítica no perfil
            </button>
          )}
        </div>
      </section>

      <section className={`health-strip health-${operationalHealth.status}`}>
        <div className="health-strip-head">
          <div>
            <span className="eyebrow-text">Saude operacional</span>
            <strong>{operationalHealthTitle(operationalHealth.status)}</strong>
          </div>
          <div className="health-strip-actions">
            {staleMaintenanceItems > 0 && (
              <button className="btn btn-secondary btn-sm" onClick={onRecoverMaintenance} disabled={maintenanceBusy}>
                <RefreshCw size={14} /> {maintenanceBusy ? 'Corrigindo...' : 'Recuperar agora'}
              </button>
            )}
            {operationalHealth.checkedAt && <span>{new Date(operationalHealth.checkedAt).toLocaleTimeString()}</span>}
          </div>
        </div>
        <div className="health-signals">
          {operationalHealth.signals.length === 0 ? (
            <span className="health-signal signal-warning">Sem leitura de saude</span>
          ) : operationalHealth.signals.map((signal) => (
            <span key={signal.key} className={`health-signal signal-${signal.status}`} title={signal.detail || signal.label}>
              {healthSignalLabel(signal)}
            </span>
          ))}
        </div>
      </section>

      <section className="extension-strip">
        <div className={`extension-orb ${extensionReady ? 'online' : 'offline'}`}><Bot size={18} /></div>
        <div className="extension-copy">
          <strong>{extensionLabel}</strong>
          <span>
            {extensionConnected && !extensionReady
              ? `Versao atual: ${extensionStatus.extensionVersion || 'desconhecida'}; requerida: ${requiredExtensionVersion}. Recarregue em chrome://extensions.`
              : extensionStatus?.currentUrl
              ? `${extensionStatus.isSupportedPage ? 'Aba suportada' : 'Aba fora do alvo'} · ${extensionStatus.currentUrl}`
              : 'Abra a extensão no Chrome e clique em conectar. O pareamento local é automático.'}
          </span>
        </div>
        <button className="btn btn-secondary btn-sm" onClick={() => window.open('chrome://extensions', '_blank')}><KeyRound size={14} /> Abrir extensões</button>
      </section>

      <div className="metrics-grid">
        <MetricCard icon={<Search size={18} />} label="Vagas encontradas" value={metrics.jobs} tone="teal" />
        <MetricCard icon={<Award size={18} />} label="Favoritas" value={metrics.shortlisted} tone="amber" />
        <MetricCard icon={<Clock size={18} />} label="Na fila" value={metrics.queued} tone="blue" />
        <MetricCard icon={<CheckCircle2 size={18} />} label="Candidaturas" value={metrics.applied} tone="green" />
        <MetricCard icon={<AlertTriangle size={18} />} label="Checkpoints" value={metrics.checkpoints} tone="red" />
      </div>
      <div className="page-grid-2" style={{ alignItems: 'start' }}>
        <div className="card">
          <div className="card-header">
            <h2><BriefcaseBusiness size={16} /> Vagas recentes</h2>
            <button className="btn btn-secondary btn-sm" onClick={() => setTab('jobs')}>Ver fila</button>
          </div>
          <div className="card-body-sm" style={{ padding: 0 }}>
            {jobs.length === 0 ? <div className="empty-state"><Search size={28} /><p>Nenhuma vaga encontrada ainda.</p></div> :
              <table className="data-table">
                <thead><tr><th>Vaga</th><th>Empresa</th><th>Pontos</th><th>Status</th></tr></thead>
                <tbody>
                  {jobs.map((job: JobLead) => (
                    <tr key={job.id} className={activeJobId === job.id ? 'active' : ''} onClick={() => setActiveJobId(job.id)} style={{ cursor: 'pointer' }}>
                      <td><b style={{ fontWeight: 600, color: '#0f172a' }}>{job.title}</b><br /><small style={{ color: '#94a3b8' }}>{job.location} {job.isRemote ? '· Remoto' : ''}</small></td>
                      <td>{job.company || '—'}</td>
                      <td><strong style={{ color: '#0d9488' }}>{job.score}</strong></td>
                      <td><span className={`badge ${statusBadge(job.status)}`}>{jobStatusLabels[job.status] ?? job.status}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            }
          </div>
        </div>
        <div className="card">
          <div className="card-header">
            <h2><CircleAlert size={16} /> Checkpoints recentes</h2>
            <button className="btn btn-secondary btn-sm" onClick={() => setTab('agent')}>Resolver</button>
          </div>
          <div className="card-body-sm">
            {checkpoints.length === 0 ? <div className="empty-state" style={{ padding: 32 }}><CircleAlert size={28} /><p>Nenhum checkpoint pendente.</p></div> :
              checkpoints.map((cp: Checkpoint) => (
                <div className="checkpoint-item" key={cp.id}>
                  <div className="checkpoint-title">{checkpointReasonLabels[cp.reasonCode] ?? cp.reasonCode}</div>
                  <div className="checkpoint-meta">{stageLabels[cp.stage] ?? cp.stage} · {new Date(cp.createdAt).toLocaleString()}</div>
                  {cp.fieldLabel && <div className="checkpoint-desc">{cp.fieldLabel}</div>}
                  {checkpointAction(cp) && <div className="checkpoint-action"><strong>Acao:</strong> {checkpointAction(cp)}</div>}
                  <div className="checkpoint-resume">{checkpointResumeLabel(cp.resumeMode)}</div>
                  <div className="checkpoint-actions">
                    <button className="btn btn-primary btn-sm" onClick={() => onResolveCheckpoint(cp.id)}>
                      <CheckCircle2 size={14} /> Marcar feito
                    </button>
                    <button className="btn btn-secondary btn-sm" onClick={() => setTab('agent')}>
                      Ver detalhes
                    </button>
                  </div>
                </div>
              ))
            }
          </div>
        </div>
      </div>
    </div>
  )
}

/* ── Profile ── */
function ProfileTab({ profile, setProfile, onSave, onImportResume, onImportResumeFile, selectedResumeFile, setSelectedResumeFile, importBusy, importResult, validationIssues }: {
  profile: Profile
  setProfile: Dispatch<SetStateAction<Profile>>
  onSave: () => void
  onImportResume: () => void
  onImportResumeFile: () => void
  selectedResumeFile: File | null
  setSelectedResumeFile: Dispatch<SetStateAction<File | null>>
  importBusy: boolean
  importResult: ResumeImportResult | null
  validationIssues: ProfileValidationIssue[]
}) {
  return (
    <div className="page-stack" style={{ maxWidth: 840 }}>
      <div className="card">
        <div className="card-header"><h2><UserRound size={16} /> Dados pessoais</h2></div>
        <div className="card-body">
          <div className="form-row form-row-2">
            <div className="form-group"><label className="form-label">Nome completo</label><input className="form-input" value={profile.fullName} onChange={(e) => setProfile({ ...profile, fullName: e.target.value })} /></div>
            <div className="form-group"><label className="form-label">E-mail</label><input className="form-input" value={profile.email} onChange={(e) => setProfile({ ...profile, email: e.target.value })} /></div>
          </div>
          <div className="form-row form-row-3">
            <div className="form-group"><label className="form-label">CPF</label><input className="form-input" value={profile.cpf} onChange={(e) => setProfile({ ...profile, cpf: e.target.value.replace(/\D/g, '').slice(0, 11) })} /></div>
            <div className="form-group"><label className="form-label">Telefone</label><input className="form-input" value={profile.phone} onChange={(e) => setProfile({ ...profile, phone: e.target.value })} /></div>
            <div className="form-group"><label className="form-label">Cidade</label><input className="form-input" value={profile.city} onChange={(e) => setProfile({ ...profile, city: e.target.value })} /></div>
            <div className="form-group"><label className="form-label">País</label><input className="form-input" value={profile.country} onChange={(e) => setProfile({ ...profile, country: e.target.value })} /></div>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-header"><h2><BriefcaseBusiness size={16} /> Perfil profissional</h2></div>
        <div className="card-body">
          <div className="form-row form-row-3">
            <div className="form-group"><label className="form-label">Cargo alvo</label><input className="form-input" value={profile.targetRole} onChange={(e) => setProfile({ ...profile, targetRole: e.target.value })} /></div>
            <div className="form-group">
              <label className="form-label">Senioridade</label>
              <select className="form-select" value={profile.seniority} onChange={(e) => setProfile({ ...profile, seniority: e.target.value })}>
                <option value="junior">Júnior</option><option value="pleno">Pleno</option><option value="senior">Sênior</option>
              </select>
            </div>
            <div className="form-group"><label className="form-label">Pretensão salarial</label><input className="form-input" value={profile.salaryExpectation} onChange={(e) => setProfile({ ...profile, salaryExpectation: e.target.value })} placeholder="ex: 5.000" /></div>
          </div>
          <div className="form-group">
            <label className="form-label">Habilidades <span style={{ fontWeight: 400, color: '#94a3b8' }}>(separadas por vírgula)</span></label>
            <input className="form-input" value={profile.skills.join(', ')} onChange={(e) => setProfile({ ...profile, skills: e.target.value.split(',').map((x: string) => x.trim()).filter(Boolean) })} />
          </div>
          <div className="form-group">
            <label className="form-label">Resumo profissional</label>
            <textarea className="form-textarea" value={profile.professionalSummary} onChange={(e) => setProfile({ ...profile, professionalSummary: e.target.value })} />
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-header"><h2><ExternalLink size={16} /> Links</h2></div>
        <div className="card-body">
          <div className="form-row form-row-3">
            <div className="form-group"><label className="form-label">LinkedIn</label><input className="form-input" value={profile.linkedinUrl} onChange={(e) => setProfile({ ...profile, linkedinUrl: e.target.value })} placeholder="https://linkedin.com/in/..." /></div>
            <div className="form-group"><label className="form-label">GitHub</label><input className="form-input" value={profile.githubUrl} onChange={(e) => setProfile({ ...profile, githubUrl: e.target.value })} placeholder="https://github.com/..." /></div>
            <div className="form-group"><label className="form-label">Portfólio</label><input className="form-input" value={profile.portfolioUrl} onChange={(e) => setProfile({ ...profile, portfolioUrl: e.target.value })} /></div>
          </div>
          <div className="form-group">
            <label className="form-label">Importar curriculo</label>
            <div className="resume-upload-row">
              <input
                className="form-input"
                type="file"
                accept=".pdf,.docx,.txt,.md,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/plain"
                onChange={(e) => setSelectedResumeFile(e.target.files?.[0] ?? null)}
              />
              <button className="btn btn-primary" onClick={onImportResumeFile} disabled={importBusy || !selectedResumeFile} style={{ whiteSpace: 'nowrap' }}>
                <FileText size={16} /> {importBusy ? 'Importando...' : 'Ler arquivo'}
              </button>
            </div>
            {selectedResumeFile && <div className="form-hint">Selecionado: {selectedResumeFile.name}</div>}
          </div>
          <div className="form-group">
            <label className="form-label">Caminho local do curriculo <span style={{ fontWeight: 400, color: '#94a3b8' }}>(modo dev)</span></label>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <input className="form-input" value={profile.resumePath} onChange={(e) => setProfile({ ...profile, resumePath: e.target.value })} placeholder="C:\Users\...\curriculo.pdf" />
              <button className="btn btn-secondary" onClick={onImportResume} disabled={importBusy || !profile.resumePath} style={{ whiteSpace: 'nowrap' }}>
                <WandSparkles size={16} /> {importBusy ? 'Importando...' : 'Importar'}
              </button>
            </div>
          </div>
          {importResult && (
            <div className="list-row-modern" style={{ marginTop: 12 }}>
              <div className="list-row-main">
                <div className="list-row-title">Currículo importado com diagnóstico</div>
                <div className="list-row-sub">
                  {importResult.extraction.extractionMethod} · {importResult.extraction.classification} · qualidade {importResult.extraction.textQualityScore}/100 · {importResult.extraction.wordCount} palavra(s).
                  {' '}{importResult.skillsAdded.length} skill(s), {importResult.answersUpserted} resposta(s), {importResult.linksFetched.length} link(s).
                  {importResult.warnings.length > 0 ? ` Avisos: ${importResult.warnings.join(' | ')}` : ''}
                </div>
              </div>
              <span className={`badge ${importResult.extraction.requiresOcr || importResult.extraction.requiresReview ? 'badge-amber' : 'badge-green'}`}>
                {importResult.extraction.requiresOcr ? 'OCR necessario' : importResult.extraction.requiresReview ? 'Revisar' : 'Confiavel'}
              </span>
              <div className="form-group" style={{ flexBasis: '100%', marginTop: 12 }}>
                <label className="form-label">Texto extraido para revisao</label>
                <textarea className="form-textarea" value={importResult.extractedText || 'Nenhum texto extraido.'} readOnly style={{ minHeight: 180, fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace', fontSize: 12 }} />
              </div>
            </div>
          )}
        </div>
      </div>

      {validationIssues.length > 0 && (
        <div className="card" style={{ borderColor: '#fecaca' }}>
          <div className="card-header" style={{ background: '#fef2f2' }}><h2><AlertTriangle size={16} color="#dc2626" /> Problemas no perfil</h2></div>
          <div className="card-body-sm">
            {validationIssues.map((issue: ProfileValidationIssue) => (
              <div key={issue.fieldName} className="list-row-modern" style={{ marginBottom: 6, borderColor: issue.isCritical ? '#fecaca' : '#fde68a' }}>
                <div className="list-row-main">
                  <div className="list-row-title">{fieldNameLabels[issue.fieldName] ?? issue.fieldName}</div>
                  <div className="list-row-sub">{issue.errorMessage}</div>
                </div>
                <span className={`badge ${issue.isCritical ? 'badge-red' : 'badge-amber'}`}>{issue.isCritical ? 'Crítico' : 'Aviso'}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      <button className="btn btn-primary btn-lg" onClick={onSave} style={{ width: 'fit-content' }}><Save size={16} /> Salvar perfil</button>
    </div>
  )
}

/* ── Section Editor ── */
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
  if (type === 'skills') return JSON.stringify({ skills: (cleaned[0]?.skills ?? '').split(/[\n,]/).map((value) => value.trim()).filter(Boolean) })
  if (type === 'raw_import') return JSON.stringify({ text: cleaned[0]?.text ?? '' })
  return JSON.stringify({ items: cleaned })
}

function VisualResumePreview({ text, layout }: { text: string; layout: string }) {
  const lines = text.split('\n').map((line) => line.trimEnd())
  return (
    <div className={`visual-resume visual-resume-${layout}`}>
      {lines.map((line, index) => {
        const trimmed = line.trim()
        if (!trimmed) return <div key={index} className="visual-resume-gap" />
        const isHeading = trimmed.length < 44 && trimmed === trimmed.toUpperCase() && /[A-ZÁÀÂÃÉÊÍÓÔÕÚÇ]/.test(trimmed)
        const isBullet = /^[-•]/.test(trimmed)
        return (
          <div key={index} className={isHeading ? 'visual-resume-heading' : isBullet ? 'visual-resume-bullet' : 'visual-resume-line'}>
            {isBullet ? trimmed.replace(/^[-•]\s*/, '') : trimmed}
          </div>
        )
      })}
    </div>
  )
}

function SectionEditorPanel({ versionId }: { versionId: string }) {
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
    if (!confirm('Excluir esta secao?')) return
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
    const ids = reordered.map((s) => s.id)
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
    setDraftItems((items) => items.map((item, itemIndex) => itemIndex === index ? { ...item, [key]: value } : item))
  }

  function addItem() {
    setDraftItems((items) => [...items, defaultSectionItem(draftType)])
  }

  function removeItem(index: number) {
    setDraftItems((items) => items.length <= 1 ? items : items.filter((_, itemIndex) => itemIndex !== index))
  }

  const fields = sectionFields[draftType] ?? sectionFields.raw_import
  const allowsMultiple = !['summary', 'skills', 'raw_import', 'identity'].includes(draftType)

  return (
    <div className="card" style={{ marginTop: 16 }}>
      <div className="card-header"><h2><FileText size={16} /> Editor de secoes</h2></div>
      <div className="card-body">
        {error && <div className="notice error" style={{ marginBottom: 12 }}>{error}</div>}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 16 }}>
          {sections.map((section, index) => (
            <div key={section.id} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', border: '1px solid #e2e8f0', borderRadius: 8, background: '#f8fafc' }}>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontWeight: 600, fontSize: 13 }}>{section.title}</div>
                <div style={{ fontSize: 11, color: '#94a3b8' }}>{section.sectionType} · ordem {section.sortOrder}</div>
              </div>
              <div style={{ display: 'flex', gap: 4 }}>
                <button className="icon-btn" title="Mover para cima" onClick={() => void moveSection(index, -1)} disabled={index === 0 || loading}><ChevronUp size={14} /></button>
                <button className="icon-btn" title="Mover para baixo" onClick={() => void moveSection(index, 1)} disabled={index === sections.length - 1 || loading}><ChevronDown size={14} /></button>
                <button className="icon-btn" title="Editar" onClick={() => startEdit(section)}><Edit size={14} /></button>
                <button className="icon-btn" title="Excluir" onClick={() => void deleteSection(section.id)}><Trash2 size={14} /></button>
              </div>
            </div>
          ))}
          {sections.length === 0 && <span className="list-row-sub">Nenhuma secao nesta versao.</span>}
        </div>

        <div style={{ borderTop: '1px solid #e2e8f0', paddingTop: 12 }}>
          <div className="form-label">{editingId ? 'Editar secao' : 'Nova secao'}</div>
          <div className="form-row form-row-2" style={{ marginBottom: 8 }}>
            <div className="form-group">
              <label className="form-label">Tipo</label>
              <select className="form-select" value={draftType} onChange={(e) => changeDraftType(e.target.value)} disabled={!!editingId}>
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
            </div>
            <div className="form-group">
              <label className="form-label">Titulo</label>
              <input className="form-input" value={draftTitle} onChange={(e) => setDraftTitle(e.target.value)} placeholder="ex: Experiencia Profissional" />
            </div>
          </div>
          <div className="section-rich-editor">
            {draftItems.map((item, index) => (
              <div key={index} className="section-rich-item">
                {allowsMultiple && (
                  <div className="section-rich-item-head">
                    <span>Item {index + 1}</span>
                    <button className="icon-btn" title="Remover item" onClick={() => removeItem(index)} disabled={draftItems.length <= 1}><Trash2 size={14} /></button>
                  </div>
                )}
                <div className="form-row form-row-2">
                  {fields.map((field) => (
                    <div className="form-group" key={field.key} style={field.multiline ? { gridColumn: '1 / -1' } : undefined}>
                      <label className="form-label">{field.label}</label>
                      {field.multiline ? (
                        <textarea className="form-textarea" value={item[field.key] ?? ''} onChange={(e) => updateItem(index, field.key, e.target.value)} rows={3} placeholder={field.placeholder} />
                      ) : (
                        <input className="form-input" value={item[field.key] ?? ''} onChange={(e) => updateItem(index, field.key, e.target.value)} placeholder={field.placeholder} />
                      )}
                    </div>
                  ))}
                </div>
              </div>
            ))}
            {allowsMultiple && (
              <button className="btn btn-secondary btn-sm" onClick={addItem} type="button">
                <Plus size={14} /> Adicionar item
              </button>
            )}
            <div className="form-hint">Os campos sao salvos como JSON estruturado no backend. O frontend nao faz OCR, scoring, evidencias ou tailoring.</div>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-primary btn-sm" onClick={() => void saveSection()} disabled={loading || !draftTitle.trim()}>
              <Save size={14} /> {editingId ? 'Salvar alteracoes' : 'Adicionar'}
            </button>
            {editingId && <button className="btn btn-secondary btn-sm" onClick={resetDraft}>Cancelar</button>}
          </div>
        </div>
      </div>
    </div>
  )
}

/* ── Searches ── */
function ResumeAtsTab({ profile, documents, selectedVersionId, setSelectedVersionId, jobDescription, setJobDescription, jobUrl, setJobUrl, screenshotFile, setScreenshotFile, draft, jobTarget, busy, exportBusy, notice, onBuild, onExport, setTab, layoutOptions, selectedLayout, setSelectedLayout, onSaveReviewAnswer, onRegenerateAfterAnswers, onUpdateTailoredSection }: {
  profile: Profile
  documents: ResumeDocumentSummary[]
  selectedVersionId: string
  setSelectedVersionId: Dispatch<SetStateAction<string>>
  jobDescription: string
  setJobDescription: Dispatch<SetStateAction<string>>
  jobUrl: string
  setJobUrl: (url: string) => void
  screenshotFile: File | null
  setScreenshotFile: Dispatch<SetStateAction<File | null>>
  draft: TailoredResume | null
  jobTarget: JobTargetDetail | null
  busy: boolean
  exportBusy: boolean
  notice: string
  onBuild: () => void
  onExport: (format: 'txt' | 'docx' | 'pdf') => void
  setTab: (tab: string) => void
  layoutOptions: ResumeLayoutOption[]
  selectedLayout: string
  setSelectedLayout: Dispatch<SetStateAction<string>>
  onSaveReviewAnswer: (reqId: string, label: string, question: string, value: string, type: string) => Promise<void>
  onRegenerateAfterAnswers: () => Promise<void>
  onUpdateTailoredSection: (sectionId: string, contentJson: string) => Promise<void>
}) {
  const [showSectionEditor, setShowSectionEditor] = useState(false)
  const versions = documents.flatMap((document) => document.versions.map((version) => ({ ...version, documentTitle: document.title })))
  const selectedVersion = versions.find((version) => version.id === selectedVersionId)
  const hasJobInput = jobDescription.trim().length > 0 || jobUrl.trim().length > 0 || Boolean(screenshotFile)
  const isGenericAtsDraft = draft?.jobTargetId === '00000000-0000-0000-0000-000000000000'
  const readiness = [
    { label: 'Curriculo base', ok: versions.length > 0 },
    { label: 'Resumo', ok: Boolean(profile.professionalSummary) },
    { label: 'Skills', ok: profile.skills.length >= 6 },
    { label: 'Contato', ok: Boolean(profile.email && profile.phone) },
  ]

  async function copyResume() {
    if (draft?.plainText) await navigator.clipboard.writeText(draft.plainText)
  }

  return (
    <div className="page-grid-2 resume-ats-grid">
      <div className="page-stack">
        <div className="card">
          <div className="card-header">
            <h2><FileText size={16} /> Montar curriculo ATS</h2>
            <button className="btn btn-secondary btn-sm" onClick={() => setTab('profile')}><UserRound size={14} /> Perfil</button>
          </div>
          <div className="card-body">
            <div className="resume-readiness">
              {readiness.map((item) => (
                <span key={item.label} className={`badge ${item.ok ? 'badge-green' : 'badge-amber'}`}>
                  {item.ok ? <CheckCircle2 size={12} /> : <AlertCircle size={12} />} {item.label}
                </span>
              ))}
            </div>
            <div className="form-group" style={{ marginTop: 16 }}>
              <label className="form-label">Versao base do curriculo</label>
              <select className="form-select" value={selectedVersionId} onChange={(event) => setSelectedVersionId(event.target.value)}>
                <option value="">Selecione uma versao importada</option>
                {versions.map((version) => (
                  <option key={version.id} value={version.id}>
                    {version.documentTitle} - {version.title} ({version.status}, score {version.atsScore})
                  </option>
                ))}
              </select>
              <div className="form-hint">
                {selectedVersion ? `Base selecionada: ${selectedVersion.targetRole || 'sem cargo alvo'}${selectedVersion.targetCompany ? ` na ${selectedVersion.targetCompany}` : ''}.` : 'Importe um curriculo em Perfil para liberar a geracao ATS.'}
              </div>
              {selectedVersionId && (
                <button className="btn btn-secondary btn-sm" style={{ marginTop: 8 }} onClick={() => setShowSectionEditor((s) => !s)}>
                  <Edit size={14} /> {showSectionEditor ? 'Ocultar editor' : 'Editar secoes'}
                </button>
              )}
            </div>
            {showSectionEditor && selectedVersionId && <SectionEditorPanel versionId={selectedVersionId} />}
            <div className="form-group" style={{ marginTop: 16 }}>
              <label className="form-label">Descricao da vaga (opcional)</label>
              <textarea
                className="form-textarea resume-job-textarea"
                value={jobDescription}
                onChange={(event) => setJobDescription(event.target.value)}
                placeholder="Opcional: cole requisitos da vaga se quiser ajustar o curriculo para uma oportunidade especifica."
              />
              <div className="form-hint">Sem vaga, o backend gera um curriculo ATS geral a partir da versao base importada. Com vaga, ele organiza requisitos, evidencias e tailoring.</div>
            </div>
            <div className="form-group" style={{ marginTop: 16 }}>
              <label className="form-label">URL da vaga (opcional)</label>
              <input
                className="form-input"
                value={jobUrl}
                onChange={(event) => setJobUrl(event.target.value)}
                placeholder="https://linkedin.com/jobs/view/..."
              />
              <div className="form-hint">Opcional. Se informada, o backend extrai o texto da pagina automaticamente.</div>
            </div>
            <div className="form-group" style={{ marginTop: 16 }}>
              <label className="form-label">Screenshot ou PDF da vaga (opcional)</label>
              <input
                className="form-input"
                type="file"
                accept="image/png,image/jpeg,image/webp,application/pdf"
                onChange={(event) => setScreenshotFile(event.target.files?.[0] ?? null)}
              />
              <div className="form-hint">
                {screenshotFile ? `${screenshotFile.name} sera enviado ao backend como image_ocr.` : 'Use quando a vaga estiver em modal, imagem ou PDF. O frontend nao faz OCR.'}
              </div>
            </div>
            <div className="form-group" style={{ marginTop: 16 }}>
              <label className="form-label">Layout do curriculo</label>
              <select className="form-select" value={selectedLayout} onChange={(e) => setSelectedLayout(e.target.value)}>
                {layoutOptions.map((layout) => (
                  <option key={layout.key} value={layout.key}>
                    {layout.label}{layout.recommendedForAts ? ' (ATS)' : ''}
                  </option>
                ))}
              </select>
              <div className="form-hint">{layoutOptions.find((layout) => layout.key === selectedLayout)?.description ?? 'O layout escolhido sera aplicado na exportacao DOCX e PDF.'}</div>
            </div>
            <button className="btn btn-primary btn-lg" onClick={onBuild} disabled={busy || !selectedVersionId}>
              <WandSparkles size={16} /> {busy ? 'Gerando...' : hasJobInput ? 'Gerar curriculo ATS para vaga' : 'Gerar curriculo ATS'}
            </button>
            {notice && <div className="notice error" style={{ marginTop: 12 }}>{notice}</div>}
          </div>
        </div>

        {jobTarget && (
          <div className="card">
            <div className="card-header"><h2><Search size={16} /> Vaga organizada</h2></div>
            <div className="card-body">
              <div className="resume-target-head">
                <div>
                  <div className="ats-verdict">{jobTarget.title || 'Cargo nao detectado'}</div>
                  <div className="list-row-sub">{[jobTarget.company, jobTarget.location, jobTarget.workplace, jobTarget.seniority].filter(Boolean).join(' • ')}</div>
                </div>
                <span className={`badge ${jobTarget.status === 'organized' ? 'badge-green' : 'badge-amber'}`}>{jobTarget.status}</span>
              </div>
              <RequirementList requirements={jobTarget.requirements} />
              <AdviceList title="Avisos da organizacao" items={jobTarget.warnings} tone="amber" />
            </div>
          </div>
        )}

        {draft && (
          <div className="card">
            <div className="card-header"><h2><ShieldCheck size={16} /> {isGenericAtsDraft ? 'Analise ATS geral' : 'Evidencias e tailoring'}</h2></div>
            <div className="card-body">
              <div className="ats-score-row">
                <div className={`ats-score ${draft.atsScore >= 70 ? 'good' : draft.atsScore >= 50 ? 'warn' : 'bad'}`}>{draft.atsScore}</div>
                <div>
                  <div className="ats-verdict">{isGenericAtsDraft ? 'Curriculo ATS geral criado' : draft.status === 'ready' ? 'Pronto para revisar' : 'Precisa revisar lacunas'}</div>
                  <div className="list-row-sub">Versao criada: {draft.version.title}</div>
                </div>
              </div>
              <ScoreComponentList components={draft.scoreComponents ?? []} />
              <SectionDiffList diffs={draft.sectionDiffs ?? []} onUpdateSection={onUpdateTailoredSection} />
              {!isGenericAtsDraft && <ReviewQuestionList questions={draft.reviewQuestions ?? []} onSaveAnswer={onSaveReviewAnswer} />}
              {!isGenericAtsDraft && draft.reviewQuestions && draft.reviewQuestions.length > 0 && (
                <button className="btn btn-secondary btn-sm" onClick={() => void onRegenerateAfterAnswers()} disabled={busy} style={{ marginTop: 8 }}>
                  <RefreshCw size={14} /> Regenerar com respostas
                </button>
              )}
              {!isGenericAtsDraft && <KeywordGroup title="Com evidencia direta" items={draft.includedKeywords} tone="green" />}
              {!isGenericAtsDraft && <KeywordGroup title="Relacionadas" items={draft.adjacentKeywords} tone="blue" />}
              {!isGenericAtsDraft && <KeywordGroup title="Fora do curriculo" items={draft.excludedKeywords} tone="amber" />}
              {!isGenericAtsDraft && <AdviceList title="Checkpoints" items={draft.checkpoints} tone="amber" />}
              <AdviceList title="Warnings" items={draft.warnings} tone="amber" />
            </div>
          </div>
        )}
      </div>

      <div className="card resume-preview-card">
        <div className="card-header">
          <h2><ClipboardList size={16} /> Preview</h2>
          <div className="topbar-actions">
            <button className="btn btn-secondary btn-sm" onClick={copyResume} disabled={!draft?.plainText}><ClipboardList size={14} /> Copiar</button>
            <button className="btn btn-secondary btn-sm" onClick={() => onExport('txt')} disabled={!draft?.plainText || exportBusy}><FileText size={14} /> TXT</button>
            <button className="btn btn-secondary btn-sm" onClick={() => onExport('docx')} disabled={!draft?.plainText || exportBusy}><FileText size={14} /> DOCX</button>
            <button className="btn btn-primary btn-sm" onClick={() => onExport('pdf')} disabled={!draft?.plainText || exportBusy}><FileText size={14} /> {exportBusy ? 'Exportando...' : 'PDF'}</button>
          </div>
        </div>
        <div className="card-body">
          {draft?.plainText ? (
            <VisualResumePreview text={draft.plainText} layout={selectedLayout} />
          ) : (
            <div className="empty-state"><FileText size={28} /><p>Gere um curriculo ATS para ver o preview aqui.</p></div>
          )}
        </div>
      </div>
    </div>
  )
}

function RequirementList({ requirements }: { requirements: JobTargetDetail['requirements'] }) {
  return (
    <div style={{ marginTop: 16 }}>
      <div className="form-label">Requisitos extraidos</div>
      <div className="resume-requirement-list">
        {requirements.length === 0 ? <span className="list-row-sub">Nenhum requisito estruturado.</span> :
          requirements.map((item) => (
            <div key={item.id} className="resume-requirement-item">
              <div>
                <strong>{item.label}</strong>
                <span>{item.category} • {item.importance}</span>
              </div>
              <p>{item.evidenceText || 'Sem trecho de evidencia retornado.'}</p>
            </div>
          ))}
      </div>
    </div>
  )
}

function ScoreComponentList({ components }: { components: TailoredResume['scoreComponents'] }) {
  return (
    <div style={{ marginTop: 16 }}>
      <div className="form-label">Score por componentes</div>
      <div className="resume-requirement-list">
        {components.length === 0 ? <span className="list-row-sub">Sem decomposicao de score.</span> :
          components.map((item) => {
            const ratio = item.maxScore > 0 ? Math.max(0, Math.min(100, Math.round((item.score / item.maxScore) * 100))) : 0
            const tone = item.status === 'good' ? 'green' : item.status === 'warn' ? 'amber' : 'red'
            return (
              <div key={item.key} className="resume-requirement-item">
                <div>
                  <strong>{item.label}</strong>
                  <span>{item.score}/{item.maxScore}</span>
                </div>
                <div style={{ height: 6, borderRadius: 999, background: '#e2e8f0', overflow: 'hidden', margin: '8px 0' }}>
                  <div style={{ width: `${ratio}%`, height: '100%', background: tone === 'green' ? '#16a34a' : tone === 'amber' ? '#d97706' : '#dc2626' }} />
                </div>
                <p>{item.reason}</p>
              </div>
            )
          })}
      </div>
    </div>
  )
}

function SectionDiffList({ diffs, onUpdateSection }: { diffs: TailoredResume['sectionDiffs']; onUpdateSection?: (sectionId: string, contentJson: string) => Promise<void> }) {
  const statusLabel = (status: string) => ({
    added: 'Adicionada',
    changed: 'Alterada',
    removed: 'Removida',
    unchanged: 'Sem mudanca',
  } as Record<string, string>)[status] ?? status
  const statusTone = (status: string) => status === 'added' ? 'green' : status === 'changed' ? 'blue' : status === 'removed' ? 'amber' : 'slate'
  const [editingId, setEditingId] = useState<string | null>(null)
  const [draftContent, setDraftContent] = useState('')

  function startEdit(item: ResumeSectionDiff) {
    if (!item.afterSectionId) return
    setEditingId(item.afterSectionId)
    setDraftContent(item.afterPreview)
  }

  async function saveEdit(sectionId: string) {
    await onUpdateSection?.(sectionId, draftContent)
    setEditingId(null)
  }

  async function revertToBefore(item: ResumeSectionDiff) {
    if (!item.afterSectionId) return
    await onUpdateSection?.(item.afterSectionId, JSON.stringify({ text: item.beforePreview }))
  }

  return (
    <div style={{ marginTop: 16 }}>
      <div className="form-label">Diff por secao</div>
      <div className="resume-requirement-list">
        {diffs.length === 0 ? <span className="list-row-sub">Nenhuma diferenca estruturada.</span> :
          diffs.map((item) => (
            <div key={`${item.sectionType}-${item.status}`} className="resume-requirement-item">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div>
                  <strong>{item.title}</strong>
                  <span className={`badge badge-${statusTone(item.status)}`}>{statusLabel(item.status)}</span>
                </div>
                {onUpdateSection && item.afterSectionId && (
                  <div style={{ display: 'flex', gap: 6 }}>
                    <button className="btn btn-secondary btn-sm" onClick={() => startEdit(item)}><Edit size={12} /> Editar</button>
                    <button className="btn btn-secondary btn-sm" onClick={() => void revertToBefore(item)}><RefreshCw size={12} /> Rejeitar</button>
                  </div>
                )}
              </div>
              {item.beforePreview && <p><strong>Antes:</strong> {item.beforePreview}</p>}
              {editingId === item.afterSectionId ? (
                <div style={{ marginTop: 8 }}>
                  <textarea className="form-textarea" value={draftContent} onChange={(e) => setDraftContent(e.target.value)} rows={3} />
                  <div style={{ display: 'flex', gap: 8, marginTop: 6 }}>
                    <button className="btn btn-primary btn-sm" onClick={() => void saveEdit(item.afterSectionId!)}><Save size={12} /> Salvar</button>
                    <button className="btn btn-secondary btn-sm" onClick={() => setEditingId(null)}>Cancelar</button>
                  </div>
                </div>
              ) : (
                item.afterPreview && <p><strong>Depois:</strong> {item.afterPreview}</p>
              )}
            </div>
          ))}
      </div>
    </div>
  )
}

function ReviewQuestionList({ questions, onSaveAnswer }: { questions: TailoredResume['reviewQuestions']; onSaveAnswer?: (reqId: string, label: string, question: string, value: string, type: string) => Promise<void> }) {
  const [answers, setAnswers] = useState<Record<string, { value: string; type: string }>>({})
  return (
    <div style={{ marginTop: 16 }}>
      <div className="form-label">Perguntas para revisar</div>
      <div className="resume-requirement-list">
        {questions.length === 0 ? <span className="list-row-sub">Nenhuma pergunta pendente.</span> :
          questions.map((item) => (
            <div key={item.requirementId} className="resume-requirement-item">
              <div>
                <strong>{item.requirementLabel}</strong>
                <span>{item.category} • {item.importance}</span>
              </div>
              <p>{item.questionText}</p>
              <p><strong>Motivo:</strong> {item.reason}</p>
              {onSaveAnswer && (
                <div style={{ display: 'flex', gap: 8, marginTop: 8, flexWrap: 'wrap' }}>
                  <input
                    className="form-input"
                    style={{ flex: 1, minWidth: 200 }}
                    placeholder="Sua resposta..."
                    value={answers[item.requirementId]?.value ?? ''}
                    onChange={(e) => setAnswers(prev => ({ ...prev, [item.requirementId]: { ...(prev[item.requirementId] ?? { type: 'unknown_confirmed' }), value: e.target.value } }))}
                  />
                  <select
                    className="form-select"
                    style={{ width: 140 }}
                    value={answers[item.requirementId]?.type ?? 'unknown_confirmed'}
                    onChange={(e) => setAnswers(prev => ({ ...prev, [item.requirementId]: { ...(prev[item.requirementId] ?? { value: '' }), type: e.target.value } }))}
                  >
                    <option value="unknown_confirmed">Confirmo que nao tenho</option>
                    <option value="proven">Tenho experiencia</option>
                    <option value="adjacent">Experiencia relacionada</option>
                    <option value="missing">Nao tenho</option>
                  </select>
                  <button
                    className="btn btn-primary btn-sm"
                    onClick={() => void onSaveAnswer(item.requirementId, item.requirementLabel, item.questionText, answers[item.requirementId]?.value ?? '', answers[item.requirementId]?.type ?? 'unknown_confirmed')}
                    disabled={!answers[item.requirementId]?.value?.trim()}
                  >
                    Salvar
                  </button>
                </div>
              )}
            </div>
          ))}
      </div>
    </div>
  )
}

function KeywordGroup({ title, items, tone }: { title: string; items: string[]; tone: 'green' | 'amber' | 'blue' }) {
  return (
    <div style={{ marginTop: 16 }}>
      <div className="form-label">{title}</div>
      <div className="chip-toggle-group">
        {items.length === 0 ? <span className="list-row-sub">Nenhuma palavra-chave nesta categoria.</span> :
          items.map((item) => <span key={item} className={`badge badge-${tone}`}>{item}</span>)}
      </div>
    </div>
  )
}

function AdviceList({ title, items, tone }: { title: string; items: string[]; tone: 'green' | 'amber' }) {
  return (
    <div style={{ marginTop: 16 }}>
      <div className="form-label">{title}</div>
      <div className="page-stack" style={{ gap: 8 }}>
        {items.length === 0 ? <span className="list-row-sub">Sem avisos.</span> :
          items.map((item) => <div key={item} className={tone === 'green' ? 'notice-ok' : 'notice-inline'}>{item}</div>)}
      </div>
    </div>
  )
}

function SearchesTab({ searches, draftSearch, setDraftSearch, onCreate, onDelete, editingSearchId, setEditingSearchId }: {
  searches: SavedSearch[]
  draftSearch: DraftSearch
  setDraftSearch: Dispatch<SetStateAction<DraftSearch>>
  onCreate: () => void
  onDelete: (id: string) => void
  editingSearchId: string
  setEditingSearchId: (id: string) => void
}) {
  return (
    <div className="page-stack" style={{ maxWidth: 900 }}>
      <div className="card">
        <div className="card-header"><h2><Search size={16} /> {editingSearchId ? 'Editar busca' : 'Nova busca'}</h2></div>
        <div className="card-body">
          <div className="form-row form-row-3">
            <div className="form-group"><label className="form-label">Nome da busca</label><input className="form-input" value={draftSearch.name} onChange={(e) => setDraftSearch({ ...draftSearch, name: e.target.value })} /></div>
            <div className="form-group"><label className="form-label">Palavras-chave</label><input className="form-input" value={draftSearch.keywords} onChange={(e) => setDraftSearch({ ...draftSearch, keywords: e.target.value })} /></div>
            <div className="form-group"><label className="form-label">Localização</label><input className="form-input" value={draftSearch.location} onChange={(e) => setDraftSearch({ ...draftSearch, location: e.target.value })} /></div>
          </div>
          <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
            <button className="btn btn-primary" onClick={onCreate}><Save size={16} /> {editingSearchId ? 'Salvar alterações' : 'Criar busca'}</button>
            {editingSearchId && <button className="btn btn-secondary" onClick={() => { setEditingSearchId(''); setDraftSearch({ name: '.NET', keywords: '.NET', location: 'Brazil' }) }}>Cancelar</button>}
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-header"><h2><ClipboardList size={16} /> Buscas salvas</h2></div>
        <div className="card-body-sm">
          {searches.length === 0 ? <div className="empty-state"><Search size={28} /><p>Nenhuma busca salva.</p></div> :
            searches.map((s: SavedSearch) => (
              <div className="list-row-modern" key={s.id}>
                <div className="list-row-main">
                  <div className="list-row-title">{s.name}</div>
                  <div className="list-row-sub">{s.keywords} · {s.location} · {s.limitPerRun} vagas/máx</div>
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <span className={`badge ${s.isActive ? 'badge-green' : 'badge-slate'}`}>{s.isActive ? 'Ativa' : 'Inativa'}</span>
                  <button className="icon-btn" title="Editar" onClick={() => { setDraftSearch({ name: s.name, keywords: s.keywords, location: s.location }); setEditingSearchId(s.id) }}><ClipboardList size={14} /></button>
                  <button className="icon-btn" title="Excluir" onClick={() => onDelete(s.id)}><Trash2 size={14} /></button>
                </div>
              </div>
            ))
          }
        </div>
      </div>
    </div>
  )
}

/* ── Jobs ── */
function JobsTab({ jobs, documents, selectedJobs, setSelectedJobs, updateJobStatus, activeJob, setActiveJobId, jobFilter, setJobFilter, onQueue, jobAudit, auditTab, setAuditTab, onAssignResume }: {
  jobs: JobLead[]
  documents: ResumeDocumentSummary[]
  selectedJobs: string[]
  setSelectedJobs: Dispatch<SetStateAction<string[]>>
  updateJobStatus: (id: string, status: string) => void
  activeJob?: JobLead
  setActiveJobId: (id: string) => void
  jobFilter: string
  setJobFilter: (status: string) => void
  onQueue: () => void
  jobAudit: JobAudit
  auditTab: AuditTab
  setAuditTab: Dispatch<SetStateAction<AuditTab>>
  onAssignResume: (jobId: string, resumeVersionId: string) => Promise<void>
}) {
  const resumeVersions = documents.flatMap((document) => document.versions.map((version) => ({ ...version, documentTitle: document.title })))
  return (
    <div className="page-grid-2">
      <div className="card" style={{ overflow: 'hidden' }}>
        <div className="card-header" style={{ flexWrap: 'wrap', gap: 12 }}>
          <div className="segmented">
            {jobStatuses.map((status: string) => (
              <button key={status} className={jobFilter === status ? 'active' : ''} onClick={() => setJobFilter(status)}>{jobStatusLabels[status]}</button>
            ))}
          </div>
          <button className="btn btn-primary btn-sm" onClick={onQueue} disabled={selectedJobs.length === 0}>
            <Play size={14} /> Colocar {selectedJobs.length > 0 ? `${selectedJobs.length} na fila` : 'na fila'}
          </button>
        </div>
        <div style={{ padding: 0 }}>
          {jobs.length === 0 ? <div className="empty-state"><BriefcaseBusiness size={28} /><p>Nenhuma vaga neste filtro.</p></div> :
            <table className="data-table">
              <thead><tr><th style={{ width: 34 }}></th><th style={{ width: 56 }}>Pts</th><th>Vaga</th><th>Empresa</th><th>Candidatura</th><th>CV</th><th>Status</th><th></th></tr></thead>
              <tbody>
                {jobs.map((job: JobLead) => (
                  <tr key={job.id} className={activeJob?.id === job.id ? 'active' : ''} onClick={() => setActiveJobId(job.id)} style={{ cursor: 'pointer' }}>
                    <td onClick={(e) => e.stopPropagation()}>
                      <input type="checkbox" checked={selectedJobs.includes(job.id)} onChange={(e) => setSelectedJobs(e.target.checked ? [...selectedJobs, job.id] : selectedJobs.filter((id: string) => id !== job.id))} />
                    </td>
                    <td><strong style={{ color: job.score >= 60 ? '#0d9488' : job.score >= 30 ? '#d97706' : '#64748b' }}>{job.score}</strong></td>
                    <td>
                      <div style={{ fontWeight: 600, color: '#0f172a', fontSize: 13 }}>{job.title}</div>
                      <div style={{ fontSize: 11, color: '#94a3b8', marginTop: 2 }}>{job.location}{job.isRemote ? ' · Remoto' : ''}</div>
                    </td>
                    <td style={{ fontSize: 13, color: '#475569' }}>{job.company || '—'}</td>
                    <td>
                      <div style={{ display: 'flex', gap: 5, flexWrap: 'wrap' }}>
                        <span className="badge badge-slate">{platformLabel(job.platform)}</span>
                        <span className={`badge ${applyPlatformBadge(job.applyPlatform)}`}>{applyPlatformLabel(job.applyPlatform)}</span>
                        <span className={`badge ${automationSupportBadge(job.automationSupport)}`}>{automationSupportLabel(job.automationSupport)}</span>
                      </div>
                    </td>
                    <td>
                      <select
                        className="form-select"
                        style={{ minWidth: 160, height: 30, fontSize: 11, padding: '4px 8px' }}
                        value={job.resume?.resumeVersionId ?? ''}
                        onClick={(event) => event.stopPropagation()}
                        onChange={(event) => {
                          event.stopPropagation()
                          void onAssignResume(job.id, event.target.value)
                        }}
                      >
                        <option value="">Sem CV vinculado</option>
                        {resumeVersions.map((version) => (
                          <option key={version.id} value={version.id}>{version.documentTitle} - {version.title}</option>
                        ))}
                      </select>
                      {job.resume?.resumeExportId && <div className="badge badge-green" style={{ fontSize: 10, marginTop: 4 }}>CV gerado</div>}
                    </td>
                    <td><span className={`badge ${statusBadge(job.status)}`}>{jobStatusLabels[job.status] ?? job.status}</span></td>
                    <td>
                      <button className="btn btn-secondary btn-sm" onClick={(e) => { e.stopPropagation(); void updateJobStatus(job.id, 'shortlisted') }}>Favoritar</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          }
        </div>
      </div>

      <JobDetailCard job={activeJob} documents={documents} onAssignResume={onAssignResume} updateJobStatus={updateJobStatus} jobAudit={jobAudit} auditTab={auditTab} setAuditTab={setAuditTab} />
    </div>
  )
}

function JobDetailCard({ job, documents, onAssignResume, updateJobStatus, jobAudit, auditTab, setAuditTab }: { job?: JobLead; documents: ResumeDocumentSummary[]; onAssignResume: (jobId: string, resumeVersionId: string) => Promise<void>; updateJobStatus: (id: string, status: string) => void; jobAudit: JobAudit; auditTab: AuditTab; setAuditTab: Dispatch<SetStateAction<AuditTab>> }) {
  if (!job) return (
    <div className="card job-detail-card">
      <div className="empty-state"><BriefcaseBusiness size={32} /><p>Selecione uma vaga para ver os detalhes.</p></div>
    </div>
  )
  const hasAudit = jobAudit.fields.length + jobAudit.answers.length + jobAudit.steps.length + jobAudit.traceSteps.length + jobAudit.checkpoints.length + jobAudit.events.length + jobAudit.learnings.length > 0
  const resumeVersions = documents.flatMap((document) => document.versions.map((version) => ({ ...version, documentTitle: document.title })))
  const auditTabs = [
    { key: 'timeline', label: 'Timeline', icon: <ShieldCheck size={13} />, count: jobAudit.traceSteps.length || jobAudit.steps.length },
    { key: 'steps', label: 'Passos', icon: <Footprints size={13} />, count: jobAudit.steps.length },
    { key: 'fields', label: 'Campos', icon: <FileText size={13} />, count: jobAudit.fields.length },
    { key: 'answers', label: 'Respostas', icon: <MessageSquare size={13} />, count: jobAudit.answers.length },
    { key: 'checkpoints', label: 'Checkpoints', icon: <AlertCircle size={13} />, count: jobAudit.checkpoints.length },
    { key: 'learnings', label: 'Aprendizados', icon: <Lightbulb size={13} />, count: jobAudit.learnings.length },
    { key: 'events', label: 'Eventos', icon: <History size={13} />, count: jobAudit.events.length },
  ] as const
  return (
    <div className="card job-detail-card" style={{ display: 'flex', flexDirection: 'column' }}>
      <div className="card-body" style={{ flex: '0 0 auto' }}>
        <div className="job-detail-header">
          <div className="score-ring">{job.score}</div>
          <div style={{ minWidth: 0 }}>
            <h2 style={{ margin: 0, fontSize: 17, fontWeight: 700, lineHeight: 1.3 }}>{job.title}</h2>
            <p className="job-detail-meta">{job.company || 'Empresa desconhecida'} · {job.location}</p>
          </div>
          <a className="btn btn-secondary btn-sm" href={job.url} target="_blank" rel="noreferrer" style={{ marginLeft: 'auto', flexShrink: 0 }}><ExternalLink size={14} /> Abrir</a>
        </div>
        <div className="detail-actions">
          {[
            ['shortlisted', 'Favoritar'], ['queued_to_apply', 'Colocar na fila'],
            ['manual_only', 'Somente manual'], ['ignored', 'Ignorar'], ['applied', 'Candidatado'],
          ].map(([status, label]) => (
            <button key={status} className={`btn btn-sm ${job.status === status ? 'btn-primary' : 'btn-secondary'}`} onClick={() => updateJobStatus(job.id, status as string)}>{label}</button>
          ))}
        </div>
        <dl className="detail-dl">
          <dt>Status</dt><dd><span className={`badge ${statusBadge(job.status)}`}>{jobStatusLabels[job.status] ?? job.status}</span></dd>
          <dt>Origem</dt><dd><span className="badge badge-slate">{platformLabel(job.platform)}</span></dd>
          <dt>Candidatura</dt><dd><span className={`badge ${applyPlatformBadge(job.applyPlatform)}`}>{applyPlatformLabel(job.applyPlatform)}</span> <span className="badge badge-slate">{applySurfaceLabel(job.applySurface)}</span></dd>
          <dt>Automacao</dt><dd><span className={`badge ${automationSupportBadge(job.automationSupport)}`}>{automationSupportLabel(job.automationSupport)}</span>{job.automationReason ? ` - ${job.automationReason}` : ''}</dd>
          <dt>URL candidatura</dt><dd>{job.applyUrl ? <a href={job.applyUrl} target="_blank" rel="noreferrer">{job.applyUrl}</a> : 'Ainda nao classificada.'}</dd>
          <dt>URL origem</dt><dd><a href={job.url} target="_blank" rel="noreferrer">{job.url}</a></dd>
          <dt>Por que essa pontuação</dt><dd>{job.scoreReason || 'Sem motivo de pontuação.'}</dd>
          <dt>Descrição</dt><dd>{job.description || 'Nenhuma descrição capturada ainda.'}</dd>
          <dt>Currículo da vaga</dt>
          <dd>
            <select
              className="form-select"
              style={{ maxWidth: 360, marginBottom: 8 }}
              value={job.resume?.resumeVersionId ?? ''}
              onChange={(event) => void onAssignResume(job.id, event.target.value)}
            >
              <option value="">Sem CV vinculado</option>
              {resumeVersions.map((version) => (
                <option key={version.id} value={version.id}>{version.documentTitle} - {version.title}</option>
              ))}
            </select>
            {job.resume?.resumeExportId ? (
              <span className="badge badge-green">CV gerado · {job.resume.resumeExportId.slice(0, 8)}</span>
            ) : job.resume?.resumeVersionId ? (
              <span className="badge badge-blue">CV vinculado · {job.resume.resumeVersionId.slice(0, 8)}</span>
            ) : (
              <span className="badge badge-slate">Nenhum CV vinculado</span>
            )}
          </dd>
          <dt>Encontrada em</dt><dd>{new Date(job.foundAt).toLocaleString()}</dd>
        </dl>
      </div>

      {hasAudit && (
        <div style={{ borderTop: '1px solid #e2e8f0', flex: '1 1 auto', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
          <div style={{ display: 'flex', gap: 2, padding: '8px 12px 0', borderBottom: '1px solid #e2e8f0', overflowX: 'auto' }}>
            {auditTabs.map((t) => (
              <button key={t.key} onClick={() => setAuditTab(t.key)} className={auditTab === t.key ? 'active' : ''} style={{
                background: auditTab === t.key ? '#f1f5f9' : 'transparent', border: 'none', borderBottom: auditTab === t.key ? '2px solid #0f172a' : '2px solid transparent',
                padding: '6px 10px', fontSize: 12, fontWeight: 600, color: auditTab === t.key ? '#0f172a' : '#64748b', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 4, whiteSpace: 'nowrap',
              }}>
                {t.icon} {t.label} <span style={{ fontSize: 10, color: '#94a3b8', fontWeight: 500 }}>({t.count})</span>
              </button>
            ))}
          </div>
          <div style={{ overflowY: 'auto', padding: 12, flex: 1 }}>
            {auditTab === 'timeline' && (
              jobAudit.traceSteps.length > 0 ? <TraceTimeline steps={jobAudit.traceSteps} /> :
              jobAudit.steps.length === 0 ? <p style={{ color: '#94a3b8', fontSize: 13 }}>Nenhum trace rico registrado.</p> :
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {jobAudit.steps.map((s) => (
                  <div key={s.id} style={{ padding: 10, borderRadius: 8, background: '#f8fafc', border: '1px solid #e2e8f0', fontSize: 12 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                      <span style={{ fontWeight: 600, color: '#0f172a' }}>#{s.stepNumber} {actionTypeLabels[s.actionType] ?? s.actionType}</span>
                      <span style={{ color: '#94a3b8' }}>{new Date(s.createdAt).toLocaleTimeString()}</span>
                    </div>
                    <div style={{ color: '#475569' }}>{s.targetLabel || s.pageState} {s.result ? `· ${s.result}` : ''}</div>
                    <div style={{ marginTop: 4, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                      <span className="badge badge-slate" style={{ fontSize: 10 }}>trace legado</span>
                      <span className="badge badge-slate" style={{ fontSize: 10 }}>conf: {Number(s.confidence).toFixed(2)}</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
            {auditTab === 'steps' && (
              jobAudit.steps.length === 0 ? <p style={{ color: '#94a3b8', fontSize: 13 }}>Nenhum passo registrado.</p> :
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {jobAudit.steps.map((s) => (
                  <div key={s.id} style={{ padding: 10, borderRadius: 8, background: '#f8fafc', border: '1px solid #e2e8f0', fontSize: 12 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 4 }}>
                      <span style={{ fontWeight: 600, color: '#0f172a' }}>#{s.stepNumber} {s.actionType}</span>
                      <span style={{ color: '#94a3b8' }}>{new Date(s.createdAt).toLocaleTimeString()}</span>
                    </div>
                    <div style={{ color: '#475569' }}>{s.targetLabel || s.pageState} {s.result ? `· ${s.result}` : ''}</div>
                    <div style={{ marginTop: 4, display: 'flex', gap: 8 }}>
                      <span className="badge badge-slate" style={{ fontSize: 10 }}>conf: {Number(s.confidence).toFixed(2)}</span>
                      <span className="badge badge-slate" style={{ fontSize: 10 }}>{s.pageState}</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
            {auditTab === 'fields' && (
              jobAudit.fields.length === 0 ? <p style={{ color: '#94a3b8', fontSize: 13 }}>Nenhum campo registrado.</p> :
              <table className="data-table" style={{ fontSize: 12 }}>
                <thead><tr><th>Campo</th><th>Tipo</th><th>Origem</th><th>Conf</th></tr></thead>
                <tbody>
                  {jobAudit.fields.map((f) => (
                    <tr key={f.id}><td>{f.label || f.fieldKey}</td><td>{f.fieldType}</td><td>{f.valueSource}</td><td>{Number(f.confidence).toFixed(2)}</td></tr>
                  ))}
                </tbody>
              </table>
            )}
            {auditTab === 'answers' && (
              jobAudit.answers.length === 0 ? <p style={{ color: '#94a3b8', fontSize: 13 }}>Nenhuma resposta registrada.</p> :
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {jobAudit.answers.map((a) => (
                  <div key={a.id} style={{ padding: 10, borderRadius: 8, background: '#f8fafc', border: '1px solid #e2e8f0', fontSize: 12 }}>
                    <div style={{ fontWeight: 600, color: '#0f172a', marginBottom: 2 }}>{a.questionText}</div>
                    <div style={{ color: '#475569' }}>{a.answerValue}</div>
                    <div style={{ marginTop: 4, display: 'flex', gap: 8 }}>
                      <span className="badge badge-slate" style={{ fontSize: 10 }}>{a.source}</span>
                      <span className="badge badge-slate" style={{ fontSize: 10 }}>conf: {Number(a.confidence).toFixed(2)}</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
            {auditTab === 'checkpoints' && (
              jobAudit.checkpoints.length === 0 ? <p style={{ color: '#94a3b8', fontSize: 13 }}>Nenhum checkpoint registrado.</p> :
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {jobAudit.checkpoints.map((c) => (
                  <div key={c.id} style={{ padding: 10, borderRadius: 8, background: c.resolutionStatus === 'open' ? '#fff7ed' : '#f0fdf4', border: `1px solid ${c.resolutionStatus === 'open' ? '#fed7aa' : '#bbf7d0'}`, fontSize: 12 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                      <span style={{ fontWeight: 600, color: c.resolutionStatus === 'open' ? '#c2410c' : '#15803d' }}>{c.reasonCode}</span>
                      <span className={`badge ${c.resolutionStatus === 'open' ? 'badge-amber' : 'badge-green'}`} style={{ fontSize: 10 }}>{c.resolutionStatus}</span>
                    </div>
                    <div style={{ color: '#475569' }}>{c.fieldLabel || c.detail}</div>
                    <div style={{ color: '#94a3b8', marginTop: 2 }}>{c.stage} · {new Date(c.createdAt).toLocaleString()}</div>
                  </div>
                ))}
              </div>
            )}
            {auditTab === 'events' && (
              jobAudit.events.length === 0 ? <p style={{ color: '#94a3b8', fontSize: 13 }}>Nenhum evento registrado.</p> :
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {jobAudit.events.map((e) => (
                  <div key={e.id} style={{ padding: 8, borderRadius: 6, background: '#f8fafc', border: '1px solid #e2e8f0', fontSize: 12, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div>
                      <span style={{ fontWeight: 600, color: '#0f172a' }}>{e.eventType}</span>
                      <span style={{ color: '#475569', marginLeft: 8 }}>{e.detail}</span>
                    </div>
                    <span style={{ color: '#94a3b8', fontSize: 11, whiteSpace: 'nowrap' }}>{new Date(e.occurredAt).toLocaleTimeString()}</span>
                  </div>
                ))}
              </div>
            )}
            {auditTab === 'learnings' && (
              jobAudit.learnings.length === 0 ? <p style={{ color: '#94a3b8', fontSize: 13 }}>Nenhum aprendizado registrado.</p> :
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {jobAudit.learnings.map((learning) => (
                  <div key={learning.id} style={{ padding: 10, borderRadius: 8, background: '#f8fafc', border: '1px solid #e2e8f0', fontSize: 12 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, marginBottom: 4 }}>
                      <span style={{ fontWeight: 600, color: '#0f172a' }}>{learning.title}</span>
                      <span className={`badge ${learning.status === 'candidate' ? 'badge-amber' : learning.status.includes('approved') ? 'badge-green' : learning.status === 'disabled' ? 'badge-red' : 'badge-slate'}`}>{learningStatusLabel(learning.status)}</span>
                    </div>
                    <div style={{ color: '#475569' }}>{learning.summary}</div>
                    <div style={{ marginTop: 5, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                      <span className="badge badge-slate" style={{ fontSize: 10 }}>{learning.learningType}</span>
                      <span className={learning.riskLevel === 'low' ? 'badge badge-green' : learning.riskLevel === 'high' ? 'badge badge-red' : 'badge badge-amber'} style={{ fontSize: 10 }}>risco {learning.riskLevel}</span>
                      <span className="badge badge-slate" style={{ fontSize: 10 }}>sucessos {learning.successCount}</span>
                      <span className="badge badge-slate" style={{ fontSize: 10 }}>falhas {learning.failureCount}</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

/* ── Runs ── */
function RunsTab({ runs }: { runs: Run[] }) {
  return (
    <div className="page-stack" style={{ maxWidth: 900 }}>
      {runs.length === 0 ? <div className="card"><div className="empty-state"><Play size={32} /><p>Nenhuma execução registrada.</p></div></div> :
        runs.map((run) => (
          <div className="card" key={run.id}>
            <div className="card-body">
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap' }}>
                <div>
                  <div style={{ fontSize: 15, fontWeight: 700, color: '#0f172a' }}>{run.name}</div>
                  <div style={{ fontSize: 12, color: '#94a3b8', marginTop: 2 }}>{new Date(run.createdAt).toLocaleString()}</div>
                </div>
                <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                  <span className={`badge ${run.state === 'completed' ? 'badge-green' : run.state === 'failed' ? 'badge-red' : run.state === 'running' ? 'badge-blue' : 'badge-slate'}`}>{runStateLabels[run.state] ?? run.state}</span>
                  <span style={{ fontSize: 13, color: '#475569', fontWeight: 600 }}>{run.totalJobs} vagas</span>
                </div>
              </div>
              {run.jobs.length > 0 && (
                <div style={{ marginTop: 12, display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {run.jobs.map((job) => (
                    <span key={job.id} className={`badge ${job.resumeExportId ? 'badge-green' : statusBadge(job.state)}`} style={{ fontSize: 10 }} title={job.resumeFilePath || job.reason || ''}>
                      {jobStatusLabels[job.state] ?? job.state}{job.resumeExportId ? ' · CV gerado' : job.resumeVersionId ? ' · CV vinculado' : ''}
                    </span>
                  ))}
                </div>
              )}
              {run.jobs.some(j => j.resumeFilePath) && (
                <div style={{ marginTop: 8, fontSize: 11, color: '#64748b' }}>
                  {run.jobs.filter(j => j.resumeFilePath).length} candidatura(s) com curriculo especifico gerado.
                </div>
              )}
            </div>
          </div>
        ))
      }
    </div>
  )
}

/* ── Answers ── */
function AnswersTab({ answers, draftAnswer, setDraftAnswer, onSave, onDelete, onDraftAi, aiBusy, editingAnswerId, setEditingAnswerId }: {
  answers: Answer[]
  draftAnswer: DraftAnswer
  setDraftAnswer: Dispatch<SetStateAction<DraftAnswer>>
  onSave: () => void
  onDelete: (id: string) => void
  onDraftAi: () => void
  aiBusy: boolean
  editingAnswerId: string
  setEditingAnswerId: (id: string) => void
}) {
  return (
    <div className="page-stack" style={{ maxWidth: 960 }}>
      <div className="card">
        <div className="card-header"><h2><Plus size={16} /> Nova resposta</h2></div>
        <div className="card-body">
          <div className="form-row form-row-4">
            <div className="form-group"><label className="form-label">Chave</label><input className="form-input" value={draftAnswer.questionKey} onChange={(e) => setDraftAnswer({ ...draftAnswer, questionKey: e.target.value })} placeholder="ex: salary" /></div>
            <div className="form-group"><label className="form-label">Pergunta</label><input className="form-input" value={draftAnswer.questionText} onChange={(e) => setDraftAnswer({ ...draftAnswer, questionText: e.target.value })} /></div>
            <div className="form-group" style={{ gridColumn: 'span 2' }}><label className="form-label">Resposta</label><input className="form-input" value={draftAnswer.answerValue} onChange={(e) => setDraftAnswer({ ...draftAnswer, answerValue: e.target.value })} /></div>
          </div>
          <div style={{ display: 'flex', gap: 8, marginTop: 4 }}>
            <button className="btn btn-secondary btn-sm" onClick={onDraftAi} disabled={aiBusy}><WandSparkles size={14} /> {aiBusy ? 'Gerando...' : 'Rascunho IA'}</button>
            <button className="btn btn-primary btn-sm" onClick={onSave}><Save size={14} /> {editingAnswerId ? 'Salvar alterações' : 'Salvar'}</button>
            {editingAnswerId && <button className="btn btn-secondary btn-sm" onClick={() => { setEditingAnswerId(''); setDraftAnswer({ questionKey: '', questionText: '', answerValue: '', category: 'screening' }) }}>Cancelar</button>}
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-header"><h2><ClipboardList size={16} /> Respostas salvas ({answers.length})</h2></div>
        <div className="card-body-sm" style={{ padding: 0 }}>
          {answers.length === 0 ? <div className="empty-state"><ClipboardList size={28} /><p>Nenhuma resposta salva.</p></div> :
            <table className="data-table">
              <thead><tr><th>Chave</th><th>Pergunta</th><th>Resposta</th><th>Categoria</th><th style={{ width: 40 }}></th></tr></thead>
              <tbody>
                {answers.map((a: Answer) => (
                  <tr key={a.id}>
                    <td><span className="badge badge-teal">{a.questionKey}</span></td>
                    <td style={{ maxWidth: 220, overflow: 'hidden', textOverflow: 'ellipsis' }}>{a.questionText}</td>
                    <td style={{ maxWidth: 300, overflow: 'hidden', textOverflow: 'ellipsis' }} title={a.answerValue}>{a.answerValue}</td>
                    <td><span className="badge badge-slate">{a.category}</span></td>
                    <td>
                      <button className="icon-btn" onClick={() => { setDraftAnswer({ questionKey: a.questionKey, questionText: a.questionText, answerValue: a.answerValue, category: a.category }); setEditingAnswerId(a.id) }} title="Editar"><ClipboardList size={14} /></button>
                      <button className="icon-btn" onClick={() => onDelete(a.id)} title="Excluir"><Trash2 size={14} /></button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          }
        </div>
      </div>
    </div>
  )
}

/* ── Filters ── */
function FiltersTab({ jobFilterConfig, setJobFilterConfig, validationIssues, patterns, draftPattern, setDraftPattern, onSaveFilter, onSavePattern, onDeletePattern, onTestPattern, patternTest, setPatternTest, onRevalidate, editingPatternId, setEditingPatternId }: {
  jobFilterConfig: JobFilter
  setJobFilterConfig: Dispatch<SetStateAction<JobFilter>>
  validationIssues: ProfileValidationIssue[]
  patterns: QuestionPattern[]
  draftPattern: DraftPattern
  setDraftPattern: Dispatch<SetStateAction<DraftPattern>>
  onSaveFilter: () => void
  onSavePattern: () => void
  onDeletePattern: (id: string) => void
  onTestPattern: () => void
  patternTest: PatternTest
  setPatternTest: Dispatch<SetStateAction<PatternTest>>
  onRevalidate: () => void
  editingPatternId: string
  setEditingPatternId: (id: string) => void
}) {
  return (
    <div className="page-stack" style={{ maxWidth: 1100 }}>
      <div className="page-grid-2">
        <div className="card">
          <div className="card-header"><h2><Filter size={16} /> Filtros de vaga</h2></div>
          <div className="card-body">
            <div className="form-row form-row-2">
              <div className="form-group">
                <label className="form-label">Senioridades permitidas</label>
                <ChipToggleGroup options={[{value:'junior',label:'Júnior'},{value:'pleno',label:'Pleno'},{value:'senior',label:'Sênior'}]} selected={jobFilterConfig.allowedSeniorities} onChange={(vals) => setJobFilterConfig((c: JobFilter) => ({ ...c, allowedSeniorities: vals }))} />
                <p className="form-hint">Usado para priorizar e revisar. Se o título não disser júnior, a vaga não é descartada automaticamente.</p>
              </div>
              <div className="form-group">
                <label className="form-label">Modelo de trabalho</label>
                <ChipToggleGroup options={[{value:'remote',label:'Remoto'},{value:'hybrid',label:'Híbrido'},{value:'onsite',label:'Presencial'}]} selected={jobFilterConfig.allowedWorkplaceTypes} onChange={(vals) => setJobFilterConfig((c: JobFilter) => ({ ...c, allowedWorkplaceTypes: vals }))} />
                <p className="form-hint">A busca no LinkedIn fica ampla. Brasil sem selo remoto entra para revisão; presencial/híbrido explícito ainda pode bloquear.</p>
              </div>
            </div>
            <div className="form-row form-row-2">
              <div className="form-group">
                <label className="form-label">Plataformas de candidatura</label>
                <ChipToggleGroup options={[{value:'linkedin_easy_apply',label:'LinkedIn simplificada'},{value:'inhire',label:'InHire'},{value:'unsupported_external',label:'Outros formularios'}]} selected={jobFilterConfig.allowedApplyTypes} onChange={(vals) => setJobFilterConfig((c: JobFilter) => ({ ...c, allowedApplyTypes: vals }))} />
              </div>
              <div className="form-group">
                <label className="form-label">Pontuação mínima</label>
                <input className="form-input" type="number" value={jobFilterConfig.minScore} onChange={(e) => setJobFilterConfig((c: JobFilter) => ({ ...c, minScore: Number(e.target.value) }))} />
                <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 10, fontSize: 12, fontWeight: 500, color: '#475569' }}>
                  <input type="checkbox" checked={jobFilterConfig.autoIgnoreLowScore} onChange={(e) => setJobFilterConfig((c: JobFilter) => ({ ...c, autoIgnoreLowScore: e.target.checked }))} />
                  Ignorar automaticamente abaixo da pontuação mínima
                </label>
              </div>
            </div>
            <div className="form-group">
              <label className="form-label">Senioridades bloqueadas <span style={{ fontWeight: 400, color: '#94a3b8' }}>(separadas por vírgula)</span></label>
              <input className="form-input" value={jobFilterConfig.blockedSeniorities.join(', ')} onChange={(e) => setJobFilterConfig((c: JobFilter) => ({ ...c, blockedSeniorities: e.target.value.split(',').map((x: string) => x.trim()).filter(Boolean) }))} />
            </div>
            <div className="form-row form-row-2">
              <div className="form-group">
                <label className="form-label">Cargos permitidos <span style={{ fontWeight: 400, color: '#94a3b8' }}>(separados por vírgula)</span></label>
                <input className="form-input" value={(jobFilterConfig.allowedRoleKeywords ?? []).join(', ')} onChange={(e) => setJobFilterConfig((c: JobFilter) => ({ ...c, allowedRoleKeywords: e.target.value.split(',').map((x: string) => x.trim()).filter(Boolean) }))} />
              </div>
              <div className="form-group">
                <label className="form-label">Cargos bloqueados <span style={{ fontWeight: 400, color: '#94a3b8' }}>(separados por vírgula)</span></label>
                <input className="form-input" value={(jobFilterConfig.blockedRoleKeywords ?? []).join(', ')} onChange={(e) => setJobFilterConfig((c: JobFilter) => ({ ...c, blockedRoleKeywords: e.target.value.split(',').map((x: string) => x.trim()).filter(Boolean) }))} />
              </div>
            </div>
            <button className="btn btn-primary" onClick={onSaveFilter} style={{ marginTop: 4 }}><Save size={14} /> Salvar filtros</button>
          </div>
        </div>

        <div className="card">
          <div className="card-header"><h2><ShieldCheck size={16} /> Validação do perfil</h2></div>
          <div className="card-body-sm">
            {validationIssues.length === 0 ? <div className="empty-state" style={{ padding: 32 }}><CheckCircle2 size={28} color="#16a34a" /><p>Todos os campos do perfil estão OK.</p></div> :
              validationIssues.map((issue: ProfileValidationIssue) => (
                <div key={issue.fieldName} className="list-row-modern" style={{ marginBottom: 6, borderColor: issue.isCritical ? '#fecaca' : '#fde68a' }}>
                  <div className="list-row-main">
                    <div className="list-row-title">{fieldNameLabels[issue.fieldName] ?? issue.fieldName}</div>
                    <div className="list-row-sub">{issue.errorMessage}</div>
                  </div>
                  <span className={`badge ${issue.isCritical ? 'badge-red' : 'badge-amber'}`}>{issue.isCritical ? 'Crítico' : 'Aviso'}</span>
                </div>
              ))
            }
            <button className="btn btn-secondary btn-sm" onClick={onRevalidate} style={{ marginTop: 10 }}><RefreshCw size={14} /> Revalidar</button>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="card-header"><h2><ClipboardList size={16} /> Padrões de pergunta ({patterns.length})</h2></div>
        <div className="card-body">
          <div style={{ background: '#f8fafc', borderRadius: 10, padding: 18, marginBottom: 20 }}>
            <div style={{ fontSize: 13, fontWeight: 700, color: '#0f172a', marginBottom: 12 }}>{editingPatternId ? 'Editar padrão' : 'Novo padrão'}</div>
            <div className="form-row form-row-4">
              <div className="form-group"><label className="form-label">Chave</label><input className="form-input" value={draftPattern.patternKey} onChange={(e) => setDraftPattern((p) => ({ ...p, patternKey: e.target.value }))} /></div>
              <div className="form-group">
                <label className="form-label">Categoria</label>
                <select className="form-select" value={draftPattern.category} onChange={(e) => setDraftPattern((p) => ({ ...p, category: e.target.value }))}>
                  <option value="screening">Triagem</option><option value="technical">Técnica</option><option value="behavioral">Comportamental</option><option value="general">Geral</option>
                </select>
              </div>
              <div className="form-group"><label className="form-label">Tags</label><input className="form-input" value={draftPattern.tags} onChange={(e) => setDraftPattern((p) => ({ ...p, tags: e.target.value }))} placeholder="salario, pretensao" /></div>
              <div className="form-group"><label className="form-label">Confiança mínima</label><input className="form-input" type="number" step={0.05} value={draftPattern.confidenceThreshold} onChange={(e) => setDraftPattern((p) => ({ ...p, confidenceThreshold: Number(e.target.value) }))} /></div>
            </div>
            <div className="form-row form-row-2">
              <div className="form-group"><label className="form-label">Modelos de pergunta <span style={{ fontWeight: 400, color: '#94a3b8' }}>(um por linha)</span></label><textarea className="form-textarea" style={{ minHeight: 70 }} value={draftPattern.questionTemplates} onChange={(e) => setDraftPattern((p) => ({ ...p, questionTemplates: e.target.value }))} /></div>
              <div className="form-group"><label className="form-label">Modelo de resposta</label><textarea className="form-textarea" style={{ minHeight: 70 }} value={draftPattern.answerTemplate} onChange={(e) => setDraftPattern((p) => ({ ...p, answerTemplate: e.target.value }))} /></div>
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 4 }}>
              <button className="btn btn-secondary btn-sm" onClick={onTestPattern}><Search size={14} /> Testar</button>
              <button className="btn btn-primary btn-sm" onClick={onSavePattern}><Save size={14} /> {editingPatternId ? 'Salvar alterações' : 'Salvar padrão'}</button>
              {editingPatternId && <button className="btn btn-secondary btn-sm" onClick={() => { setEditingPatternId(''); setDraftPattern({ patternKey: '', category: 'screening', tags: '', questionTemplates: '', answerTemplate: '', valueSource: 'profile_field', profileFieldMapping: '', confidenceThreshold: 0.75 }) }}>Cancelar</button>}
            </div>
          </div>

          {patterns.length > 0 && (
            <table className="data-table">
              <thead><tr><th>Chave</th><th>Categoria</th><th>Fonte</th><th>Usos</th><th style={{ width: 40 }}></th></tr></thead>
              <tbody>
                {patterns.map((p: QuestionPattern) => (
                  <tr key={p.id}>
                    <td><b style={{ fontWeight: 600 }}>{p.patternKey}</b></td>
                    <td><span className="badge badge-slate">{p.category}</span></td>
                    <td>{valueSourceLabels[p.valueSource] ?? p.valueSource}</td>
                    <td>{p.useCount}</td>
                    <td style={{ whiteSpace: 'nowrap' }}>
                      <button className="icon-btn" onClick={() => { setDraftPattern({ patternKey: p.patternKey, category: p.category, tags: Array.isArray(p.tags) ? p.tags.join(', ') : p.tags, questionTemplates: Array.isArray(p.questionTemplates) ? p.questionTemplates.join('\n') : p.questionTemplates, answerTemplate: p.answerTemplate, valueSource: p.valueSource, profileFieldMapping: p.profileFieldMapping || '', confidenceThreshold: p.confidenceThreshold }); setEditingPatternId(p.id) }} title="Editar"><ClipboardList size={14} /></button>
                      <button className="icon-btn" onClick={() => onDeletePattern(p.id)} title="Excluir"><Trash2 size={14} /></button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <div style={{ marginTop: 18, display: 'flex', gap: 8, alignItems: 'end' }}>
            <div className="form-group" style={{ flex: 1, marginBottom: 0 }}>
              <label className="form-label">Testar pergunta</label>
              <input className="form-input" value={patternTest.question} onChange={(e) => setPatternTest((p) => ({ ...p, question: e.target.value }))} placeholder="Digite uma pergunta para testar o matching..." />
            </div>
            <button className="btn btn-secondary btn-sm" onClick={onTestPattern}><WandSparkles size={14} /> Testar</button>
          </div>
          {patternTest.result && <pre style={{ marginTop: 10, padding: 12, background: '#f8fafc', borderRadius: 8, fontSize: 12, color: '#334155' }}>{patternTest.result}</pre>}
        </div>
      </div>
    </div>
  )
}

/* ── Credentials ── */
function CredentialsTab({ platformCreds, draft, setDraft, onSave, onDelete, editingId, setEditingId }: {
  platformCreds: PlatformCredential[]
  draft: DraftPlatformCredential
  setDraft: Dispatch<SetStateAction<DraftPlatformCredential>>
  onSave: () => void
  onDelete: (id: string) => void
  editingId: string
  setEditingId: (id: string) => void
}) {
  return (
    <div className="page-stack" style={{ maxWidth: 1000 }}>
      <div className="card">
        <div className="card-header"><h2><ShieldCheck size={16} /> Contas de Plataforma</h2></div>
        <div className="card-body">
          <div style={{ background: '#f8fafc', borderRadius: 10, padding: 18, marginBottom: 20 }}>
            <div style={{ fontSize: 13, fontWeight: 700, color: '#0f172a', marginBottom: 12 }}>{editingId ? 'Editar conta' : 'Nova conta'}</div>
            <div style={{ display: 'grid', gap: 12 }}>
              <div>
                <label style={{ fontSize: 12, fontWeight: 600, color: '#475569' }}>Plataforma</label>
                <select value={draft.platform} onChange={(e) => setDraft({ ...draft, platform: e.target.value })} style={{ width: '100%', marginTop: 4, padding: '8px 10px', borderRadius: 8, border: '1px solid #cbd5e1', fontSize: 13 }}>
                  <option value="linkedin">LinkedIn</option>
                  <option value="indeed">Indeed</option>
                  <option value="glassdoor">Glassdoor</option>
                </select>
              </div>
              <div>
                <label style={{ fontSize: 12, fontWeight: 600, color: '#475569' }}>E-mail / Usuário</label>
                <input type="text" value={draft.accountEmail} onChange={(e) => setDraft({ ...draft, accountEmail: e.target.value })} placeholder="contato@exemplo.com" style={{ width: '100%', marginTop: 4, padding: '8px 10px', borderRadius: 8, border: '1px solid #cbd5e1', fontSize: 13 }} />
              </div>
              <div>
                <label style={{ fontSize: 12, fontWeight: 600, color: '#475569' }}>Senha {editingId ? '(deixe em branco para manter)' : ''}</label>
                <input type="password" value={draft.password} onChange={(e) => setDraft({ ...draft, password: e.target.value })} placeholder="Senha" style={{ width: '100%', marginTop: 4, padding: '8px 10px', borderRadius: 8, border: '1px solid #cbd5e1', fontSize: 13 }} />
              </div>
              <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: '#334155' }}>
                <input type="checkbox" checked={draft.isActive} onChange={(e) => setDraft({ ...draft, isActive: e.target.checked })} /> Ativa
              </label>
              <div style={{ display: 'flex', gap: 8, marginTop: 4 }}>
                <button className="btn btn-primary" onClick={onSave}><Save size={14} /> {editingId ? 'Atualizar' : 'Salvar'}</button>
                {editingId && <button className="btn btn-secondary" onClick={() => { setEditingId(''); setDraft({ platform: 'linkedin', accountEmail: '', password: '', isActive: true }) }}>Cancelar</button>}
              </div>
            </div>
          </div>

          <div style={{ fontSize: 13, fontWeight: 700, color: '#0f172a', marginBottom: 12 }}>Contas salvas</div>
          {platformCreds.length === 0 && <div style={{ color: '#94a3b8', fontSize: 13 }}>Nenhuma conta configurada.</div>}
          {platformCreds.map((cred: PlatformCredential) => (
            <div key={cred.id} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 14px', borderRadius: 10, background: '#fff', border: '1px solid #e2e8f0', marginBottom: 10 }}>
              <div>
                <div style={{ fontWeight: 700, fontSize: 13, color: '#0f172a', textTransform: 'capitalize' }}>{cred.platform}</div>
                <div style={{ fontSize: 12, color: '#64748b' }}>{cred.accountEmail} {cred.hasPassword ? '• senha salva' : '• sem senha'}</div>
              </div>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <span style={{ fontSize: 11, padding: '2px 8px', borderRadius: 12, background: cred.isActive ? '#dcfce7' : '#f1f5f9', color: cred.isActive ? '#166534' : '#64748b' }}>{cred.isActive ? 'Ativa' : 'Inativa'}</span>
                <button className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => { setEditingId(cred.id); setDraft({ platform: cred.platform, accountEmail: cred.accountEmail, password: '', isActive: cred.isActive }) }}><ExternalLink size={12} /></button>
                <button className="btn btn-danger" style={{ padding: '4px 8px' }} onClick={() => onDelete(cred.id)}><Trash2 size={12} /></button>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

/* ── AI ── */
function AiTab({ providers, draftProvider, setDraftProvider, policy, setPolicy, onSaveProvider, onDeleteProvider, onMakeDefault, onTestProvider, providerStatus, onSavePolicy, editingProviderId, setEditingProviderId }: {
  providers: AiProvider[]
  draftProvider: DraftProvider
  setDraftProvider: Dispatch<SetStateAction<DraftProvider>>
  policy: AgentPolicy
  setPolicy: Dispatch<SetStateAction<AgentPolicy>>
  onSaveProvider: () => void
  onDeleteProvider: (id: string) => void
  onMakeDefault: (provider: AiProvider) => void
  onTestProvider: (id: string) => void
  providerStatus: string
  onSavePolicy: () => void
  editingProviderId: string
  setEditingProviderId: (id: string) => void
}) {
  return (
    <div className="page-stack" style={{ maxWidth: 1000 }}>
      <div className="card">
        <div className="card-header"><h2><KeyRound size={16} /> Provedores de IA</h2></div>
        <div className="card-body">
          <div style={{ background: '#f8fafc', borderRadius: 10, padding: 18, marginBottom: 20 }}>
            <div style={{ fontSize: 13, fontWeight: 700, color: '#0f172a', marginBottom: 12 }}>{editingProviderId ? 'Editar provedor' : 'Novo provedor'}</div>
            <div className="form-row form-row-3">
              <div className="form-group"><label className="form-label">Nome</label><input className="form-input" value={draftProvider.name} onChange={(e) => setDraftProvider({ ...draftProvider, name: e.target.value })} /></div>
              <div className="form-group">
                <label className="form-label">Tipo</label>
                <select className="form-select" value={draftProvider.providerType} onChange={(e) => setDraftProvider({ ...draftProvider, providerType: e.target.value })}>
                  <option value="ollama">Ollama local</option><option value="kimi">Kimi</option><option value="openai-compatible">OpenAI-compativel</option><option value="anthropic">Anthropic</option><option value="google">Google</option>
                </select>
              </div>
              <div className="form-group"><label className="form-label">Modelo</label><input className="form-input" value={draftProvider.model} onChange={(e) => setDraftProvider({ ...draftProvider, model: e.target.value })} /></div>
            </div>
            <div className="form-row form-row-2">
              <div className="form-group"><label className="form-label">URL Base</label><input className="form-input" value={draftProvider.baseUrl} onChange={(e) => setDraftProvider({ ...draftProvider, baseUrl: e.target.value })} /></div>
              <div className="form-group">
                <label className="form-label">Formato da API</label>
                <select className="form-select" value={draftProvider.apiFormat} onChange={(e) => setDraftProvider({ ...draftProvider, apiFormat: e.target.value })}>
                  <option value="ollama-chat">Ollama Chat</option><option value="anthropic-messages">Mensagens Anthropic</option><option value="openai-compatible">Chat Completions</option>
                </select>
              </div>
            </div>
            <div className="form-row form-row-2">
              <div className="form-group"><label className="form-label">Chave da API</label><input className="form-input" type="password" value={draftProvider.apiKey} onChange={(e) => setDraftProvider({ ...draftProvider, apiKey: e.target.value })} placeholder="sk-..." /></div>
              <div style={{ display: 'flex', gap: 12, alignItems: 'end', paddingBottom: 4 }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, fontWeight: 500, color: '#475569' }}><input type="checkbox" checked={draftProvider.isActive} onChange={(e) => setDraftProvider({ ...draftProvider, isActive: e.target.checked })} /> Ativo</label>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, fontWeight: 500, color: '#475569' }}><input type="checkbox" checked={draftProvider.isDefault} onChange={(e) => setDraftProvider({ ...draftProvider, isDefault: e.target.checked })} /> Padrão</label>
              </div>
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 4 }}>
              <button className="btn btn-primary btn-sm" onClick={onSaveProvider}><Save size={14} /> {editingProviderId ? 'Salvar alterações' : 'Adicionar provedor'}</button>
              {editingProviderId && <button className="btn btn-secondary btn-sm" onClick={() => { setEditingProviderId(''); setDraftProvider({ ...emptyProvider, apiKey: '' }) }}>Cancelar</button>}
            </div>
          </div>

          {providers.length > 0 && (
            <table className="data-table">
              <thead><tr><th>Nome</th><th>Modelo</th><th>Chave</th><th>Status</th><th style={{ width: 140 }}></th></tr></thead>
              <tbody>
                {providers.map((provider: AiProvider) => (
                  <tr key={provider.id}>
                    <td><b style={{ fontWeight: 600 }}>{provider.name}</b><br /><small style={{ color: '#94a3b8' }}>{provider.providerType}</small></td>
                    <td style={{ fontSize: 13 }}>{provider.model}</td>
                    <td>{provider.hasApiKey ? <span className="badge badge-green">Salva</span> : <span className="badge badge-slate">Sem chave</span>}</td>
                    <td><span className={`badge ${provider.isDefault ? 'badge-purple' : provider.isActive ? 'badge-blue' : 'badge-slate'}`}>{provider.isDefault ? 'Padrão' : provider.isActive ? 'Ativo' : 'Inativo'}</span></td>
                    <td style={{ whiteSpace: 'nowrap' }}>
                      <button className="btn btn-secondary btn-sm" onClick={() => { setDraftProvider({ name: provider.name, providerType: provider.providerType, baseUrl: provider.baseUrl, model: provider.model, apiFormat: provider.apiFormat, apiKey: '', isActive: provider.isActive, isDefault: provider.isDefault }); setEditingProviderId(provider.id) }}>Editar</button>{' '}
                      <button className="btn btn-secondary btn-sm" onClick={() => onTestProvider(provider.id)}>Testar</button>{' '}
                      {!provider.isDefault && <button className="btn btn-secondary btn-sm" onClick={() => onMakeDefault(provider)}>Padrão</button>}{' '}
                      <button className="icon-btn" onClick={() => onDeleteProvider(provider.id)}><Trash2 size={14} /></button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {providerStatus && <p style={{ marginTop: 10, fontSize: 13, color: '#64748b' }}>{providerStatus.replace('Provider ok','Provedor OK').replace('Provider failed','Falhou').replace('Testing provider','Testando...')}</p>}
        </div>
      </div>

      <div className="card">
        <div className="card-header"><h2><Zap size={16} /> Política do piloto automático</h2></div>
        <div className="card-body">
          <div className="form-row form-row-4">
            <div className="form-group">
              <label className="form-label">Modo</label>
              <select className="form-select" value={policy.autoSubmitMode} onChange={(e) => setPolicy({ ...policy, autoSubmitMode: e.target.value })}>
                <option value="conservative">Conservador</option><option value="aggressive">Agressivo</option><option value="review_only">Somente revisar</option>
              </select>
            </div>
            <div className="form-group">
              <label className="form-label">Alvo</label>
              <select className="form-select" value={normalizeExecutionTarget(policy.executionTarget)} onChange={(e) => setPolicy({ ...policy, executionTarget: normalizeExecutionTarget(e.target.value) })}>
                <option value="extension">Extensao Chrome</option>
                <option value="local_dedicated_browser">Navegador local dedicado</option>
                <option value="remote_browser">Worker remoto (em breve)</option>
                <option value="server_connector">Conector server-side (em breve)</option>
              </select>
            </div>
            <div className="form-group"><label className="form-label">Máx por execução</label><input className="form-input" type="number" value={policy.maxApplicationsPerRun} onChange={(e) => setPolicy({ ...policy, maxApplicationsPerRun: Number(e.target.value) || 0 })} /></div>
            <div className="form-group"><label className="form-label">Máx por dia</label><input className="form-input" type="number" value={policy.maxApplicationsPerDay} onChange={(e) => setPolicy({ ...policy, maxApplicationsPerDay: Number(e.target.value) || 0 })} /></div>
            <div className="form-group" style={{ display: 'flex', alignItems: 'end', paddingBottom: 4 }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, fontWeight: 500, color: '#475569' }}><input type="checkbox" checked={policy.isPaused} onChange={(e) => setPolicy({ ...policy, isPaused: e.target.checked })} /> Pausado</label>
            </div>
          </div>
          <div className="form-row form-row-2">
            <div className="form-group"><label className="form-label">Domínios permitidos</label><input className="form-input" value={policy.allowedDomains.join(', ')} onChange={(e) => setPolicy({ ...policy, allowedDomains: e.target.value.split(',').map((x: string) => x.trim()).filter(Boolean) })} /></div>
            <div className="form-group"><label className="form-label">Domínios bloqueados</label><input className="form-input" value={policy.blockedDomains.join(', ')} onChange={(e) => setPolicy({ ...policy, blockedDomains: e.target.value.split(',').map((x: string) => x.trim()).filter(Boolean) })} /></div>
          </div>
          <button className="btn btn-primary btn-sm" onClick={onSavePolicy} style={{ marginTop: 4 }}><Save size={14} /> Salvar política</button>
        </div>
      </div>
    </div>
  )
}

/* ── Agent ── */
function AgentTab({ events, checkpoints, agentHistory, agentToken, onPair, onDiscover, onAutopilot, onResolveCheckpoint, automationBusy, automationNotice, agentTab, setAgentTab }: {
  events: EventLog[]
  checkpoints: Checkpoint[]
  agentHistory: AgentHistory
  agentToken: string
  onPair: () => void
  onDiscover: () => void
  onAutopilot: () => void
  onResolveCheckpoint: (id: string, action?: string, value?: string) => void
  automationBusy: string
  automationNotice: string
  agentTab: 'events' | 'history'
  setAgentTab: (tab: 'events' | 'history') => void
}) {
  const answerReviews = checkpoints.filter((cp) => cp.reasonCode === 'answer_needs_confirmation')
  const [draftResolutions, setDraftResolutions] = useState<Record<string, string>>({})
  const resolutionValue = (checkpoint: Checkpoint) => draftResolutions[checkpoint.id] ?? extractCheckpointDraftValue(checkpoint)
  const setResolutionValue = (checkpoint: Checkpoint, value: string) => setDraftResolutions((current) => ({ ...current, [checkpoint.id]: value }))
  const saveAnswerReview = async (checkpoint: Checkpoint) => {
    const value = resolutionValue(checkpoint).trim()
    if (!value) return
    await onResolveCheckpoint(checkpoint.id, 'save_answer', value)
    setDraftResolutions((current) => {
      const next = { ...current }
      delete next[checkpoint.id]
      return next
    })
  }
  return (
    <div className="page-stack" style={{ maxWidth: 1100 }}>
      <div className="page-grid-2">
        <div className="card">
          <div className="card-header"><h2><Bot size={16} /> Diagnostico avancado</h2></div>
          <div className="card-body">
            <p style={{ fontSize: 13, color: '#64748b', lineHeight: 1.6, margin: '0 0 16px' }}>
              Esta area fica para diagnostico e compatibilidade do runtime local. O fluxo principal para o usuario comum fica no Cockpit com a extensao do navegador.
            </p>
            <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 16 }}>
              <button className="btn btn-primary" onClick={onDiscover} disabled={automationBusy !== ''}><Search size={16} /> {automationBusy === 'discover' ? 'Buscando...' : 'Buscar vagas'}</button>
              <button className="btn btn-primary" onClick={onAutopilot} disabled={automationBusy !== ''}><Play size={16} /> {automationBusy === 'apply' ? 'Executando...' : 'Piloto automático'}</button>
            </div>
            {automationNotice && (
              <div style={{ border: '1px solid #bfdbfe', background: '#eff6ff', color: '#1e40af', borderRadius: 8, padding: '10px 12px', fontSize: 13, lineHeight: 1.5, marginBottom: 16 }}>
                {automationNotice}
              </div>
            )}
            <button className="btn btn-secondary btn-sm" onClick={onPair}><KeyRound size={14} /> Verificar runtime local</button>
            {agentToken && (
              <div className="notice-ok" style={{ marginTop: 14 }}>
                Runtime local validado nesta sessao. Para o usuario comum, continue pelo Cockpit e pela extensao do navegador.
              </div>
            )}
          </div>
        </div>

        <div className="card">
          <div className="card-header"><h2><MessageSquare size={16} /> Pendências do agente</h2></div>
          <div className="card-body-sm">
            {answerReviews.length === 0 ? (
              <div className="empty-state" style={{ padding: 32 }}><CheckCircle2 size={28} /><p>Nenhuma resposta provisória pendente.</p></div>
            ) : answerReviews.slice(0, 8).map((checkpoint) => (
              <div className="checkpoint-item" key={checkpoint.id}>
                <div className="checkpoint-title">{checkpoint.fieldLabel || 'Pergunta sem rótulo'}</div>
                <div className="checkpoint-meta">{new Date(checkpoint.createdAt).toLocaleString()}</div>
                <div className="checkpoint-desc">{checkpoint.detail}</div>
                <textarea
                  className="form-textarea"
                  value={resolutionValue(checkpoint)}
                  onChange={(event) => setResolutionValue(checkpoint, event.target.value)}
                  rows={3}
                  style={{ marginTop: 10 }}
                />
                <div className="checkpoint-actions">
                  <button className="btn btn-primary btn-sm" onClick={() => void saveAnswerReview(checkpoint)} disabled={!resolutionValue(checkpoint).trim()}>
                    <Save size={14} /> Salvar resposta
                  </button>
                  <button className="btn btn-secondary btn-sm" onClick={() => onResolveCheckpoint(checkpoint.id)}>
                    <CheckCircle2 size={14} /> Marcar feito
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="card">
          <div className="card-header" style={{ padding: 0 }}>
            <div className="card-tabs" style={{ margin: 8 }}>
              <button className={`card-tab ${agentTab === 'events' ? 'active' : ''}`} onClick={() => setAgentTab('events')}><Terminal size={14} /> Eventos</button>
              <button className={`card-tab ${agentTab === 'history' ? 'active' : ''}`} onClick={() => setAgentTab('history')}><Clock size={14} /> Histórico</button>
            </div>
          </div>
          <div className="card-body-sm">
            {agentTab === 'events' ? (
              events.length === 0 ? <div className="empty-state" style={{ padding: 32 }}><Terminal size={28} /><p>Nenhum evento registrado.</p></div> :
                events.slice(0, 20).map((event: EventLog) => (
                  <div className="event-item" key={event.id}>
                    <div className="event-title">{eventTypeLabels[event.eventType] ?? event.eventType}</div>
                    <div className="event-meta">{new Date(event.occurredAt).toLocaleString()}</div>
                    <div className="event-desc">{event.detail}</div>
                  </div>
                ))
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                {agentHistory.steps.length === 0 && agentHistory.fields.length === 0 && agentHistory.memories.length === 0 ? <div className="empty-state" style={{ padding: 32 }}><Clock size={28} /><p>Sem histórico de execução.</p></div> : null}
                {agentHistory.steps.length > 0 && (
                  <div>
                    <div style={{ fontSize: 12, fontWeight: 700, color: '#94a3b8', textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 8 }}>Passos</div>
                    {agentHistory.steps.slice(0, 6).map((step) => (
                      <div className="event-item" key={step.id}>
                        <div className="event-title">{step.stepNumber}. {actionTypeLabels[step.actionType] ?? step.actionType} · {pageStateLabels[step.pageState] ?? step.pageState}</div>
                        <div className="event-meta">{step.agentName} · confiança {Number(step.confidence).toFixed(2)}</div>
                        {step.targetLabel && <div className="event-desc">{step.targetLabel}</div>}
                      </div>
                    ))}
                  </div>
                )}
                {agentHistory.fields.length > 0 && (
                  <div>
                    <div style={{ fontSize: 12, fontWeight: 700, color: '#94a3b8', textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: 8 }}>Campos preenchidos</div>
                    {agentHistory.fields.slice(0, 6).map((field) => (
                      <div className="event-item" key={field.id}>
                        <div className="event-title">{field.label}</div>
                        <div className="event-meta">{fieldTypeLabels[field.fieldType] ?? field.fieldType} · {valueSourceLabels[field.valueSource] ?? field.valueSource} · confiança {Number(field.confidence).toFixed(2)}</div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

/* ── Helpers ── */
function LearningsTab({ learnings, allLearnings, filter, setFilter, typeFilter, setTypeFilter, riskFilter, setRiskFilter, domainFilter, setDomainFilter, activeId, setActiveId, onReview }: {
  learnings: AgentLearning[]
  allLearnings: AgentLearning[]
  filter: string
  setFilter: (value: string) => void
  typeFilter: string
  setTypeFilter: (value: string) => void
  riskFilter: string
  setRiskFilter: (value: string) => void
  domainFilter: string
  setDomainFilter: (value: string) => void
  activeId: string
  setActiveId: (id: string) => void
  onReview: (id: string, action: 'approve' | 'reject' | 'disable') => void
}) {
  const active = allLearnings.find((item) => item.id === activeId) ?? learnings[0]
  const counts = {
    candidate: allLearnings.filter((x: AgentLearning) => x.status === 'candidate').length,
    auto: allLearnings.filter((x: AgentLearning) => x.status === 'auto_approved').length,
    approved: allLearnings.filter((x: AgentLearning) => x.status === 'approved').length,
    disabled: allLearnings.filter((x: AgentLearning) => x.status === 'disabled').length,
  }
  return (
    <div className="page-stack">
      <div className="metrics-grid">
        <MetricCard icon={<Lightbulb size={18} />} label="Candidatos" value={counts.candidate} tone="amber" />
        <MetricCard icon={<CheckCircle2 size={18} />} label="Auto-aprovados" value={counts.auto} tone="green" />
        <MetricCard icon={<ShieldCheck size={18} />} label="Aprovados" value={counts.approved} tone="blue" />
        <MetricCard icon={<CircleAlert size={18} />} label="Desativados" value={counts.disabled} tone="red" />
      </div>
      <div className="page-grid-2">
        <div className="card">
          <div className="card-header"><h2><Lightbulb size={16} /> Aprendizados</h2></div>
          <div className="card-body-sm">
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 12 }}>
              {['all', 'candidate', 'auto_approved', 'approved', 'rejected', 'disabled'].map((status) => (
                <button key={status} className={`btn btn-sm ${filter === status ? 'btn-primary' : 'btn-secondary'}`} onClick={() => setFilter(status)}>{learningStatusLabel(status)}</button>
              ))}
            </div>
            <div className="form-row-3" style={{ marginBottom: 12 }}>
              <label>Tipo
                <select value={typeFilter} onChange={(e) => setTypeFilter(e.target.value)}>
                  {['all', 'technical', 'format', 'answer', 'sensitive', 'workflow'].map((type) => <option key={type} value={type}>{type === 'all' ? 'Todos' : type}</option>)}
                </select>
              </label>
              <label>Risco
                <select value={riskFilter} onChange={(e) => setRiskFilter(e.target.value)}>
                  {['all', 'low', 'medium', 'high'].map((risk) => <option key={risk} value={risk}>{risk === 'all' ? 'Todos' : risk}</option>)}
                </select>
              </label>
              <label>Domínio
                <input value={domainFilter} onChange={(e) => setDomainFilter(e.target.value)} placeholder="linkedin.com" />
              </label>
            </div>
            {learnings.length === 0 ? <div className="empty-state" style={{ padding: 32 }}><Lightbulb size={28} /><p>Nenhum aprendizado neste filtro.</p></div> :
              learnings.map((learning: AgentLearning) => (
                <button key={learning.id} onClick={() => setActiveId(learning.id)} style={{ width: '100%', textAlign: 'left', border: active?.id === learning.id ? '1px solid #2563eb' : '1px solid #e2e8f0', background: '#fff', borderRadius: 10, padding: 12, marginBottom: 10, cursor: 'pointer' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: 10 }}>
                    <strong style={{ color: '#0f172a', fontSize: 13 }}>{learning.title}</strong>
                    <span className={`badge ${learning.status === 'candidate' ? 'badge-amber' : learning.status.includes('approved') ? 'badge-green' : learning.status === 'disabled' ? 'badge-red' : 'badge-slate'}`}>{learningStatusLabel(learning.status)}</span>
                  </div>
                  <div style={{ color: '#64748b', fontSize: 12, marginTop: 5 }}>{learning.domain || 'dominio desconhecido'} · {learning.learningType} · risco {learning.riskLevel}</div>
                  <div style={{ color: '#94a3b8', fontSize: 12, marginTop: 5 }}>sucessos {learning.successCount} · falhas {learning.failureCount}</div>
                </button>
              ))}
          </div>
        </div>
        <div className="card">
          <div className="card-header"><h2><ClipboardList size={16} /> Evidência e revisão</h2></div>
          <div className="card-body">
            {!active ? <div className="empty-state"><Lightbulb size={28} /><p>Selecione um aprendizado.</p></div> : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
                <div>
                  <h3 style={{ margin: 0, fontSize: 18, color: '#0f172a' }}>{active.title}</h3>
                  <p style={{ margin: '8px 0 0', color: '#475569', fontSize: 13, lineHeight: 1.5 }}>{active.summary || 'Sem resumo registrado.'}</p>
                </div>
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  <span className="badge badge-blue">{active.learningType}</span>
                  <span className={active.riskLevel === 'low' ? 'badge badge-green' : active.riskLevel === 'high' ? 'badge badge-red' : 'badge badge-amber'}>risco {active.riskLevel}</span>
                  <span className="badge badge-slate">{active.patternKey}</span>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                  <JsonBox title="Gatilho" value={active.triggerJson} />
                  <JsonBox title="Ação" value={active.actionJson} />
                </div>
                <JsonBox title="Evidência" value={active.evidenceJson} />
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  <button className="btn btn-primary btn-sm" onClick={() => onReview(active.id, 'approve')} disabled={active.status === 'approved'}><CheckCircle2 size={14} /> Aprovar</button>
                  <button className="btn btn-secondary btn-sm" onClick={() => onReview(active.id, 'reject')} disabled={active.status === 'rejected'}><CircleAlert size={14} /> Rejeitar</button>
                  <button className="btn btn-danger btn-sm" onClick={() => onReview(active.id, 'disable')} disabled={active.status === 'disabled'}><Trash2 size={14} /> Desativar</button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

type TraceJson = Record<string, unknown>

function TraceTimeline({ steps }: { steps: AgentTraceStep[] }) {
  const [query, setQuery] = useState('')
  const [riskFilter, setRiskFilter] = useState('all')
  const [plannerFilter, setPlannerFilter] = useState('all')
  const [statusFilter, setStatusFilter] = useState('all')

  const items = useMemo(() => steps.map(toTraceViewModel), [steps])
  const summary = useMemo(() => summarizeTrace(items), [items])
  const planners = useMemo(() => Array.from(new Set(items.map((item) => item.step.planner).filter(Boolean))).sort(), [items])
  const filteredItems = useMemo(() => {
    const needle = query.trim().toLowerCase()
    return items.filter((item) => {
      const matchesQuery = !needle || item.searchable.includes(needle)
      const matchesRisk = riskFilter === 'all' || (riskFilter === 'high' ? riskRank(item.riskLevel) >= 3 : item.riskLevel === riskFilter)
      const matchesPlanner = plannerFilter === 'all' || item.step.planner === plannerFilter
      const matchesStatus =
        statusFilter === 'all' ||
        (statusFilter === 'checkpoint' && item.hasCheckpoint) ||
        (statusFilter === 'blocked' && item.isBlocked) ||
        (statusFilter === 'safety' && item.signals.length > 0) ||
        (statusFilter === 'submit' && item.isSubmitBoundary) ||
        (statusFilter === 'success' && item.outcomeStatus.toLowerCase().includes('validated'))
      return matchesQuery && matchesRisk && matchesPlanner && matchesStatus
    })
  }, [items, plannerFilter, query, riskFilter, statusFilter])

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(118px, 1fr))', gap: 8 }}>
        <TraceMetric label="Steps" value={summary.total} />
        <TraceMetric label="Checkpoints" value={summary.checkpoints} tone={summary.checkpoints > 0 ? 'amber' : 'slate'} />
        <TraceMetric label="Bloqueios" value={summary.blocked} tone={summary.blocked > 0 ? 'red' : 'slate'} />
        <TraceMetric label="Risco alto" value={summary.highRisk} tone={summary.highRisk > 0 ? 'red' : 'slate'} />
        <TraceMetric label="Safety" value={summary.safetySignals} tone={summary.safetySignals > 0 ? 'amber' : 'slate'} />
        <TraceMetric label="Submit" value={summary.submitBoundaries} tone={summary.submitBoundaries > 0 ? 'blue' : 'slate'} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 8 }}>
        <label style={{ position: 'relative', minWidth: 0 }}>
          <Search size={14} style={{ position: 'absolute', left: 10, top: 10, color: '#94a3b8' }} />
          <input className="form-input" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Buscar no trace" style={{ paddingLeft: 32 }} />
        </label>
        <select className="form-select" value={riskFilter} onChange={(event) => setRiskFilter(event.target.value)}>
          <option value="all">Todos os riscos</option>
          <option value="high">Alto ou crítico</option>
          <option value="Medium">Médio</option>
          <option value="Low">Baixo</option>
          <option value="None">Sem risco</option>
        </select>
        <select className="form-select" value={plannerFilter} onChange={(event) => setPlannerFilter(event.target.value)}>
          <option value="all">Todos planners</option>
          {planners.map((planner) => <option key={planner} value={planner}>{plannerLabels[planner] ?? planner}</option>)}
        </select>
        <select className="form-select" value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
          <option value="all">Todos status</option>
          <option value="checkpoint">Checkpoints</option>
          <option value="blocked">Bloqueados</option>
          <option value="safety">Safety signals</option>
          <option value="submit">Submit boundary</option>
          <option value="success">Validados</option>
        </select>
      </div>

      {filteredItems.length === 0 ? (
        <div style={{ padding: 18, border: '1px solid #e2e8f0', borderRadius: 8, color: '#64748b', fontSize: 13, background: '#f8fafc' }}>Nenhum step corresponde aos filtros.</div>
      ) : filteredItems.map((item) => {
        const step = item.step
        return (
          <details key={step.id} open={Boolean(query.trim()) || item.opened} style={{ border: '1px solid #e2e8f0', borderRadius: 8, background: '#fff', overflow: 'hidden', contentVisibility: 'auto' }}>
            <summary style={{ listStyle: 'none', cursor: 'pointer', padding: '10px 12px', display: 'grid', gridTemplateColumns: 'auto 1fr auto', gap: 10, alignItems: 'center' }}>
              <span style={{ width: 34, height: 34, borderRadius: 8, display: 'grid', placeItems: 'center', background: '#f8fafc', border: '1px solid #e2e8f0', fontSize: 12, fontWeight: 800, color: '#0f172a' }}>#{step.stepNumber}</span>
              <span style={{ minWidth: 0 }}>
                <span style={{ display: 'block', color: '#0f172a', fontSize: 13, fontWeight: 800, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {step.objectiveKind || 'objective'} · {step.phase || 'runtime'} · {plannerLabels[step.planner] ?? (step.planner || 'planner')}
                </span>
                <span style={{ display: 'block', color: '#64748b', fontSize: 12, marginTop: 2, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {step.platform || 'platform'} / {step.module || 'module'} · {actionTypeLabels[item.actionType] ?? (item.actionType || 'ação')}{item.actionLabel ? ` · ${item.actionLabel}` : ''}
                </span>
              </span>
              <span style={{ display: 'flex', gap: 6, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                {item.isSubmitBoundary && <span className="badge badge-blue" style={{ fontSize: 10 }}>submit</span>}
                <span className={`badge ${riskBadgeClass(item.riskLevel)}`} style={{ fontSize: 10 }}>{riskLevelLabels[item.riskLevel] ?? (item.riskLevel || 'Risco')}</span>
                <span className="badge badge-slate" style={{ fontSize: 10 }}>{guardVerdictLabels[item.verdictCode] ?? (item.verdictCode || 'veredito')}</span>
              </span>
            </summary>
            <div style={{ borderTop: '1px solid #e2e8f0', padding: 12, display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 10 }}>
                <TraceField label="Objetivo" value={step.objectiveKind} />
                <TraceField label="Planner" value={plannerLabels[step.planner] ?? step.planner} />
                <TraceField label="Proposta" value={`${actionTypeLabels[item.actionType] ?? (item.actionType || 'ação')}${item.actionLabel ? ` · ${item.actionLabel}` : ''}`} />
                <TraceField label="Risco" value={riskLevelLabels[item.riskLevel] ?? item.riskLevel} />
                <TraceField label="Guard" value={guardVerdictLabels[item.verdictCode] ?? item.verdictCode} />
                <TraceField label="Execução" value={item.executionStatus || 'registrada'} />
                <TraceField label="Outcome" value={item.outcomeStatus || 'sem status'} />
                <TraceField label="Recovery" value={recoveryModeLabels[item.recoveryMode] ?? (item.recoveryMode || 'Nenhuma')} />
                <TraceField label="Antes" value={step.beforeFingerprint || 'sem fingerprint'} mono />
                <TraceField label="Depois" value={step.afterFingerprint || 'sem fingerprint'} mono />
              </div>

              {item.signals.length > 0 && (
                <div style={{ padding: 10, borderRadius: 8, background: '#fff7ed', border: '1px solid #fed7aa' }}>
                  <div style={{ fontSize: 12, fontWeight: 800, color: '#9a3412', marginBottom: 6 }}>Sinais de prompt injection / conteúdo não confiável</div>
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    {item.signals.map((signal, index) => {
                      const signalJson = asTraceJson(signal)
                      const signalSeverity = normalizeRiskLevel(readTraceString(signalJson, 'severity', 'Severity'))
                      const signalCode = readTraceString(signalJson, 'code', 'Code', 'type', 'Type') || 'sinal'
                      return (
                        <span key={index} className={`badge ${riskBadgeClass(signalSeverity)}`} style={{ fontSize: 10 }}>
                          {signalCode} · {readTraceString(signalJson, 'severity', 'Severity') || 'risco'}
                        </span>
                      )
                    })}
                  </div>
                </div>
              )}

              {step.evidencePath && (
                <div style={{ padding: 10, borderRadius: 8, background: '#f8fafc', border: '1px solid #e2e8f0', fontSize: 12, color: '#475569' }}>
                  <strong style={{ color: '#0f172a' }}>Evidência:</strong> {renderEvidencePath(step.evidencePath)}
                </div>
              )}

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 12 }}>
                <JsonBox title="Objetivo" value={step.objectiveJson} />
                <JsonBox title="Proposta" value={step.proposalJson} />
                <JsonBox title="Risco" value={step.riskJson} />
                <JsonBox title="Guard" value={step.guardVerdictJson} />
                <JsonBox title="Execução" value={step.executionJson} />
                <JsonBox title="Outcome" value={step.outcomeJson} />
                <JsonBox title="Recovery" value={step.recoveryJson} />
                <JsonBox title="Content safety" value={step.contentSafetyJson} />
              </div>
              <div style={{ fontSize: 11, color: '#94a3b8' }}>
                {new Date(step.startedAt || step.createdAt).toLocaleString()} → {new Date(step.completedAt || step.createdAt).toLocaleString()}
              </div>
            </div>
          </details>
        )
      })}
    </div>
  )
}

type TraceViewModel = {
  step: AgentTraceStep
  proposal: TraceJson
  risk: TraceJson
  verdict: TraceJson
  execution: TraceJson
  outcome: TraceJson
  recovery: TraceJson
  safety: TraceJson
  riskLevel: string
  actionType: string
  actionLabel: string
  verdictCode: string
  executionStatus: string
  outcomeStatus: string
  recoveryMode: string
  signals: unknown[]
  hasCheckpoint: boolean
  isBlocked: boolean
  isSubmitBoundary: boolean
  opened: boolean
  searchable: string
}

function toTraceViewModel(step: AgentTraceStep): TraceViewModel {
  const proposal = parseTraceJson(step.proposalJson)
  const risk = parseTraceJson(step.riskJson)
  const verdict = parseTraceJson(step.guardVerdictJson)
  const execution = parseTraceJson(step.executionJson)
  const outcome = parseTraceJson(step.outcomeJson)
  const recovery = parseTraceJson(step.recoveryJson)
  const safety = parseTraceJson(step.contentSafetyJson)
  const riskLevel = normalizeRiskLevel(readTraceString(risk, 'level', 'Level'))
  const actionType = readTraceString(proposal, 'type', 'Type')
  const actionLabel = readTraceString(proposal, 'label', 'Label', 'targetLabel', 'TargetLabel')
  const verdictCode = readTraceString(verdict, 'verdict', 'Verdict', 'reasonCode', 'ReasonCode')
  const executionStatus = readTraceString(execution, 'status', 'Status') || readTraceString(execution, 'result', 'Result')
  const outcomeStatus = readTraceString(outcome, 'status', 'Status')
  const recoveryMode = readTraceString(recovery, 'mode', 'Mode')
  const signals = readTraceArray(safety, 'signals', 'Signals')
  const allowed = readTraceBool(verdict, 'allowed', 'Allowed')
  const isBlocked = allowed === false || (Boolean(verdictCode) && verdictCode !== 'approved')
  const isSubmitBoundary = readTraceBool(risk, 'isSubmitBoundary', 'IsSubmitBoundary') === true
  const hasCheckpoint = recoveryMode === 'Checkpoint' || readTraceBool(risk, 'requiresCheckpoint', 'RequiresCheckpoint') === true
  const opened = riskRank(riskLevel) >= 3 || signals.length > 0 || hasCheckpoint || isBlocked
  const searchable = [
    step.objectiveKind,
    step.platform,
    step.module,
    step.phase,
    step.planner,
    riskLevel,
    actionType,
    actionLabel,
    verdictCode,
    outcomeStatus,
    recoveryMode,
    step.proposalJson,
    step.riskJson,
    step.guardVerdictJson,
    step.outcomeJson,
    step.recoveryJson,
    step.contentSafetyJson,
  ].join(' ').toLowerCase()

  return { step, proposal, risk, verdict, execution, outcome, recovery, safety, riskLevel, actionType, actionLabel, verdictCode, executionStatus, outcomeStatus, recoveryMode, signals, hasCheckpoint, isBlocked, isSubmitBoundary, opened, searchable }
}

function summarizeTrace(items: TraceViewModel[]) {
  return items.reduce((summary, item) => {
    summary.total += 1
    if (item.hasCheckpoint) summary.checkpoints += 1
    if (item.isBlocked) summary.blocked += 1
    if (riskRank(item.riskLevel) >= 3) summary.highRisk += 1
    if (item.signals.length > 0) summary.safetySignals += item.signals.length
    if (item.isSubmitBoundary) summary.submitBoundaries += 1
    return summary
  }, { total: 0, checkpoints: 0, blocked: 0, highRisk: 0, safetySignals: 0, submitBoundaries: 0 })
}

function TraceMetric({ label, value, tone = 'slate' }: { label: string; value: number; tone?: 'slate' | 'amber' | 'red' | 'blue' }) {
  const color = tone === 'red' ? '#dc2626' : tone === 'amber' ? '#d97706' : tone === 'blue' ? '#2563eb' : '#334155'
  return (
    <div style={{ border: '1px solid #e2e8f0', background: '#f8fafc', borderRadius: 8, padding: '9px 10px', minWidth: 0 }}>
      <div style={{ fontSize: 10, fontWeight: 800, color: '#94a3b8', textTransform: 'uppercase' }}>{label}</div>
      <div style={{ marginTop: 3, fontSize: 18, lineHeight: 1, fontWeight: 800, color }}>{value}</div>
    </div>
  )
}

function TraceField({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div style={{ minWidth: 0 }}>
      <div style={{ fontSize: 11, fontWeight: 800, color: '#94a3b8', textTransform: 'uppercase' }}>{label}</div>
      <div style={{ marginTop: 3, color: '#334155', fontSize: 12, lineHeight: 1.35, overflowWrap: 'anywhere', fontFamily: mono ? 'ui-monospace, SFMono-Regular, Menlo, monospace' : undefined }}>{value || '—'}</div>
    </div>
  )
}

function parseTraceJson(value: string): TraceJson {
  try {
    const parsed = JSON.parse(value || '{}') as unknown
    return asTraceJson(parsed)
  } catch {
    return value ? { raw: value } : {}
  }
}

function asTraceJson(value: unknown): TraceJson {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as TraceJson : {}
}

function readTraceString(source: TraceJson, ...keys: string[]) {
  for (const key of keys) {
    const value = source[key]
    if (value !== undefined && value !== null && value !== '') return String(value)
  }
  return ''
}

function readTraceArray(source: TraceJson, ...keys: string[]) {
  for (const key of keys) {
    const value = source[key]
    if (Array.isArray(value)) return value
  }
  return []
}

function readTraceBool(source: TraceJson, ...keys: string[]) {
  for (const key of keys) {
    const value = source[key]
    if (typeof value === 'boolean') return value
    if (typeof value === 'string' && ['true', 'false'].includes(value.toLowerCase())) return value.toLowerCase() === 'true'
  }
  return undefined
}

function normalizeRiskLevel(value: string) {
  const numeric: Record<string, string> = { '0': 'None', '1': 'Low', '2': 'Medium', '3': 'High', '4': 'Critical' }
  const normalized = numeric[value] ?? value
  if (!normalized) return 'None'
  return normalized[0].toUpperCase() + normalized.slice(1).toLowerCase()
}

function riskRank(level: string) {
  return ({ None: 0, Low: 1, Medium: 2, High: 3, Critical: 4 } as Record<string, number>)[normalizeRiskLevel(level)] ?? 0
}

function riskBadgeClass(level: string) {
  const normalized = level.toLowerCase()
  if (normalized === 'critical' || normalized === 'high') return 'badge-red'
  if (normalized === 'medium') return 'badge-amber'
  if (normalized === 'low') return 'badge-green'
  return 'badge-slate'
}

function renderEvidencePath(path: string) {
  const parts = path.split(';').map((part) => part.trim()).filter(Boolean)
  return (
    <span style={{ display: 'inline-flex', gap: 6, flexWrap: 'wrap' }}>
      {parts.map((part, index) => part.startsWith('http')
        ? <a key={`${part}-${index}`} href={part} target="_blank" rel="noreferrer">{part}</a>
        : <code key={`${part}-${index}`} style={{ background: '#e2e8f0', borderRadius: 4, padding: '1px 4px' }}>{part}</code>)}
    </span>
  )
}

function JsonBox({ title, value }: { title: string; value: string }) {
  const formatted = (() => {
    try {
      return JSON.stringify(JSON.parse(value || '{}'), null, 2)
    } catch {
      return value || '{}'
    }
  })()
  return (
    <div>
      <div style={{ fontSize: 12, fontWeight: 700, color: '#64748b', marginBottom: 6 }}>{title}</div>
      <pre style={{ margin: 0, whiteSpace: 'pre-wrap', maxHeight: 220, overflow: 'auto', padding: 12, background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: 8, fontSize: 12, color: '#334155' }}>{formatted}</pre>
    </div>
  )
}

function learningStatusLabel(status: string) {
  return ({ all: 'Todos', candidate: 'Candidato', auto_approved: 'Auto-aprovado', approved: 'Aprovado', rejected: 'Rejeitado', disabled: 'Desativado' } as Record<string, string>)[status] ?? status
}

function platformLabel(platform?: string) {
  return ({ linkedin: 'LinkedIn' } as Record<string, string>)[platform || ''] ?? (platform || 'Origem?')
}

function applyPlatformLabel(platform?: string) {
  return ({
    linkedin_easy_apply: 'LinkedIn simplificada',
    inhire: 'InHire',
    unsupported_external: 'Outro',
    unknown: 'Desconhecida',
  } as Record<string, string>)[platform || 'unknown'] ?? (platform || 'Desconhecida')
}

function applySurfaceLabel(surface?: string) {
  return ({
    easy_apply_modal: 'Modal',
    external_ats: 'ATS externo',
    unsupported_external: 'Externo nao suportado',
    already_applied: 'Ja candidatado',
    unknown: 'Nao classificada',
  } as Record<string, string>)[surface || 'unknown'] ?? (surface || 'Nao classificada')
}

function automationSupportLabel(support?: string) {
  return ({
    supported: 'Automatico',
    review_required: 'Revisao',
    manual_only: 'Manual',
    unknown: 'A classificar',
  } as Record<string, string>)[support || 'unknown'] ?? (support || 'A classificar')
}

function applyPlatformBadge(platform?: string) {
  if (platform === 'linkedin_easy_apply' || platform === 'inhire') return 'badge-blue'
  if (platform === 'unsupported_external') return 'badge-slate'
  return 'badge-amber'
}

function automationSupportBadge(support?: string) {
  if (support === 'supported') return 'badge-green'
  if (support === 'manual_only') return 'badge-slate'
  if (support === 'review_required') return 'badge-amber'
  return 'badge-slate'
}

function ChipToggleGroup({ options, selected, onChange }: { options: { value: string; label: string }[]; selected: string[]; onChange: (vals: string[]) => void }) {
  function toggle(value: string) {
    if (selected.includes(value)) onChange(selected.filter((v) => v !== value))
    else onChange([...selected, value])
  }
  return (
    <div className="chip-toggle-group">
      {options.map((opt) => (
        <button key={opt.value} className={`chip-toggle ${selected.includes(opt.value) ? 'active' : ''}`} onClick={() => toggle(opt.value)} type="button">
          <span className="check">{selected.includes(opt.value) ? <CheckCircle2 size={10} /> : null}</span>
          {opt.label}
        </button>
      ))}
    </div>
  )
}

function titleFor(tab: string) {
  return ({ dashboard: 'Cockpit', profile: 'Perfil', resume: 'Curriculo ATS', searches: 'Buscas salvas', jobs: 'Fila de vagas', runs: 'Execuções', answers: 'Respostas', ai: 'Provedores de IA', filters: 'Filtros e Regras', agent: 'Checkpoints e automação', learnings: 'Aprendizados' } as Record<string, string>)[tab]
}

function extractCheckpointDraftValue(checkpoint: Checkpoint) {
  if (checkpoint.resolutionValue) return checkpoint.resolutionValue
  const match = checkpoint.detail.match(/value=(.*?)(?:;|$)/i)
  return match?.[1]?.trim() ?? ''
}

function operationalHealthTitle(status: string) {
  return ({
    ok: 'Tudo pronto para operar',
    attention: 'Precisa de atencao',
    error: 'Operacao bloqueada',
    idle: 'Sem atividade agora',
  } as Record<string, string>)[status] ?? 'Saude desconhecida'
}

function healthSignalLabel(signal: HealthSignal) {
  const names = ({
    database: 'Banco',
    policy: 'Politica',
    limits: 'Limites',
    ai: 'IA',
    executor: 'Executor',
    extension: 'Extensao',
    commands: 'Comandos',
    maintenance: 'Manutencao',
    queue: 'Fila',
  } as Record<string, string>)
  return `${names[signal.key] ?? signal.key}: ${signal.label}`
}

export default App
