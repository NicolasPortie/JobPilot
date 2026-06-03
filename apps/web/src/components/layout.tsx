import type { ReactNode } from 'react'
import {
  Bot,
  BriefcaseBusiness,
  ClipboardList,
  FileText,
  KeyRound,
  LayoutDashboard,
  Lightbulb,
  Play,
  RefreshCw,
  Search,
  ShieldCheck,
  SlidersHorizontal,
  UserRound,
  X,
} from 'lucide-react'

export function Sidebar({ tab, setTab, onLogout }: { tab: string; setTab: (id: string) => void; onLogout: () => void }) {
  const sections = [
    {
      title: 'Operar',
      items: [
        { id: 'dashboard', icon: <LayoutDashboard size={18} />, label: 'Cockpit' },
        { id: 'jobs', icon: <BriefcaseBusiness size={18} />, label: 'Fila de vagas' },
      ],
    },
    {
      title: 'Revisar',
      items: [
        { id: 'agent', icon: <Bot size={18} />, label: 'Checkpoints' },
        { id: 'runs', icon: <Play size={18} />, label: 'Execucoes' },
        { id: 'learnings', icon: <Lightbulb size={18} />, label: 'Aprendizados' },
      ],
    },
    {
      title: 'Configurar',
      items: [
        { id: 'profile', icon: <UserRound size={18} />, label: 'Perfil' },
        { id: 'resume', icon: <FileText size={18} />, label: 'Curriculo ATS' },
        { id: 'searches', icon: <Search size={18} />, label: 'Buscas' },
        { id: 'answers', icon: <ClipboardList size={18} />, label: 'Respostas' },
        { id: 'filters', icon: <SlidersHorizontal size={18} />, label: 'Filtros e regras' },
        { id: 'ai', icon: <KeyRound size={18} />, label: 'IA' },
        { id: 'credentials', icon: <ShieldCheck size={18} />, label: 'Contas' },
      ],
    },
  ]

  return (
    <aside className="sidebar">
      <div className="sidebar-header">
        <div className="logo-icon"><Bot size={20} /></div>
        <span>JobPilot</span>
      </div>
      {sections.map((section) => (
        <div className="nav-section" key={section.title}>
          <div className="nav-section-title">{section.title}</div>
          {section.items.map((item) => (
            <button key={item.id} className={`nav-item ${tab === item.id ? 'active' : ''}`} onClick={() => setTab(item.id)}>
              {item.icon}{item.label}
            </button>
          ))}
        </div>
      ))}
      <div className="sidebar-footer">
        <button className="nav-item" onClick={onLogout} style={{ color: '#94a3b8' }}><X size={18} /> Sair</button>
      </div>
    </aside>
  )
}

export function Topbar({ title, onRefresh, onLogout }: { title: string; onRefresh: () => void; onLogout: () => void }) {
  return (
    <header className="topbar">
      <h1>{title}</h1>
      <div className="topbar-actions">
        <button className="btn btn-secondary btn-sm" onClick={onRefresh}><RefreshCw size={14} /> Atualizar</button>
        <button className="icon-btn" title="Sair" onClick={onLogout}><X size={16} /></button>
      </div>
    </header>
  )
}

export function MetricCard({ icon, label, value, tone }: { icon: ReactNode; label: string; value: number; tone: string }) {
  const toneMap: Record<string, { bg: string; text: string }> = {
    teal: { bg: '#f0fdfa', text: '#0d9488' },
    amber: { bg: '#fffbeb', text: '#d97706' },
    blue: { bg: '#eff6ff', text: '#2563eb' },
    green: { bg: '#f0fdf4', text: '#16a34a' },
    red: { bg: '#fef2f2', text: '#dc2626' },
    slate: { bg: '#f1f5f9', text: '#475569' },
  }
  const t = toneMap[tone] ?? toneMap.slate

  return (
    <div className="metric-card">
      <div className="metric-icon" style={{ background: t.bg, color: t.text }}>{icon}</div>
      <div className="metric-label">{label}</div>
      <div className="metric-value">{value}</div>
    </div>
  )
}
