import { useEffect, useState, type ComponentType, type ReactNode } from 'react'
import { AlertCircle, ArrowRight, FileText, ListChecks, LogOut, Menu, Settings, UserRound, X } from 'lucide-react'
import { clsx } from 'clsx'
import { WorkspaceEntrance } from './motion/WorkspaceEntrance'
import { PageHeader, type PageHeaderInfo, type UserMeta } from './PageHeader'

type NavItem = {
  id: string
  icon: ComponentType<{ size?: number; strokeWidth?: number; className?: string }>
  label: string
  description: string
}

const navItems: NavItem[] = [
  { id: 'resume', icon: FileText, label: 'Currículo', description: 'Adaptar e exportar' },
  { id: 'applications', icon: ListChecks, label: 'Candidaturas', description: 'Histórico e envios' },
  { id: 'profile', icon: UserRound, label: 'Perfil', description: 'Base profissional' },
  { id: 'settings', icon: Settings, label: 'Configurações', description: 'IA e preferências' },
]

const pageInfo: Record<string, PageHeaderInfo> = {
  resume: {
    eyebrow: 'Currículo que te aproxima',
    title: 'Adapte seu currículo para esta vaga',
    description: 'Cole a vaga e receba, em segundos, uma versão verdadeira do seu currículo ajustada ao que ela pede. Você revisa, baixa o PDF e envia a candidatura.',
  },
  applications: {
    eyebrow: 'Acompanhamento',
    title: 'Candidaturas preparadas',
    description: 'Baixe de novo o currículo de cada vaga, abra o anúncio e marque o que já enviou.',
  },
  profile: {
    eyebrow: 'Base profissional',
    title: 'Tudo começa pelo seu repertório',
    description: 'Dados claros geram análises melhores e sugestões mais honestas.',
  },
  settings: {
    eyebrow: 'Preferências',
    title: 'Configurações',
    description: 'Escolha a inteligência artificial que ajuda a escrever o currículo e teste a conexão.',
  },
}

export type ResumeMeta = UserMeta

export function AppLayout({ tab, setTab, onRefresh, onLogout, children, notice = '', refreshing = false, resumeMeta }: {
  tab: string
  setTab: (id: string) => void
  onRefresh: () => void
  onLogout: () => void
  children: ReactNode
  notice?: string
  refreshing?: boolean
  actionBusy?: boolean
  resumeMeta: UserMeta
}) {
  const [mobileOpen, setMobileOpen] = useState(false)
  const page = pageInfo[tab] ?? pageInfo.resume

  // O fundo quente e a faixa lateral valem para todas as páginas autenticadas.
  useEffect(() => {
    document.body.classList.add('cv-fundo')
    return () => document.body.classList.remove('cv-fundo')
  }, [])

  const selectTab = (id: string) => {
    setTab(id)
    setMobileOpen(false)
  }

  return (
    <div className="jp-app-shell min-h-[100dvh] text-[var(--cv-ink)] lg:grid lg:grid-cols-[248px_minmax(0,1fr)]">
      <Sidebar tab={tab} onSelect={selectTab} onLogout={onLogout} completion={resumeMeta.completion} className="sticky top-0 hidden h-[100dvh] lg:flex" />

      {mobileOpen && (
        <div className="jp-mobile-scrim fixed inset-0 z-50 lg:hidden" onClick={() => setMobileOpen(false)}>
          <Sidebar
            tab={tab}
            onSelect={selectTab}
            onLogout={onLogout}
            completion={resumeMeta.completion}
            className="h-full w-[min(88vw,328px)]"
            onClose={() => setMobileOpen(false)}
          />
        </div>
      )}

      <main className="w-full min-w-0 px-4 pb-8 pt-20 sm:px-6 lg:px-10 lg:py-8">
        <button
          className="jp-mobile-menu fixed left-4 top-4 z-40 inline-flex size-11 items-center justify-center rounded-xl border border-[var(--cv-border)] bg-[var(--cv-card)] text-[var(--cv-muted)] shadow-[var(--cv-shadow)] transition hover:border-[var(--cv-border-strong)] hover:text-[var(--cv-ink)] lg:hidden"
          onClick={() => setMobileOpen(true)}
          aria-label="Abrir menu"
        >
          <Menu size={20} />
        </button>

        <WorkspaceEntrance className="jp-workspace">
          <div className="jp-main-content">
            <div data-entrance>
              <PageHeader info={page} user={resumeMeta} refreshing={refreshing} onNavigate={selectTab} onRefresh={onRefresh} onLogout={onLogout} />
            </div>
            {notice && (
              <div className="cv-alert cv-alert--amber" style={{ marginBottom: 16 }} role="status">
                <AlertCircle size={16} className="mt-0.5 shrink-0" />
                {notice}
              </div>
            )}
            <div key={tab} data-entrance className="jp-page-content">
              {children}
            </div>
          </div>
        </WorkspaceEntrance>
      </main>
    </div>
  )
}

