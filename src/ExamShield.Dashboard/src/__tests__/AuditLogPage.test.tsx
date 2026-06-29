import { render, screen, within, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import AuditLogPage from '../pages/AuditLogPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getAuditLog: vi.fn(),
    exportAuditLog: vi.fn(),
  },
}))

const makeEntry = (id: string, action: string, captureId = 'ccc') => ({
  id,
  action,
  captureId,
  userId: 'user1',
  ipAddress: '10.0.0.1',
  occurredAt: '2026-06-26T10:00:00Z',
  reason: null,
  contentHash: 'abc123',
  serverSignature: 'validBase64Sig==',
})

const mockEntries = [
  makeEntry('aaa', 'CaptureRegistered'),
  makeEntry('bbb', 'ImageUploaded'),
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
  vi.mocked(apiClient.api.exportAuditLog).mockResolvedValue(new Blob(['csv'], { type: 'text/csv' }))
})

describe('AuditLogPage — display', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /audit log/i })).toBeInTheDocument()
  })

  it('renders a row for each audit entry', async () => {
    renderPage()
    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(3) // header + 2 data rows
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
    vi.mocked(apiClient.api.getAuditLog).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('shows an error state when fetch fails', async () => {
    vi.mocked(apiClient.api.getAuditLog).mockRejectedValue(new Error('Network error'))
    renderPage()
    expect(await screen.findByText(/failed to load/i)).toBeInTheDocument()
  })
})

describe('AuditLogPage — filters', () => {
  it('shows All Actions option in the action filter dropdown', async () => {
    renderPage()
    await screen.findByText('CaptureRegistered')
    expect(screen.getByRole('option', { name: /all actions/i })).toBeInTheDocument()
  })

  it('re-fetches with action filter when action is selected', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('CaptureRegistered')
    await user.selectOptions(
      screen.getByRole('combobox'),
      'CaptureRegistered'
    )
    await waitFor(() =>
      expect(apiClient.api.getAuditLog).toHaveBeenCalledWith(
        expect.objectContaining({ action: 'CaptureRegistered' })
      )
    )
  })

  it('re-fetches with captureId filter when capture ID is typed', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('CaptureRegistered')
    await user.type(screen.getByPlaceholderText(/filter by capture id/i), 'cap-123')
    await waitFor(() =>
      expect(apiClient.api.getAuditLog).toHaveBeenCalledWith(
        expect.objectContaining({ captureId: 'cap-123' })
      )
    )
  })

  it('resets to page 1 when capture ID filter changes', async () => {
    const user = userEvent.setup()
    vi.mocked(apiClient.api.getAuditLog).mockResolvedValue({ entries: mockEntries, totalCount: 50 })
    renderPage()
    await screen.findByText('CaptureRegistered')
    await user.type(screen.getByPlaceholderText(/filter by capture id/i), 'x')
    await waitFor(() =>
      expect(apiClient.api.getAuditLog).toHaveBeenCalledWith(
        expect.objectContaining({ page: 1 })
      )
    )
  })
})

describe('AuditLogPage — pagination', () => {
  beforeEach(() => {
    vi.mocked(apiClient.api.getAuditLog).mockResolvedValue({
      entries: mockEntries,
      totalCount: 50, // 3 pages at PAGE_SIZE=20
    })
  })

  it('shows page 1 of 3 initially', async () => {
    renderPage()
    expect(await screen.findByText(/page 1 of 3/i)).toBeInTheDocument()
  })

  it('Previous page button is disabled on page 1', async () => {
    renderPage()
    await screen.findByText(/page 1 of 3/i)
    expect(screen.getByRole('button', { name: /previous page/i })).toBeDisabled()
  })

  it('Next page button is enabled on page 1', async () => {
    renderPage()
    await screen.findByText(/page 1 of 3/i)
    expect(screen.getByRole('button', { name: /next page/i })).not.toBeDisabled()
  })

  it('advances to page 2 when Next is clicked', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/page 1 of 3/i)
    await user.click(screen.getByRole('button', { name: /next page/i }))
    await waitFor(() =>
      expect(apiClient.api.getAuditLog).toHaveBeenCalledWith(
        expect.objectContaining({ page: 2 })
      )
    )
  })
})

describe('AuditLogPage — export', () => {
  it('shows Export CSV button', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: /export csv/i })).toBeInTheDocument()
  })

  it('calls exportAuditLog when Export CSV is clicked', async () => {
    const user = userEvent.setup()
    const createObjectURL = vi.fn().mockReturnValue('blob:mock')
    const revokeObjectURL = vi.fn()
    Object.defineProperty(window, 'URL', {
      value: { createObjectURL, revokeObjectURL },
      writable: true,
    })

    renderPage()
    await user.click(await screen.findByRole('button', { name: /export csv/i }))
    await waitFor(() => expect(apiClient.api.exportAuditLog).toHaveBeenCalled())
  })
})
