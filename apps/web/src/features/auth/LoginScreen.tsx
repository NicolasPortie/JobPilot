import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { AlertCircle, Eye, EyeOff } from 'lucide-react'
import { checkApiHealth } from '../../api/healthClient'

type LoginScreenProps = {
  email: string
  password: string
  error: string
  busy?: boolean
  setEmail: (value: string) => void
  setPassword: (value: string) => void
  onLogin: () => void
}

export function LoginScreen({ email, password, error, busy = false, setEmail, setPassword, onLogin }: LoginScreenProps) {
  const [showPassword, setShowPassword] = useState(false)
  const [rememberMe, setRememberMe] = useState(() => {
    try {
      return localStorage.getItem('jobpilot_remember_email') === 'true'
    } catch {
      return false
    }
  })
  const [apiStatus, setApiStatus] = useState<'checking' | 'online' | 'offline'>('checking')

  // Mesmo fundo quente do restante do app.
  useEffect(() => {
    document.body.classList.add('cv-fundo')
    return () => document.body.classList.remove('cv-fundo')
  }, [])

  useEffect(() => {
    let active = true

    const refreshApiStatus = async () => {
      const healthy = await checkApiHealth()
      if (active) setApiStatus(healthy ? 'online' : 'offline')
    }

    void refreshApiStatus()
    const intervalId = window.setInterval(() => void refreshApiStatus(), 10000)

    return () => {
      active = false
      window.clearInterval(intervalId)
    }
  }, [])

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    try {
      localStorage.setItem('jobpilot_remember_email', rememberMe ? 'true' : 'false')
      if (rememberMe) localStorage.setItem('jobpilot_saved_email', email)
      else localStorage.removeItem('jobpilot_saved_email')
    } catch {
      // Storage can be unavailable in private browsing.
    }
    onLogin()
  }

  return (
    <main className="cv-login">
      <aside className="cv-login__side">
        <div className="cv-s-brand" style={{ padding: 0 }}>
          <p className="cv-s-brand__logo">JobPilot</p>
          <p className="cv-s-brand__tag">Sua carreira<br />com mais direção</p>
        </div>
        <div className="cv-s-art">
          <p className="cv-s-art__text">Grandes carreiras começam com boas decisões.</p>
          <div className="cv-s-art__rule" />
        </div>
      </aside>

      <div className="cv-login__box">
        <h1 className="cv-login__title">Entrar</h1>
        <p className="cv-hint" style={{ marginTop: 4 }}>Use o e-mail e a senha da sua conta.</p>

        <form className="cv-form" style={{ marginTop: 20 }} onSubmit={handleSubmit}>
          {apiStatus === 'offline' && (
            <div className="cv-alert cv-alert--amber" role="status">
              <AlertCircle size={18} className="mt-0.5 shrink-0" />
              <div>
                <p style={{ fontWeight: 800 }}>A API local está indisponível</p>
                <p>Suba o ambiente com <code>.\scripts\start-mvp.ps1</code> ou execute a API depois do banco.</p>
              </div>
            </div>
          )}

          <label htmlFor="email">
            <span className="cv-label">E-mail</span>
            <input
              aria-required="true"
              autoComplete="email"
              className="cv-input"
              id="email"
              name="email"
              onChange={(event) => setEmail(event.target.value)}
              placeholder="voce@exemplo.com"
              type="email"
              value={email}
            />
          </label>

          <label htmlFor="password">
            <span className="cv-label">Senha</span>
            <span className="cv-login__field">
              <input
                aria-required="true"
                autoComplete="current-password"
                className="cv-input cv-login__input--toggle"
                id="password"
                name="password"
                onChange={(event) => setPassword(event.target.value)}
                placeholder="Sua senha"
                type={showPassword ? 'text' : 'password'}
                value={password}
              />
              <button
                aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'}
                className="cv-iconbtn cv-login__toggle"
                onClick={() => setShowPassword(!showPassword)}
                type="button"
              >
                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </span>
          </label>

          <label className="cv-check">
            <input checked={rememberMe} onChange={(event) => setRememberMe(event.target.checked)} type="checkbox" />
            Lembrar neste dispositivo
          </label>

          {error && (
            <div className="cv-alert cv-alert--red" role="alert">
              <AlertCircle size={18} className="shrink-0" />
              {error}
            </div>
          )}

          <button className="cv-btn cv-btn--primary cv-btn--block" type="submit" disabled={busy}>
            {busy ? 'Entrando…' : 'Entrar no JobPilot'}
          </button>
        </form>
      </div>
    </main>
  )
}
