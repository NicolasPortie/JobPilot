import { jobTargetClient } from '../api/jobTargetClient'
import { profileClient } from '../api/profileClient'
import { resumeClient } from '../api/resumeClient'
import type { AtsProfileSkillAddition } from '../types'
import { emptyProfile } from './defaults'
import type { ResumeWorkflowState } from './resumeWorkflowModels'
import { responseMessage } from './utils'

export function useResumeWorkflow(state: ResumeWorkflowState) {
  function activeBaseVersionId() {
    const versions = state.resumeDocuments.flatMap((document) => document.versions)
    const selected = versions.find((version) => version.id === state.selectedResumeVersionId)
    if (selected) return selected.id
    const baseVersions = versions.filter((version) => version.title.toLowerCase().includes('base importada'))
    return (baseVersions.length > 0 ? baseVersions : versions)
      .sort((left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt))[0]?.id ?? ''
  }

  async function importResumeFile() {
    if (!state.selectedResumeFile) return
    state.setProfileImportBusy(true)
    state.setProfileImportResult(null)
    state.setProfileImportNotice('')
    try {
      const payload = new FormData()
      payload.append('file', state.selectedResumeFile)
      const { data } = await profileClient.importResumeFile(payload)
      state.setProfileImportResult(data)
      state.setProfile({ ...emptyProfile, ...data.profile })
      state.setSelectedResumeFile(null)
      await state.refresh()
    } catch (error: unknown) {
      state.setProfileImportNotice(responseMessage(error, 'Nao foi possivel ler o arquivo. Verifique se a API local esta rodando.'))
    } finally {
      state.setProfileImportBusy(false)
    }
  }

  async function buildAtsResume() {
    state.setAtsResumeBusy(true)
    state.setAtsResumeNotice('')
    try {
      const versionId = activeBaseVersionId()
      if (!versionId) {
        state.setAtsResumeNotice('Importe um currículo no Perfil antes de gerar um currículo ATS.')
        return
      }
      const hasUrl = state.atsJobUrl.trim().length > 0
      const hasJobInput = state.atsJobDescription.trim().length > 0 || hasUrl || Boolean(state.atsScreenshotFile)
      if (!hasJobInput) {
        const { data } = await resumeClient.buildAts({
          baseResumeVersionId: versionId,
          layout: state.selectedLayout,
          useAi: state.atsUseAi,
        })
        state.setAtsJobTarget(null)
        state.setAtsTailoredResume(data)
        await state.refresh()
        return
      }
      const organized = state.atsScreenshotFile
        ? await jobTargetClient.organizeScreenshot((() => {
            const payload = new FormData()
            payload.append('file', state.atsScreenshotFile)
            return payload
          })(), `useAi=${state.atsUseAi}`)
        : await jobTargetClient.organize({
            sourceKind: hasUrl ? 'url' : 'manual_text',
            text: state.atsJobDescription,
            url: hasUrl ? state.atsJobUrl : '',
            useAi: state.atsUseAi,
          })
      const tailored = await resumeClient.tailor({
        baseResumeVersionId: versionId,
        jobTargetId: organized.data.id,
        layout: state.selectedLayout,
        useAi: state.atsUseAi,
      })
      state.setAtsJobTarget(organized.data)
      state.setAtsTailoredResume(tailored.data)
      state.setAtsScreenshotFile(null)
      await state.refresh()
    } catch (error: unknown) {
      state.setAtsResumeNotice(responseMessage(error, 'Não foi possível gerar o currículo ATS agora.'))
    } finally {
      state.setAtsResumeBusy(false)
    }
  }

  async function buildBaseResume() {
    state.setAtsResumeBusy(true)
    state.setAtsResumeNotice('')
    try {
      const versionId = activeBaseVersionId()
      if (!versionId) {
        state.setAtsResumeNotice('Importe um currículo no Perfil antes de gerar um currículo ATS.')
        return
      }
      const { data } = await resumeClient.buildAts({
        baseResumeVersionId: versionId,
        layout: state.selectedLayout,
        useAi: state.atsUseAi,
      })
      state.setAtsJobTarget(null)
      state.setAtsTailoredResume(data)
      await state.refresh()
    } catch (error: unknown) {
      state.setAtsResumeNotice(responseMessage(error, 'Nao foi possivel gerar a base ATS agora.'))
    } finally {
      state.setAtsResumeBusy(false)
    }
  }

  async function exportAtsResume(format: 'word' | 'pdf') {
    if (!state.atsTailoredResume) return
    state.setAtsExportBusy(true)
    state.setAtsResumeNotice('')
    try {
      const { data } = await resumeClient.exportVersion(state.atsTailoredResume.tailoredVersionId, { format, layout: state.selectedLayout })
      if (data.parseWarnings && data.parseWarnings.length > 0) {
        state.setAtsResumeNotice(`Exportado com avisos: ${data.parseWarnings.join(' · ')}`)
      }
      const download = await resumeClient.downloadExport(data.id)
      const url = URL.createObjectURL(download.data)
      const link = document.createElement('a')
      link.href = url
      const extension = format === 'word' ? 'docx' : 'pdf'
      link.download = `jobpilot-curriculo-${state.atsTailoredResume.tailoredVersionId}.${extension}`
      document.body.appendChild(link)
      link.click()
      link.remove()
      URL.revokeObjectURL(url)
    } catch (error: unknown) {
      state.setAtsResumeNotice(responseMessage(error, `Não foi possível exportar o currículo ${format === 'word' ? 'WORD' : 'PDF'}.`))
    } finally {
      state.setAtsExportBusy(false)
    }
  }

  async function saveReviewAnswer(reqId: string, label: string, question: string, value: string, type: string) {
    try {
      const { data } = await resumeClient.saveReviewAnswer({
        evidenceMapRunId: state.atsTailoredResume?.evidenceMapRunId,
        jobRequirementId: reqId,
        requirementLabel: label,
        questionText: question,
        answerValue: value,
        answerType: type,
      })
      state.setReviewAnswers((prev) => ({ ...prev, [reqId]: { id: data.id, value, type, evidence: '' } }))
      state.setAtsResumeNotice('Resposta salva. Clique em "Regenerar com respostas" para aplicar.')
    } catch (error: unknown) {
      state.setAtsResumeNotice(responseMessage(error, 'Nao foi possivel salvar a resposta.'))
    }
  }

  async function regenerateAfterAnswers() {
    if (!state.atsTailoredResume) return
    state.setAtsResumeBusy(true)
    state.setAtsResumeNotice('')
    try {
      const answerIds = Object.values(state.reviewAnswers)
        .filter((answer) => answer.value.trim().length > 0)
        .flatMap((answer) => answer.id ? [answer.id] : [])
      if (answerIds.length === 0) {
        state.setAtsResumeNotice('Responda pelo menos uma pergunta antes de regenerar.')
        return
      }
      const { data } = await resumeClient.regenerateAfterAnswers({
        answerIds,
        baseResumeVersionId: state.atsTailoredResume.baseResumeVersionId,
        jobTargetId: state.atsTailoredResume.jobTargetId,
        layout: state.selectedLayout,
        useAi: state.atsUseAi,
      })
      state.setAtsTailoredResume(data.tailoredResume)
      state.setAtsResumeNotice('Curriculo regenerado com respostas aplicadas.')
      await state.refresh()
    } catch (error: unknown) {
      state.setAtsResumeNotice(responseMessage(error, 'Nao foi possivel regenerar o curriculo.'))
    } finally {
      state.setAtsResumeBusy(false)
    }
  }

  async function updateTailoredSection(sectionId: string, contentJson: string) {
    const versionId = state.atsTailoredResume?.tailoredVersionId
    try {
      const { data: savedSection } = await resumeClient.updateSection(sectionId, { contentJson })
      if (versionId) {
        // The server rebuilds the full text, including structured section content.
        const { data: version } = await resumeClient.getVersion(versionId)
        state.setAtsTailoredResume((prev) => {
          if (!prev || prev.tailoredVersionId !== versionId) return prev
          return {
            ...prev,
            plainText: version.plainText,
            sectionDiffs: prev.sectionDiffs.map((diff) =>
              diff.afterSectionId === sectionId
                ? { ...diff, afterPreview: savedSection.contentJson.slice(0, 200), status: 'changed' }
                : diff),
            version,
          }
        })
      }
      state.setAtsResumeNotice('Secao atualizada.')
    } catch (error: unknown) {
      state.setAtsResumeNotice(responseMessage(error, 'Erro ao atualizar secao.'))
    }
  }

  async function addProfileSkill(skill: string, context?: { kind?: 'missing' | 'related'; reason?: string }) {
    const clean = skill.trim()
    if (!clean) return
    const exists = state.profile.skills.some((item) => item.trim().toLowerCase() === clean.toLowerCase())
    if (exists) {
      state.setAtsResumeNotice(`${clean} ja esta no perfil.`)
      return
    }
    const nextProfile = {
      ...state.profile,
      skills: [...state.profile.skills, clean].sort((left, right) => left.localeCompare(right)),
    }
    await profileClient.save(nextProfile)
    state.setProfile(nextProfile)
    state.setRecommendedSkillAdditions((current) => {
      const addition: AtsProfileSkillAddition = {
        id: `${Date.now()}-${clean.toLowerCase().replace(/[^a-z0-9]+/gi, '-')}`,
        skill: clean,
        kind: context?.kind ?? 'missing',
        reason: context?.reason ?? 'Adicionada a partir de uma recomendacao ATS.',
        jobTitle: state.atsJobTarget?.title ?? '',
        company: state.atsJobTarget?.company ?? '',
        createdAt: new Date().toISOString(),
      }
      return [addition, ...current.filter((item) => item.skill.trim().toLowerCase() !== clean.toLowerCase())].slice(0, 50)
    })
      state.setAtsResumeNotice(`${clean} adicionado ao perfil. Gere o currículo novamente para recalcular a nota.`)
    await state.refresh('resume')
  }

  return {
    importResumeFile,
    buildAtsResume,
    buildBaseResume,
    exportAtsResume,
    saveReviewAnswer,
    regenerateAfterAnswers,
    updateTailoredSection,
    addProfileSkill,
  }
}
