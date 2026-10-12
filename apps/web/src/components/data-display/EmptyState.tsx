import type { ComponentType } from 'react'

type EmptyStateProps = {
  icon?: ComponentType<{ size?: number; strokeWidth?: number; className?: string }>
  title: string
  description?: string
  action?: React.ReactNode
}

export function EmptyState({ icon: Icon, title, description, action }: EmptyStateProps) {
  return (
    <div className="jp-empty-state">
      <div>
        {Icon && (
          <div className="jp-empty-state__icon">
            <Icon size={32} strokeWidth={1.4} />
          </div>
        )}
        <h3 className="jp-empty-state__title">{title}</h3>
        {description && <p className="jp-empty-state__desc">{description}</p>}
        {action && <div className="mt-4">{action}</div>}
      </div>
    </div>
  )
}
