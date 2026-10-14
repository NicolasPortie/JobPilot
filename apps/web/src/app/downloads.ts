import { resumeClient } from '../api/resumeClient'

export type DownloadFormat = 'word' | 'pdf'

/** Nome vindo do servidor (Content-Disposition); o nome genérico só entra se o cabeçalho faltar. */
export function fileNameFromDisposition(header: unknown, fallback: string) {
  if (typeof header !== 'string') return fallback
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(header)
  if (encoded) {
    try { return decodeURIComponent(encoded[1]) } catch { /* cai para o nome simples */ }
  }
  const plain = /filename="?([^";]+)"?/i.exec(header)
  return plain ? plain[1] : fallback
}

/** Gera a exportação no servidor e baixa o arquivo. Devolve os avisos de leitura ATS, se houver. */
export async function downloadResumeVersion(versionId: string, format: DownloadFormat, layout: string) {
  const { data } = await resumeClient.exportVersion(versionId, { format, layout })
  const download = await resumeClient.downloadExport(data.id)
  const extension = format === 'word' ? 'docx' : 'pdf'
  const fileName = fileNameFromDisposition(download.headers?.['content-disposition'], `curriculo.${extension}`)

  const url = URL.createObjectURL(download.data)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
  return { fileName, warnings: data.parseWarnings ?? [] }
}
