import { useState, type ComponentType, type ReactNode } from 'react'
import {
  ArrowRight,
  ArrowUpRight,
  FileText,
  ListChecks,
  LogOut,
  Menu,
  RefreshCw,
  UserRound,
  X,
} from 'lucide-react'
import { clsx } from 'clsx'
import { WorkspaceEntrance } from './motion/WorkspaceEntrance'

type NavItem = {
  id: string
  icon: ComponentType<{ size?: number; strokeWidth?: number; className?: string }>
  label: string
  description: string
}

const navGroups: { label: string; items: NavItem[] }[] = [
  {
    label: 'Seu fluxo',
    items: [
      { id: 'resume', icon: FileText, label: 'Currículo', description: 'Adaptar e exportar' },
      { id: 'applications', icon: ListChecks, label: 'Candidaturas', description: 'Histórico e envios' },
      { id: 'profile', icon: UserRound, label: 'Perfil', description: 'Base profissional' },
    ],
  },
]

const pageInfo: Record<string, { eyebrow: string; title: string; description: string }> = {
  resume: { eyebrow: 'Material de candidatura', title: 'Seu currículo, com contexto', description: 'Mantenha uma base verdadeira e adapte cada versão para a oportunidade certa.' },
  applications: { eyebrow: 'Acompanhamento', title: 'Candidaturas preparadas', description: 'Baixe de novo o currículo de cada vaga, abra o anúncio e marque o que já enviou.' },
  profile: { eyebrow: 'Base profissional', title: 'Tudo começa pelo seu repertório', description: 'Dados claros geram análises melhores e sugestões mais honestas.' },
}

export type ResumeMeta = {
  userName: string
  userRole: string
  completion: number
}

