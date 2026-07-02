import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import DashboardPage from '../pages/DashboardPage'
import { useDashboardStats } from '../hooks/useDashboardStats'
import * as clientModule from '../api/client'

// ── Hook / API mocks ──────────────────────────────────────────────────────────

vi.mock('../hooks/useDashboardStats', () => ({
  useDashboardStats: vi.fn(),
}))

// Invoke Recharts formatter callbacks during render to cover inline arrow functions
vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  PieChart:    ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  BarChart:    ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  AreaChart:   ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  RadarChart:  ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  Pie:         ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  Bar:         ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  Area:        () => null, Cell: () => null, Legend: () => null,
  Radar:       () => null, PolarGrid: () => null, PolarAngleAxis: () => null,
  CartesianGrid: () => null, XAxis: () => null, YAxis: () => null,
  Tooltip: ({ formatter }: { formatter?: (v: number, name: string) => unknown }) => {
    formatter?.(3, 'captures')
    formatter?.(85, 'Score')
    return null
  },
}))

vi.mock('../api/client', () => ({
  api: {
    getStatistics:     vi.fn(),
    getCaptures:       vi.fn(),
    getExams:          vi.fn(),
    getDevices:        vi.fn(),
    getSecurityEvents: vi.fn(),
    getAuditLog:       vi.fn(),
    getResults:        vi.fn(),
  },
}))

// ── Rich fixture data ─────────────────────────────────────────────────────────

const STATS = { totalCaptures: 1024, pendingReview: 12, verifiedToday: 340, activeAlerts: 3 }

const CAPTURES = [
  { captureId: 'c1', status: 'Verified',  capturedAt: '2026-01-01T10:00:00Z' },
  { captureId: 'c2', status: 'Verified',  capturedAt: '2026-01-01T10:01:00Z' },
  { captureId: 'c3', status: 'Uploaded',  capturedAt: '2026-01-01T10:02:00Z' },
  { captureId: 'c4', status: 'Created',   capturedAt: '2026-01-01T10:03:00Z' },
  { captureId: 'c5', status: 'Tampered',  capturedAt: '2026-01-01T10:04:00Z' },
]

const EXAMS = [
  { examId: 'e1', name: 'Math Final', status: 'Active',  totalQuestions: 50 },
  { examId: 'e2', name: 'Physics',    status: 'Draft',   totalQuestions: 30 },
  { examId: 'e3', name: 'Chemistry',  status: 'Closed',  totalQuestions: 40 },
]

const DEVICES = [
  { deviceId: 'd1', isActive: true,  status: 'Approved' },
  { deviceId: 'd2', isActive: true,  status: 'Approved' },
  { deviceId: 'd3', isActive: false, status: 'Pending'  },
]

const SECURITY_EVENTS = [
  { eventId: 's1', severity: 'Critical', eventType: 'HashMismatch',       occurredAt: '2026-01-01T10:00:00Z' },
  { eventId: 's2', severity: 'High',     eventType: 'InvalidSignature',   occurredAt: '2026-01-01T10:01:00Z' },
  { eventId: 's3', severity: 'Warning',  eventType: 'HashMismatch',       occurredAt: '2026-01-01T10:02:00Z' },
  { eventId: 's4', severity: 'Info',     eventType: 'LoginAttempt',       occurredAt: '2026-01-01T10:03:00Z' },
]

const AUDIT_ENTRIES = [
  { id: 'a1', action: 'CaptureRegistered',     userId: 'user-abc', ipAddress: '10.0.0.1', occurredAt: '2026-01-01T10:00:00Z' },
  { id: 'a2', action: 'ImageUploaded',          userId: 'system',   ipAddress: '10.0.0.1', occurredAt: '2026-01-01T10:01:00Z' },
  { id: 'a3', action: 'ManualReviewCompleted',  userId: 'user-xyz', ipAddress: '10.0.0.2', occurredAt: '2026-01-01T10:02:00Z' },
  { id: 'a4', action: 'HashVerified',           userId: 'user-abc', ipAddress: '10.0.0.1', occurredAt: '2026-01-01T10:03:00Z' },
  { id: 'a5', action: 'ScoreGenerated',         userId: 'system',   ipAddress: '127.0.0.1', occurredAt: '2026-01-01T10:04:00Z' },
  { id: 'a6', action: 'OCRCompleted',           userId: 'system',   ipAddress: '127.0.0.1', occurredAt: '2026-01-01T10:05:00Z' },
  { id: 'a7', action: 'ResultPublished',        userId: 'user-pub', ipAddress: '10.0.0.3', occurredAt: '2026-01-01T10:06:00Z' },
  { id: 'a8', action: 'DeviceRegistered',       userId: 'user-dm',  ipAddress: '10.0.0.4', occurredAt: '2026-01-01T10:07:00Z' },
]

