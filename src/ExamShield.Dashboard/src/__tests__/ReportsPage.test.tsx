import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import ReportsPage from '../pages/ReportsPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getReportSummary:   vi.fn(),
    getResults:         vi.fn(),
    getAuditLog:        vi.fn(),
    getSecurityEvents:  vi.fn(),
    getExamReport:      vi.fn(),
    exportExamReportCsv: vi.fn(),
  },
}))

// ── Fixtures ──────────────────────────────────────────────────────────────────

const SUMMARY = {
  generatedAt: '2026-06-27T08:00:00Z',
  captures: { total: 120, created: 5, uploaded: 20, verified: 90, tampered: 5 },
  ocr: { totalProcessed: 85, averageConfidence: 0.924 },
  scores: { totalScored: 80, averagePercentage: 76.5, highestPercentage: 99.0, lowestPercentage: 42.0 },
  security: { totalEvents: 15, criticalEvents: 3 },
}

const RESULTS = {
  results: [
    { scoreId: 's1', captureId: 'c1', examId: 'e1', studentId: 'stu1', correctAnswers: 45, totalQuestions: 50, percentage: 92.0, scoredAt: '2026-06-27T09:00:00Z' },
    { scoreId: 's2', captureId: 'c2', examId: 'e1', studentId: 'stu2', correctAnswers: 38, totalQuestions: 50, percentage: 76.0, scoredAt: '2026-06-27T09:01:00Z' },
    { scoreId: 's3', captureId: 'c3', examId: 'e1', studentId: 'stu3', correctAnswers: 31, totalQuestions: 50, percentage: 62.0, scoredAt: '2026-06-27T09:02:00Z' },
    { scoreId: 's4', captureId: 'c4', examId: 'e1', studentId: 'stu4', correctAnswers: 22, totalQuestions: 50, percentage: 44.0, scoredAt: '2026-06-27T09:03:00Z' },
  ],
}

const AUDIT_ENTRIES = {
  entries: [
    { id: 'a1', action: 'CaptureRegistered', userId: 'user-1', ipAddress: '10.0.0.1', occurredAt: '2026-01-01T10:00:00Z' },
    { id: 'a2', action: 'ImageUploaded',      userId: 'system',  ipAddress: '127.0.0.1', occurredAt: '2026-01-01T10:01:00Z' },
    { id: 'a3', action: 'CaptureRegistered', userId: 'user-2', ipAddress: '10.0.0.2', occurredAt: '2026-01-01T10:02:00Z' },
  ],
  totalCount: 3,
}

const SECURITY_EVENTS = {
  events: [
    { eventId: 's1', severity: 'Critical', eventType: 'HashMismatch',     occurredAt: '2026-01-01T10:00:00Z' },
    { eventId: 's2', severity: 'High',     eventType: 'InvalidSignature', occurredAt: '2026-01-01T10:01:00Z' },
  ],
}

const EXAM_REPORT = {
  examId: 'exam-1',
  examName: 'Math Exam 2026',
  examStatus: 'Closed',
  totalQuestions: 40,
  generatedAt: '2026-06-27T10:00:00Z',
  totalCaptures: 50,
  verifiedCaptures: 45,
  tamperedCaptures: 2,
  totalReviewRequests: 8,
  totalOcrProcessed: 48,
  ocrAverageConfidence: 0.875,
  lowConfidenceCount: 6,
  totalScored: 44,
  averageScorePercentage: 68.5,
  highestScorePercentage: 95.0,
  lowestScorePercentage: 32.0,
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <ReportsPage />
    </QueryClientProvider>
  )
}

function mockCreateObjectUrl() {
  const create = vi.fn(() => 'blob:mock-url')
  const revoke = vi.fn()
  vi.stubGlobal('URL', { createObjectURL: create, revokeObjectURL: revoke })
  return { create, revoke }
}