export function AppLayout({ tab, setTab, onRefresh, onLogout, children, notice = '', refreshing = false, resumeMeta }: {
  tab: string
  setTab: (id: string) => void
  onRefresh: () => void
  onLogout: () => void
  children: ReactNode
  notice?: string
  refreshing?: boolean
  actionBusy?: boolean
  resumeMeta?: ResumeMeta
}) {
  const [mobileOpen, setMobileOpen] = useState(false)
  const page = pageInfo[tab] ?? pageInfo.resume
  const isCv = tab === 'resume'

  const selectTab = (id: string) => {
    setTab(id)
    setMobileOpen(false)
  }

  return (
    <div className="jp-app-shell min-h-[100dvh] text-[var(--jp-text-primary)] lg:grid lg:grid-cols-[248px_minmax(0,1fr)]">
      <Sidebar tab={tab} onSelect={selectTab} onLogout={onLogout} resumeMeta={resumeMeta} className="sticky top-0 hidden h-[100dvh] lg:flex" />

      {mobileOpen && (
        <div className="jp-mobile-scrim fixed inset-0 z-50 lg:hidden" onClick={() => setMobileOpen(false)}>
          <Sidebar
            tab={tab}
            onSelect={selectTab}
            onLogout={onLogout}
            resumeMeta={resumeMeta}
            className="h-full w-[min(88vw,328px)]"
            onClose={() => setMobileOpen(false)}
          />
        </div>
      )}

      <main className="w-full min-w-0 px-4 pb-8 pt-20 sm:px-6 lg:px-10 lg:py-8">
        <button
          className="jp-mobile-menu fixed left-4 top-4 z-40 inline-flex size-11 items-center justify-center rounded-xl border border-[var(--jp-border)] bg-[var(--jp-surface)] text-[var(--jp-text-secondary)] shadow-[var(--jp-shadow-sm)] transition hover:border-[var(--jp-border-strong)] hover:text-[var(--jp-text-primary)] lg:hidden"
          onClick={() => setMobileOpen(true)}
          aria-label="Abrir menu"
        >
          <Menu size={20} />
        </button>

        <WorkspaceEntrance className="jp-workspace">
          <div className="jp-main-content">
            {!(isCv && resumeMeta) && (
            <header data-entrance className="jp-topbar mb-8 flex flex-col gap-5 border-b border-[var(--jp-border)] pb-6 sm:flex-row sm:items-end sm:justify-between">
              <div className="min-w-0">
                <p className="jp-eyebrow">{page.eyebrow}</p>
                <h1 className="jp-page-title mt-2">{page.title}</h1>
                <p className="jp-page-description mt-2">{page.description}</p>
              </div>
              <div className="flex shrink-0 items-center gap-3">
                <button className="jp-btn jp-btn--secondary jp-btn--compact disabled:opacity-60" onClick={onRefresh} title="Atualizar dados" disabled={refreshing} aria-busy={refreshing}>
                  <RefreshCw size={14} className={refreshing ? 'animate-spin' : ''} />
                  <span className="hidden sm:inline">{refreshing ? 'Atualizando…' : 'Atualizar'}</span>
                </button>
                <div className="jp-user-mark" aria-label="Conta ativa">JP</div>
              </div>
            </header>
            )}
            {notice && (
              <div className="jp-alert jp-alert--amber mb-6 text-sm" role="status">
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

function Sidebar({ tab, onSelect, onLogout, resumeMeta, className, onClose }: {
  tab: string
  onSelect: (id: string) => void
  onLogout: () => void
  resumeMeta?: ResumeMeta
  className?: string
  onClose?: () => void
}) {
  if ((tab === 'resume' || tab === 'applications') && resumeMeta) {
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
            <button className={clsx('cv-s-item', tab === 'resume' && 'cv-s-item--active')} onClick={() => onSelect('resume')} aria-current={tab === 'resume' ? 'page' : undefined}>
              <span className="cv-s-item__icon"><FileText size={17} /></span>
              <span className="min-w-0 flex-1">
                <span className="cv-s-item__label">Currículo</span>
                <span className="cv-s-item__desc">Adaptar e exportar</span>
              </span>
            </button>
            <button className={clsx('cv-s-item', tab === 'applications' && 'cv-s-item--active')} onClick={() => onSelect('applications')} aria-current={tab === 'applications' ? 'page' : undefined}>
              <span className="cv-s-item__icon"><ListChecks size={17} /></span>
              <span className="min-w-0 flex-1">
                <span className="cv-s-item__label">Candidaturas</span>
                <span className="cv-s-item__desc">Histórico e envios</span>
              </span>
            </button>
            <button className="cv-s-item" onClick={() => onSelect('profile')}>
              <span className="cv-s-item__icon"><UserRound size={17} /></span>
              <span className="min-w-0 flex-1">
                <span className="cv-s-item__label">Perfil</span>
                <span className="cv-s-item__desc">Base profissional</span>
              </span>
            </button>
          </div>

          <div className="cv-s-profile">
            <p className="cv-s-profile__title">Perfil {resumeMeta.completion}% completo</p>
            <div className="cv-s-profile__bar" role="progressbar" aria-valuenow={resumeMeta.completion} aria-valuemin={0} aria-valuemax={100} aria-label="Completude do perfil">
              <div className="cv-s-profile__fill" style={{ width: resumeMeta.completion.toString() + '%' }} />
            </div>
            <p className="cv-s-profile__text">Complete seu perfil para receber recomendações mais precisas.</p>
            <button className="cv-s-profile__link" onClick={() => onSelect('profile')}>
              Completar perfil <ArrowRight size={14} />
            </button>
          </div>

          <div className="cv-s-foot">
            <button className="cv-s-item" onClick={onLogout}>
              <span className="cv-s-item__icon"><LogOut size={17} /></span>
              <span className="min-w-0 flex-1"><span className="cv-s-item__label">Sair da conta</span></span>
            </button>
          </div>
        </nav>

        <div className="cv-s-art" aria-hidden={false}>
          <p className="cv-s-art__text">Grandes carreiras começam com boas decisões.</p>
          <div className="cv-s-art__rule" />
        </div>
      </aside>
    )
  }

  return (
    <aside className={clsx('jp-sidebar flex flex-col border-r border-[var(--jp-border)] px-3 py-4', className)} onClick={(event) => event.stopPropagation()}>
      <div className="mb-8 flex items-center justify-between px-2">
        <button className="group flex min-w-0 items-center gap-3 text-left" onClick={() => onSelect('resume')} aria-label="Abrir currículo">
          <span className="jp-brand-mark"><ArrowUpRight size={18} strokeWidth={2.4} /></span>
          <span className="min-w-0">
            <span className="block truncate text-[16px] font-black tracking-[-0.03em] text-[var(--jp-text-primary)]">JobPilot</span>
            <span className="mt-0.5 block truncate text-[10px] font-bold uppercase tracking-[0.18em] text-[var(--jp-text-tertiary)]">career workspace</span>
          </span>
        </button>

        {onClose && (
          <button className="grid size-9 shrink-0 place-items-center rounded-lg text-[var(--jp-text-tertiary)] transition hover:bg-[var(--jp-surface-muted)] hover:text-[var(--jp-text-primary)]" onClick={onClose} aria-label="Fechar menu">
            <X size={19} />
          </button>
        )}
      </div>

      <nav className="min-h-0 flex-1 overflow-y-auto pr-1 scrollbar-hide" aria-label="Navegação principal">
        <div className="grid gap-8">
          {navGroups.map((group) => (
            <div key={group.label}>
              <div className="jp-nav-label mb-2 px-3">{group.label}</div>
              <div className="grid gap-1">
                {group.items.map((item) => {
                  const isActive = tab === item.id
                  return (
                    <button
                      key={item.id}
                      className={clsx('jp-nav-item group relative flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-left transition', isActive && 'jp-nav-item--active')}
                      onClick={() => onSelect(item.id)}
                    >
                      <span className="jp-nav-icon grid size-8 shrink-0 place-items-center rounded-lg transition">
                        <item.icon size={16} strokeWidth={isActive ? 2.4 : 1.9} />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-[13px] font-extrabold">{item.label}</span>
                        <span className="mt-0.5 block truncate text-[11px] font-semibold text-[var(--jp-text-tertiary)] group-hover:text-[var(--jp-text-secondary)]">{item.description}</span>
                      </span>
                    </button>
                  )
                })}
              </div>
            </div>
          ))}
        </div>
      </nav>

      <div className="mt-8 border-t border-[var(--jp-border)] pt-4">
        <div className="mb-3 rounded-xl bg-[var(--jp-surface-muted)] px-3 py-3">
          <p className="text-[11px] font-extrabold uppercase tracking-[0.12em] text-[var(--jp-text-tertiary)]">Princípio do espaço</p>
          <p className="mt-1 text-xs leading-5 text-[var(--jp-text-secondary)]">Clareza primeiro. Cada sugestão precisa ter uma fonte real.</p>
        </div>
        <button className="jp-btn jp-btn--ghost min-h-10 w-full justify-start px-3 text-xs" onClick={onLogout}>
          <LogOut size={14} /> Sair da conta
        </button>
      </div>
    </aside>
  )
}
