import { useEffect, useState } from 'react'
import { api } from '../api/client'
import type { AuditTab, JobAudit } from '../types'

const emptyAudit: JobAudit = { fields: [], answers: [], steps: [], traceSteps: [], checkpoints: [], events: [], learnings: [] }

export function useJobAudit(token: string, activeJobId: string) {
  const [loadedAudit, setLoadedAudit] = useState<JobAudit>(emptyAudit)
  const [auditTab, setAuditTab] = useState<AuditTab>('timeline')
  const jobAudit = token && activeJobId ? loadedAudit : emptyAudit

  useEffect(() => {
    if (!token || !activeJobId) {
      return
    }

    let cancelled = false

    async function loadJobAudit() {
      try {
        const auditRes = await api.get(`/jobs/${activeJobId}/audit`)
        if (cancelled) return
        const data = auditRes.data ?? {}
        setLoadedAudit({
          fields: data.fields ?? [],
          answers: data.answers ?? [],
          steps: data.steps ?? [],
          traceSteps: data.traceSteps ?? [],
          checkpoints: data.checkpoints ?? [],
          events: data.events ?? [],
          learnings: data.learnings ?? [],
        })
      } catch {
        try {
          const [fieldsRes, answersRes, stepsRes, traceRes, checkpointsRes, eventsRes, learningsRes] = await Promise.all([
            api.get(`/jobs/${activeJobId}/fields`),
            api.get(`/jobs/${activeJobId}/answers`),
            api.get(`/jobs/${activeJobId}/steps`),
            api.get(`/jobs/${activeJobId}/trace-steps`).catch(() => ({ data: [] })),
            api.get(`/jobs/${activeJobId}/checkpoints`),
            api.get(`/jobs/${activeJobId}/events`),
            api.get(`/jobs/${activeJobId}/learnings`),
          ])
          if (cancelled) return
          setLoadedAudit({
            fields: fieldsRes.data ?? [],
            answers: answersRes.data ?? [],
            steps: stepsRes.data ?? [],
            traceSteps: traceRes.data ?? [],
            checkpoints: checkpointsRes.data ?? [],
            events: eventsRes.data ?? [],
            learnings: learningsRes.data ?? [],
          })
        } catch {
          if (!cancelled) setLoadedAudit(emptyAudit)
        }
      }
    }

    void loadJobAudit()
    return () => { cancelled = true }
  }, [activeJobId, token])

  return { jobAudit, auditTab, setAuditTab }
}