function Sidebar({ tab, onSelect, onLogout, completion, className, onClose }: {
  tab: string
  onSelect: (id: string) => void
  onLogout: () => void
  completion: number
  className?: string
  onClose?: () => void
}) {
  return (
    <aside className={clsx('jp-sidebar flex flex-col px-3 pb-0 pt-5', className)} onClick={(event) => event.stopPropagation()}>
      <div className="mb-2 flex items-center justify-between">
        <div className="cv-s-brand">
          <p className="cv-s-brand__logo">JobPilot</p>
          <p className="cv-s-brand__tag">Sua carreira<br />com mais direção</p>
        </div>
        {onClose && (
          <button className="grid size-9 shrink-0 place-items-center rounded-lg text-[var(--cv-muted)] transition hover:bg-[var(--cv-peach)] hover:text-[var(--cv-ink)]" onClick={onClose} aria-label="Fechar menu">
            <X size={19} />
          </button>
        )}
      </div>

      <nav className="min-h-0 min-w-0 flex-1 overflow-y-auto pr-1 scrollbar-hide" aria-label="Navegação principal">
        <div className="cv-s-nav">
          {navItems.map((item) => (
            <button
              key={item.id}
              className={clsx('cv-s-item', tab === item.id && 'cv-s-item--active')}
              onClick={() => onSelect(item.id)}
              aria-current={tab === item.id ? 'page' : undefined}
            >
              <span className="cv-s-item__icon"><item.icon size={17} /></span>
              <span className="min-w-0 flex-1">
                <span className="cv-s-item__label">{item.label}</span>
                <span className="cv-s-item__desc">{item.description}</span>
              </span>
            </button>
          ))}
        </div>

        <div className="cv-s-profile">
          <p className="cv-s-profile__title">Perfil {completion}% completo</p>
          <div className="cv-s-profile__bar" role="progressbar" aria-valuenow={completion} aria-valuemin={0} aria-valuemax={100} aria-label="Completude do perfil">
            <div className="cv-s-profile__fill" style={{ width: `${completion}%` }} />
          </div>
          <p className="cv-s-profile__text">Complete seu perfil para receber recomendações mais precisas.</p>
          {tab !== 'profile' && (
            <button className="cv-s-profile__link" onClick={() => onSelect('profile')}>
              Completar perfil <ArrowRight size={14} />
            </button>
          )}
        </div>

        <div className="cv-s-foot">
          <button className="cv-s-item" onClick={onLogout}>
            <span className="cv-s-item__icon"><LogOut size={17} /></span>
            <span className="min-w-0 flex-1"><span className="cv-s-item__label">Sair da conta</span></span>
          </button>
        </div>
      </nav>

      <div className="cv-s-art">
        <p className="cv-s-art__text">Grandes carreiras começam com boas decisões.</p>
        <div className="cv-s-art__rule" />
      </div>
    </aside>
  )
}
