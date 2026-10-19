import type { ComponentType, ReactNode } from 'react'
import { clsx } from 'clsx'

type SectionCardProps = {
  title: string
  description?: string
  icon?: ComponentType<{ size?: number; strokeWidth?: number; className?: string }>
  children: ReactNode
  action?: ReactNode
  className?: string
}

/** Card de seção das páginas internas: mesmo desenho dos passos da tela de Currículo. */
export function SectionCard({ title, description, icon: Icon, children, action, className }: SectionCardProps) {
  return (
    <section className={clsx('cv-card', className)}>
      <div className="cv-sectionhead">
        {Icon && (
          <span className="cv-sectionhead__icon" aria-hidden>
            <Icon size={18} strokeWidth={2} />
          </span>
        )}
        <div className="min-w-0 flex-1">
          <h2 className="cv-steptitle">{title}</h2>
          {description && <p className="cv-stepsub">{description}</p>}
        </div>
        {action && <div className="cv-sectionhead__action">{action}</div>}
      </div>
      <div className="cv-sectionbody">{children}</div>
    </section>
  )
}