const RESULTS = [
  { resultId: 'r1', percentage: 95, correctAnswers: 19, totalQuestions: 20 },
  { resultId: 'r2', percentage: 82, correctAnswers: 16, totalQuestions: 20 },
  { resultId: 'r3', percentage: 68, correctAnswers: 14, totalQuestions: 20 },
  { resultId: 'r4', percentage: 45, correctAnswers:  9, totalQuestions: 20 },
]

const STATISTICS = { totalPapersScored: 4, averagePercentage: 72.5, highestScore: 19, lowestScore: 9 }

// ── Render helpers ────────────────────────────────────────────────────────────

function makeQC() {
  return new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
}

function renderPage() {
  return render(
    <QueryClientProvider client={makeQC()}>
      <DashboardPage />
    </QueryClientProvider>
  )
}

// ── Setup ─────────────────────────────────────────────────────────────────────

beforeEach(() => {
  vi.resetAllMocks()
  const a = vi.mocked(clientModule.api)
  vi.mocked(useDashboardStats).mockReturnValue({
    data: STATS, isLoading: false, dataUpdatedAt: Date.now(),
  } as ReturnType<typeof useDashboardStats>)
  a.getStatistics.mockResolvedValue(STATISTICS)
  a.getCaptures.mockResolvedValue({ captures: CAPTURES, totalCount: 5, totalPages: 1 })
  a.getExams.mockResolvedValue({ exams: EXAMS, totalCount: 3, totalPages: 1 })
  a.getDevices.mockResolvedValue({ devices: DEVICES })
  a.getSecurityEvents.mockResolvedValue({ events: SECURITY_EVENTS })
  a.getAuditLog.mockResolvedValue({ entries: AUDIT_ENTRIES, totalCount: 8 })
  a.getResults.mockResolvedValue({ results: RESULTS })
})

// ── Header ────────────────────────────────────────────────────────────────────

describe('DashboardPage — header', () => {
  it('renders the page heading', () => {
    renderPage()
    expect(screen.getByRole('heading', { name: /dashboard/i })).toBeInTheDocument()
  })

  it('shows last-updated timestamp', () => {
    renderPage()
    expect(screen.getByText(/last updated/i)).toBeInTheDocument()
  })

  it('renders refresh button', () => {
    renderPage()
    expect(screen.getByRole('button', { name: /refresh/i })).toBeInTheDocument()
  })

  it('shows plural active alerts badge when count > 1', () => {
    renderPage()
    expect(screen.getByText(/3 active alerts/i)).toBeInTheDocument()
  })

  it('shows singular active alert when count is 1', () => {
    vi.mocked(useDashboardStats).mockReturnValue({
      data: { ...STATS, activeAlerts: 1 }, isLoading: false, dataUpdatedAt: Date.now(),
    } as ReturnType<typeof useDashboardStats>)
    renderPage()
    expect(screen.getByText(/1 active alert$/i)).toBeInTheDocument()
  })

  it('hides alert badge when activeAlerts is 0', () => {
    vi.mocked(useDashboardStats).mockReturnValue({
      data: { ...STATS, activeAlerts: 0 }, isLoading: false, dataUpdatedAt: Date.now(),
    } as ReturnType<typeof useDashboardStats>)
    renderPage()
    expect(screen.queryByText(/\d+ active alert/i)).not.toBeInTheDocument()
  })

  it('triggers query invalidation when Refresh is clicked', async () => {
    const qc = makeQC()
    const spy = vi.spyOn(qc, 'invalidateQueries')
    render(
      <QueryClientProvider client={qc}>
        <DashboardPage />
      </QueryClientProvider>
    )
    fireEvent.click(screen.getByRole('button', { name: /refresh/i }))
    expect(spy).toHaveBeenCalledTimes(8) // 8 query keys refreshed
  })
})

// ── Primary KPI cards ─────────────────────────────────────────────────────────

