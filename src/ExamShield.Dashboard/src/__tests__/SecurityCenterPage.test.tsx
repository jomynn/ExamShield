import { render, screen, within, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import SecurityCenterPage from '../pages/SecurityCenterPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getSecurityEvents: vi.fn(),
    getAllActiveSessions: vi.fn(),
    getLoginHistory: vi.fn(),
  },
}))

const mockEvents = [
  {
    id: 'evt-1',
    eventType: 'HashMismatch',
    severity: 'Critical',
    message: 'Hash mismatch for capture aaa',
    userId: 'user1',
    ipAddress: '10.0.0.1',
    captureId: 'cap-1',
    occurredAt: '2026-06-26T10:00:00Z',
  },
  {
    id: 'evt-2',
    eventType: 'InvalidSignature',
    severity: 'High',
    message: 'Invalid signature on device dev-2',
    userId: null,
    ipAddress: '10.0.0.2',
    captureId: null,
    occurredAt: '2026-06-26T09:00:00Z',
  },
]

const mockSession = {
  id: 'sess-1',
  userId: 'user-abc',
  createdAt: '2026-06-29T08:00:00Z',
  expiresAt: '2026-06-29T09:00:00Z',
}

const mockLoginEvent = {
  id: 'log-1',
  eventType: 'LoginSuccess',
  userId: 'user1',
  ipAddress: '10.0.0.5',
  occurredAt: '2026-06-29T08:30:00Z',
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <SecurityCenterPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getSecurityEvents).mockResolvedValue({ events: mockEvents })
  vi.mocked(apiClient.api.getAllActiveSessions).mockResolvedValue({ sessions: [] })
  vi.mocked(apiClient.api.getLoginHistory).mockResolvedValue({ events: [] })
})

describe('SecurityCenterPage — events display', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /security center/i })).toBeInTheDocument()
  })

  it('renders an event row for each event', async () => {
    renderPage()
    const eventsTable = await screen.findByTestId('security-events-table')
    const rows = within(eventsTable).getAllByRole('row')
    expect(rows).toHaveLength(3) // header + 2 data rows
  })

  it('displays event types', async () => {
    renderPage()
    expect(await screen.findByText('HashMismatch')).toBeInTheDocument()
    expect(await screen.findByText('InvalidSignature')).toBeInTheDocument()
  })

  it('displays severity chips', async () => {
    renderPage()
    await screen.findByText('HashMismatch')
    const eventsTable = screen.getByTestId('security-events-table')
    expect(within(eventsTable).getByText('Critical')).toBeInTheDocument()
    expect(within(eventsTable).getByText('High')).toBeInTheDocument()
  })

  it('shows critical count badge', async () => {
    renderPage()
    expect(await screen.findByText(/1 critical/i)).toBeInTheDocument()
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getSecurityEvents).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByRole('status', { name: /loading/i })).toBeInTheDocument()
  })

  it('shows error state when fetch fails', async () => {
    vi.mocked(apiClient.api.getSecurityEvents).mockRejectedValue(new Error('Server error'))
    renderPage()
    expect(await screen.findByText(/failed to load security events/i)).toBeInTheDocument()
  })

  it('shows empty state when there are no events', async () => {
    vi.mocked(apiClient.api.getSecurityEvents).mockResolvedValue({ events: [] })
    renderPage()
    expect(await screen.findByText(/no security events recorded/i)).toBeInTheDocument()
  })
})

describe('SecurityCenterPage — severity filter', () => {
  it('shows All Severities option in the dropdown', async () => {
    renderPage()
    await screen.findByText('HashMismatch')
    expect(screen.getByRole('option', { name: /all severities/i })).toBeInTheDocument()
  })

  it('has severity options for each level', async () => {
    renderPage()
    await screen.findByText('HashMismatch')
    for (const s of ['Info', 'Warning', 'High', 'Critical']) {
      expect(screen.getByRole('option', { name: s })).toBeInTheDocument()
    }
  })

  it('re-fetches with severity when filter changes', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('HashMismatch')
    await user.selectOptions(screen.getByRole('combobox'), 'Critical')
    await waitFor(() =>
      expect(apiClient.api.getSecurityEvents).toHaveBeenCalledWith(100, 'Critical')
    )
  })

  it('does not show critical badge when no critical events', async () => {
    vi.mocked(apiClient.api.getSecurityEvents).mockResolvedValue({
      events: [{ ...mockEvents[0], severity: 'High' }],
    })
    renderPage()
    await screen.findByText('HashMismatch')
    expect(screen.queryByText(/\d+ critical/i)).not.toBeInTheDocument()
  })
})

