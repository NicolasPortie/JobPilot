import { profileClient } from '../api/profileClient'
import { resumeClient } from '../api/resumeClient'
import type { ResumeWorkflowState } from './resumeWorkflowModels'
import { responseMessage } from './utils'

/** Ações depois que o currículo já foi gerado: respostas de revisão, edição de seções e competências. */
export function useResumeReview(state: ResumeWorkflowState) {
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

  /** O servidor decide se a habilidade já existe, normaliza a lista e guarda o histórico da recomendação. */
  async function addProfileSkill(skill: string, context?: { kind?: 'missing' | 'related'; reason?: string }) {
    const clean = skill.trim()
    if (!clean) return
    try {
      const { data } = await profileClient.addSkill({
        skill: clean,
        kind: context?.kind,
        reason: context?.reason,
        jobTitle: state.atsJobTarget?.title,
        company: state.atsJobTarget?.company,
      })
      state.setProfile((current) => ({ ...current, ...data.profile }))
      state.setAtsResumeNotice(data.alreadyPresent
        ? `${clean} já está no perfil.`
        : `${clean} adicionado ao perfil. Gere o currículo novamente para recalcular a nota.`)
      await state.refresh()
    } catch (error: unknown) {
      state.setAtsResumeNotice(responseMessage(error, 'Não foi possível adicionar a habilidade ao perfil.'))
    }
  }

  return {
    saveReviewAnswer,
    regenerateAfterAnswers,
    updateTailoredSection,
    addProfileSkill,
  }
}
