import React from 'react'
import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import UserPanel from '../components/ui/UserPanel'

const anchorRef = { current: null } as React.RefObject<HTMLButtonElement | null>

function renderPanel(props: Partial<React.ComponentProps<typeof UserPanel>> = {}) {
  const defaults = {
    open: true,
    anchorRef,
    userName: 'John Doe',
    onLogout: vi.fn(),
    onClose: vi.fn(),
  }
  return render(
    <MemoryRouter>
      <UserPanel {...defaults} {...props} />
    </MemoryRouter>
  )
}

describe('UserPanel', () => {
  beforeEach(() => { vi.clearAllMocks() })

  it('renders nothing when open=false', () => {
    renderPanel({ open: false })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('renders dialog with aria-label "User menu" when open=true', () => {
    renderPanel()
    expect(screen.getByRole('dialog', { name: /user menu/i })).toBeInTheDocument()
  })

  it('shows the userName', () => {
    renderPanel({ userName: 'Alice Admin' })
    expect(screen.getAllByText('Alice Admin').length).toBeGreaterThan(0)
  })

  it('shows userEmail when provided', () => {
    renderPanel({ userEmail: 'alice@example.com' })
    expect(screen.getByText('alice@example.com')).toBeInTheDocument()
  })

  it('does not show email when userEmail is not provided', () => {
    renderPanel({ userEmail: undefined })
    expect(screen.queryByText(/@/)).not.toBeInTheDocument()
  })

  it('shows "MFA Active" when hasMfa=true', () => {
    renderPanel({ hasMfa: true })
    expect(screen.getByText('MFA Active')).toBeInTheDocument()
  })

  it('shows "MFA Off" when hasMfa=false', () => {
    renderPanel({ hasMfa: false })
    expect(screen.getByText('MFA Off')).toBeInTheDocument()
  })

  it('shows "Unknown" when expiresAt is null', () => {
    renderPanel({ expiresAt: null })
    expect(screen.getByText('Unknown')).toBeInTheDocument()
  })

  it('shows "Expired" when expiresAt is in the past', () => {
    const past = new Date(Date.now() - 60_000)
    renderPanel({ expiresAt: past })
    expect(screen.getByText('Expired')).toBeInTheDocument()
  })

  it('shows minutes remaining when expiry is < 1 hour away', () => {
    const future = new Date(Date.now() + 25 * 60_000)
    renderPanel({ expiresAt: future })
    expect(screen.getByText(/^\d+ min$/)).toBeInTheDocument()
  })

  it('shows hours and minutes when expiry is > 1 hour away', () => {
    const future = new Date(Date.now() + 2 * 60 * 60_000 + 30 * 60_000)
    renderPanel({ expiresAt: future })
    expect(screen.getByText(/\d+h \d+m/)).toBeInTheDocument()
  })

  it('renders "My Profile" account link', () => {
    renderPanel()
    expect(screen.getByText('My Profile')).toBeInTheDocument()
  })

  it('renders "Change Password" account link', () => {
    renderPanel()
    expect(screen.getByText('Change Password')).toBeInTheDocument()
  })

  it('renders "MFA Settings" account link', () => {
    renderPanel()
    expect(screen.getByText('MFA Settings')).toBeInTheDocument()
  })

  it('renders "Active Sessions" security link', () => {
    renderPanel()
    expect(screen.getByText('Active Sessions')).toBeInTheDocument()
  })

  it('calls onClose when a navigation link is clicked', () => {
    const onClose = vi.fn()
    renderPanel({ onClose })
    fireEvent.click(screen.getByText('My Profile'))
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('calls onLogout and onClose when Sign Out is clicked', () => {
    const onLogout = vi.fn()
    const onClose = vi.fn()
    renderPanel({ onLogout, onClose })
    fireEvent.click(screen.getByText('Sign Out'))
    expect(onLogout).toHaveBeenCalledTimes(1)
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('shows initials from userName in avatar', () => {
    renderPanel({ userName: 'John Doe' })
    expect(screen.getByText('JD')).toBeInTheDocument()
  })

  it('calls onClose when "Active Sessions" security link is clicked', () => {
    const onClose = vi.fn()
    renderPanel({ onClose })
    fireEvent.click(screen.getByText('Active Sessions'))
    expect(onClose).toHaveBeenCalledTimes(1)
  })
})
