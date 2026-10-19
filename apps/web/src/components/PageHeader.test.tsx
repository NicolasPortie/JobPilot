import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PageHeader } from './PageHeader'

const info = { eyebrow: 'Base profissional', title: 'Tudo começa pelo seu repertório', description: 'Dados claros.' }

function setup(overrides: Partial<Parameters<typeof PageHeader>[0]> = {}) {
  const props = {
    info,
    user: { userName: 'Ana Silva', userRole: 'Pleno', completion: 40 },
    refreshing: false,
    onNavigate: vi.fn(),
    onRefresh: vi.fn(),
    onLogout: vi.fn(),
    ...overrides,
  }
  render(<PageHeader {...props} />)
  return props
}

describe('PageHeader', () => {
  afterEach(() => cleanup())

  it('shows the page title and the account chip', () => {
    setup()
    expect(screen.getByRole('heading', { level: 1, name: info.title })).toBeTruthy()
    expect(screen.getByText('BASE PROFISSIONAL')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Conta de Ana Silva' })).toBeTruthy()
  })

  it('opens the account menu and navigates, refreshes and logs out', () => {
    const props = setup()
    const chip = screen.getByRole('button', { name: 'Conta de Ana Silva' })
    expect(screen.queryByRole('menu')).toBeNull()

    fireEvent.click(chip)
    fireEvent.click(screen.getByRole('menuitem', { name: /Configurações/ }))
    expect(props.onNavigate).toHaveBeenCalledWith('settings')
    expect(screen.queryByRole('menu')).toBeNull()

    fireEvent.click(chip)
    fireEvent.click(screen.getByRole('menuitem', { name: /Meu perfil/ }))
    expect(props.onNavigate).toHaveBeenLastCalledWith('profile')

    fireEvent.click(chip)
    fireEvent.click(screen.getByRole('menuitem', { name: /Atualizar dados/ }))
    expect(props.onRefresh).toHaveBeenCalledOnce()

    fireEvent.click(chip)
    fireEvent.click(screen.getByRole('menuitem', { name: /Sair da conta/ }))
    expect(props.onLogout).toHaveBeenCalledOnce()
  })

  it('closes the menu with Escape and disables refresh while loading', () => {
    setup({ refreshing: true })
    fireEvent.click(screen.getByRole('button', { name: 'Conta de Ana Silva' }))
    expect((screen.getByRole('menuitem', { name: /Atualizando/ }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.keyDown(document, { key: 'Escape' })
    expect(screen.queryByRole('menu')).toBeNull()
  })

  it('falls back to a neutral label when the profile has no name yet', () => {
    setup({ user: { userName: '', userRole: '', completion: 0 } })
    expect(screen.getByRole('button', { name: 'Conta de Minha conta' })).toBeTruthy()
  })
})
