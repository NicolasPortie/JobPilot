import type { ResumeDocumentSummary } from '../types'

export type SelectableVersion = ResumeDocumentSummary['versions'][number] & { documentTitle: string }

/**
 * Versões que podem servir de base para adaptar. O backend classifica cada versão (`kind`);
 * a tela só escolhe entre as bases e, sem nenhuma, mostra o que existir.
 */
function allVersions(documents: ResumeDocumentSummary[]): SelectableVersion[] {
  return documents.flatMap((document) =>
    document.versions.map((version) => ({ ...version, documentTitle: document.title })))
}

export function selectableVersions(documents: ResumeDocumentSummary[]): SelectableVersion[] {
  const versions = allVersions(documents)
  const bases = versions.filter((version) => version.kind === 'base')
  return bases.length > 0 ? bases : versions
}

/** Versão escolhida pelo usuário ou, na falta dela, a base atualizada mais recentemente. */
export function activeBaseVersion(documents: ResumeDocumentSummary[], selectedId: string) {
  const chosen = allVersions(documents).find((version) => version.id === selectedId)
  if (chosen) return chosen
  return [...selectableVersions(documents)].sort((left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt))[0]
}
