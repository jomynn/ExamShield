import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import ScoringPage from '../pages/ScoringPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getScoringQueue: vi.fn(),
    scoreCapture: vi.fn(),
    batchScore: vi.fn(),
    exportScores: vi.fn(),
  },
}))

const mockQueue = [
  {
    captureId: 'cap-1',
    examId: 'exam-1',
    ocrResultId: 'ocr-1',
    ocrStatus: 'Completed',
    overallConfidence: 0.92,
    completedAt: '2026-06-26T10:00:00Z',
  },
  {
    captureId: 'cap-2',
    examId: 'exam-1',
    ocrResultId: 'ocr-2',
    ocrStatus: 'Completed',
    overallConfidence: 0.75,
    completedAt: '2026-06-26T11:00:00Z',
  },
]

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <ScoringPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(apiClient.api.getScoringQueue).mockResolvedValue({ items: mockQueue })
  vi.mocked(apiClient.api.scoreCapture).mockResolvedValue({
    scoreId: 'score-new',
    correctAnswers: 45,
    totalQuestions: 50,
    percentage: 90.0,
  })
  vi.mocked(apiClient.api.batchScore).mockResolvedValue({ scored: 3, skipped: 1 })
  vi.mocked(apiClient.api.exportScores).mockResolvedValue(new Blob(['csv'], { type: 'text/csv' }))
})

describe('ScoringPage — display', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /scoring/i })).toBeInTheDocument()
  })

  it('shows pending count', async () => {
    renderPage()
    expect(await screen.findByText(/2 pending/i)).toBeInTheDocument()
  })

  it('renders a row per item', async () => {
    renderPage()
    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(3) // header + 2
  })

  it('displays (truncated) capture IDs', async () => {
    renderPage()
    expect(await screen.findByText(/cap-1/i)).toBeInTheDocument()
    expect(screen.getByText(/cap-2/i)).toBeInTheDocument()
  })

  it('shows confidence values', async () => {
    renderPage()
    expect(await screen.findByText('92%')).toBeInTheDocument()
    expect(screen.getByText('75%')).toBeInTheDocument()
  })

  it('renders a Score button per row', async () => {
    renderPage()
    await screen.findByText(/cap-1/i)
    expect(screen.getAllByRole('button', { name: /^score$/i })).toHaveLength(2)
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getScoringQueue).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('shows empty state when queue is empty', async () => {
    vi.mocked(apiClient.api.getScoringQueue).mockResolvedValue({ items: [] })
    renderPage()
    expect(await screen.findByText(/no captures/i)).toBeInTheDocument()
  })
})

describe('ScoringPage — individual Score action', () => {
  it('calls scoreCapture with captureId on Score click', async () => {
    renderPage()
    await screen.findByText(/cap-1/i)
    fireEvent.click(screen.getAllByRole('button', { name: /^score$/i })[0])
    await waitFor(() => expect(apiClient.api.scoreCapture).toHaveBeenCalledWith('cap-1'))
  })
})

describe('ScoringPage — batch score', () => {
  it('shows Score All for Exam section', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /score all for exam/i })).toBeInTheDocument()
  })

  it('Score All button is present', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: /score all/i })).toBeInTheDocument()
  })

  it('calls batchScore with exam ID on form submit', async () => {
    const user = userEvent.setup()
    renderPage()
    const input = await screen.findByPlaceholderText('Exam ID (UUID)')
    await user.type(input, 'exam-xyz')
    await user.click(screen.getByRole('button', { name: /score all/i }))
    await waitFor(() => expect(apiClient.api.batchScore).toHaveBeenCalledWith('exam-xyz'))
  })

  it('shows scored/skipped summary after batch completes', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.type(await screen.findByPlaceholderText('Exam ID (UUID)'), 'exam-xyz')
    await user.click(screen.getByRole('button', { name: /score all/i }))
    expect(await screen.findByText(/scored 3 captures, skipped 1/i)).toBeInTheDocument()
  })

  it('clears exam ID input after batch completes', async () => {
    const user = userEvent.setup()
    renderPage()
    const input = await screen.findByPlaceholderText('Exam ID (UUID)')
    await user.type(input, 'exam-xyz')
    await user.click(screen.getByRole('button', { name: /score all/i }))
    await screen.findByText(/scored 3/i)
    expect(input).toHaveValue('')
  })
})

describe('ScoringPage — export', () => {
  it('shows Export Scores section', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /export scores/i })).toBeInTheDocument()
  })

  it('shows Export CSV button', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: /export csv/i })).toBeInTheDocument()
  })

  it('calls exportScores when Export CSV is clicked', async () => {
    const createObjectURL = vi.fn().mockReturnValue('blob:mock')
    const revokeObjectURL = vi.fn()
    Object.defineProperty(window, 'URL', { value: { createObjectURL, revokeObjectURL }, writable: true })

    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /export csv/i }))
    await waitFor(() => expect(apiClient.api.exportScores).toHaveBeenCalled())
  })

  it('passes exam ID to exportScores when filter is set', async () => {
    const user = userEvent.setup()
    const createObjectURL = vi.fn().mockReturnValue('blob:mock')
    const revokeObjectURL = vi.fn()
    Object.defineProperty(window, 'URL', { value: { createObjectURL, revokeObjectURL }, writable: true })

    renderPage()
    await user.type(await screen.findByPlaceholderText(/leave blank for all/i), 'exam-abc')
    fireEvent.click(screen.getByRole('button', { name: /export csv/i }))
    await waitFor(() => expect(apiClient.api.exportScores).toHaveBeenCalledWith('exam-abc'))
  })
})