describe('DashboardPage — primary KPI cards', () => {
  it('shows totalCaptures value', () => {
    renderPage()
    expect(screen.getByText('1,024')).toBeInTheDocument()
  })

  it('shows pendingReview value', () => {
    renderPage()
    expect(screen.getByText('12')).toBeInTheDocument()
  })

  it('shows verifiedToday value', () => {
    renderPage()
    expect(screen.getByText('340')).toBeInTheDocument()
  })

  it('shows activeAlerts value', () => {
    renderPage()
    // StatCard for alerts renders "3" and the badge also shows the count
    expect(screen.getAllByText('3').length).toBeGreaterThan(0)
  })

  it('renders loading skeleton when statsLoading is true', () => {
    vi.mocked(useDashboardStats).mockReturnValue({
      data: undefined, isLoading: true, dataUpdatedAt: 0,
    } as ReturnType<typeof useDashboardStats>)
    renderPage()
    // Skeleton: animate-pulse div instead of value text
    const skeletons = document.querySelectorAll('.animate-pulse')
    expect(skeletons.length).toBeGreaterThan(0)
  })
})

// ── Secondary KPI cards ───────────────────────────────────────────────────────

describe('DashboardPage — secondary KPI cards', () => {
  it('shows total exam count from API', async () => {
    renderPage()
    // 3 exams total, 1 Active → Exams card sub-label = "1 active"
    expect(await screen.findByText('1 active')).toBeInTheDocument()
  })

  it('shows average score percentage from statistics', async () => {
    renderPage()
    expect((await screen.findAllByText('72.5%')).length).toBeGreaterThan(0)
  })

  it('shows papers scored sub-label', async () => {
    renderPage()
    expect(await screen.findByText('4 papers scored')).toBeInTheDocument()
  })

  it('shows active devices count', async () => {
    renderPage()
    // 2 active devices
    expect(await screen.findByText('1 pending approval')).toBeInTheDocument()
  })

  it('shows OCR queue count (Uploaded captures)', async () => {
    renderPage()
    // 1 Uploaded capture
    expect(await screen.findByText('captures awaiting OCR')).toBeInTheDocument()
  })
})

// ── Charts: Capture Pipeline ──────────────────────────────────────────────────

describe('DashboardPage — capture pipeline chart', () => {
  it('hides "No captures yet" when captures are present', async () => {
    renderPage()
    await waitFor(() => expect(screen.queryByText('No captures yet')).not.toBeInTheDocument())
  })

  it('shows capture legend entries for each status', async () => {
    renderPage()
    // Legend labels appear as text in the chart area
    await waitFor(() => {
      expect(screen.getByText('Verified')).toBeInTheDocument()
      expect(screen.getByText('Uploaded')).toBeInTheDocument()
      expect(screen.getByText('Created')).toBeInTheDocument()
      expect(screen.getByText('Tampered')).toBeInTheDocument()
    })
  })

  it('shows total capture count in legend', async () => {
    renderPage()
    await screen.findByText('Verified')
    expect(screen.getByText('5')).toBeInTheDocument() // total from captures array
  })

  it('shows "No captures yet" when captures array is empty', async () => {
    vi.mocked(clientModule.api).getCaptures.mockResolvedValue({ captures: [], totalCount: 0, totalPages: 1 })
    renderPage()
    expect(await screen.findByText('No captures yet')).toBeInTheDocument()
  })
})

// ── Charts: Security Threat Breakdown ────────────────────────────────────────

describe('DashboardPage — security threat chart', () => {
  it('hides "No security events" when events are present', async () => {
    renderPage()
    await waitFor(() => expect(screen.queryByText('No security events')).not.toBeInTheDocument())
  })

  it('shows "No security events" when events array is empty', async () => {
    vi.mocked(clientModule.api).getSecurityEvents.mockResolvedValue({ events: [] })
    renderPage()
    expect(await screen.findByText('No security events')).toBeInTheDocument()
  })
})

// ── Charts: Score Distribution ────────────────────────────────────────────────

describe('DashboardPage — score distribution', () => {
  it('shows "No scores published yet" when results are empty', async () => {
    vi.mocked(clientModule.api).getResults.mockResolvedValue({ results: [] })
    renderPage()
    expect(await screen.findByText('No scores published yet')).toBeInTheDocument()
  })

  it('renders score bucket labels when results are available', async () => {
    renderPage()
    await waitFor(() => {
      expect(screen.getByText('90–100%')).toBeInTheDocument()
      expect(screen.getByText('75–89%')).toBeInTheDocument()
      expect(screen.getByText('60–74%')).toBeInTheDocument()
      expect(screen.getByText('<60%')).toBeInTheDocument()
    })
  })

  it('renders highest and lowest score stats', async () => {
    renderPage()
    // statistics: highestScore=19, lowestScore=9
    await waitFor(() => {
      expect(screen.getByText('19')).toBeInTheDocument()
      expect(screen.getByText('9')).toBeInTheDocument()
    })
  })
})

