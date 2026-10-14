import { profileClient } from '../api/profileClient'
import { resumeClient } from '../api/resumeClient'
import type { AtsProfileSkillAddition } from '../types'
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
    saveReviewAnswer,
    regenerateAfterAnswers,
    updateTailoredSection,
    addProfileSkill,
  }
}
