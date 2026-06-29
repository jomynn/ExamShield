import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import ReportsPage from '../pages/ReportsPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getReportSummary: vi.fn(),
    getResults: vi.fn(),
    getAuditLog: vi.fn(),
    getSecurityEvents: vi.fn(),
  },
}))

const mockSummary = {
  generatedAt: '2026-06-27T08:00:00Z',
  captures: { total: 120, created: 5, uploaded: 20, verified: 90, tampered: 5 },
  ocr: { totalProcessed: 85, averageConfidence: 0.924 },
  scores: { totalScored: 80, averagePercentage: 76.5, highestPercentage: 99.0, lowestPercentage: 42.0 },
  security: { totalEvents: 15, criticalEvents: 3 },
}

const mockResults = { results: [
  { scoreId: 's1', captureId: 'c1', examId: 'e1', studentId: 'stu1', correctAnswers: 45, totalQuestions: 50, percentage: 90.0, scoredAt: '2026-06-27T09:00:00Z' },
] }

const mockAudit = { entries: [], totalCount: 0 }

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <ReportsPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getReportSummary).mockResolvedValue(mockSummary)
  vi.mocked(apiClient.api.getResults).mockResolvedValue(mockResults)
  vi.mocked(apiClient.api.getAuditLog).mockResolvedValue(mockAudit)
  vi.mocked(apiClient.api.getSecurityEvents).mockResolvedValue({ events: [] })
})

describe('ReportsPage', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /reports/i })).toBeInTheDocument()
  })

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

  it('shows critical security events', async () => {
    renderPage()
    expect(await screen.findByText('3')).toBeInTheDocument()
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

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getReportSummary).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('shows generated timestamp', async () => {
    renderPage()
    expect(await screen.findByText(/generated/i)).toBeInTheDocument()
  })
})
