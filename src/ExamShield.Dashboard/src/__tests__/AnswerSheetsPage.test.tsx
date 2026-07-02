import { render, screen, fireEvent, waitFor, within } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import AnswerSheetsPage from '../pages/AnswerSheetsPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getCaptures:    vi.fn(),
    exportCaptures: vi.fn(),
  },
}))

vi.mock('../hooks/useAuth', () => ({
  useAuth: vi.fn(() => ({ auth: { role: 'Operator' } })),
}))

const mockFlagMutate = vi.fn()
vi.mock('../hooks/useCaptures', () => ({
  useChainOfCustody:        vi.fn(() => ({ data: null, isLoading: false })),
  useFlagCaptureAsTampered: vi.fn(() => ({ mutate: mockFlagMutate, isPending: false })),
}))

import { useAuth } from '../hooks/useAuth'
import { useChainOfCustody } from '../hooks/useCaptures'

const CAPTURES = [
  { captureId: 'cap-1', examId: 'exam-1', studentId: 'stu-1', deviceId: 'dev-1', status: 'Verified', capturedAt: '2024-01-01T10:00:00Z', storageKey: 'key-1' },
  { captureId: 'cap-2', examId: 'exam-2', studentId: 'stu-2', deviceId: 'dev-2', status: 'Uploaded', capturedAt: '2024-01-02T10:00:00Z', storageKey: 'key-2' },
  { captureId: 'cap-3', examId: 'exam-3', studentId: 'stu-3', deviceId: 'dev-3', status: 'Created',  capturedAt: '2024-01-03T10:00:00Z', storageKey: null  },
]

const CHAIN_DATA = {
  status: 'Verified',
  pageNumber: 1,
  hashHex: 'deadbeef01234567',
  ocrResult: { overallConfidence: 0.95 },
  score: { correctAnswers: 8, totalQuestions: 10, percentage: 80 },
  auditTrail: [
    { action: 'CaptureRegistered', occurredAt: '2024-01-01T10:01:00Z', reason: null },
    { action: 'ImageUploaded',     occurredAt: '2024-01-01T10:02:00Z', reason: 'upload ok' },
  ],
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <AnswerSheetsPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.resetAllMocks()
  mockFlagMutate.mockReset()
  vi.mocked(apiClient.api.getCaptures).mockResolvedValue({ captures: CAPTURES, totalCount: 3, totalPages: 1 })
  vi.mocked(apiClient.api.exportCaptures).mockResolvedValue(new Blob(['csv'], { type: 'text/csv' }))
  vi.mocked(useAuth).mockReturnValue({ auth: { role: 'Operator' } } as ReturnType<typeof useAuth>)
  vi.mocked(useChainOfCustody).mockReturnValue({ data: null, isLoading: false } as ReturnType<typeof useChainOfCustody>)
  mockFlagMutate.mockImplementation((_vars: unknown, callbacks?: { onSuccess?: () => void }) => callbacks?.onSuccess?.())
  Object.defineProperty(URL, 'createObjectURL', { value: vi.fn(() => 'blob:x'), configurable: true })
  Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true })
})

// ── Basic rendering ───────────────────────────────────────────────────────────

describe('AnswerSheetsPage — basic rendering', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /answer sheets/i })).toBeInTheDocument()
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getCaptures).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByRole('status', { name: /loading/i })).toBeInTheDocument()
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

  it('shows total count from API', async () => {
    renderPage()
    expect(await screen.findByText(/3 total captures/i)).toBeInTheDocument()
  })

  it('shows empty message when no captures match filters', async () => {
    vi.mocked(apiClient.api.getCaptures).mockResolvedValue({ captures: [], totalCount: 0, totalPages: 1 })
    renderPage()
    expect(await screen.findByText(/no answer sheets match/i)).toBeInTheDocument()
  })

  it('shows no image controls for captures without storage key', async () => {
    renderPage()
    await screen.findByText(/stu-3/)
    const rows = screen.getAllByRole('row')
    const created = rows.find(r => r.textContent?.includes('stu-3'))
    expect(within(created!).queryByRole('button', { name: /view image/i })).toBeNull()
    expect(within(created!).queryByText(/restricted/i)).toBeNull()
  })
})

// ── Image access control ──────────────────────────────────────────────────────

