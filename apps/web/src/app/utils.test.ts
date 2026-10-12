import { describe, expect, it } from 'vitest'
import { isApiConnectionError, LOCAL_API_OFFLINE_MESSAGE, responseMessage } from './utils'

describe('application utilities', () => {
  it('extracts API response messages safely', () => {
    expect(responseMessage({ response: { data: { message: 'detail' } } }, 'fallback')).toBe('detail')
    expect(responseMessage({ response: { data: { detail: 'problem detail' } } }, 'fallback')).toBe('problem detail')
    expect(responseMessage({ response: { status: 401, data: {} } }, 'fallback')).toBe('E-mail ou senha inválidos.')
    expect(responseMessage(new Error('hidden'), 'fallback')).toBe('fallback')
  })

  it('detects API connection failures and returns setup guidance', () => {
    expect(isApiConnectionError({ code: 'ERR_NETWORK', response: undefined })).toBe(true)
    expect(responseMessage({ code: 'ERR_NETWORK' }, 'fallback')).toBe(LOCAL_API_OFFLINE_MESSAGE)
    expect(isApiConnectionError({ response: { status: 401 } })).toBe(false)
  })
})
