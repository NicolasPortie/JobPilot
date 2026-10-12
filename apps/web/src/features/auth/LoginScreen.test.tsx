import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LoginScreen } from './LoginScreen'

describe('LoginScreen', () => {
  afterEach(() => cleanup())

  it('captures credentials and submits login', async () => {
    const user = userEvent.setup()
    const setEmail = vi.fn()
    const setPassword = vi.fn()
    const onLogin = vi.fn()

    render(
      <LoginScreen
        email=""
        password=""
        error=""
        setEmail={setEmail}
        setPassword={setPassword}
        onLogin={onLogin}
      />,
    )

    fireEvent.change(screen.getByRole('textbox', { name: /e-mail/i }), { target: { value: 'candidate@example.com' } })
    fireEvent.change(screen.getByPlaceholderText('Sua senha'), { target: { value: 'secret' } })
    await user.click(screen.getByRole('button', { name: /entrar no jobpilot/i }))

    expect(setEmail).toHaveBeenLastCalledWith('candidate@example.com')
    expect(setPassword).toHaveBeenLastCalledWith('secret')
    expect(onLogin).toHaveBeenCalledOnce()
  })

  it('toggles password visibility and renders errors', async () => {
    const user = userEvent.setup()

    render(
      <LoginScreen
        email="candidate@example.com"
        password="secret"
        error="Credenciais invalidas"
        setEmail={vi.fn()}
        setPassword={vi.fn()}
        onLogin={vi.fn()}
      />,
    )

    const passwordInput = screen.getByPlaceholderText('Sua senha')
    expect(passwordInput.getAttribute('type')).toBe('password')
    expect(screen.getByText('Credenciais invalidas')).toBeTruthy()

    await user.click(screen.getByRole('button', { name: /mostrar senha/i }))
    expect(passwordInput.getAttribute('type')).toBe('text')
  })
})