// ── Setup ─────────────────────────────────────────────────────────────────────

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(apiClient.api.getReportSummary).mockResolvedValue(SUMMARY)
  vi.mocked(apiClient.api.getResults).mockResolvedValue(RESULTS)
  vi.mocked(apiClient.api.getAuditLog).mockResolvedValue(AUDIT_ENTRIES)
  vi.mocked(apiClient.api.getSecurityEvents).mockResolvedValue(SECURITY_EVENTS)
  vi.mocked(apiClient.api.getExamReport).mockResolvedValue(EXAM_REPORT)
  vi.mocked(apiClient.api.exportExamReportCsv).mockResolvedValue(new Blob(['csv'], { type: 'text/csv' }))
})

// ── Header ────────────────────────────────────────────────────────────────────

describe('ReportsPage — header', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /reports/i })).toBeInTheDocument()
  })

  it('shows generated timestamp', async () => {
    renderPage()
    expect(await screen.findByText(/generated/i)).toBeInTheDocument()
  })

  it('shows export results button', async () => {
    renderPage()
    await screen.findByText('120')
    expect(screen.getByRole('button', { name: /export results/i })).toBeInTheDocument()
  })

  it('shows export audit button', async () => {
    renderPage()
    await screen.findByText('120')
    expect(screen.getByRole('button', { name: /export audit/i })).toBeInTheDocument()
  })

  it('shows loading state before data loads', () => {
    vi.mocked(apiClient.api.getReportSummary).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })
})

// ── Summary stats ─────────────────────────────────────────────────────────────

describe('ReportsPage — summary stats', () => {
  it('shows total captures from summary', async () => {
    renderPage()
    expect(await screen.findByText('120')).toBeInTheDocument()
  })

  it('shows total scored', async () => {
    renderPage()
    expect(await screen.findByText('80')).toBeInTheDocument()
  })

  it('shows average score percentage', async () => {
    renderPage()
    expect(await screen.findByText(/76\.5%/)).toBeInTheDocument()
  })

  it('shows critical security events count', async () => {
    renderPage()
    expect(await screen.findByText('3')).toBeInTheDocument()
  })

  it('shows OCR total processed', async () => {
    renderPage()
    expect(await screen.findByText('85')).toBeInTheDocument()
  })

  it('shows avg OCR confidence as percentage', async () => {
    renderPage()
    expect(await screen.findByText('92.4%')).toBeInTheDocument()
  })

  it('shows section headings', async () => {
    renderPage()
    await screen.findByText('120')
    expect(screen.getByText('Processing')).toBeInTheDocument()
    expect(screen.getByText('Per-Exam Report')).toBeInTheDocument()
  })
})

// ── Capture pipeline chart ────────────────────────────────────────────────────

describe('ReportsPage — capture pipeline chart', () => {
  it('shows Capture Pipeline Status heading', async () => {
    renderPage()
    expect(await screen.findByText('Capture Pipeline Status')).toBeInTheDocument()
  })

  it('shows capture status legend entries', async () => {
    renderPage()
    await screen.findByText('Capture Pipeline Status')
    expect(screen.getByText('Verified')).toBeInTheDocument()
    expect(screen.getByText('Uploaded')).toBeInTheDocument()
    expect(screen.getByText('Tampered')).toBeInTheDocument()
  })

  it('shows total captures in legend', async () => {
    renderPage()
    await screen.findByText('Capture Pipeline Status')
    expect(screen.getByText(/total:/i)).toBeInTheDocument()
  })

  it('shows "No captures yet" when all counts are zero', async () => {
    vi.mocked(apiClient.api.getReportSummary).mockResolvedValue({
      ...SUMMARY,
      captures: { total: 0, created: 0, uploaded: 0, verified: 0, tampered: 0 },
    })
    renderPage()
    expect(await screen.findByText('No captures yet')).toBeInTheDocument()
  })
})

// ── Score distribution chart ──────────────────────────────────────────────────

describe('ReportsPage — score distribution chart', () => {
  it('shows Score Distribution heading', async () => {
    renderPage()
    expect(await screen.findByText('Score Distribution')).toBeInTheDocument()
  })

  it('renders score bucket ranges when scores are present', async () => {
    renderPage()
    await screen.findByText('Score Distribution')
    expect(screen.getByText('90–100%')).toBeInTheDocument()
    expect(screen.getByText('75–89%')).toBeInTheDocument()
    expect(screen.getByText('60–74%')).toBeInTheDocument()
    expect(screen.getByText('<60%')).toBeInTheDocument()
  })

  it('shows "No scores published yet" when results empty', async () => {
    vi.mocked(apiClient.api.getResults).mockResolvedValue({ results: [] })
    renderPage()
    expect(await screen.findByText('No scores published yet')).toBeInTheDocument()
  })
})

