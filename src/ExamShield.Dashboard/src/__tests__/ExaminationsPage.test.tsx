import { render, screen, fireEvent, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import ExaminationsPage from '../pages/ExaminationsPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getExams:              vi.fn(),
    createExam:            vi.fn(),
    deleteExam:            vi.fn(),
    updateExam:            vi.fn(),
    activateExam:          vi.fn(),
    closeExam:             vi.fn(),
    getAnswerKey:          vi.fn(),
    setAnswerKey:          vi.fn(),
    getExamCandidates:     vi.fn(),
    getExamSubmissionStatus: vi.fn(),
    enrollStudent:         vi.fn(),
    unenrollStudent:       vi.fn(),
    bulkEnrollStudents:    vi.fn(),
    exportExams:           vi.fn(),
  },
}))

const mockDraftExam = {
  examId: 'exam-draft',
  name: 'Mathematics Final 2026',
  description: 'Final exam for mathematics',
  status: 'Draft',
  totalQuestions: 50,
  scheduledAt: null,
  endsAt: null,
  createdAt: '2026-06-01T08:00:00Z',
}

const mockActiveExam = {
  examId: 'exam-active',
  name: 'Physics Midterm',
  description: null,
  status: 'Active',
  totalQuestions: 10,
  scheduledAt: '2026-06-15T09:00:00Z',
  endsAt: '2026-06-15T12:00:00Z',
  createdAt: '2026-06-10T09:00:00Z',
}

function renderPage() {
  const qc = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  })
  return render(
    <QueryClientProvider client={qc}>
      <ExaminationsPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(apiClient.api.getExams).mockResolvedValue({
    exams: [mockDraftExam, mockActiveExam],
    totalCount: 2,
    totalPages: 1,
  })
  vi.mocked(apiClient.api.createExam).mockResolvedValue({
    ...mockDraftExam, examId: 'exam-new', name: 'New Exam',
  })
  vi.mocked(apiClient.api.deleteExam).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.updateExam).mockResolvedValue(mockDraftExam)
  vi.mocked(apiClient.api.activateExam).mockResolvedValue({ ...mockDraftExam, status: 'Active' })
  vi.mocked(apiClient.api.closeExam).mockResolvedValue({ ...mockActiveExam, status: 'Closed' })
  vi.mocked(apiClient.api.getAnswerKey).mockResolvedValue(null)
  vi.mocked(apiClient.api.setAnswerKey).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.getExamCandidates).mockResolvedValue({ candidates: [] })
  vi.mocked(apiClient.api.getExamSubmissionStatus).mockResolvedValue({
    totalEnrolled: 0, submitted: 0, missing: 0, students: [],
  })
  vi.mocked(apiClient.api.enrollStudent).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.unenrollStudent).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.bulkEnrollStudents).mockResolvedValue({ enrolled: 0, alreadyEnrolled: 0 })
  vi.mocked(apiClient.api.exportExams).mockResolvedValue(new Blob(['csv'], { type: 'text/csv' }))
})

// ── Basic rendering ───────────────────────────────────────────────────────────

describe('ExaminationsPage — basic rendering', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /examinations/i })).toBeInTheDocument()
  })

  it('renders one table row per exam', async () => {
    renderPage()
    expect(await screen.findAllByRole('row')).toHaveLength(3) // header + 2 exams
  })

  it('displays both exam names', async () => {
    renderPage()
    expect(await screen.findByText('Mathematics Final 2026')).toBeInTheDocument()
    expect(await screen.findByText('Physics Midterm')).toBeInTheDocument()
  })

  it('shows status chips for Draft and Active', async () => {
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    expect(screen.getAllByText('Draft').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Active').length).toBeGreaterThan(0)
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getExams).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('shows empty message when no exams', async () => {
    vi.mocked(apiClient.api.getExams).mockResolvedValue({ exams: [], totalCount: 0, totalPages: 1 })
    renderPage()
    expect(await screen.findByText(/no exams/i)).toBeInTheDocument()
  })

  it('renders scheduled time range when both dates present', async () => {
    renderPage()
    await screen.findByText('Physics Midterm')
    // Active exam has scheduledAt and endsAt — arrow separator should appear
    expect(screen.getByText(/→/)).toBeInTheDocument()
  })
})

// ── Create exam form ──────────────────────────────────────────────────────────