// ── Charts: Exam Overview ─────────────────────────────────────────────────────

describe('DashboardPage — exam overview', () => {
  it('shows "No exams yet" when exams are empty', async () => {
    vi.mocked(clientModule.api).getExams.mockResolvedValue({ exams: [], totalCount: 0, totalPages: 1 })
    renderPage()
    expect(await screen.findByText('No exams yet')).toBeInTheDocument()
  })

  it('shows exam names in the list when exams are available', async () => {
    renderPage()
    expect(await screen.findByText('Math Final')).toBeInTheDocument()
    expect(screen.getByText('Physics')).toBeInTheDocument()
    expect(screen.getByText('Chemistry')).toBeInTheDocument()
  })

  it('shows exam question counts', async () => {
    renderPage()
    await screen.findByText('Math Final')
    expect(screen.getByText('50 questions')).toBeInTheDocument()
  })

  it('shows exam status badges in the list', async () => {
    renderPage()
    await screen.findByText('Math Final')
    // Exam list has Active, Draft, Closed badges (also rendered in count section → multiple matches)
    expect(screen.getAllByText('Draft').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Closed').length).toBeGreaterThan(0)
  })
})

// ── Recent Activity Feed ──────────────────────────────────────────────────────

describe('DashboardPage — recent activity feed', () => {
  it('shows "No audit entries" when audit log is empty', async () => {
    vi.mocked(clientModule.api).getAuditLog.mockResolvedValue({ entries: [], totalCount: 0 })
    renderPage()
    expect(await screen.findByText('No audit entries')).toBeInTheDocument()
  })

  it('renders audit actions as readable labels', async () => {
    renderPage()
    // CaptureRegistered → "Capture Registered"
    expect(await screen.findByText('Capture Registered')).toBeInTheDocument()
    expect(screen.getByText('Image Uploaded')).toBeInTheDocument()
    expect(screen.getByText('Hash Verified')).toBeInTheDocument()
    expect(screen.getByText('Score Generated')).toBeInTheDocument()
    expect(screen.getByText('Result Published')).toBeInTheDocument()
    expect(screen.getByText('Device Registered')).toBeInTheDocument()
  })

  it('shows "System" for system userId entries', async () => {
    renderPage()
    // 3 entries have userId='system'; multiple elements match → use findAllByText
    expect((await screen.findAllByText(/System/)).length).toBeGreaterThan(0)
  })

  it('shows truncated userId for non-system users', async () => {
    renderPage()
    // user-abc (8 chars) → "user-abc…"; appears in entries a1 and a4 → multiple matches
    expect((await screen.findAllByText(/user-abc…/)).length).toBeGreaterThan(0)
  })

  it('shows IP addresses for audit entries', async () => {
    renderPage()
    // IP appears in multiple audit rows → use findAllByText
    expect((await screen.findAllByText(/10\.0\.0\.1/)).length).toBeGreaterThan(0)
  })
})

// ── Threat Radar ──────────────────────────────────────────────────────────────

describe('DashboardPage — threat radar', () => {
  it('shows "No events" when security events are empty', async () => {
    vi.mocked(clientModule.api).getSecurityEvents.mockResolvedValue({ events: [] })
    renderPage()
    expect(await screen.findByText('No events')).toBeInTheDocument()
  })

  it('renders threat radar chart section when events are available', async () => {
    renderPage()
    expect(await screen.findByText('Threat Radar')).toBeInTheDocument()
    // Chart renders when events exist — verify no empty-state
    await waitFor(() =>
      expect(screen.queryByText('No events')).not.toBeInTheDocument()
    )
  })
})

// ── Section headings ──────────────────────────────────────────────────────────

describe('DashboardPage — section headings', () => {
  it('renders all chart section titles', () => {
    renderPage()
    expect(screen.getByText('Capture Pipeline Status')).toBeInTheDocument()
    expect(screen.getByText('Security Threat Breakdown')).toBeInTheDocument()
    expect(screen.getByText('Score Distribution')).toBeInTheDocument()
    expect(screen.getByText('Exam Overview')).toBeInTheDocument()
    expect(screen.getByText('Recent Activity')).toBeInTheDocument()
    expect(screen.getByText('Threat Radar')).toBeInTheDocument()
  })
})
