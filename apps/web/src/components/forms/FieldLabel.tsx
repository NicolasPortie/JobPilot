import type { ComponentType, ReactNode } from 'react'

type FieldLabelProps = {
  children: ReactNode
  icon?: ComponentType<{ size?: number; className?: string }>
  auxiliary?: string
  required?: boolean
}

export function FieldLabel({ children, icon: Icon, auxiliary, required }: FieldLabelProps) {
  return (
    <span className="mb-1.5 flex items-center gap-1.5 text-sm font-bold text-slate-700">
      {Icon && <Icon size={14} className="text-slate-400" />}
      <span>{children}</span>
      {required && <span className="text-red-500">*</span>}
      {auxiliary && (
        <span className="ml-auto text-xs font-normal text-slate-400">{auxiliary}</span>
      )}
    </span>
  )
}
