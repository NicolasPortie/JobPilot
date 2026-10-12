const DEFAULT_API_ORIGIN = 'http://localhost:5000'

export function apiOrigin() {
  const configured = import.meta.env.VITE_API_URL?.trim()
  if (!configured) return DEFAULT_API_ORIGIN
  return configured.replace(/\/api\/?$/, '')
}

export async function checkApiHealth(timeoutMs = 3000): Promise<boolean> {
  const controller = new AbortController()
  const timeoutId = window.setTimeout(() => controller.abort(), timeoutMs)

  try {
    const response = await fetch(`${apiOrigin()}/health`, {
      method: 'GET',
      signal: controller.signal,
      cache: 'no-store',
    })
    return response.ok
  } catch {
    return false
  } finally {
    window.clearTimeout(timeoutId)
  }
}
