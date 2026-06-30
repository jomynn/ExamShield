import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import SetupPage from '../pages/SetupPage'
import * as apiClient from '../api/client'

// ── Mocks ──────────────────────────────────────────────────────────────────

const mockNavigate = vi.fn()
vi.mock('react-router-dom', async (importOriginal) => {
  const real = await importOriginal<typeof import('react-router-dom')>()
  return { ...real, useNavigate: () => mockNavigate }
})

vi.mock('../hooks/useSetupStatus', () => ({
  useSetupStatus: vi.fn(),
}))

vi.mock('../api/client', () => ({
  api: {
    completeSetup: vi.fn(),
  },
}))

import { useSetupStatus } from '../hooks/useSetupStatus'

// ── Helpers ────────────────────────────────────────────────────────────────

const HEALTHY_CHECKS = { api: 'Healthy', postgres: 'Healthy', redis: 'Healthy', rabbitmq: 'Healthy', minio: 'Healthy' }
const UNHEALTHY_CHECKS = { api: 'Healthy', postgres: 'Unhealthy', redis: 'Healthy', rabbitmq: 'Healthy', minio: 'Healthy' }

function stubSetupStatus(override?: Partial<ReturnType<typeof useSetupStatus>>) {
  vi.mocked(useSetupStatus).mockReturnValue({
    status: { isFirstRun: true, version: '1.0.0', checks: HEALTHY_CHECKS },
    loading: false,
    error: null,
    refresh: vi.fn(),
    ...override,
  })
}

function renderPage() {
  return render(<MemoryRouter><SetupPage /></MemoryRouter>)
}

function fillAdminForm({
  email = 'admin@example.com',
  name  = 'System Admin',
  pw    = 'Admin@123!',
} = {}) {
  fireEvent.change(screen.getByPlaceholderText(/admin@yourorganization/i), { target: { value: email } })
  fireEvent.change(screen.getByPlaceholderText(/system administrator/i), { target: { value: name } })
  fireEvent.change(screen.getByPlaceholderText(/min. 8 chars/i), { target: { value: pw } })
  fireEvent.change(screen.getByPlaceholderText(/re-enter password/i), { target: { value: pw } })
}

beforeEach(() => {
  vi.clearAllMocks()
  stubSetupStatus()
  vi.mocked(apiClient.api.completeSetup).mockResolvedValue(undefined)
})

// ── Step 1: System Check ───────────────────────────────────────────────────

describe('SetupPage — Step 1: System Check', () => {
  it('renders ExamShield First-Time Setup heading', () => {
    renderPage()
    expect(screen.getByText(/first-time setup/i)).toBeInTheDocument()
  })

  it('shows System Requirements heading', () => {
    renderPage()
    expect(screen.getByText(/system requirements/i)).toBeInTheDocument()
  })

  it('shows service health badges', () => {
    renderPage()
    expect(screen.getByText(/postgresql/i)).toBeInTheDocument()
    expect(screen.getByText(/redis/i)).toBeInTheDocument()
  })

  it('Next button is enabled when api and postgres are Healthy', () => {
    renderPage()
    const nextBtn = screen.getByRole('button', { name: /next/i })
    expect(nextBtn).not.toBeDisabled()
  })

  it('Next button is disabled when postgres is Unhealthy', () => {
    stubSetupStatus({
      status: { isFirstRun: true, version: '1.0.0', checks: UNHEALTHY_CHECKS },
    })
    renderPage()
    const nextBtn = screen.getByRole('button', { name: /next/i })
    expect(nextBtn).toBeDisabled()
  })

  it('shows loading spinner while loading', () => {
    stubSetupStatus({ loading: true, status: null })
    renderPage()
    expect(document.querySelector('.animate-spin')).toBeInTheDocument()
  })

  it('redirects away when setup is already complete', () => {
    stubSetupStatus({ status: { isFirstRun: false, version: '1.0.0', checks: HEALTHY_CHECKS } })
    renderPage()
    expect(mockNavigate).toHaveBeenCalledWith('/', { replace: true })
  })

  it('shows API error message when health check fails', () => {
    stubSetupStatus({ loading: false, status: null, error: 'Cannot reach API' })
    renderPage()
    expect(screen.getByText(/cannot reach the api/i)).toBeInTheDocument()
  })
})

// ── Step 2: Admin Account ─────────────────────────────────────────────────

