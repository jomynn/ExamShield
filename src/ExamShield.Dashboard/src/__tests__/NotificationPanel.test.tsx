import React from 'react'
import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import NotificationPanel from '../components/ui/NotificationPanel'
import type { RealtimeNotification } from '../hooks/useNotifications'

const anchorRef = { current: null } as React.RefObject<HTMLButtonElement | null>

const makeNotification = (overrides?: Partial<RealtimeNotification>): RealtimeNotification => ({
  type: 'HashMismatch',
  message: 'Hash mismatch on cap-123',
  severity: 'Critical',
  occurredAt: new Date().toISOString(),
  ...overrides,
})

function renderPanel(props: Partial<React.ComponentProps<typeof NotificationPanel>> = {}) {
  const defaults = {
    open: true,
    anchorRef,
    notifications: [] as RealtimeNotification[],
    onDismiss: vi.fn(),
    onClearAll: vi.fn(),
    onClose: vi.fn(),
  }
  return render(<NotificationPanel {...defaults} {...props} />)
}

describe('NotificationPanel', () => {
  beforeEach(() => { vi.clearAllMocks() })

  it('renders nothing when open=false', () => {
    renderPanel({ open: false })
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('renders dialog with aria-label when open=true', () => {
    renderPanel()
    expect(screen.getByRole('dialog', { name: /notifications/i })).toBeInTheDocument()
  })

  it('shows "No notifications" message when notifications array is empty', () => {
    renderPanel({ notifications: [] })
    expect(screen.getByText('No notifications')).toBeInTheDocument()
  })

  it('renders each notification message when provided', () => {
    const notifications = [
      makeNotification({ message: 'Hash mismatch on cap-123', severity: 'Critical' }),
      makeNotification({ type: 'DuplicateUpload', message: 'Duplicate upload detected', severity: 'Warning' }),
    ]
    renderPanel({ notifications })
    expect(screen.getByText('Hash mismatch on cap-123')).toBeInTheDocument()
    expect(screen.getByText('Duplicate upload detected')).toBeInTheDocument()
  })

  it('renders severity badge for each notification', () => {
    const notifications = [makeNotification({ severity: 'Critical' })]
    renderPanel({ notifications })
    expect(screen.getAllByText('Critical').length).toBeGreaterThan(0)
  })

  it('calls onClearAll when "Clear all" button is clicked', () => {
    const onClearAll = vi.fn()
    const notifications = [makeNotification()]
    renderPanel({ notifications, onClearAll })
    fireEvent.click(screen.getByText('Clear all'))
    expect(onClearAll).toHaveBeenCalledTimes(1)
  })

  it('calls onClose when close (X) button is clicked', () => {
    const onClose = vi.fn()
    renderPanel({ onClose })
    fireEvent.click(screen.getByLabelText('Close notifications'))
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('calls onDismiss with correct index when dismiss button is clicked', () => {
    const onDismiss = vi.fn()
    const notifications = [
      makeNotification({ message: 'First notification' }),
      makeNotification({ message: 'Second notification', severity: 'Warning' }),
    ]
    renderPanel({ notifications, onDismiss })
    const dismissButtons = screen.getAllByLabelText('Dismiss notification')
    fireEvent.click(dismissButtons[1])
    expect(onDismiss).toHaveBeenCalledWith(1)
  })

  it('does not show "Clear all" when notifications list is empty', () => {
    renderPanel({ notifications: [] })
    expect(screen.queryByText('Clear all')).not.toBeInTheDocument()
  })

  it('inserts spaces before capitals in notification type display', () => {
    const notifications = [makeNotification({ type: 'HashMismatch', message: 'msg' })]
    renderPanel({ notifications })
    // type.replace(/([A-Z])/g, ' $1').trim() → "Hash Mismatch"
    expect(screen.getByText('Hash Mismatch')).toBeInTheDocument()
  })

  it('calls onClose when mousedown fires outside the panel and anchor', () => {
    const onClose = vi.fn()
    const btn = document.createElement('button')
    document.body.appendChild(btn)
    const ref = { current: btn } as React.RefObject<HTMLButtonElement | null>
    renderPanel({ onClose, anchorRef: ref })
    fireEvent.mouseDown(document.body)
    expect(onClose).toHaveBeenCalledTimes(1)
    btn.remove()
  })
})
