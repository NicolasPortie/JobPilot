import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { AlertCircle, ArrowRight, BriefcaseBusiness, CircleCheck, Eye, EyeOff, FileText, Lock, Mail } from 'lucide-react'
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

const productSignals = [
  {
    index: '01',
    icon: BriefcaseBusiness,
    title: 'Traga a vaga',
    description: 'Cole o link, o texto ou o PDF e veja os requisitos com fonte e confiança.',
  },
  {
    index: '02',
    icon: FileText,
    title: 'Adapte sem inventar',
    description: 'Crie uma versão do currículo baseada no seu repertório real e nas evidências da vaga.',
  },
  {
    index: '03',
    icon: CircleCheck,
    title: 'Você decide o envio',
    description: 'Revise as diferenças, exporte o material e candidate-se onde fizer sentido.',
  },
]

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
    <main className="jp-login-shell grid min-h-[100dvh] bg-[var(--jp-bg)] text-[var(--jp-text-primary)] lg:grid-cols-[minmax(0,1.08fr)_minmax(420px,0.92fr)]">
      <section className="jp-login-story relative hidden overflow-hidden bg-[var(--jp-ink)] px-12 py-12 text-[#fffefa] lg:flex lg:flex-col xl:px-20">
        <div className="relative z-10 flex items-center gap-3">
          <span className="jp-brand-mark"><ArrowRight size={18} strokeWidth={2.5} /></span>
          <span>
            <span className="block text-[16px] font-black tracking-[-0.03em]">JobPilot</span>
            <span className="mt-0.5 block text-[10px] font-bold uppercase tracking-[0.18em] text-white/50">career workspace</span>
          </span>
        </div>

        <div className="relative z-10 my-auto max-w-[650px] py-16">
          <p className="jp-eyebrow text-[var(--jp-accent-400)]">Currículo certo para cada vaga</p>
          <h1 className="mt-6 max-w-[620px] font-serif text-[clamp(3.3rem,5.2vw,5.8rem)] font-medium leading-[0.94] tracking-[-0.065em] text-[#fffefa]">
            Adapte sem inventar nada.
          </h1>
          <p className="mt-7 max-w-[540px] text-[15px] leading-7 text-white/65">
            O JobPilot cruza seu currículo com a vaga, mostra sua aderência com evidências e prepara uma versão ATS fiel ao seu percurso.
          </p>

          <ol className="mt-12 grid max-w-[600px] gap-5 border-t border-white/12 pt-6">
            {productSignals.map(({ index, icon: Icon, title, description }) => (
              <li key={index} className="grid grid-cols-[34px_28px_minmax(0,1fr)] items-start gap-3">
                <span className="pt-1 font-mono text-[11px] font-bold text-white/35">{index}</span>
                <Icon size={17} className="mt-0.5 text-[var(--jp-accent-400)]" strokeWidth={1.8} />
                <span>
                  <span className="block text-sm font-extrabold text-white">{title}</span>
                  <span className="mt-1 block max-w-[450px] text-xs leading-5 text-white/50">{description}</span>
                </span>
              </li>
            ))}
          </ol>
        </div>

        <div className="relative z-10 flex items-end justify-between border-t border-white/12 pt-5 text-xs text-white/45">
          <span>Preparação de carreira, com contexto.</span>
          <span className="font-mono text-[10px] uppercase tracking-[0.16em]">JP / 01</span>
        </div>
      </section>

      <section className="flex min-h-[100dvh] items-center justify-center bg-[var(--jp-surface)] px-6 py-12 sm:px-10">
        <div className="w-full max-w-[420px]">
          <div className="mb-12 flex items-center gap-3 lg:hidden">
            <span className="jp-brand-mark"><ArrowRight size={18} strokeWidth={2.5} /></span>
            <span className="text-[18px] font-black tracking-[-0.03em]">JobPilot</span>
          </div>

          <div className="mb-9">
            <p className="jp-eyebrow">Acesso privado</p>
            <h2 className="mt-3 font-serif text-[clamp(2.2rem,5vw,3.2rem)] font-medium leading-none tracking-[-0.06em] text-[var(--jp-text-primary)]">Volte para o seu espaço.</h2>
            <p className="mt-4 text-sm leading-6 text-[var(--jp-text-secondary)]">Continue de onde parou e prepare sua próxima candidatura.</p>
          </div>

          <form className="grid gap-5" onSubmit={handleSubmit}>
            {apiStatus === 'offline' && (
              <div className="jp-alert jp-alert--amber text-sm leading-relaxed" role="status">
                <AlertCircle size={18} className="mt-0.5 shrink-0" />
                <div>
                  <p className="font-bold">A API local está indisponível</p>
                  <p className="mt-1">Suba o ambiente com <code className="rounded bg-amber-100 px-1 py-0.5 text-xs">.\scripts\start-mvp.ps1</code> ou execute a API depois do banco.</p>
                </div>
              </div>
            )}

            <label className="grid gap-2" htmlFor="email">
              <span className="text-xs font-extrabold uppercase tracking-[0.1em] text-[var(--jp-text-secondary)]">E-mail</span>
              <span className="group relative flex items-center">
                <Mail className="pointer-events-none absolute left-4 text-[var(--jp-text-tertiary)] transition-colors group-focus-within:text-[var(--jp-primary-600)]" size={18} />
                <input
                  aria-required="true"
                  autoComplete="email"
                  className="jp-input pl-12"
                  id="email"
                  name="email"
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="voce@exemplo.com"
                  type="email"
                  value={email}
                />
              </span>
            </label>

            <label className="grid gap-2" htmlFor="password">
              <span className="flex items-center justify-between">
                <span className="text-xs font-extrabold uppercase tracking-[0.1em] text-[var(--jp-text-secondary)]">Senha</span>
                <span className="text-[11px] font-semibold text-[var(--jp-text-tertiary)]">Acesso local</span>
              </span>
              <span className="group relative flex items-center">
                <Lock className="pointer-events-none absolute left-4 text-[var(--jp-text-tertiary)] transition-colors group-focus-within:text-[var(--jp-primary-600)]" size={18} />
                <input
                  aria-required="true"
                  autoComplete="current-password"
                  className="jp-input pl-12 pr-12"
                  id="password"
                  name="password"
                  onChange={(event) => setPassword(event.target.value)}
                  placeholder="Sua senha"
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                />
                <button
                  aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'}
                  className="absolute right-3 grid size-9 place-items-center rounded-lg text-[var(--jp-text-tertiary)] transition hover:bg-[var(--jp-surface-muted)] hover:text-[var(--jp-text-primary)]"
                  onClick={() => setShowPassword(!showPassword)}
                  type="button"
                >
                  {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </span>
            </label>

            <label className="flex cursor-pointer select-none items-center gap-2 pt-1 text-sm text-[var(--jp-text-secondary)]">
              <input checked={rememberMe} className="jp-checkbox" onChange={(event) => setRememberMe(event.target.checked)} type="checkbox" />
              Lembrar neste dispositivo
            </label>

            {error && (
              <div className="jp-alert jp-alert--red text-sm font-semibold" role="alert">
                <AlertCircle size={18} className="shrink-0" />
                {error}
              </div>
            )}

            <button className="jp-btn jp-btn--primary jp-login-submit mt-2 w-full py-4 disabled:opacity-60" type="submit" disabled={busy}>
              {busy ? 'Entrando…' : 'Entrar no JobPilot'}
              <ArrowRight size={16} />
            </button>
          </form>

          <div className="mt-10 border-t border-[var(--jp-border)] pt-5">
            <div className="flex items-center justify-between gap-4 text-[11px] font-semibold text-[var(--jp-text-tertiary)]">
              <span className="inline-flex items-center gap-2"><span className="size-1.5 rounded-full bg-[var(--jp-primary-500)]" /> Ambiente privado</span>
              <span>JobPilot / 2026</span>
            </div>
          </div>
        </div>
      </section>
    </main>
  )
}
