export const LOCAL_API_OFFLINE_MESSAGE =
  'API local indisponivel. O Docker sobe apenas o PostgreSQL. Suba o stack com .\\scripts\\start-mvp.ps1 ou rode dotnet run --project apps\\api depois do docker compose.'

export function responseMessage(error: unknown, fallback: string) {
  if (isApiConnectionError(error)) return LOCAL_API_OFFLINE_MESSAGE

  if (typeof error === 'object' && error && 'response' in error) {
    const response = (error as { response?: { data?: { detail?: string, message?: string, title?: string }, status?: number } }).response
    if (response?.data?.detail) return response.data.detail
    if (response?.data?.message) return response.data.message
    if (response?.status === 401) return 'E-mail ou senha inválidos.'
    return response?.data?.title ?? fallback
  }
  return fallback
}

/** Devolve a URL só se for http(s); qualquer outro esquema (ex.: javascript:) não vira link. */
export function safeHttpUrl(value: string | null | undefined) {
  const text = (value ?? '').trim()
  if (!text || /\s/.test(text)) return ''
  try {
    const url = new URL(text)
    return url.protocol === 'http:' || url.protocol === 'https:' ? text : ''
  } catch {
    return ''
  }
}

export function isApiConnectionError(error: unknown) {
  if (typeof error !== 'object' || !error) return false

  const candidate = error as {
    code?: string
    message?: string
    response?: unknown
  }

  if (candidate.response) return false

  const code = candidate.code?.toUpperCase() ?? ''
  const message = candidate.message?.toLowerCase() ?? ''
  return (
    code === 'ERR_NETWORK' ||
    code === 'ECONNABORTED' ||
    message.includes('network error') ||
    message.includes('failed to fetch')
  )
}

export function isUnauthorizedResponse(error: unknown) {
  if (typeof error !== 'object' || !error || !('response' in error)) return false
  const response = (error as { response?: { status?: number } }).response
  return response?.status === 401
}
