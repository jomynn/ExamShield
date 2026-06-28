import { render, screen, fireEvent, waitFor, within } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import AnswerSheetsPage from '../pages/AnswerSheetsPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getCaptures: vi.fn(),
  },
}))

const CAPTURES = [
  { captureId: 'cap-1', examId: 'exam-1', studentId: 'stu-1', deviceId: 'dev-1', status: 'Verified',  capturedAt: '2024-01-01T10:00:00Z', storageKey: 'key-1' },
  { captureId: 'cap-2', examId: 'exam-2', studentId: 'stu-2', deviceId: 'dev-2', status: 'Uploaded',  capturedAt: '2024-01-02T10:00:00Z', storageKey: 'key-2' },
  { captureId: 'cap-3', examId: 'exam-3', studentId: 'stu-3', deviceId: 'dev-3', status: 'Created',   capturedAt: '2024-01-03T10:00:00Z', storageKey: null   },
]

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <AnswerSheetsPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getCaptures).mockResolvedValue({ captures: CAPTURES })
})

describe('AnswerSheetsPage', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /answer sheets/i })).toBeInTheDocument()
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getCaptures).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('renders a row per capture', async () => {
    renderPage()
    const rows = await screen.findAllByRole('row')
    expect(rows.length).toBeGreaterThan(CAPTURES.length)
  })

  it('shows student IDs in the table', async () => {
    renderPage()
    expect(await screen.findByText(/stu-1/)).toBeInTheDocument()
    expect(screen.getByText(/stu-2/)).toBeInTheDocument()
  })

  it('shows status badges', async () => {
    renderPage()
    expect(await screen.findByText('Verified')).toBeInTheDocument()
    expect(screen.getByText('Uploaded')).toBeInTheDocument()
  })

  it('opens image viewer when View Image is clicked for uploaded capture', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    const viewBtns = screen.getAllByRole('button', { name: /view image/i })
    fireEvent.click(viewBtns[0])
    await waitFor(() => expect(screen.getByAltText(/answer sheet/i)).toBeInTheDocument())
  })

  it('shows no image button for captures without storage key', async () => {
    renderPage()
    await screen.findByText(/stu-3/)
    const rows = screen.getAllByRole('row')
    const created = rows.find(r => r.textContent?.includes('stu-3'))
    expect(within(created!).queryByRole('button', { name: /view image/i })).toBeNull()
  })
})
