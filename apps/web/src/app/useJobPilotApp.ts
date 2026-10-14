import { useCallback, useEffect, useState } from 'react'
import { configureApiToken } from '../api/client'
import { authClient } from '../api/authClient'
import { profileClient, type RetentionPolicy } from '../api/profileClient'
import type { AtsProfileSkillAddition, JobTargetDetail, Profile, ProfileValidationIssue, QuickTailorItem, ResumeDocumentSummary, ResumeImportResult, ResumeLayoutOption, TailoredResume } from '../types'
import { emptyProfile, fallbackResumeLayouts } from './defaults'
import { loadJobPilotTab } from './dataLoaders'
import { useResumeWorkflow } from './useResumeWorkflow'
import type { ReviewAnswers } from './resumeWorkflowModels'
import { isUnauthorizedResponse, LOCAL_API_OFFLINE_MESSAGE, responseMessage } from './utils'

const recommendedSkillsStorageKey = 'jobpilot_ats_profile_skill_additions'

function loadRecommendedSkillAdditions(): AtsProfileSkillAddition[] {
  try {
    const raw = localStorage.getItem(recommendedSkillsStorageKey)
    if (!raw) return []
    const parsed = JSON.parse(raw)
    return Array.isArray(parsed) ? parsed : []
  } catch {
    return []
  }
}

