import { applicationsClient } from '../api/applicationsClient'
import { jobTargetClient } from '../api/jobTargetClient'
import { profileClient } from '../api/profileClient'
import { resumeClient } from '../api/resumeClient'
import type { AtsProfileSkillAddition } from '../types'
import { emptyProfile } from './defaults'
import { downloadResumeVersion, type DownloadFormat } from './downloads'
import type { ResumeWorkflowState } from './resumeWorkflowModels'
import { responseMessage } from './utils'

export function useResumeWorkflow(state: ResumeWorkflowState) {
  function activeBaseVersionId() {
    const versions = state.resumeDocuments.flatMap((document) => document.versions)
    const selected = versions.find((version) => version.id === state.selectedResumeVersionId)
    if (selected) return selected.id
    const baseVersions = versions.filter((version) =>
      version.kind ? version.kind === 'base' : version.title.toLowerCase().includes('base importada'))
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

  /** Gera o currículo da vaga informada (link, vários links, texto ou anexo) a partir da base importada. */
  async function buildAtsResume() {
    state.setAtsResumeBusy(true)
    state.setAtsResumeNotice('')
    try {
      const versionId = activeBaseVersionId()
      if (!versionId) {
        state.setAtsResumeNotice('Importe um currículo no Perfil antes de gerar um currículo ATS.')
        return
      }
      const description = state.atsJobDescription.trim()
      const links = state.atsJobUrl.trim()
      if (!description && !links && !state.atsScreenshotFile) {
        const { data } = await resumeClient.buildAts({
          baseResumeVersionId: versionId,
          layout: state.selectedLayout,
          useAi: state.atsUseAi,
        })
        state.setAtsJobTarget(null)
        state.setAtsTailoredResume(data)
        state.setAtsBatchItems?.([])
        await state.refresh()
        return
      }

      if (state.atsScreenshotFile) {
        const payload = new FormData()
        payload.append('file', state.atsScreenshotFile)
        const organized = await jobTargetClient.organizeScreenshot(payload, `useAi=${state.atsUseAi}`)
        const tailored = await resumeClient.tailor({
          baseResumeVersionId: versionId,
          jobTargetId: organized.data.id,
          layout: state.selectedLayout,
          useAi: state.atsUseAi,
        })
        state.setAtsJobTarget(organized.data)
        state.setAtsTailoredResume(tailored.data)
        state.setAtsBatchItems?.([])
        state.setAtsScreenshotFile(null)
        await state.refresh()
        return
      }

      // Texto colado tem prioridade; o link, nesse caso, é só a origem para "Abrir vaga".
      const { data } = await applicationsClient.quick({
        input: description || links,
        jobUrl: description && links ? links : undefined,
        useAi: state.atsUseAi,
        layout: state.selectedLayout,
        baseResumeVersionId: versionId,
      })
      if (data.items.length === 1) {
        const [item] = data.items
        if (!item.succeeded || !item.tailored || !item.target) {
          state.setAtsResumeNotice(item.error ?? 'Não foi possível ler esta vaga. Cole o texto da descrição.')
          return
        }
        state.setAtsJobTarget(item.target)
        state.setAtsTailoredResume(item.tailored)
        state.setAtsBatchItems?.([])
      } else {
        const generated = data.items.filter((item) => item.succeeded).length
        state.setAtsJobTarget(null)
        state.setAtsTailoredResume(null)
        state.setAtsBatchItems?.(data.items)
        state.setAtsResumeNotice(`${generated} de ${data.items.length} currículos gerados.`)
      }
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
      state.setAtsBatchItems?.([])
      await state.refresh()
    } catch (error: unknown) {
      state.setAtsResumeNotice(responseMessage(error, 'Nao foi possivel gerar a base ATS agora.'))
    } finally {
      state.setAtsResumeBusy(false)
    }
  }

  /** Baixa o PDF/Word de qualquer versão gerada (a aberta na tela, um item do lote ou do histórico). */
  async function downloadVersion(versionId: string, format: DownloadFormat) {
    state.setAtsExportBusy(true)
    state.setAtsResumeNotice('')
    try {
      const { warnings } = await downloadResumeVersion(versionId, format, state.selectedLayout)
      if (warnings.length > 0) state.setAtsResumeNotice(`Exportado com avisos: ${warnings.join(' · ')}`)
    } catch (error: unknown) {
      state.setAtsResumeNotice(responseMessage(error, `Não foi possível exportar o currículo ${format === 'word' ? 'WORD' : 'PDF'}.`))
    } finally {
      state.setAtsExportBusy(false)
    }
  }

  async function exportAtsResume(format: DownloadFormat) {
    if (!state.atsTailoredResume) return
    await downloadVersion(state.atsTailoredResume.tailoredVersionId, format)
  }

  async function markApplied(versionId: string, applied: boolean) {
    try {
      await applicationsClient.setApplied(versionId, applied)
      state.setAppliedVersionIds?.((current) =>
        applied ? [...new Set([...current, versionId])] : current.filter((id) => id !== versionId))
    } catch (error: unknown) {
      state.setAtsResumeNotice(responseMessage(error, 'Não foi possível atualizar o status da candidatura.'))
    }
  }

  /** Limpa vaga e resultado para começar a próxima candidatura. */
  function startNextJob() {
    state.setAtsJobTarget(null)
    state.setAtsTailoredResume(null)
    state.setAtsBatchItems?.([])
    state.setAtsScreenshotFile(null)
    state.setAtsResumeNotice('')
    state.clearJobInputs?.()
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
    downloadVersion,
    markApplied,
    startNextJob,
    saveReviewAnswer,
    regenerateAfterAnswers,
    updateTailoredSection,
    addProfileSkill,
  }
}
