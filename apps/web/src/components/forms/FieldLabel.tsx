import type { ComponentType, ReactNode } from 'react'

type FieldLabelProps = {
  children: ReactNode
  icon?: ComponentType<{ size?: number; className?: string }>
  auxiliary?: string
  required?: boolean
}

export function FieldLabel({ children, icon: Icon, auxiliary, required }: FieldLabelProps) {
  return (
    <span className="cv-label cv-label--row">
      {Icon && <Icon size={14} className="text-[var(--cv-faint)]" />}
      <span>{children}</span>
      {required && <span className="cv-label__req">*</span>}
      {auxiliary && <span className="cv-label__aux">{auxiliary}</span>}
    </span>
  )
}