describe('ExaminationsPage — create exam', () => {
  it('shows create button', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: /create exam/i })).toBeInTheDocument()
  })

  it('toggles create form on button click', async () => {
    renderPage()
    await screen.findByRole('button', { name: /create exam/i })
    fireEvent.click(screen.getByRole('button', { name: /create exam/i }))
    expect(screen.getByLabelText(/exam name/i)).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /create exam/i }))
    expect(screen.queryByLabelText(/exam name/i)).not.toBeInTheDocument()
  })

  it('calls createExam with correct payload on submit', async () => {
    renderPage()
    await screen.findByRole('button', { name: /create exam/i })
    fireEvent.click(screen.getByRole('button', { name: /create exam/i }))

    fireEvent.change(screen.getByLabelText(/exam name/i), { target: { value: 'Chemistry Test' } })
    fireEvent.change(screen.getByLabelText(/total questions/i), { target: { value: '40' } })
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

  it('hides form after successful create', async () => {
    renderPage()
    await screen.findByRole('button', { name: /create exam/i })
    fireEvent.click(screen.getByRole('button', { name: /create exam/i }))
    fireEvent.change(screen.getByLabelText(/exam name/i), { target: { value: 'Bio' } })
    fireEvent.click(screen.getByRole('button', { name: /^create$/i }))

    await waitFor(() => expect(screen.queryByLabelText(/exam name/i)).not.toBeInTheDocument())
  })
})

// ── Search & filter ───────────────────────────────────────────────────────────

describe('ExaminationsPage — search and filter', () => {
  it('updates search and refetches when search input changes', async () => {
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    fireEvent.change(screen.getByPlaceholderText(/search by name/i), {
      target: { value: 'Physics' },
    })
    await waitFor(() =>
      expect(apiClient.api.getExams).toHaveBeenCalledWith(
        1, 20, 'Physics', undefined, undefined, undefined
      )
    )
  })

  it('filters by status when select changes', async () => {
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'Active' } })
    await waitFor(() =>
      expect(apiClient.api.getExams).toHaveBeenCalledWith(
        1, 20, undefined, 'Active', undefined, undefined
      )
    )
  })

  it('shows clear button when search is active and clears on click', async () => {
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    fireEvent.change(screen.getByPlaceholderText(/search by name/i), {
      target: { value: 'foo' },
    })
    const clearBtn = await screen.findByRole('button', { name: /clear/i })
    fireEvent.click(clearBtn)
    await waitFor(() =>
      expect(apiClient.api.getExams).toHaveBeenCalledWith(
        1, 20, undefined, undefined, undefined, undefined
      )
    )
  })
})

// ── Export CSV ────────────────────────────────────────────────────────────────

describe('ExaminationsPage — export CSV', () => {
  it('calls api.exportExams when Export CSV is clicked', async () => {
    // createObjectURL / revokeObjectURL are not in jsdom
    Object.defineProperty(URL, 'createObjectURL', { value: vi.fn(() => 'blob:x'), configurable: true })
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true })

    renderPage()
    const btn = await screen.findByRole('button', { name: /export csv/i })
    fireEvent.click(btn)

    await waitFor(() =>
      expect(apiClient.api.exportExams).toHaveBeenCalled()
    )
  })
})

// ── Draft exam actions: Activate & Delete ────────────────────────────────────

describe('ExaminationsPage — Draft exam actions', () => {
  it('calls activateExam when Activate is clicked', async () => {
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    fireEvent.click(screen.getByRole('button', { name: /^activate$/i }))
    await waitFor(() =>
      expect(apiClient.api.activateExam).toHaveBeenCalledWith('exam-draft')
    )
  })

  it('calls deleteExam after user confirms the dialog', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    fireEvent.click(screen.getByRole('button', { name: /^delete$/i }))
    await waitFor(() =>
      expect(apiClient.api.deleteExam).toHaveBeenCalledWith('exam-draft')
    )
  })

  it('does NOT call deleteExam when user cancels the confirm dialog', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    fireEvent.click(screen.getByRole('button', { name: /^delete$/i }))
    expect(apiClient.api.deleteExam).not.toHaveBeenCalled()
  })
})

// ── Edit exam modal ───────────────────────────────────────────────────────────