// ── Security Overview chart ───────────────────────────────────────────────────

describe('ReportsPage — security overview chart', () => {
  it('shows Security Overview heading', async () => {
    renderPage()
    expect(await screen.findByText('Security Overview')).toBeInTheDocument()
  })

  it('shows total events count', async () => {
    renderPage()
    expect(await screen.findByText('15')).toBeInTheDocument()
  })

  it('does not show "No security events" when events are present', async () => {
    renderPage()
    await screen.findByText('Security Overview')
    await waitFor(() =>
      expect(screen.queryByText('No security events to display')).not.toBeInTheDocument()
    )
  })

  it('shows "No security events to display" when events empty', async () => {
    vi.mocked(apiClient.api.getSecurityEvents).mockResolvedValue({ events: [] })
    renderPage()
    expect(await screen.findByText('No security events to display')).toBeInTheDocument()
  })
})

// ── Audit Activity chart ──────────────────────────────────────────────────────

describe('ReportsPage — audit activity chart', () => {
  it('shows Audit Activity by Action heading', async () => {
    renderPage()
    expect(await screen.findByText('Audit Activity by Action')).toBeInTheDocument()
  })

  it('does not show "No audit entries" when entries are present', async () => {
    renderPage()
    await screen.findByText('Audit Activity by Action')
    await waitFor(() =>
      expect(screen.queryByText(/no audit entries/i)).not.toBeInTheDocument()
    )
  })

  it('shows "No audit entries" when audit log is empty', async () => {
    vi.mocked(apiClient.api.getAuditLog).mockResolvedValue({ entries: [], totalCount: 0 })
    renderPage()
    expect(await screen.findByText('No audit entries')).toBeInTheDocument()
  })
})

// ── Export buttons ────────────────────────────────────────────────────────────

describe('ReportsPage — export buttons', () => {
  it('Export Results triggers CSV download', async () => {
    const { create } = mockCreateObjectUrl()
    renderPage()
    await screen.findByText('120')
    fireEvent.click(screen.getByRole('button', { name: /export results/i }))
    expect(create).toHaveBeenCalledWith(expect.any(Blob))
  })

  it('Export Audit triggers CSV download', async () => {
    const { create } = mockCreateObjectUrl()
    renderPage()
    await screen.findByText('120')
    fireEvent.click(screen.getByRole('button', { name: /export audit/i }))
    expect(create).toHaveBeenCalledWith(expect.any(Blob))
  })
})

// ── Per-exam drill-down ───────────────────────────────────────────────────────