describe('SetupPage — Step 2: Admin Account', () => {
  function goToStep2() {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: /next/i }))
  }

  it('shows Create Super Administrator heading after Next click', () => {
    goToStep2()
    expect(screen.getByText(/create super administrator/i)).toBeInTheDocument()
  })

  it('shows email, display name, password, confirm password fields', () => {
    goToStep2()
    expect(screen.getByPlaceholderText(/admin@yourorganization/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/system administrator/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/min. 8 chars/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/re-enter password/i)).toBeInTheDocument()
  })

  it('Next button is disabled when form is empty', () => {
    goToStep2()
    expect(screen.getByRole('button', { name: /next →/i })).toBeDisabled()
  })

  it('shows validation error for invalid email', () => {
    goToStep2()
    fireEvent.change(screen.getByPlaceholderText(/admin@yourorganization/i), { target: { value: 'bad-email' } })
    fireEvent.change(screen.getByPlaceholderText(/min. 8 chars/i), { target: { value: 'Admin@1!' } })
    expect(screen.getByText(/valid email address required/i)).toBeInTheDocument()
  })

  it('shows mismatching password error', () => {
    goToStep2()
    fireEvent.change(screen.getByPlaceholderText(/min. 8 chars/i), { target: { value: 'Admin@1!' } })
    fireEvent.change(screen.getByPlaceholderText(/re-enter password/i), { target: { value: 'Different@1!' } })
    expect(screen.getByText(/passwords do not match/i)).toBeInTheDocument()
  })

  it('shows password strength indicator when password is typed', () => {
    goToStep2()
    fireEvent.change(screen.getByPlaceholderText(/min. 8 chars/i), { target: { value: 'Admin@123!' } })
    // The strength bars are rendered as coloured divs; confirm at least one strength-colour bar appears
    const bars = document.querySelectorAll('.bg-green-400, .bg-blue-400, .bg-yellow-400')
    expect(bars.length).toBeGreaterThan(0)
  })

  it('Back button returns to System Check', () => {
    goToStep2()
    fireEvent.click(screen.getByRole('button', { name: /← back/i }))
    expect(screen.getByText(/system requirements/i)).toBeInTheDocument()
  })

  it('Next is enabled with valid form and navigates to Options step', () => {
    goToStep2()
    fillAdminForm()
    fireEvent.click(screen.getByRole('button', { name: /next →/i }))
    expect(screen.getByText(/setup options/i)).toBeInTheDocument()
  })
})

// ── Step 3: Options ────────────────────────────────────────────────────────

describe('SetupPage — Step 3: Options', () => {
  function goToStep3() {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: /next/i }))
    fillAdminForm()
    fireEvent.click(screen.getByRole('button', { name: /next →/i }))
  }

  it('shows Setup Options heading', () => {
    goToStep3()
    expect(screen.getByText(/setup options/i)).toBeInTheDocument()
  })

  it('shows Load Demo Data checkbox unchecked by default', () => {
    goToStep3()
    expect(screen.getByText(/load demo data/i)).toBeInTheDocument()
    expect(screen.getByText(/clean installation/i)).toBeInTheDocument()
  })

  it('toggling Load Demo Data shows demo tag chips', () => {
    goToStep3()
    fireEvent.click(screen.getByText(/load demo data/i))
    expect(screen.getByText(/26 demo users/i)).toBeInTheDocument()
  })

  it('Complete Setup button calls api.completeSetup', async () => {
    goToStep3()
    fireEvent.click(screen.getByRole('button', { name: /complete setup/i }))
    await waitFor(() =>
      expect(apiClient.api.completeSetup).toHaveBeenCalledWith({
        adminEmail:       'admin@example.com',
        adminDisplayName: 'System Admin',
        adminPassword:    'Admin@123!',
        seedDemoData:     false,
      })
    )
  })

  it('calls completeSetup with seedDemoData=true when toggled', async () => {
    goToStep3()
    fireEvent.click(screen.getByText(/load demo data/i))
    fireEvent.click(screen.getByRole('button', { name: /complete setup/i }))
    await waitFor(() =>
      expect(apiClient.api.completeSetup).toHaveBeenCalledWith(
        expect.objectContaining({ seedDemoData: true })
      )
    )
  })

  it('Back button returns to admin account step', () => {
    goToStep3()
    fireEvent.click(screen.getByRole('button', { name: /← back/i }))
    expect(screen.getByText(/create super administrator/i)).toBeInTheDocument()
  })
})

// ── Step 4: Complete ───────────────────────────────────────────────────────

describe('SetupPage — Step 4: Complete', () => {
  async function goToComplete() {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: /next/i }))
    fillAdminForm()
    fireEvent.click(screen.getByRole('button', { name: /next →/i }))
    fireEvent.click(screen.getByRole('button', { name: /complete setup/i }))
    await screen.findByText(/examshield is ready/i)
  }

  it('shows ExamShield is Ready after successful setup', async () => {
    await goToComplete()
    expect(screen.getByText(/examshield is ready/i)).toBeInTheDocument()
  })

  it('shows the admin email in the credentials section', async () => {
    await goToComplete()
    expect(screen.getByText('admin@example.com')).toBeInTheDocument()
  })

  it('shows Go to Login button', async () => {
    await goToComplete()
    expect(screen.getByRole('button', { name: /go to login/i })).toBeInTheDocument()
  })

  it('clicking Go to Login navigates to /login', async () => {
    await goToComplete()
    fireEvent.click(screen.getByRole('button', { name: /go to login/i }))
    expect(mockNavigate).toHaveBeenCalledWith('/login')
  })
})

// ── Error handling ─────────────────────────────────────────────────────────

describe('SetupPage — error handling', () => {
  function goToStep3() {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: /next/i }))
    fillAdminForm()
    fireEvent.click(screen.getByRole('button', { name: /next →/i }))
  }

  it('shows error message when completeSetup fails', async () => {
    vi.mocked(apiClient.api.completeSetup).mockRejectedValue(new Error('Network error'))
    goToStep3()
    fireEvent.click(screen.getByRole('button', { name: /complete setup/i }))
    await screen.findByText(/network error/i)
  })

  it('shows friendly message on 409 conflict', async () => {
    vi.mocked(apiClient.api.completeSetup).mockRejectedValue(new Error('409 Conflict'))
    goToStep3()
    fireEvent.click(screen.getByRole('button', { name: /complete setup/i }))
    await screen.findByText(/already been completed/i)
  })
})