describe('ExaminationsPage — edit exam modal', () => {
  it('opens edit modal when Edit is clicked on a Draft exam', async () => {
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    fireEvent.click(screen.getByRole('button', { name: /^edit$/i }))
    expect(await screen.findByRole('heading', { name: /edit exam/i })).toBeInTheDocument()
  })

  it('pre-fills the edit form with the exam name', async () => {
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    fireEvent.click(screen.getByRole('button', { name: /^edit$/i }))
    const nameInput = await screen.findByDisplayValue('Mathematics Final 2026')
    expect(nameInput).toBeInTheDocument()
  })

  it('calls updateExam with updated values on Save', async () => {
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    fireEvent.click(screen.getByRole('button', { name: /^edit$/i }))
    const nameInput = await screen.findByDisplayValue('Mathematics Final 2026')
    fireEvent.change(nameInput, { target: { value: 'Math Final Updated' } })
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }))

    await waitFor(() =>
      expect(apiClient.api.updateExam).toHaveBeenCalledWith(
        'exam-draft',
        expect.objectContaining({ name: 'Math Final Updated' })
      )
    )
  })

  it('closes edit modal after successful save', async () => {
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    fireEvent.click(screen.getByRole('button', { name: /^edit$/i }))
    await screen.findByRole('heading', { name: /edit exam/i })
    fireEvent.click(screen.getByRole('button', { name: /^save$/i }))
    await waitFor(() =>
      expect(screen.queryByRole('heading', { name: /edit exam/i })).not.toBeInTheDocument()
    )
  })

  it('closes edit modal on Cancel without calling updateExam', async () => {
    renderPage()
    await screen.findByText('Mathematics Final 2026')
    fireEvent.click(screen.getByRole('button', { name: /^edit$/i }))
    await screen.findByRole('heading', { name: /edit exam/i })
    fireEvent.click(screen.getByRole('button', { name: /^cancel$/i }))
    expect(screen.queryByRole('heading', { name: /edit exam/i })).not.toBeInTheDocument()
    expect(apiClient.api.updateExam).not.toHaveBeenCalled()
  })
})

// ── Active exam actions: Answer Key, Students, Close ─────────────────────────

describe('ExaminationsPage — Active exam: Close', () => {
  it('calls closeExam when Close is clicked', async () => {
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /^close$/i }))
    await waitFor(() =>
      expect(apiClient.api.closeExam).toHaveBeenCalledWith('exam-active')
    )
  })
})

describe('ExaminationsPage — Answer Key modal', () => {
  it('opens answer key modal when Answer Key clicked on Active exam', async () => {
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /answer key/i }))
    expect(await screen.findByRole('heading', { name: /answer key/i })).toBeInTheDocument()
  })

  it('renders input fields for each question when no existing key', async () => {
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /answer key/i }))
    // Active exam has 10 questions → 10 question inputs
    const inputs = await screen.findAllByPlaceholderText('A')
    expect(inputs).toHaveLength(10)
  })

  it('calls setAnswerKey with answers on Save', async () => {
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /answer key/i }))
    const inputs = await screen.findAllByPlaceholderText('A')
    fireEvent.change(inputs[0], { target: { value: 'A' } })
    fireEvent.click(screen.getByRole('button', { name: /save answer key/i }))

    await waitFor(() =>
      expect(apiClient.api.setAnswerKey).toHaveBeenCalledWith(
        'exam-active',
        expect.objectContaining({ 1: 'A' })
      )
    )
  })

  it('closes answer key modal on Cancel', async () => {
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /answer key/i }))
    await screen.findByRole('heading', { name: /answer key/i })
    fireEvent.click(screen.getByRole('button', { name: /^cancel$/i }))
    expect(screen.queryByRole('heading', { name: /answer key/i })).not.toBeInTheDocument()
  })

  it('shows read-only key entries when key already exists', async () => {
    vi.mocked(apiClient.api.getAnswerKey).mockResolvedValue({
      examId: 'exam-active',
      answers: { 1: 'A', 2: 'B' },
    })
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /answer key/i }))
    // Should display "already set" label instead of save button
    expect(await screen.findByText(/already set/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /save answer key/i })).not.toBeInTheDocument()
  })

  it('closes the read-only key modal with Close button', async () => {
    vi.mocked(apiClient.api.getAnswerKey).mockResolvedValue({
      examId: 'exam-active',
      answers: { 1: 'C' },
    })
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /answer key/i }))
    const alreadySetHeading = await screen.findByText(/already set/i)
    // scope within the modal to avoid collision with the "Close exam" button in the table
    const modal = alreadySetHeading.closest('div[class*="bg-background"]')!
    fireEvent.click(within(modal).getByRole('button', { name: /^close$/i }))
    expect(screen.queryByText(/already set/i)).not.toBeInTheDocument()
  })
})

// ── Enrollment modal ──────────────────────────────────────────────────────────

