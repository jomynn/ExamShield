import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import OcrQueuePage from '../pages/OcrQueuePage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getOcrQueue: vi.fn(),
    triggerOcr: vi.fn(),
    triggerBatchOcr: vi.fn(),
  },
}))

const mockQueue = [
  { captureId: 'cap-1', examId: 'exam-1', studentId: 'stu-1', uploadedAt: '2026-06-26T10:00:00Z' },
  { captureId: 'cap-2', examId: 'exam-1', studentId: 'stu-2', uploadedAt: '2026-06-26T11:00:00Z' },
]

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <OcrQueuePage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(apiClient.api.getOcrQueue).mockResolvedValue({ items: mockQueue })
  vi.mocked(apiClient.api.triggerOcr).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.triggerBatchOcr).mockResolvedValue({ queued: 5, skipped: 2 })
})

describe('OcrQueuePage — display', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /ocr queue/i })).toBeInTheDocument()
  })

  it('shows item count', async () => {
    renderPage()
    expect(await screen.findByText(/2 pending/i)).toBeInTheDocument()
  })

  it('renders a row per queued capture', async () => {
    renderPage()
    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(3) // header + 2 items
  })

  it('displays (truncated) capture IDs', async () => {
    renderPage()
    expect(await screen.findByText(/cap-1/i)).toBeInTheDocument()
    expect(screen.getByText(/cap-2/i)).toBeInTheDocument()
  })

  it('displays (truncated) student IDs', async () => {
    renderPage()
    expect(await screen.findByText(/stu-1/i)).toBeInTheDocument()
    expect(screen.getByText(/stu-2/i)).toBeInTheDocument()
  })

  it('renders a Trigger OCR button per row', async () => {
    renderPage()
    await screen.findByText(/cap-1/i)
    expect(screen.getAllByRole('button', { name: /trigger ocr/i })).toHaveLength(2)
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getOcrQueue).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('shows empty message when queue is empty', async () => {
    vi.mocked(apiClient.api.getOcrQueue).mockResolvedValue({ items: [] })
    renderPage()
    expect(await screen.findByText(/no captures/i)).toBeInTheDocument()
  })
})

describe('OcrQueuePage — individual trigger', () => {
  it('calls triggerOcr with captureId on button click', async () => {
    renderPage()
    await screen.findByText(/cap-1/i)
    fireEvent.click(screen.getAllByRole('button', { name: /trigger ocr/i })[0])
    await waitFor(() => expect(apiClient.api.triggerOcr).toHaveBeenCalledWith('cap-1'))
  })
})

describe('OcrQueuePage — batch OCR', () => {
  it('shows Process All for Exam section', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /process all for exam/i })).toBeInTheDocument()
  })

  it('Process All button is present', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: /process all/i })).toBeInTheDocument()
  })

  it('calls triggerBatchOcr with exam ID on form submit', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByPlaceholderText(/exam id/i)
    await user.type(screen.getByPlaceholderText(/exam id/i), 'exam-xyz')
    await user.click(screen.getByRole('button', { name: /process all/i }))
    await waitFor(() => expect(apiClient.api.triggerBatchOcr).toHaveBeenCalledWith('exam-xyz'))
  })

  it('shows queued/skipped summary after batch completes', async () => {
    const user = userEvent.setup()
    renderPage()
    await user.type(await screen.findByPlaceholderText(/exam id/i), 'exam-xyz')
    await user.click(screen.getByRole('button', { name: /process all/i }))
    expect(await screen.findByText(/queued 5 captures, skipped 2/i)).toBeInTheDocument()
  })

  it('clears exam ID input after batch completes', async () => {
    const user = userEvent.setup()
    renderPage()
    const input = await screen.findByPlaceholderText(/exam id/i)
    await user.type(input, 'exam-xyz')
    await user.click(screen.getByRole('button', { name: /process all/i }))
    await screen.findByText(/queued 5/i)
    expect(input).toHaveValue('')
  })
})
