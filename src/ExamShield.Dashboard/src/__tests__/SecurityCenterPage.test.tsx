import { render, screen, within } from '@testing-library/react'
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

describe('SecurityCenterPage', () => {
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
})
