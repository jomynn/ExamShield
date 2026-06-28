import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import RankingsPage from '../pages/RankingsPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getExams: vi.fn(),
    getExamRankings: vi.fn(),
    getExamStatistics: vi.fn(),
  },
}))

const mockExams = [
  { examId: 'exam-1', name: 'Mathematics Final', status: 'Active', totalQuestions: 50, createdAt: '2026-06-01T00:00:00Z' },
  { examId: 'exam-2', name: 'Physics Midterm',   status: 'Closed', totalQuestions: 30, createdAt: '2026-06-05T00:00:00Z' },
]

const mockRankings = {
  rankings: [
    { rank: 1, studentId: 'stu-aaa', correctAnswers: 48, totalQuestions: 50, percentage: 96.0 },
    { rank: 2, studentId: 'stu-bbb', correctAnswers: 45, totalQuestions: 50, percentage: 90.0 },
    { rank: 3, studentId: 'stu-ccc', correctAnswers: 40, totalQuestions: 50, percentage: 80.0 },
  ],
}

const mockStats = {
  totalStudents: 3,
  averagePercentage: 88.7,
  passRate: 100.0,
  gradeDistribution: { A: 2, B: 1, C: 0, D: 0, F: 0 },
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <RankingsPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getExams).mockResolvedValue({ exams: mockExams })
  vi.mocked(apiClient.api.getExamRankings).mockResolvedValue(mockRankings)
  vi.mocked(apiClient.api.getExamStatistics).mockResolvedValue(mockStats)
})

describe('RankingsPage', () => {
  it('renders page heading', () => {
    renderPage()
    expect(screen.getByRole('heading', { name: /score rankings/i })).toBeInTheDocument()
  })

  it('shows exam selector once exams load', async () => {
    renderPage()
    expect(await screen.findByRole('option', { name: 'Mathematics Final' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Physics Midterm' })).toBeInTheDocument()
  })

  it('shows prompt when no exam is selected', async () => {
    renderPage()
    await screen.findByRole('option', { name: 'Mathematics Final' })
    expect(screen.getByText(/select an exam/i)).toBeInTheDocument()
  })

  it('shows rankings table after selecting an exam', async () => {
    renderPage()
    await screen.findByRole('option', { name: 'Mathematics Final' })
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'exam-1' } })
    const rows = await screen.findAllByRole('row')
    expect(rows.length).toBeGreaterThan(1) // header + data rows
  })

  it('calls getExamRankings with the selected exam id', async () => {
    renderPage()
    await screen.findByRole('option', { name: 'Mathematics Final' })
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'exam-1' } })
    await screen.findAllByRole('row')
    expect(apiClient.api.getExamRankings).toHaveBeenCalledWith('exam-1')
  })

  it('displays rank numbers and student ids', async () => {
    renderPage()
    await screen.findByRole('option', { name: 'Mathematics Final' })
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'exam-1' } })
    expect(await screen.findByText(/#1/)).toBeInTheDocument()
    expect(screen.getByText(/#2/)).toBeInTheDocument()
    expect(screen.getByText(/#3/)).toBeInTheDocument()
  })

  it('shows statistics strip when stats are available', async () => {
    renderPage()
    await screen.findByRole('option', { name: 'Mathematics Final' })
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'exam-1' } })
    expect(await screen.findByText('88.7%')).toBeInTheDocument()
    expect(screen.getByText('100.0%')).toBeInTheDocument()
  })

  it('shows no-scores message when rankings list is empty', async () => {
    vi.mocked(apiClient.api.getExamRankings).mockResolvedValue({ rankings: [] })
    vi.mocked(apiClient.api.getExamStatistics).mockResolvedValue({
      totalStudents: 0, averagePercentage: 0, passRate: 0, gradeDistribution: {},
    })
    renderPage()
    await screen.findByRole('option', { name: 'Mathematics Final' })
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'exam-1' } })
    expect(await screen.findByText(/no scores submitted/i)).toBeInTheDocument()
  })
})