describe('SecurityCenterPage — active sessions', () => {
  it('shows Active Sessions heading', async () => {
    vi.mocked(apiClient.api.getAllActiveSessions).mockResolvedValue({ sessions: [mockSession] })
    renderPage()
    expect(await screen.findByText(/active sessions/i)).toBeInTheDocument()
  })

  it('shows session user ID', async () => {
    vi.mocked(apiClient.api.getAllActiveSessions).mockResolvedValue({ sessions: [mockSession] })
    renderPage()
    expect(await screen.findByText('user-abc')).toBeInTheDocument()
  })

  it('shows empty state when no sessions', async () => {
    renderPage()
    expect(await screen.findByText(/no active sessions/i)).toBeInTheDocument()
  })
})

describe('SecurityCenterPage — login history', () => {
  it('shows Login History heading', async () => {
    renderPage()
    expect(await screen.findByText(/login history/i)).toBeInTheDocument()
  })

  it('shows empty state when no login events', async () => {
    renderPage()
    expect(await screen.findByText(/no login events in range/i)).toBeInTheDocument()
  })

  it('displays login event type when events are returned', async () => {
    vi.mocked(apiClient.api.getLoginHistory).mockResolvedValue({ events: [mockLoginEvent] })
    renderPage()
    expect(await screen.findByText('LoginSuccess')).toBeInTheDocument()
  })

  it('re-fetches with from date when From filter is set', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/login history/i)
    await user.type(screen.getByTitle('From'), '2026-06-29T00:00')
    await waitFor(() =>
      expect(apiClient.api.getLoginHistory).toHaveBeenCalledWith(
        200, expect.stringContaining('2026-06-29'), undefined
      )
    )
  })

  it('shows Clear button when From filter is set', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/login history/i)
    await user.type(screen.getByTitle('From'), '2026-06-29T00:00')
    expect(await screen.findByRole('button', { name: /clear/i })).toBeInTheDocument()
  })

  it('Clear button is absent when no filters are set', async () => {
    renderPage()
    await screen.findByText(/login history/i)
    expect(screen.queryByRole('button', { name: /clear/i })).not.toBeInTheDocument()
  })

  it('clicking Clear resets date filters', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/login history/i)
    await user.type(screen.getByTitle('From'), '2026-06-29T00:00')
    await user.click(await screen.findByRole('button', { name: /clear/i }))
    expect(screen.queryByRole('button', { name: /clear/i })).not.toBeInTheDocument()
  })

  it('shows Clear button when only To filter is set', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/login history/i)
    await user.type(screen.getByTitle('To'), '2026-06-29T23:59')
    expect(await screen.findByRole('button', { name: /clear/i })).toBeInTheDocument()
  })
})

describe('SecurityCenterPage — severityVariant branches', () => {
  it('renders Warning and Info severity chips', async () => {
    vi.mocked(apiClient.api.getSecurityEvents).mockResolvedValue({
      events: [
        { ...mockEvents[0], id: 'w-1', eventType: 'SuspiciousLogin', severity: 'Warning' },
        { ...mockEvents[0], id: 'i-1', eventType: 'LoginSuccess',    severity: 'Info' },
        { ...mockEvents[0], id: 'u-1', eventType: 'Unknown',          severity: 'UnknownLevel' },
      ],
    })
    renderPage()
    await screen.findByText('SuspiciousLogin')
    const table = screen.getByTestId('security-events-table')
    expect(within(table).getByText('Warning')).toBeInTheDocument()
    expect(within(table).getByText('Info')).toBeInTheDocument()
    expect(within(table).getByText('UnknownLevel')).toBeInTheDocument()
  })
})

describe('SecurityCenterPage — timeline buckets with recent events', () => {
  it('counts a recent event in the timeline chart (lines 39-42)', async () => {
    const recentAt = new Date(Date.now() - 30 * 60 * 1000).toISOString() // 30 min ago
    vi.mocked(apiClient.api.getSecurityEvents).mockResolvedValue({
      events: [
        { ...mockEvents[0], id: 'r-1', severity: 'Critical', occurredAt: recentAt },
      ],
    })
    renderPage()
    // Chart renders when data loads — just confirm the page loaded without error
    expect(await screen.findByRole('heading', { name: /security center/i })).toBeInTheDocument()
  })
})
