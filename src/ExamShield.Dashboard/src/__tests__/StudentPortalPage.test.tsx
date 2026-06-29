import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import StudentPortalPage from '../pages/StudentPortalPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getStudentResults: vi.fn(),
    submitReviewRequest: vi.fn(),
  },
}))

const STUDENT_ID = 'stu-abc-123'

const mockResults = {
  studentId: STUDENT_ID,
  results: [
    {
      scoreId: 's1',
      captureId: 'c1',
      examId: 'e1',
      examName: 'Mathematics Final',
      correctAnswers: 45,
      totalQuestions: 50,
      percentage: 90.0,
      scoredAt: '2026-06-27T10:00:00Z',
      hashHex: 'abc123def456abcd',
      isVerified: true,
    },
    {
      scoreId: 's2',
      captureId: 'c2',
      examId: 'e2',
      examName: 'Physics Midterm',
      correctAnswers: 30,
      totalQuestions: 50,
      percentage: 60.0,
      scoredAt: '2026-06-27T11:00:00Z',
      hashHex: 'def789abc012efgh',
      isVerified: false,
    },
  ],
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <StudentPortalPage />
    </QueryClientProvider>
  )
}

function lookUp(id = STUDENT_ID) {
  fireEvent.change(screen.getByPlaceholderText(/student id/i), { target: { value: id } })
  fireEvent.click(screen.getByRole('button', { name: /look up/i }))
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(apiClient.api.getStudentResults).mockResolvedValue(mockResults)
  vi.mocked(apiClient.api.submitReviewRequest).mockResolvedValue(undefined)
})

describe('StudentPortalPage — layout', () => {
  it('renders page heading', () => {
    renderPage()
    expect(screen.getByRole('heading', { name: /student portal/i })).toBeInTheDocument()
  })

  it('shows student ID search field', () => {
    renderPage()
    expect(screen.getByPlaceholderText(/student id/i)).toBeInTheDocument()
  })

  it('shows Look Up button', () => {
    renderPage()
    expect(screen.getByRole('button', { name: /look up/i })).toBeInTheDocument()
  })

  it('review request section is hidden before lookup', () => {
    renderPage()
    expect(screen.queryByText(/submit a review request/i)).not.toBeInTheDocument()
  })
})

describe('StudentPortalPage — lookup', () => {
  it('calls getStudentResults with entered ID on button click', async () => {
    renderPage()
    lookUp()
    await waitFor(() =>
      expect(apiClient.api.getStudentResults).toHaveBeenCalledWith(STUDENT_ID)
    )
  })

  it('pressing Enter triggers lookup', async () => {
    renderPage()
    const input = screen.getByPlaceholderText(/student id/i)
    fireEvent.change(input, { target: { value: STUDENT_ID } })
    fireEvent.keyDown(input, { key: 'Enter' })
    await waitFor(() =>
      expect(apiClient.api.getStudentResults).toHaveBeenCalledWith(STUDENT_ID)
    )
  })

  it('does not call API when input is empty', async () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: /look up/i }))
    await new Promise(r => setTimeout(r, 50))
    expect(apiClient.api.getStudentResults).not.toHaveBeenCalled()
  })

  it('displays exam names after lookup', async () => {
    renderPage()
    lookUp()
    expect(await screen.findByText('Mathematics Final')).toBeInTheDocument()
    expect(screen.getByText('Physics Midterm')).toBeInTheDocument()
  })

  it('shows score percentages', async () => {
    renderPage()
    lookUp()
    expect(await screen.findByText(/90\.0%/)).toBeInTheDocument()
    expect(screen.getByText(/60\.0%/)).toBeInTheDocument()
  })

  it('shows score fraction (correct/total)', async () => {
    renderPage()
    lookUp()
    expect(await screen.findByText('45/50')).toBeInTheDocument()
  })

  it('shows Verified badge for verified captures', async () => {
    renderPage()
    lookUp()
    await screen.findByText('Mathematics Final')
    expect(screen.getAllByText(/verified/i).length).toBeGreaterThanOrEqual(1)
  })

  it('shows truncated hash in results table', async () => {
    renderPage()
    lookUp()
    await screen.findByText('Mathematics Final')
    expect(screen.getByText(/abc123def456/i)).toBeInTheDocument()
  })

  it('shows Print Certificate button after results load', async () => {
    renderPage()
    lookUp()
    expect(await screen.findByRole('button', { name: /print/i })).toBeInTheDocument()
  })

  it('shows student ID in results section', async () => {
    renderPage()
    lookUp()
    await screen.findByText('Mathematics Final')
    expect(screen.getByText(STUDENT_ID)).toBeInTheDocument()
  })

  it('shows empty state message when no results', async () => {
    vi.mocked(apiClient.api.getStudentResults).mockResolvedValue({ studentId: STUDENT_ID, results: [] })
    renderPage()
    lookUp()
    expect(await screen.findByText(/no scored results/i)).toBeInTheDocument()
  })
})

