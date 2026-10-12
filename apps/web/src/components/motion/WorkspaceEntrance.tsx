import { useRef, type ReactNode } from 'react'
import { useGSAP } from '@gsap/react'
import { gsap } from 'gsap'

type WorkspaceEntranceProps = {
  children: ReactNode
  className?: string
}

/**
 * A restrained entrance for the workspace. It is intentionally scoped to the
 * shell so navigation remains calm and feature interactions stay immediate.
 */
export function WorkspaceEntrance({ children, className }: WorkspaceEntranceProps) {
  const root = useRef<HTMLDivElement>(null)

  useGSAP(() => {
    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
    const targets = gsap.utils.toArray<HTMLElement>('[data-entrance]', root.current)

    if (reduceMotion || targets.length === 0) {
      gsap.set(targets, { autoAlpha: 1, y: 0 })
      return
    }

    const timeline = gsap.timeline({ defaults: { ease: 'power3.out' } })
    timeline.fromTo(
      targets,
      { autoAlpha: 0, y: 10 },
      { autoAlpha: 1, y: 0, duration: 0.38, stagger: { each: 0.055, from: 'start' } },
    )
  }, { scope: root })

  return <div ref={root} className={className}>{children}</div>
}