describe('ReportsPage — per-exam drill-down', () => {
  it('shows exam ID input and Load button', async () => {
    renderPage()
    await screen.findByText('Per-Exam Report')
    expect(screen.getByPlaceholderText(/exam id/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^load$/i })).toBeInTheDocument()
  })

  it('loads and shows exam report after typing ID and clicking Load', async () => {
    renderPage()
    await screen.findByText('Per-Exam Report')
    fireEvent.change(screen.getByPlaceholderText(/exam id/i), { target: { value: 'exam-1' } })
    fireEvent.click(screen.getByRole('button', { name: /^load$/i }))
    expect(await screen.findByText('Math Exam 2026')).toBeInTheDocument()
    expect(apiClient.api.getExamReport).toHaveBeenCalledWith('exam-1')
  })

  it('loads exam report on Enter key', async () => {
    renderPage()
    await screen.findByText('Per-Exam Report')
    const input = screen.getByPlaceholderText(/exam id/i)
    fireEvent.change(input, { target: { value: 'exam-1' } })
    fireEvent.keyDown(input, { key: 'Enter' })
    expect(await screen.findByText('Math Exam 2026')).toBeInTheDocument()
  })

  it('shows exam status, question count, and generated timestamp in report', async () => {
    renderPage()
    await screen.findByText('Per-Exam Report')
    fireEvent.change(screen.getByPlaceholderText(/exam id/i), { target: { value: 'exam-1' } })
    fireEvent.click(screen.getByRole('button', { name: /^load$/i }))
    await screen.findByText('Math Exam 2026')
    expect(screen.getByText(/closed/i)).toBeInTheDocument()
    expect(screen.getByText(/40 questions/i)).toBeInTheDocument()
  })

  it('shows capture counts from exam report', async () => {
    renderPage()
    await screen.findByText('Per-Exam Report')
    fireEvent.change(screen.getByPlaceholderText(/exam id/i), { target: { value: 'exam-1' } })
    fireEvent.click(screen.getByRole('button', { name: /^load$/i }))
    await screen.findByText('Math Exam 2026')
    // totalCaptures=50, verifiedCaptures=45, tamperedCaptures=2, reviewRequests=8
    expect(screen.getByText('50')).toBeInTheDocument()
    expect(screen.getByText('45')).toBeInTheDocument()
  })

  it('shows OCR stats from exam report', async () => {
    renderPage()
    await screen.findByText('Per-Exam Report')
    fireEvent.change(screen.getByPlaceholderText(/exam id/i), { target: { value: 'exam-1' } })
    fireEvent.click(screen.getByRole('button', { name: /^load$/i }))
    await screen.findByText('Math Exam 2026')
    // ocrAverageConfidence = 0.875 → 87.5%
    expect(screen.getByText('87.5%')).toBeInTheDocument()
    // lowConfidenceCount = 6
    expect(screen.getByText('6')).toBeInTheDocument()
  })

  it('shows avg score from exam report', async () => {
    renderPage()
    await screen.findByText('Per-Exam Report')
    fireEvent.change(screen.getByPlaceholderText(/exam id/i), { target: { value: 'exam-1' } })
    fireEvent.click(screen.getByRole('button', { name: /^load$/i }))
    await screen.findByText('Math Exam 2026')
    // averageScorePercentage = 68.5%
    expect(screen.getByText('68.5%')).toBeInTheDocument()
  })

  it('shows per-exam score distribution heading when totalScored > 0', async () => {
    renderPage()
    await screen.findByText('Per-Exam Report')
    fireEvent.change(screen.getByPlaceholderText(/exam id/i), { target: { value: 'exam-1' } })
    fireEvent.click(screen.getByRole('button', { name: /^load$/i }))
    await screen.findByText('Math Exam 2026')
    // Both the global chart heading and the per-exam distribution section render "Score Distribution"
    expect((await screen.findAllByText('Score Distribution')).length).toBeGreaterThanOrEqual(2)
  })

  it('does not show per-exam score distribution when totalScored is 0', async () => {
    vi.mocked(apiClient.api.getExamReport).mockResolvedValue({ ...EXAM_REPORT, totalScored: 0 })
    renderPage()
    await screen.findByText('Per-Exam Report')
    fireEvent.change(screen.getByPlaceholderText(/exam id/i), { target: { value: 'exam-1' } })
    fireEvent.click(screen.getByRole('button', { name: /^load$/i }))
    await screen.findByText('Math Exam 2026')
    // Only the main chart heading renders; per-exam distribution section is hidden
    await waitFor(() =>
      expect(screen.getAllByText('Score Distribution').length).toBe(1)
    )
  })

  it('Export CSV button in exam report calls exportExamReportCsv', async () => {
    mockCreateObjectUrl()
    renderPage()
    await screen.findByText('Per-Exam Report')
    fireEvent.change(screen.getByPlaceholderText(/exam id/i), { target: { value: 'exam-1' } })
    fireEvent.click(screen.getByRole('button', { name: /^load$/i }))
    await screen.findByText('Math Exam 2026')
    fireEvent.click(screen.getByRole('button', { name: /export csv/i }))
    await waitFor(() =>
      expect(apiClient.api.exportExamReportCsv).toHaveBeenCalledWith('exam-1')
    )
  })
})