describe('StudentPortalPage — review request', () => {
  it('shows review request section after student lookup', async () => {
    renderPage()
    lookUp()
    expect(await screen.findByText(/submit a review request/i)).toBeInTheDocument()
  })

  it('Submit button is disabled when both fields are empty', async () => {
    renderPage()
    lookUp()
    await screen.findByText(/submit a review request/i)
    expect(screen.getByRole('button', { name: /submit request/i })).toBeDisabled()
  })

  it('Submit button is disabled when only capture ID is filled', async () => {
    renderPage()
    lookUp()
    await screen.findByText(/submit a review request/i)
    fireEvent.change(screen.getByPlaceholderText(/capture id/i), { target: { value: 'c-1' } })
    expect(screen.getByRole('button', { name: /submit request/i })).toBeDisabled()
  })

  it('Submit button is disabled when only reason is filled', async () => {
    renderPage()
    lookUp()
    await screen.findByText(/submit a review request/i)
    fireEvent.change(screen.getByPlaceholderText(/reason/i), { target: { value: 'OCR error' } })
    expect(screen.getByRole('button', { name: /submit request/i })).toBeDisabled()
  })

  it('Submit button is enabled when both fields are filled', async () => {
    renderPage()
    lookUp()
    await screen.findByText(/submit a review request/i)
    fireEvent.change(screen.getByPlaceholderText(/capture id/i), { target: { value: 'c-1' } })
    fireEvent.change(screen.getByPlaceholderText(/reason/i), { target: { value: 'OCR misread Q5' } })
    expect(screen.getByRole('button', { name: /submit request/i })).not.toBeDisabled()
  })

  it('calls submitReviewRequest with studentId, captureId, and reason', async () => {
    renderPage()
    lookUp()
    await screen.findByText(/submit a review request/i)
    fireEvent.change(screen.getByPlaceholderText(/capture id/i), { target: { value: 'cap-xyz' } })
    fireEvent.change(screen.getByPlaceholderText(/reason/i), { target: { value: 'OCR misread Q5' } })
    fireEvent.click(screen.getByRole('button', { name: /submit request/i }))
    await waitFor(() =>
      expect(apiClient.api.submitReviewRequest).toHaveBeenCalledWith(
        STUDENT_ID, 'cap-xyz', 'OCR misread Q5'
      )
    )
  })

  it('shows success message after submission', async () => {
    renderPage()
    lookUp()
    await screen.findByText(/submit a review request/i)
    fireEvent.change(screen.getByPlaceholderText(/capture id/i), { target: { value: 'cap-xyz' } })
    fireEvent.change(screen.getByPlaceholderText(/reason/i), { target: { value: 'OCR error' } })
    fireEvent.click(screen.getByRole('button', { name: /submit request/i }))
    expect(await screen.findByText(/submitted successfully/i)).toBeInTheDocument()
  })

  it('clears capture ID and reason after successful submission', async () => {
    renderPage()
    lookUp()
    await screen.findByText(/submit a review request/i)
    fireEvent.change(screen.getByPlaceholderText(/capture id/i), { target: { value: 'cap-xyz' } })
    fireEvent.change(screen.getByPlaceholderText(/reason/i), { target: { value: 'OCR error' } })
    fireEvent.click(screen.getByRole('button', { name: /submit request/i }))
    await screen.findByText(/submitted successfully/i)
    expect(screen.getByPlaceholderText(/capture id/i)).toHaveValue('')
    expect(screen.getByPlaceholderText(/reason/i)).toHaveValue('')
  })
})
