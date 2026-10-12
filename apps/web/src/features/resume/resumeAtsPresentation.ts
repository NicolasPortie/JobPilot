import type { JobRequirementDto, ResumeSection } from '../../types'

export type RequirementStatus = 'direct' | 'adjacent' | 'missing'

export type RequirementReviewItem = JobRequirementDto & {
  status: RequirementStatus
}

export function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return ''
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}

// Leitura somente-exibicao do contentJson das secoes (mesmos campos do editor).
export function sectionItems(sections: ResumeSection[], type: string): Record<string, string>[] {
  const section = sections.find((item) => item.sectionType === type)
  if (!section) return []
  try {
    const parsed = JSON.parse(section.contentJson || '{}') as { items?: Record<string, unknown>; summary?: unknown }
    if (Array.isArray(parsed.items)) {
      return parsed.items.map((item) => Object.fromEntries(
        Object.entries(item ?? {}).map(([key, value]) => [key, String(value ?? '')]),
      ))
    }
    if (typeof parsed.summary === 'string') return [{ summary: parsed.summary }]
    return []
  } catch {
    return []
  }
}

