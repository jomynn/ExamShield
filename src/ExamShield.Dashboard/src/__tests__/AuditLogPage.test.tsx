import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import AuditLogPage from '../pages/AuditLogPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getAuditLog: vi.fn(),
  },
}))

const mockEntries = [
  {
    id: 'aaa',
    action: 'CaptureRegistered',
    captureId: 'ccc',
    userId: 'user1',
    ipAddress: '10.0.0.1',
    occurredAt: '2026-06-26T10:00:00Z',
    reason: null,
    contentHash: 'abc123',
    serverSignature: 'validBase64Sig==',
  },
  {
    id: 'bbb',
    action: 'ImageUploaded',
    captureId: 'ccc',
    userId: 'user1',
    ipAddress: '10.0.0.1',
    occurredAt: '2026-06-26T10:01:00Z',
    reason: null,
    contentHash: 'def456',
    serverSignature: 'anotherSig==',
  },
]

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <AuditLogPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getAuditLog).mockResolvedValue({
    entries: mockEntries,
    totalCount: 2,
  })
})

describe('AuditLogPage', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /audit log/i })).toBeInTheDocument()
  })

  it('renders a row for each audit entry', async () => {
    renderPage()
    const rows = await screen.findAllByRole('row')
    // header row + 2 data rows
    expect(rows).toHaveLength(3)
  })

  it('displays the action name in each row', async () => {
    renderPage()
    expect(await screen.findByText('CaptureRegistered')).toBeInTheDocument()
    expect(await screen.findByText('ImageUploaded')).toBeInTheDocument()
  })

  it('displays a verification badge per row', async () => {
    renderPage()
    await screen.findByText('CaptureRegistered')
    const badges = await screen.findAllByTestId('verification-badge')
    expect(badges).toHaveLength(2)
  })

  it('shows total count', async () => {
    renderPage()
    expect(await screen.findByText(/2 entries/i)).toBeInTheDocument()
  })

  it('shows a loading state initially', () => {
    vi.mocked(apiClient.api.getAuditLog).mockImplementation(
      () => new Promise(() => {})  // never resolves
    )
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })
})