describe('AnswerSheetsPage — image access control', () => {
  it('shows View Image for Operator (allowed)', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    expect(screen.getAllByRole('button', { name: /view image/i }).length).toBeGreaterThan(0)
    expect(screen.queryByText(/restricted/i)).toBeNull()
  })

  it('shows View Image for ManualReviewer (allowed)', async () => {
    vi.mocked(useAuth).mockReturnValue({ auth: { role: 'ManualReviewer' } } as ReturnType<typeof useAuth>)
    renderPage()
    await screen.findByText(/stu-1/)
    expect(screen.getAllByRole('button', { name: /view image/i }).length).toBeGreaterThan(0)
  })

  it('shows View Image for InvestigationOfficer (allowed)', async () => {
    vi.mocked(useAuth).mockReturnValue({ auth: { role: 'InvestigationOfficer' } } as ReturnType<typeof useAuth>)
    renderPage()
    await screen.findByText(/stu-1/)
    expect(screen.getAllByRole('button', { name: /view image/i }).length).toBeGreaterThan(0)
  })

  it('shows Restricted for Administrator (blocked)', async () => {
    vi.mocked(useAuth).mockReturnValue({ auth: { role: 'Administrator' } } as ReturnType<typeof useAuth>)
    renderPage()
    await screen.findByText(/stu-1/)
    expect(screen.queryByRole('button', { name: /view image/i })).toBeNull()
    expect(screen.getAllByText(/restricted/i).length).toBeGreaterThan(0)
  })

  it('shows Restricted for Auditor (blocked)', async () => {
    vi.mocked(useAuth).mockReturnValue({ auth: { role: 'Auditor' } } as ReturnType<typeof useAuth>)
    renderPage()
    await screen.findByText(/stu-1/)
    expect(screen.queryByRole('button', { name: /view image/i })).toBeNull()
    expect(screen.getAllByText(/restricted/i).length).toBeGreaterThan(0)
  })

  it('shows Restricted for SuperAdministrator (blocked)', async () => {
    vi.mocked(useAuth).mockReturnValue({ auth: { role: 'SuperAdministrator' } } as ReturnType<typeof useAuth>)
    renderPage()
    await screen.findByText(/stu-1/)
    expect(screen.queryByRole('button', { name: /view image/i })).toBeNull()
    expect(screen.getAllByText(/restricted/i).length).toBeGreaterThan(0)
  })
})

// ── Image viewer ──────────────────────────────────────────────────────────────

describe('AnswerSheetsPage — image viewer', () => {
  it('opens image viewer when View Image is clicked', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /view image/i })[0])
    await waitFor(() => expect(screen.getByAltText(/answer sheet/i)).toBeInTheDocument())
  })

  it('closes image viewer when Close is clicked', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /view image/i })[0])
    await waitFor(() => screen.getByAltText(/answer sheet/i))
    fireEvent.click(screen.getByRole('button', { name: /^close$/i }))
    expect(screen.queryByAltText(/answer sheet/i)).not.toBeInTheDocument()
  })

  it('toggles image viewer off when View Image is clicked again for same capture', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    const viewBtns = screen.getAllByRole('button', { name: /view image/i })
    fireEvent.click(viewBtns[0])
    await waitFor(() => screen.getByAltText(/answer sheet/i))
    fireEvent.click(screen.getAllByRole('button', { name: /view image/i })[0])
    await waitFor(() => expect(screen.queryByAltText(/answer sheet/i)).not.toBeInTheDocument())
  })
})

// ── Filters ───────────────────────────────────────────────────────────────────

describe('AnswerSheetsPage — filters', () => {
  it('refetches with deviceId when Device ID input changes', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.change(screen.getByPlaceholderText(/filter by device id/i), {
      target: { value: 'dev-uuid-9' },
    })
    await waitFor(() =>
      expect(apiClient.api.getCaptures).toHaveBeenCalledWith(
        1, 20, undefined, undefined, 'dev-uuid-9', undefined
      )
    )
  })

  it('refetches with studentId when Student ID input changes', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.change(screen.getByPlaceholderText(/filter by student id/i), {
      target: { value: 'stu-uuid-5' },
    })
    await waitFor(() =>
      expect(apiClient.api.getCaptures).toHaveBeenCalledWith(
        1, 20, undefined, undefined, undefined, 'stu-uuid-5'
      )
    )
  })

  it('refetches with status filter when select changes', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'Tampered' } })
    await waitFor(() =>
      expect(apiClient.api.getCaptures).toHaveBeenCalledWith(
        1, 20, undefined, 'Tampered', undefined, undefined
      )
    )
  })

  it('shows Clear button when any filter is active', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.change(screen.getByPlaceholderText(/filter by exam id/i), {
      target: { value: 'exam-uuid-x' },
    })
    expect(await screen.findByRole('button', { name: /clear/i })).toBeInTheDocument()
  })

  it('clears all filters when Clear is clicked', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.change(screen.getByPlaceholderText(/filter by student id/i), { target: { value: 'abc' } })
    fireEvent.click(await screen.findByRole('button', { name: /clear/i }))
    await waitFor(() =>
      expect(apiClient.api.getCaptures).toHaveBeenCalledWith(
        1, 20, undefined, undefined, undefined, undefined
      )
    )
  })
})

// ── Export CSV ────────────────────────────────────────────────────────────────

describe('AnswerSheetsPage — export CSV', () => {
  it('calls api.exportCaptures when Export CSV is clicked', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getByRole('button', { name: /export csv/i }))
    await waitFor(() => expect(apiClient.api.exportCaptures).toHaveBeenCalled())
  })
})

