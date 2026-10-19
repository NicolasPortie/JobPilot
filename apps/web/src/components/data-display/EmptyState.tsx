import type { ComponentType, ReactNode } from 'react'

type EmptyStateProps = {
  icon?: ComponentType<{ size?: number; strokeWidth?: number; className?: string }>
  title: string
  description?: string
  action?: ReactNode
}

export function EmptyState({ icon: Icon, title, description, action }: EmptyStateProps) {
  return (
    <div className="cv-empty">
      {Icon && (
        <span className="cv-empty__icon" aria-hidden>
          <Icon size={22} strokeWidth={1.8} />
        </span>
      )}
      <h3 className="cv-empty__title">{title}</h3>
      {description && <p className="cv-empty__text">{description}</p>}
      {action && <div style={{ marginTop: 8 }}>{action}</div>}
    </div>
  )
}
