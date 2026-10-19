import { applicationsClient } from '../api/applicationsClient'
import { jobTargetClient } from '../api/jobTargetClient'
import { profileClient } from '../api/profileClient'
import { resumeClient } from '../api/resumeClient'
import { activeBaseVersion } from './baseVersions'
import { emptyProfile } from './defaults'
import { downloadResumeVersion, type DownloadFormat } from './downloads'
import type { ResumeWorkflowState } from './resumeWorkflowModels'
import { useResumeReview } from './useResumeReview'
import { responseMessage } from './utils'

export function useResumeWorkflow(state: ResumeWorkflowState) {
  const review = useResumeReview(state)

  function activeBaseVersionId() {
    return activeBaseVersion(state.resumeDocuments, state.selectedResumeVersionId)?.id ?? ''
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
    state.setReviewAnswers({})
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

  /** Devolve false quando o servidor não gravou o status, para a tela não mostrar um estado falso. */
  async function markApplied(versionId: string, applied: boolean) {
    try {
      await applicationsClient.setApplied(versionId, applied)
      state.setAppliedVersionIds?.((current) =>
        applied ? [...new Set([...current, versionId])] : current.filter((id) => id !== versionId))
      return true
    } catch (error: unknown) {
      state.setAtsResumeNotice(responseMessage(error, 'Não foi possível atualizar o status da candidatura.'))
      return false
    }
  }

  /** Limpa vaga e resultado para começar a próxima candidatura. */
  function startNextJob() {
    state.setAtsJobTarget(null)
    state.setAtsTailoredResume(null)
    state.setAtsBatchItems?.([])
    state.setAtsScreenshotFile(null)
    state.setAtsResumeNotice('')
    // Respostas de revisão pertencem à vaga anterior: não podem ir junto na próxima regeneração.
    state.setReviewAnswers({})
    state.clearJobInputs?.()
  }

  return {
    importResumeFile,
    buildAtsResume,
    buildBaseResume,
    exportAtsResume,
    downloadVersion,
    markApplied,
    startNextJob,
    ...review,
  }
}
