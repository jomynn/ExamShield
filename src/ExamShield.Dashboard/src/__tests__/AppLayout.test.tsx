import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import AppLayout from '../components/layout/AppLayout'

vi.mock('../hooks/useNotifications', () => ({
  useNotifications: vi.fn(),
}))

import * as notifHook from '../hooks/useNotifications'

const EMPTY_NOTIF = { notifications: [], dismiss: vi.fn(), clearAll: vi.fn() }

beforeEach(() => {
  vi.mocked(notifHook.useNotifications).mockReturnValue(EMPTY_NOTIF)
})

const onLogout = vi.fn()

function renderLayout(children = <div>content</div>) {
  return render(
    <MemoryRouter>
      <AppLayout userName="Admin" onLogout={onLogout}>{children}</AppLayout>
    </MemoryRouter>
  )
}

describe('AppLayout', () => {
  it('renders the sidebar', () => {
    renderLayout()
    expect(screen.getByRole('navigation', { name: /sidebar/i })).toBeInTheDocument()
  })

  it('renders the top navigation bar', () => {
    renderLayout()
    expect(screen.getByRole('banner')).toBeInTheDocument()
  })

  it('displays the user name in the top nav', () => {
    renderLayout()
    const matches = screen.getAllByText('Admin')
    expect(matches.length).toBeGreaterThanOrEqual(1)
  })

  it('renders sidebar navigation links', () => {
    renderLayout()
    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /audit/i })).toBeInTheDocument()
  })

  it('collapses the sidebar on toggle', async () => {
    renderLayout()
    const toggle = screen.getByRole('button', { name: /toggle sidebar/i })
    await userEvent.click(toggle)
    expect(screen.getByRole('navigation', { name: /sidebar/i })).toHaveClass('collapsed')
  })

  it('opens notification panel when bell button is clicked', async () => {
    renderLayout()
    fireEvent.click(screen.getByRole('button', { name: 'Notifications' }))
    expect(await screen.findByRole('dialog', { name: 'Notifications' })).toBeInTheDocument()
  })

  it('closes notification panel when X button inside panel is clicked', async () => {
    renderLayout()
    fireEvent.click(screen.getByRole('button', { name: 'Notifications' }))
    await screen.findByRole('dialog', { name: 'Notifications' })
    fireEvent.click(screen.getByRole('button', { name: 'Close notifications' }))
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'Notifications' })).not.toBeInTheDocument()
    )
  })

  it('shows notification badge when there are unread notifications', () => {
    vi.mocked(notifHook.useNotifications).mockReturnValue({
      notifications: [{ type: 'HashMismatch', message: 'Alert', severity: 'Critical', occurredAt: new Date().toISOString() }],
      dismiss: vi.fn(),
      clearAll: vi.fn(),
    })
    renderLayout()
    expect(screen.getByRole('button', { name: 'Notifications' }).querySelector('span')).toBeInTheDocument()
  })

  it('opens user panel when user menu button is clicked', async () => {
    renderLayout()
    fireEvent.click(screen.getByRole('button', { name: 'User menu' }))
    expect(await screen.findByRole('dialog', { name: 'User menu' })).toBeInTheDocument()
  })

  it('closes user panel when clicking outside it', async () => {
    renderLayout()
    fireEvent.click(screen.getByRole('button', { name: 'User menu' }))
    await screen.findByRole('dialog', { name: 'User menu' })
    fireEvent.mouseDown(document.body)
    await waitFor(() =>
      expect(screen.queryByRole('dialog', { name: 'User menu' })).not.toBeInTheDocument()
    )
  })
})
