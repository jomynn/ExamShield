import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import ManualReviewPage from '../pages/ManualReviewPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getPendingReviews: vi.fn(),
    getReviewDetail:   vi.fn(),
    submitReview:      vi.fn(),
    approveReview:     vi.fn(),
    rejectReview:      vi.fn(),
    escalateReview:    vi.fn(),
    getCaptureImage:   vi.fn(),
  },
}))

const OCR_ANSWERS = [
  { questionNumber: 1, text: 'A', confidence: 0.45 },
  { questionNumber: 2, text: 'B', confidence: 0.92 },
]

const mockReviews = [
  { reviewId: 'rev-1', captureId: 'cap-1', ocrResultId: 'ocr-1', createdAt: '2026-06-26T10:00:00Z' },
]

const mockReviewLongId = [
  { reviewId: 'rev-2', captureId: 'cap-long-1234567890', ocrResultId: 'ocr-2', createdAt: '2026-06-26T10:00:00Z' },
]

const mockDetail = {
  reviewId: 'rev-1', captureId: 'cap-1', ocrResultId: 'ocr-1',
  status: 'Pending', ocrAnswers: OCR_ANSWERS, createdAt: '2026-06-26T10:00:00Z',
}

const completedDetail = {
  reviewId: 'rev-1', captureId: 'cap-1', ocrResultId: 'ocr-1',
  status: 'Completed', ocrAnswers: OCR_ANSWERS, createdAt: '2026-06-26T10:00:00Z',
}

const approvedDetail = {
  reviewId: 'rev-1', captureId: 'cap-1', ocrResultId: 'ocr-1',
  status: 'Approved', ocrAnswers: OCR_ANSWERS, createdAt: '2026-06-26T10:00:00Z',
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <ManualReviewPage />
      </MemoryRouter>
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(apiClient.api.getPendingReviews).mockResolvedValue({ reviews: mockReviews })
  vi.mocked(apiClient.api.getReviewDetail).mockResolvedValue(mockDetail)
  vi.mocked(apiClient.api.submitReview).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.approveReview).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.rejectReview).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.escalateReview).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.getCaptureImage).mockResolvedValue('blob:mock-capture-image')
})

describe('ManualReviewPage', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /manual review/i })).toBeInTheDocument()
  })

  it('shows pending review count', async () => {
    renderPage()
    expect(await screen.findByText(/1 pending/i)).toBeInTheDocument()
  })

  it('renders a row per pending review', async () => {
    renderPage()
    const rows = await screen.findAllByRole('row')
    expect(rows.length).toBeGreaterThanOrEqual(2) // header + at least 1
  })

  it('loads review detail on row click', async () => {
    renderPage()
    const reviewRow = await screen.findByText('cap-1')
    fireEvent.click(reviewRow)
    expect(await screen.findByText(/ocr answers/i)).toBeInTheDocument()
  })

  it('shows OCR answers with confidence in detail panel', async () => {
    renderPage()
    fireEvent.click(await screen.findByText('cap-1'))
    expect(await screen.findByText('Q1')).toBeInTheDocument()
    expect(await screen.findByText('Q2')).toBeInTheDocument()
  })

  it('shows low-confidence warning for answers below threshold', async () => {
    renderPage()
    fireEvent.click(await screen.findByText('cap-1'))
    await screen.findByText('Q1')
    expect(screen.getByText(/45%/)).toBeInTheDocument()
  })

  it('submit button calls submitReview', async () => {
    renderPage()
    fireEvent.click(await screen.findByText('cap-1'))
    await screen.findByText('Q1')
    const submitBtn = screen.getByRole('button', { name: /submit/i })
    fireEvent.click(submitBtn)
    await waitFor(() => expect(apiClient.api.submitReview).toHaveBeenCalledWith('rev-1', expect.any(Array)))
  })

  it('shows loading state', () => {
    vi.mocked(apiClient.api.getPendingReviews).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('shows the original capture image in the detail panel', async () => {
    renderPage()
    fireEvent.click(await screen.findByText('cap-1'))
    const img = await screen.findByRole('img', { name: /answer sheet/i })
    expect(img).toHaveAttribute('src', 'blob:mock-capture-image')
  })

  it('shows pixel lock badge in the image panel', async () => {
    renderPage()
    fireEvent.click(await screen.findByText('cap-1'))
    expect(await screen.findByText(/pixel lock/i)).toBeInTheDocument()
  })

  it('fetches capture image using the review captureId', async () => {
    renderPage()
    fireEvent.click(await screen.findByText('cap-1'))
    await screen.findByRole('img', { name: /answer sheet/i })
    expect(apiClient.api.getCaptureImage).toHaveBeenCalledWith('cap-1')
  })

  it('shows "All reviews complete" when list is empty', async () => {
    vi.mocked(apiClient.api.getPendingReviews).mockResolvedValue({ reviews: [] })
    renderPage()
    expect(await screen.findByText(/all reviews complete/i)).toBeInTheDocument()
  })

  it('shows "Select a review to begin" placeholder in right panel initially', async () => {
    renderPage()
    await screen.findByText('cap-1') // list loaded
    expect(screen.getByText(/select a review to begin/i)).toBeInTheDocument()
  })

  it('truncates long captureId in the list to 8 chars + ellipsis', async () => {
    vi.mocked(apiClient.api.getPendingReviews).mockResolvedValue({ reviews: mockReviewLongId })
    vi.mocked(apiClient.api.getReviewDetail).mockResolvedValue(completedDetail)
    renderPage()
    expect(await screen.findByText('cap-long…')).toBeInTheDocument()
  })

  it('editing an answer input updates the value', async () => {
    renderPage()
    fireEvent.click(await screen.findByText('cap-1'))
    await screen.findByText('Q1')
    const inputs = screen.getAllByRole('textbox').filter(
      el => (el as HTMLInputElement).placeholder === 'A'
    )
    fireEvent.change(inputs[0], { target: { value: 'C' } })
    expect(inputs[0]).toHaveValue('C')
  })
})