export function useJobPilotApp() {
  // Keep the bearer token out of persistent storage. The backend uses the
  // Authorization header, so sessionStorage keeps the session across tabs
  // while avoiding a long-lived token in the browser profile.
  const [token, setToken] = useState(sessionStorage.getItem('jobpilot_token') ?? '')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loginError, setLoginError] = useState('')
  const [loginBusy, setLoginBusy] = useState(false)
  const [actionBusy, setActionBusy] = useState(false)
  const [refreshBusy, setRefreshBusy] = useState(false)
  const [globalNotice, setGlobalNotice] = useState('')
  const [tab, setTab] = useState('resume')
  const [profile, setProfile] = useState<Profile>(emptyProfile)
  const [profileImportBusy, setProfileImportBusy] = useState(false)
  const [profileImportResult, setProfileImportResult] = useState<ResumeImportResult | null>(null)
  const [profileImportNotice, setProfileImportNotice] = useState('')
  const [selectedResumeFile, setSelectedResumeFile] = useState<File | null>(null)
  const [resumeDocuments, setResumeDocuments] = useState<ResumeDocumentSummary[]>([])
  const [selectedResumeVersionId, setSelectedResumeVersionId] = useState('')
  const [atsJobDescription, setAtsJobDescription] = useState('')
  const [atsJobUrl, setAtsJobUrl] = useState('')
  const [atsScreenshotFile, setAtsScreenshotFile] = useState<File | null>(null)
  const [atsResumeBusy, setAtsResumeBusy] = useState(false)
  const [atsTailoredResume, setAtsTailoredResume] = useState<TailoredResume | null>(null)
  const [atsJobTarget, setAtsJobTarget] = useState<JobTargetDetail | null>(null)
  const [atsBatchItems, setAtsBatchItems] = useState<QuickTailorItem[]>([])
  const [appliedVersionIds, setAppliedVersionIds] = useState<string[]>([])
  const [atsUseAi, setAtsUseAi] = useState(true)
  const [atsExportBusy, setAtsExportBusy] = useState(false)
  const [atsResumeNotice, setAtsResumeNotice] = useState('')
  const [resumeLayouts, setResumeLayouts] = useState<ResumeLayoutOption[]>(fallbackResumeLayouts)
  const [selectedLayout, setSelectedLayout] = useState('ats_standard')
  const [reviewAnswers, setReviewAnswers] = useState<ReviewAnswers>({})
  const [recommendedSkillAdditions, setRecommendedSkillAdditions] = useState<AtsProfileSkillAddition[]>(loadRecommendedSkillAdditions)
  const [validationIssues, setValidationIssues] = useState<ProfileValidationIssue[]>([])
  const [profileCompletion, setProfileCompletion] = useState(0)
  const [retention, setRetention] = useState<RetentionPolicy | null>(null)
  const [dangerNotice, setDangerNotice] = useState('')
  const [dangerBusy, setDangerBusy] = useState(false)

  async function login() {
    if (loginBusy) return
    setLoginBusy(true)
    setLoginError('')
    try {
      const { data } = await authClient.login(email, password)
      sessionStorage.setItem('jobpilot_token', data.token)
      if (data.refreshToken) sessionStorage.setItem('jobpilot_refresh', data.refreshToken)
      setToken(data.token)
    } catch (error: unknown) {
      setLoginError(responseMessage(error, LOCAL_API_OFFLINE_MESSAGE))
    } finally {
      setLoginBusy(false)
    }
  }

  function logout() {
    const refreshToken = sessionStorage.getItem('jobpilot_refresh') ?? ''
    if (refreshToken) void authClient.logout(refreshToken).catch(() => undefined)
    sessionStorage.removeItem('jobpilot_token')
    sessionStorage.removeItem('jobpilot_refresh')
    setToken('')
  }

  const refresh = useCallback(async () => {
    setGlobalNotice('')
    setRefreshBusy(true)
    try {
      await loadJobPilotTab({
        setProfile,
        setResumeDocuments,
        setSelectedResumeVersionId,
        setResumeLayouts,
        setValidationIssues,
        setProfileCompletion,
      })
    } catch (error: unknown) {
      if (isUnauthorizedResponse(error)) {
        const refreshToken = sessionStorage.getItem('jobpilot_refresh') ?? ''
        if (refreshToken) {
          try {
            const { data } = await authClient.refresh(refreshToken)
            sessionStorage.setItem('jobpilot_token', data.token)
            sessionStorage.setItem('jobpilot_refresh', data.refreshToken)
            configureApiToken(data.token)
            setToken(data.token)
            return
          } catch {
            /* refresh inválido: cai para logout abaixo */
          }
        }
        sessionStorage.removeItem('jobpilot_token')
        sessionStorage.removeItem('jobpilot_refresh')
        configureApiToken('')
        setToken('')
        return
      }
      setGlobalNotice(responseMessage(error, LOCAL_API_OFFLINE_MESSAGE))
    } finally {
      setRefreshBusy(false)
    }
  }, [])

  const loadRetention = useCallback(async () => {
    try {
      const { data } = await profileClient.retention()
      setRetention(data)
    } catch {
      setRetention(null)
    }
  }, [])

  useEffect(() => {
    configureApiToken(token)
    if (token) queueMicrotask(() => void refresh())
    if (token) queueMicrotask(() => void loadRetention())
  }, [refresh, loadRetention, token])

  useEffect(() => {
    localStorage.setItem(recommendedSkillsStorageKey, JSON.stringify(recommendedSkillAdditions))
  }, [recommendedSkillAdditions])

  const clearJobInputs = useCallback(() => {
    setAtsJobDescription('')
    setAtsJobUrl('')
  }, [])

  const {
    importResumeFile,
    buildAtsResume,
    buildBaseResume,
    exportAtsResume,
    downloadVersion,
    markApplied,
    startNextJob,
    saveReviewAnswer,
    regenerateAfterAnswers,
    updateTailoredSection,
    addProfileSkill,
  } = useResumeWorkflow({
    profile,
    selectedResumeFile,
    selectedResumeVersionId,
    resumeDocuments,
    atsJobDescription,
    atsJobUrl,
    atsScreenshotFile,
    atsTailoredResume,
    atsJobTarget,
    atsUseAi,
    selectedLayout,
    reviewAnswers,
    setRecommendedSkillAdditions,
    setProfile,
    setProfileImportBusy,
    setProfileImportResult,
    setProfileImportNotice,
    setSelectedResumeFile,
    setAtsResumeBusy,
    setAtsJobTarget,
    setAtsTailoredResume,
    setAtsScreenshotFile,
    setAtsExportBusy,
    setAtsResumeNotice,
    setReviewAnswers,
    setAtsBatchItems,
    setAppliedVersionIds,
    clearJobInputs,
    refresh,
  })

  async function saveProfile() {
    if (actionBusy) return
    setActionBusy(true)
    try {
      await profileClient.save(profile)
      setProfileImportNotice('Perfil salvo.')
      await refresh()
    } catch (error: unknown) {
      setProfileImportNotice(responseMessage(error, 'Não foi possível salvar o perfil. Verifique a API local.'))
    } finally {
      setActionBusy(false)
    }
  }

  function removeRecommendedSkillAddition(id: string) {
    setRecommendedSkillAdditions((current) => current.filter((item) => item.id !== id))
  }

  async function deleteProfile() {
    if (dangerBusy) return
    setDangerBusy(true)
    setDangerNotice('')
    try {
      await profileClient.remove()
      setDangerNotice('Base profissional excluída. Você pode reconstruí-la quando quiser.')
      await refresh()
    } catch (error: unknown) {
      setDangerNotice(responseMessage(error, 'Não foi possível excluir a base. Tente novamente.'))
    } finally {
      setDangerBusy(false)
    }
  }

  async function deleteAccount() {
    if (dangerBusy) return
    setDangerBusy(true)
    setDangerNotice('')
    try {
      await authClient.deleteAccount()
      sessionStorage.removeItem('jobpilot_token')
      sessionStorage.removeItem('jobpilot_refresh')
      configureApiToken('')
      setToken('')
    } catch (error: unknown) {
      setDangerNotice(responseMessage(error, 'Não foi possível excluir a conta. Tente novamente.'))
    } finally {
      setDangerBusy(false)
    }
  }

  function retryDanger() {
    setDangerNotice('')
    void loadRetention()
    void refresh()
  }

  return {
    token,
    email,
    setEmail,
    password,
    setPassword,
    loginError,
    loginBusy,
    actionBusy,
    refreshBusy,
    globalNotice,
    tab,
    setTab,
    profile,
    setProfile,
    recommendedSkillAdditions,
    validationIssues,
    profileCompletion,
    profileImportBusy,
    profileImportResult,
    profileImportNotice,
    selectedResumeFile,
    setSelectedResumeFile,
    resumeDocuments,
    selectedResumeVersionId,
    setSelectedResumeVersionId,
    atsJobDescription,
    setAtsJobDescription,
    atsJobUrl,
    setAtsJobUrl,
    atsScreenshotFile,
    setAtsScreenshotFile,
    atsResumeBusy,
    atsTailoredResume,
    atsJobTarget,
    atsBatchItems,
    appliedVersionIds,
    atsUseAi,
    setAtsUseAi,
    atsExportBusy,
    atsResumeNotice,
    resumeLayouts,
    selectedLayout,
    setSelectedLayout,
    login,
    logout,
    saveProfile,
    removeRecommendedSkillAddition,
    deleteProfile,
    deleteAccount,
    retryDanger,
    retention,
    dangerNotice,
    dangerBusy,
    importResumeFile,
    buildAtsResume,
    buildBaseResume,
    exportAtsResume,
    downloadVersion,
    markApplied,
    startNextJob,
    saveReviewAnswer,
    regenerateAfterAnswers,
    updateTailoredSection,
    addProfileSkill,
    refresh,
  }
}