// ── Chain of Custody panel ────────────────────────────────────────────────────

describe('AnswerSheetsPage — chain of custody', () => {
  it('shows a Chain button for every capture row', async () => {
    renderPage()
    await screen.findByText(/stu-1/)
    expect(screen.getAllByRole('button', { name: /^chain$/i })).toHaveLength(CAPTURES.length)
  })

  it('opens chain panel when Chain is clicked', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    expect(await screen.findByText(/chain of custody/i)).toBeInTheDocument()
  })

  it('shows loading indicator while chain is fetching', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: null, isLoading: true } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    expect(await screen.findByText(/loading…/i)).toBeInTheDocument()
  })

  it('displays hash when chain data is loaded', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    expect(await screen.findByText('deadbeef01234567')).toBeInTheDocument()
  })

  it('displays OCR confidence percentage', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    expect(await screen.findByText('95.0%')).toBeInTheDocument()
  })

  it('displays score details', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    expect(await screen.findByText('8/10 (80.0%)')).toBeInTheDocument()
  })

  it('displays audit trail events', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    await screen.findByText(/chain of custody/i)
    expect(screen.getByText('CaptureRegistered')).toBeInTheDocument()
    expect(screen.getByText('ImageUploaded')).toBeInTheDocument()
    expect(screen.getByText(/— upload ok/i)).toBeInTheDocument()
    expect(screen.getByText(/2 events/i)).toBeInTheDocument()
  })

  it('closes chain panel on ✕ Close', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    await screen.findByText(/chain of custody/i)
    fireEvent.click(screen.getByRole('button', { name: /✕ close/i }))
    expect(screen.queryByText(/chain of custody/i)).not.toBeInTheDocument()
  })

  it('toggles panel off when same Chain button clicked again', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    const chainBtns = screen.getAllByRole('button', { name: /^chain$/i })
    fireEvent.click(chainBtns[0])
    await screen.findByText(/chain of custody/i)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    await waitFor(() => expect(screen.queryByText(/chain of custody/i)).not.toBeInTheDocument())
  })
})

// ── Flag as Tampered ──────────────────────────────────────────────────────────

describe('AnswerSheetsPage — flag as tampered', () => {
  it('shows flag form when capture is not already Tampered', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    expect(await screen.findByText(/flag as tampered/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/reason for flagging/i)).toBeInTheDocument()
  })

  it('Flag button is disabled when reason is empty', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    await screen.findByText(/flag as tampered/i)
    expect(screen.getByRole('button', { name: /^flag$/i })).toBeDisabled()
  })

  it('calls flagTampered.mutate with captureId and reason when Flag is clicked', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    await screen.findByText(/flag as tampered/i)
    fireEvent.change(screen.getByPlaceholderText(/reason for flagging/i), {
      target: { value: 'Pixel discrepancy detected' },
    })
    fireEvent.click(screen.getByRole('button', { name: /^flag$/i }))
    expect(mockFlagMutate).toHaveBeenCalledWith(
      { captureId: 'cap-1', reason: 'Pixel discrepancy detected' },
      expect.any(Object)
    )
  })

  it('clears reason input after successful flag', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    await screen.findByText(/flag as tampered/i)
    fireEvent.change(screen.getByPlaceholderText(/reason for flagging/i), {
      target: { value: 'Evidence of tampering' },
    })
    fireEvent.click(screen.getByRole('button', { name: /^flag$/i }))
    await waitFor(() =>
      expect(screen.getByPlaceholderText(/reason for flagging/i)).toHaveValue('')
    )
  })

  it('shows error message when flagTampered mutation fails', async () => {
    mockFlagMutate.mockImplementation(
      (_vars: unknown, callbacks?: { onError?: () => void }) => callbacks?.onError?.()
    )
    vi.mocked(useChainOfCustody).mockReturnValue({ data: CHAIN_DATA, isLoading: false } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    await screen.findByText(/flag as tampered/i)
    fireEvent.change(screen.getByPlaceholderText(/reason for flagging/i), {
      target: { value: 'Suspicious pixel change' },
    })
    fireEvent.click(screen.getByRole('button', { name: /^flag$/i }))
    expect(await screen.findByText(/failed — capture may already be tampered/i)).toBeInTheDocument()
  })

  it('hides flag form when capture is already Tampered', async () => {
    vi.mocked(useChainOfCustody).mockReturnValue({
      data: { ...CHAIN_DATA, status: 'Tampered' },
      isLoading: false,
    } as ReturnType<typeof useChainOfCustody>)
    renderPage()
    await screen.findByText(/stu-1/)
    fireEvent.click(screen.getAllByRole('button', { name: /^chain$/i })[0])
    await screen.findByText(/chain of custody/i)
    expect(screen.queryByText(/flag as tampered/i)).not.toBeInTheDocument()
  })
})
