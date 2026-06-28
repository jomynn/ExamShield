import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'

// ── Hook mocks ────────────────────────────────────────────────────────────────
vi.mock('../hooks/useAuth', () => ({
  useAuth: vi.fn(),
}))

vi.mock('../hooks/useDashboardStats', () => ({
  useDashboardStats: vi.fn(() => ({ data: null, isLoading: true })),
}))

vi.mock('../hooks/useNotifications', () => ({
  useNotifications: vi.fn(() => ({ notifications: [], dismiss: vi.fn() })),
}))

// Heavy page mocks — keep tests fast and focused on routing/auth
vi.mock('../pages/DashboardPage',         () => ({ default: () => <div>Dashboard</div> }))
vi.mock('../pages/AuditLogPage',          () => ({ default: () => <div>Audit Log</div> }))
vi.mock('../pages/LoginPage',             () => ({
  default: ({ onLogin }: { onLogin?: () => void }) => (
    <div>
      Login Page
      <button onClick={onLogin}>Sign In</button>
    </div>
  ),
}))
vi.mock('../pages/ForgotPasswordPage',    () => ({ default: () => <div>Forgot Password</div> }))
vi.mock('../pages/PublicVerificationPage',() => ({ default: () => <div>Public Verify</div> }))
vi.mock('../components/layout/AppLayout', () => ({
  default: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}))

import App from '../App'
import { useAuth } from '../hooks/useAuth'

const mockUseAuth = useAuth as ReturnType<typeof vi.fn>

function setupAuth(isAuthenticated: boolean) {
  mockUseAuth.mockReturnValue({
    isAuthenticated,
    auth: { token: isAuthenticated ? 'tok' : null, role: isAuthenticated ? 'Administrator' : null },
    login: vi.fn(),
    completeMfaLogin: vi.fn(),
    requiresMfa: false,
  })
}

// ── Helpers ───────────────────────────────────────────────────────────────────

// react-router v6 uses the real window.history, so we push a path before rendering
function renderAt(path: string) {
  window.history.pushState({}, '', path)
  return render(<App />)
}

// ─────────────────────────────────────────────────────────────────────────────

describe('App — unauthenticated', () => {
  beforeEach(() => setupAuth(false))

  it('renders Login page at /login', () => {
    renderAt('/login')
    expect(screen.getByText('Login Page')).toBeInTheDocument()
  })

  it('redirects / to /login when not authenticated', () => {
    renderAt('/')
    expect(screen.getByText('Login Page')).toBeInTheDocument()
  })

  it('redirects /audit to /login when not authenticated', () => {
    renderAt('/audit')
    expect(screen.getByText('Login Page')).toBeInTheDocument()
  })

  it('renders ForgotPasswordPage at /forgot-password without auth', () => {
    renderAt('/forgot-password')
    expect(screen.getByText('Forgot Password')).toBeInTheDocument()
  })

  it('renders PublicVerificationPage at /public/verify without auth', () => {
    renderAt('/public/verify')
    expect(screen.getByText('Public Verify')).toBeInTheDocument()
  })
})

describe('App — authenticated', () => {
  beforeEach(() => setupAuth(true))

  it('redirects /login to / when already authenticated', () => {
    renderAt('/login')
    // Should render the protected area (LiveDashboard loading state via mocked useDashboardStats)
    expect(screen.queryByText('Login Page')).not.toBeInTheDocument()
  })

  it('renders protected content at /', () => {
    renderAt('/')
    // LiveDashboard shows loading text while useDashboardStats returns isLoading:true
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('renders AuditLogPage at /audit', () => {
    renderAt('/audit')
    expect(screen.getByText('Audit Log')).toBeInTheDocument()
  })

  it('renders ForgotPasswordPage at /forgot-password when authenticated', () => {
    renderAt('/forgot-password')
    expect(screen.getByText('Forgot Password')).toBeInTheDocument()
  })

  it('renders PublicVerificationPage at /public/verify when authenticated', () => {
    renderAt('/public/verify')
    expect(screen.getByText('Public Verify')).toBeInTheDocument()
  })
})

describe('App — QueryClientProvider', () => {
  it('wraps the app in a QueryClientProvider (no context error)', () => {
    setupAuth(false)
    expect(() => renderAt('/login')).not.toThrow()
  })
})
