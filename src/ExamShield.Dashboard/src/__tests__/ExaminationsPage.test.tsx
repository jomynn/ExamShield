import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import ExaminationsPage from '../pages/ExaminationsPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getExams: vi.fn(),
    createExam: vi.fn(),
  },
}))

const mockExams = [
  {
    examId: 'exam-1',
    name: 'Mathematics Final 2026',
    description: 'Final exam for mathematics',
    status: 'Active',
    totalQuestions: 50,
    createdAt: '2026-06-01T08:00:00Z',
  },
  {
    examId: 'exam-2',
    name: 'Physics Midterm',
    description: null,
    status: 'Draft',
    totalQuestions: 30,
    createdAt: '2026-06-10T09:00:00Z',
  },
]

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <ExaminationsPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getExams).mockResolvedValue({ exams: mockExams })
  vi.mocked(apiClient.api.createExam).mockResolvedValue({
    examId: 'exam-new',
    name: 'New Exam',
    description: null,
    status: 'Draft',
    totalQuestions: 10,
    createdAt: '2026-06-26T10:00:00Z',
  })
})

describe('ExaminationsPage', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /examinations/i })).toBeInTheDocument()
  })

  it('renders a row per exam', async () => {
    renderPage()
    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(3) // header + 2 exams
  })

  it('displays exam names', async () => {
    renderPage()
    expect(await screen.findByText('Mathematics Final 2026')).toBeInTheDocument()
    expect(await screen.findByText('Physics Midterm')).toBeInTheDocument()
  })

  it('displays status chips', async () => {
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    expect(screen.getAllByText('Active').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Draft').length).toBeGreaterThan(0)
  })

  it('shows create exam button', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: /create exam/i })).toBeInTheDocument()
  })

  it('shows create form when button clicked', async () => {
    renderPage()
    await screen.findByRole('button', { name: /create exam/i })
    fireEvent.click(screen.getByRole('button', { name: /create exam/i }))
    expect(screen.getByLabelText(/exam name/i)).toBeInTheDocument()
  })

  it('calls createExam on form submit', async () => {
    renderPage()
    await screen.findByRole('button', { name: /create exam/i })
    fireEvent.click(screen.getByRole('button', { name: /create exam/i }))

    fireEvent.change(screen.getByLabelText(/exam name/i), {
      target: { value: 'Chemistry Test' },
    })
    fireEvent.change(screen.getByLabelText(/total questions/i), {
      target: { value: '40' },
    })
    fireEvent.click(screen.getByRole('button', { name: /^create$/i }))

    await waitFor(() =>
      expect(apiClient.api.createExam).toHaveBeenCalledWith({
        name: 'Chemistry Test',
        description: '',
        totalQuestions: 40,
        scheduledAt: null,
        endsAt: null,
      })
    )
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getExams).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('shows empty message when no exams', async () => {
    vi.mocked(apiClient.api.getExams).mockResolvedValue({ exams: [] })
    renderPage()
    expect(await screen.findByText(/no exams/i)).toBeInTheDocument()
  })
})
