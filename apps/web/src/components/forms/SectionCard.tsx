import type { ComponentType, ReactNode } from 'react'
import { clsx } from 'clsx'

type SectionCardProps = {
  title: string
  icon?: ComponentType<{ size?: number; strokeWidth?: number; className?: string }>
  children: ReactNode
  action?: ReactNode
  className?: string
}

export function SectionCard({ title, icon: Icon, children, action, className }: SectionCardProps) {
  return (
    <div className={clsx('jp-card', className)}>
      <div className="jp-section-header">
        {Icon && (
          <span className="grid size-8 place-items-center rounded-lg bg-teal-50 text-teal-700">
            <Icon size={16} strokeWidth={2} />
          </span>
        )}
        <h2 className="flex-1 text-sm font-black text-slate-900">{title}</h2>
        {action && <div className="shrink-0">{action}</div>}
      </div>
      <div className="p-5">{children}</div>
    </div>
  )
}
