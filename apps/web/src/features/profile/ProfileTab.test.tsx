import { useState } from 'react'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { emptyProfile } from '../../app/defaults'
import type { Profile } from '../../types'
import { ProfileTab } from './ProfileTab'

type Props = Parameters<typeof ProfileTab>[0]

function Harness({ initial = emptyProfile, onProfile, extra = {} }: { initial?: Profile; onProfile?: (profile: Profile) => void; extra?: Partial<Props> }) {
  const [profile, setProfile] = useState<Profile>(initial)
  onProfile?.(profile)
  return (
    <ProfileTab
      profile={profile}
      setProfile={setProfile}
      recommendedSkillAdditions={[]}
      onSave={vi.fn()}
      saveBusy={false}
      notice={null}
      onRemoveRecommendedSkillAddition={vi.fn()}
      onImportResumeFile={vi.fn()}
      onDeleteProfile={vi.fn()}
      onDeleteAccount={vi.fn()}
      onRetryDanger={vi.fn()}
      retention={null}
      dangerNotice=""
      dangerBusy={false}
      selectedResumeFile={null}
      setSelectedResumeFile={vi.fn()}
      importBusy={false}
      importResult={null}
      importNotice=""
      validationIssues={[]}
      {...extra}
    />
  )
}

describe('ProfileTab', () => {
  afterEach(() => cleanup())

  it('lets the user type a comma-separated skill list without losing separators', () => {
    let latest = emptyProfile
    render(<Harness onProfile={(profile) => { latest = profile }} />)
    const field = screen.getByPlaceholderText('React, TypeScript, Node.js...') as HTMLInputElement

    fireEvent.change(field, { target: { value: 'React, ' } })
    expect(field.value).toBe('React, ')
    fireEvent.change(field, { target: { value: 'React, Vue' } })

    expect(field.value).toBe('React, Vue')
    expect(latest.skills).toEqual(['React', 'Vue'])
  })

  it('starts with the skills already saved in the profile', () => {
    const view = render(<Harness initial={{ ...emptyProfile, skills: ['C#'] }} />)
    expect((screen.getByPlaceholderText('React, TypeScript, Node.js...') as HTMLInputElement).value).toBe('C#')
    view.unmount()
  })

  it('does not rewrite the UF on the screen; the server normalizes it', () => {
    let latest = emptyProfile
    render(<Harness onProfile={(profile) => { latest = profile }} />)
    fireEvent.change(screen.getByPlaceholderText('SP'), { target: { value: 's' } })
    expect(latest.state).toBe('s')
  })

  it('shows the save result next to the save button', () => {
    const onSave = vi.fn()
    render(<Harness extra={{ onSave, notice: { tone: 'ok', text: 'Perfil salvo.' } }} />)
    expect(screen.getByRole('status').textContent).toContain('Perfil salvo.')
    fireEvent.click(screen.getByRole('button', { name: /Salvar perfil/ }))
    expect(onSave).toHaveBeenCalledOnce()
  })

  it('disables saving while a save is running', () => {
    render(<Harness extra={{ saveBusy: true }} />)
    expect((screen.getByRole('button', { name: /Salvando/ }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('removes a recommendation from the history', () => {
    const onRemove = vi.fn()
    render(<Harness extra={{
      onRemoveRecommendedSkillAddition: onRemove,
      recommendedSkillAdditions: [{ id: 'r1', skill: 'Docker', kind: 'missing', reason: 'Requisito da vaga', jobTitle: 'Dev', company: 'ACME', createdAt: '2026-10-18T12:00:00Z' }],
    }} />)
    fireEvent.click(screen.getByRole('button', { name: 'Remover Docker do histórico' }))
    expect(onRemove).toHaveBeenCalledWith('r1')
  })
})
