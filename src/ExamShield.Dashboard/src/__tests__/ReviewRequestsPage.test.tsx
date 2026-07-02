import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import ReviewRequestsPage from '../pages/ReviewRequestsPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getAllReviewRequests: vi.fn(),
    resolveReviewRequest: vi.fn(),
    rejectReviewRequest: vi.fn(),
  },
}))

const mockItems = [
  {
    reviewRequestId: 'rr-111',
    captureId: 'cap-aaa',
    studentId: 'stu-bbb',
    reason: 'OCR misread question 5',
    status: 'Pending',
    resolutionNote: null,
    createdAt: '2026-06-26T09:00:00Z',
  },
  {
    reviewRequestId: 'rr-222',
    captureId: 'cap-ccc',
    studentId: 'stu-ddd',
    reason: 'Wrong answer detected for Q3',
    status: 'Resolved',
    resolutionNote: 'Confirmed correct on re-review',
    createdAt: '2026-06-25T14:00:00Z',
  },
]

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <ReviewRequestsPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getAllReviewRequests).mockResolvedValue({ items: mockItems })
  vi.mocked(apiClient.api.resolveReviewRequest).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.rejectReviewRequest).mockResolvedValue(undefined)
})

describe('ReviewRequestsPage', () => {
  it('renders page heading', () => {
    renderPage()
    expect(screen.getByRole('heading', { name: /review requests/i })).toBeInTheDocument()
  })

  it('shows status tab buttons', () => {
    renderPage()
    expect(screen.getByRole('button', { name: /all/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /pending/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /resolved/i })).toBeInTheDocument()
  })

  it('shows loading indicator while fetching', () => {
    vi.mocked(apiClient.api.getAllReviewRequests).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('renders a card per review request', async () => {
    renderPage()
    expect(await screen.findByText('OCR misread question 5')).toBeInTheDocument()
    expect(screen.getByText('Wrong answer detected for Q3')).toBeInTheDocument()
  })

  it('displays status badges on each card', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    // 'Pending' appears as tab button AND as status badge — both should be present
    expect(screen.getAllByText('Pending').length).toBeGreaterThanOrEqual(2)
    // 'Resolved' appears as tab button AND as status badge
    expect(screen.getAllByText('Resolved').length).toBeGreaterThanOrEqual(2)
  })

  it('shows resolution note when present', async () => {
    renderPage()
    expect(await screen.findByText(/confirmed correct on re-review/i)).toBeInTheDocument()
  })

  it('shows Act button for pending requests', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    expect(screen.getByRole('button', { name: /act/i })).toBeInTheDocument()
  })

  it('shows Resolve and Reject options when Act is clicked', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    fireEvent.click(screen.getByRole('button', { name: /act/i }))
    // Use exact match: "Resolve" action button, not "Resolved" tab
    expect(screen.getByRole('button', { name: /^resolve$/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^reject$/i })).toBeInTheDocument()
  })

  it('shows note textarea when Resolve is clicked', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    fireEvent.click(screen.getByRole('button', { name: /act/i }))
    fireEvent.click(screen.getByRole('button', { name: /^resolve$/i }))
    expect(screen.getByPlaceholderText(/resolution note/i)).toBeInTheDocument()
  })

  it('calls resolveReviewRequest with note on confirm', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    fireEvent.click(screen.getByRole('button', { name: /act/i }))
    fireEvent.click(screen.getByRole('button', { name: /^resolve$/i }))
    fireEvent.change(screen.getByPlaceholderText(/resolution note/i), {
      target: { value: 'Verified correct' },
    })
    fireEvent.click(screen.getByRole('button', { name: /confirm resolve/i }))
    await waitFor(() =>
      expect(apiClient.api.resolveReviewRequest).toHaveBeenCalledWith('rr-111', 'Verified correct')
    )
  })

  it('shows empty message when no items match the filter', async () => {
    vi.mocked(apiClient.api.getAllReviewRequests).mockResolvedValue({ items: [] })
    renderPage()
    expect(await screen.findByText(/no.*review requests/i)).toBeInTheDocument()
  })

  it('filters by clicking tab buttons', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    fireEvent.click(screen.getByRole('button', { name: /^all$/i }))
    await waitFor(() =>
      expect(apiClient.api.getAllReviewRequests).toHaveBeenCalledWith(undefined)
    )
  })

  // ── Reject flow ──────────────────────────────────────────────────────────────

  it('shows rejection note textarea when Reject is clicked', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    fireEvent.click(screen.getByRole('button', { name: /act/i }))
    fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))
    expect(screen.getByPlaceholderText(/rejection reason/i)).toBeInTheDocument()
  })

  it('shows Confirm Reject button after clicking Reject', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    fireEvent.click(screen.getByRole('button', { name: /act/i }))
    fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))
    expect(screen.getByRole('button', { name: /confirm reject/i })).toBeInTheDocument()
  })

  it('Confirm Reject is disabled when rejection reason is empty', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    fireEvent.click(screen.getByRole('button', { name: /act/i }))
    fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))
    expect(screen.getByRole('button', { name: /confirm reject/i })).toBeDisabled()
  })

  it('calls rejectReviewRequest with note on Confirm Reject', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    fireEvent.click(screen.getByRole('button', { name: /act/i }))
    fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))
    fireEvent.change(screen.getByPlaceholderText(/rejection reason/i), {
      target: { value: 'Evidence insufficient' },
    })
    fireEvent.click(screen.getByRole('button', { name: /confirm reject/i }))
    await waitFor(() =>
      expect(apiClient.api.rejectReviewRequest).toHaveBeenCalledWith('rr-111', 'Evidence insufficient')
    )
  })

  it('Cancel from reject mode returns to idle (Resolve/Reject buttons)', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    fireEvent.click(screen.getByRole('button', { name: /act/i }))
    fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))
    await screen.findByPlaceholderText(/rejection reason/i)
    fireEvent.click(screen.getByRole('button', { name: /^cancel$/i }))
    expect(screen.getByRole('button', { name: /^resolve$/i })).toBeInTheDocument()
    expect(screen.queryByPlaceholderText(/rejection reason/i)).not.toBeInTheDocument()
  })

  it('Cancel from resolve mode returns to idle', async () => {
    renderPage()
    await screen.findByText('OCR misread question 5')
    fireEvent.click(screen.getByRole('button', { name: /act/i }))
    fireEvent.click(screen.getByRole('button', { name: /^resolve$/i }))
    await screen.findByPlaceholderText(/resolution note/i)
    fireEvent.click(screen.getByRole('button', { name: /^cancel$/i }))
    expect(screen.queryByPlaceholderText(/resolution note/i)).not.toBeInTheDocument()
  })

  it('shows pending count badge on Pending tab when viewing All tab', async () => {
    vi.mocked(apiClient.api.getAllReviewRequests).mockResolvedValue({
      items: [{ ...mockItems[0] }], // only pending item
    })
    renderPage()
    await screen.findByText('OCR misread question 5')
    fireEvent.click(screen.getByRole('button', { name: /^all$/i }))
    // badge shows pending count when not on the Pending tab
    await waitFor(() =>
      expect(screen.getByText('1')).toBeInTheDocument()
    )
  })
})