// ── Supervisor actions (Completed status) ─────────────────────────────────────

describe('ManualReviewPage — supervisor actions (Completed review)', () => {
  beforeEach(() => {
    vi.mocked(apiClient.api.getReviewDetail).mockResolvedValue(completedDetail)
  })

  async function openDetail() {
    renderPage()
    fireEvent.click(await screen.findByText('cap-1'))
    await screen.findByText(/ocr answers/i)
  }

  it('shows Approve, Reject, Escalate buttons for Completed reviews', async () => {
    await openDetail()
    expect(screen.getByRole('button', { name: /^approve$/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^reject$/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^escalate$/i })).toBeInTheDocument()
  })

  it('does NOT show Submit Review button for Completed reviews', async () => {
    await openDetail()
    expect(screen.queryByRole('button', { name: /submit review/i })).not.toBeInTheDocument()
  })

  it('calls approveReview when Approve is clicked', async () => {
    await openDetail()
    fireEvent.click(screen.getByRole('button', { name: /^approve$/i }))
    await waitFor(() => expect(apiClient.api.approveReview).toHaveBeenCalledWith('rev-1'))
  })

  it('shows reason form when Reject is clicked', async () => {
    await openDetail()
    fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))
    expect(await screen.findByPlaceholderText(/rejection reason/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /confirm reject/i })).toBeInTheDocument()
  })

  it('Confirm Reject button is disabled when reason is empty', async () => {
    await openDetail()
    fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))
    await screen.findByPlaceholderText(/rejection reason/i)
    expect(screen.getByRole('button', { name: /confirm reject/i })).toBeDisabled()
  })

  it('calls rejectReview with the typed reason', async () => {
    await openDetail()
    fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))
    const reasonInput = await screen.findByPlaceholderText(/rejection reason/i)
    fireEvent.change(reasonInput, { target: { value: 'Image unclear' } })
    fireEvent.click(screen.getByRole('button', { name: /confirm reject/i }))
    await waitFor(() =>
      expect(apiClient.api.rejectReview).toHaveBeenCalledWith('rev-1', 'Image unclear')
    )
  })

  it('Cancel hides the reason form', async () => {
    await openDetail()
    fireEvent.click(screen.getByRole('button', { name: /^reject$/i }))
    await screen.findByPlaceholderText(/rejection reason/i)
    fireEvent.click(screen.getByRole('button', { name: /^cancel$/i }))
    expect(screen.queryByPlaceholderText(/rejection reason/i)).not.toBeInTheDocument()
  })

  it('shows reason form when Escalate is clicked', async () => {
    await openDetail()
    fireEvent.click(screen.getByRole('button', { name: /^escalate$/i }))
    expect(await screen.findByPlaceholderText(/escalation reason/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /confirm escalate/i })).toBeInTheDocument()
  })

  it('calls escalateReview with the typed reason', async () => {
    await openDetail()
    fireEvent.click(screen.getByRole('button', { name: /^escalate$/i }))
    const reasonInput = await screen.findByPlaceholderText(/escalation reason/i)
    fireEvent.change(reasonInput, { target: { value: 'Needs senior review' } })
    fireEvent.click(screen.getByRole('button', { name: /confirm escalate/i }))
    await waitFor(() =>
      expect(apiClient.api.escalateReview).toHaveBeenCalledWith('rev-1', 'Needs senior review')
    )
  })
})

// ── Terminal state ─────────────────────────────────────────────────────────────

describe('ManualReviewPage — terminal state (Approved/Rejected)', () => {
  it('shows "no further action" message for Approved status', async () => {
    vi.mocked(apiClient.api.getReviewDetail).mockResolvedValue(approvedDetail)
    renderPage()
    fireEvent.click(await screen.findByText('cap-1'))
    expect(await screen.findByText(/approved.*no further action|no further action/i)).toBeInTheDocument()
  })

  it('answers are read-only in terminal state', async () => {
    vi.mocked(apiClient.api.getReviewDetail).mockResolvedValue(approvedDetail)
    renderPage()
    fireEvent.click(await screen.findByText('cap-1'))
    await screen.findByText('Q1')
    // In read-only mode, answers render as <span> not <input>
    expect(screen.queryByRole('textbox', { name: /q1/i })).not.toBeInTheDocument()
  })
})
