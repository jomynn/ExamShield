import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import NotificationToast from '../components/ui/NotificationToast'
import type { RealtimeNotification } from '../hooks/useNotifications'

function makeNotification(
  overrides: Partial<RealtimeNotification> = {}
): RealtimeNotification {
  return {
    type: 'SecurityAlert',
    message: 'Hash mismatch detected',
    severity: 'Critical',
    occurredAt: new Date().toISOString(),
    ...overrides,
  }
}

describe('NotificationToast', () => {
  it('renders nothing when notifications list is empty', () => {
    const { container } = render(
      <NotificationToast notifications={[]} onDismiss={vi.fn()} />
    )
    expect(container.firstChild).toBeNull()
  })

  it('renders a notification with its message', () => {
    render(
      <NotificationToast
        notifications={[makeNotification({ message: 'Tampering detected on capture X' })]}
        onDismiss={vi.fn()}
      />
    )
    expect(screen.getByText('Tampering detected on capture X')).toBeInTheDocument()
  })

  it('renders the type label with spaces inserted before capital letters', () => {
    render(
      <NotificationToast
        notifications={[makeNotification({ type: 'CaptureRegistered' })]}
        onDismiss={vi.fn()}
      />
    )
    expect(screen.getByText(/capture registered/i)).toBeInTheDocument()
  })

  it('applies Critical severity styles', () => {
    const { container } = render(
      <NotificationToast
        notifications={[makeNotification({ severity: 'Critical' })]}
        onDismiss={vi.fn()}
      />
    )
    const toast = container.querySelector('.border-red-500')
    expect(toast).toBeInTheDocument()
  })

  it('applies Info severity styles', () => {
    const { container } = render(
      <NotificationToast
        notifications={[makeNotification({ severity: 'Info', type: 'SystemInfo' })]}
        onDismiss={vi.fn()}
      />
    )
    const toast = container.querySelector('.border-blue-500')
    expect(toast).toBeInTheDocument()
  })

  it('applies Warning severity styles', () => {
    const { container } = render(
      <NotificationToast
        notifications={[makeNotification({ severity: 'Warning' })]}
        onDismiss={vi.fn()}
      />
    )
    const toast = container.querySelector('.border-yellow-500')
    expect(toast).toBeInTheDocument()
  })

  it('applies High severity styles', () => {
    const { container } = render(
      <NotificationToast
        notifications={[makeNotification({ severity: 'High' })]}
        onDismiss={vi.fn()}
      />
    )
    const toast = container.querySelector('.border-orange-500')
    expect(toast).toBeInTheDocument()
  })

  it('renders a Dismiss button for each notification', () => {
    render(
      <NotificationToast
        notifications={[makeNotification(), makeNotification({ message: 'Second' })]}
        onDismiss={vi.fn()}
      />
    )
    expect(screen.getAllByRole('button', { name: /dismiss/i })).toHaveLength(2)
  })

  it('calls onDismiss with the correct index when dismissed', async () => {
    const onDismiss = vi.fn()
    render(
      <NotificationToast
        notifications={[
          makeNotification({ message: 'First' }),
          makeNotification({ message: 'Second' }),
        ]}
        onDismiss={onDismiss}
      />
    )
    const dismissButtons = screen.getAllByRole('button', { name: /dismiss/i })
    await userEvent.click(dismissButtons[1])
    expect(onDismiss).toHaveBeenCalledWith(1)
  })

  it('caps rendering at 5 notifications even if more are provided', () => {
    const notifications = Array.from({ length: 7 }, (_, i) =>
      makeNotification({ message: `Message ${i}` })
    )
    render(<NotificationToast notifications={notifications} onDismiss={vi.fn()} />)
    expect(screen.getAllByRole('button', { name: /dismiss/i })).toHaveLength(5)
  })

  it('renders the timestamp for each notification', () => {
    const iso = '2026-06-28T10:00:00.000Z'
    render(
      <NotificationToast
        notifications={[makeNotification({ occurredAt: iso })]}
        onDismiss={vi.fn()}
      />
    )
    // The component calls toLocaleTimeString() — just verify some time text is present
    const timeEl = document.querySelector('.opacity-50')
    expect(timeEl?.textContent).toBeTruthy()
  })
})