describe('ExaminationsPage — Enrollment modal', () => {
  it('opens enrollment modal when Students is clicked', async () => {
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /^students$/i }))
    expect(await screen.findByRole('heading', { name: /enrolled students/i })).toBeInTheDocument()
  })

  it('shows empty message when no candidates enrolled', async () => {
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /^students$/i }))
    expect(await screen.findByText(/no students enrolled yet/i)).toBeInTheDocument()
  })

  it('calls enrollStudent when form is submitted', async () => {
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /^students$/i }))
    const input = await screen.findByPlaceholderText(/student id/i)
    fireEvent.change(input, { target: { value: 'stu-uuid-001' } })
    fireEvent.submit(input.closest('form')!)

    await waitFor(() =>
      expect(apiClient.api.enrollStudent).toHaveBeenCalledWith('exam-active', 'stu-uuid-001')
    )
  })

  it('calls bulkEnrollStudents when Bulk Enroll is clicked', async () => {
    vi.mocked(apiClient.api.bulkEnrollStudents).mockResolvedValue({
      enrolled: 2, alreadyEnrolled: 1,
    })
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /^students$/i }))
    const textarea = await screen.findByPlaceholderText(/uuid1/i)
    fireEvent.change(textarea, { target: { value: 'id1\nid2\nid3' } })
    fireEvent.click(screen.getByRole('button', { name: /^bulk enroll$/i }))

    await waitFor(() =>
      expect(apiClient.api.bulkEnrollStudents).toHaveBeenCalledWith(
        'exam-active', ['id1', 'id2', 'id3']
      )
    )
  })

  it('shows bulk enroll result summary after completion', async () => {
    vi.mocked(apiClient.api.bulkEnrollStudents).mockResolvedValue({
      enrolled: 3, alreadyEnrolled: 1,
    })
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /^students$/i }))
    const textarea = await screen.findByPlaceholderText(/uuid1/i)
    fireEvent.change(textarea, { target: { value: 'id1\nid2\nid3' } })
    fireEvent.click(screen.getByRole('button', { name: /^bulk enroll$/i }))

    await waitFor(() =>
      expect(screen.getByText(/3 enrolled, 1 skipped/i)).toBeInTheDocument()
    )
  })

  it('shows enrolled candidates with submission status', async () => {
    vi.mocked(apiClient.api.getExamCandidates).mockResolvedValue({
      candidates: [{ studentId: 'stu-abc-001' }],
    })
    vi.mocked(apiClient.api.getExamSubmissionStatus).mockResolvedValue({
      totalEnrolled: 1, submitted: 0, missing: 1,
      students: [{ studentId: 'stu-abc-001', hasSubmitted: false, captureStatus: null }],
    })
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /^students$/i }))
    expect(await screen.findByText('stu-abc-001')).toBeInTheDocument()
    expect(screen.getByText('Missing')).toBeInTheDocument()
  })

  it('shows submission progress bar when students are enrolled', async () => {
    vi.mocked(apiClient.api.getExamSubmissionStatus).mockResolvedValue({
      totalEnrolled: 4, submitted: 2, missing: 2,
      students: [],
    })
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /^students$/i }))
    expect(await screen.findByText(/2 \/ 4/)).toBeInTheDocument()
  })

  it('calls unenroll when remove button is clicked for a non-submitted student', async () => {
    vi.mocked(apiClient.api.getExamCandidates).mockResolvedValue({
      candidates: [{ studentId: 'stu-abc-001' }],
    })
    vi.mocked(apiClient.api.getExamSubmissionStatus).mockResolvedValue({
      totalEnrolled: 1, submitted: 0, missing: 1,
      students: [{ studentId: 'stu-abc-001', hasSubmitted: false, captureStatus: null }],
    })
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /^students$/i }))
    const removeBtn = await screen.findByTitle(/remove from exam/i)
    fireEvent.click(removeBtn)

    await waitFor(() =>
      expect(apiClient.api.unenrollStudent).toHaveBeenCalledWith('exam-active', 'stu-abc-001')
    )
  })

  it('closes enrollment modal on Close button', async () => {
    renderPage()
    await screen.findByText('Physics Midterm')
    fireEvent.click(screen.getByRole('button', { name: /^students$/i }))
    await screen.findByRole('heading', { name: /enrolled students/i })
    // Close button is the last button in the modal
    const modal = screen.getByRole('heading', { name: /enrolled students/i }).closest('div')!
    const closeBtn = within(modal.parentElement!).getByRole('button', { name: /^close$/i })
    fireEvent.click(closeBtn)
    expect(screen.queryByRole('heading', { name: /enrolled students/i })).not.toBeInTheDocument()
  })
})
