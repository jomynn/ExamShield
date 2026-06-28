import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import SessionManagementPage from '../pages/SessionManagementPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getSessions: vi.fn(),
    revokeSession: vi.fn(),
    revokeAllSessions: vi.fn(),
  },
}))

const FUTURE = new Date(Date.now() + 60 * 60 * 1000).toISOString()
const PAST   = new Date(Date.now() - 60 * 60 * 1000).toISOString()

const mockSessions = [
  { id: 'sess-aaa-1111-2222', createdAt: new Date(Date.now() - 5 * 60_000).toISOString(), expiresAt: FUTURE },
  { id: 'sess-bbb-3333-4444', createdAt: new Date(Date.now() - 2 * 60_000).toISOString(), expiresAt: FUTURE },
  { id: 'sess-ccc-5555-6666', createdAt: new Date(Date.now() - 2 * 3600_000).toISOString(), expiresAt: PAST },
]

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <SessionManagementPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getSessions).mockResolvedValue({ sessions: mockSessions })
  vi.mocked(apiClient.api.revokeSession).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.revokeAllSessions).mockResolvedValue(undefined)
})

describe('SessionManagementPage', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /active sessions/i })).toBeInTheDocument()
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getSessions).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('shows active and expired session counts', async () => {
    renderPage()
    expect(await screen.findByText(/2 active/i)).toBeInTheDocument()
    expect(screen.getByText(/1 expired/i)).toBeInTheDocument()
  })

  it('renders session rows in Active section', async () => {
    renderPage()
    // Wait for session counts to appear (data-dependent)
    expect(await screen.findByText(/2 active/i)).toBeInTheDocument()
    expect(screen.getByText(/1 expired/i)).toBeInTheDocument()
  })

  it('shows Revoke buttons for active sessions only', async () => {
    renderPage()
    // Wait for data to load before querying buttons
    const revokeBtns = await screen.findAllByRole('button', { name: /^revoke$/i })
    expect(revokeBtns).toHaveLength(2) // 2 active sessions
  })

  it('calls revokeSession with session id on Revoke click', async () => {
    renderPage()
    const [firstRevoke] = await screen.findAllByRole('button', { name: /^revoke$/i })
    fireEvent.click(firstRevoke)
    await waitFor(() =>
      expect(apiClient.api.revokeSession).toHaveBeenCalledWith('sess-aaa-1111-2222')
    )
  })

  it('shows Revoke all button when multiple active sessions exist', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: /revoke all/i })).toBeInTheDocument()
  })

  it('calls revokeAllSessions when Revoke all is clicked', async () => {
    renderPage()
    await screen.findByRole('button', { name: /revoke all/i })
    fireEvent.click(screen.getByRole('button', { name: /revoke all/i }))
    await waitFor(() =>
      expect(apiClient.api.revokeAllSessions).toHaveBeenCalled()
    )
  })

  it('shows empty state when no sessions exist', async () => {
    vi.mocked(apiClient.api.getSessions).mockResolvedValue({ sessions: [] })
    renderPage()
    expect(await screen.findByText(/no sessions found/i)).toBeInTheDocument()
  })

  it('does not show Revoke all when only one active session', async () => {
    vi.mocked(apiClient.api.getSessions).mockResolvedValue({ sessions: [mockSessions[0]] })
    renderPage()
    await screen.findByRole('heading', { name: /active sessions/i })
    expect(screen.queryByRole('button', { name: /revoke all/i })).toBeNull()
  })
})
