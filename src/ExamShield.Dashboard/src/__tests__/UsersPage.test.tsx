import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import UsersPage from '../pages/UsersPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getUsers:          vi.fn(),
    updateUserRole:    vi.fn(),
    deactivateUser:    vi.fn(),
    activateUser:      vi.fn(),
    updateUserProfile: vi.fn(),
    exportUsers:       vi.fn(),
  },
}))

// ── Fixtures ──────────────────────────────────────────────────────────────────

const ACTIVE_ADMIN = {
  userId: 'user-1', email: 'admin@test.com',   role: 'Administrator',
  isActive: true,  createdAt: '2026-01-01T00:00:00Z',
}
const ACTIVE_OP = {
  userId: 'user-2', email: 'op@test.com',       role: 'Operator',
  isActive: true,  createdAt: '2026-02-01T00:00:00Z',
}
const INACTIVE_OP = {
  userId: 'user-3', email: 'inactive@test.com', role: 'Operator',
  isActive: false, createdAt: '2026-03-01T00:00:00Z',
}

const MOCK_USERS = [ACTIVE_ADMIN, ACTIVE_OP, INACTIVE_OP]

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <UsersPage />
    </QueryClientProvider>
  )
}

// ── Setup ─────────────────────────────────────────────────────────────────────

beforeEach(() => {
  vi.resetAllMocks()
  vi.mocked(apiClient.api.getUsers).mockResolvedValue({ users: MOCK_USERS, totalCount: 3, totalPages: 1 })
  vi.mocked(apiClient.api.updateUserRole).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.deactivateUser).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.activateUser).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.updateUserProfile).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.exportUsers).mockResolvedValue(new Blob(['csv'], { type: 'text/csv' }))
})

// ── Header ────────────────────────────────────────────────────────────────────

describe('UsersPage — header', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /users/i })).toBeInTheDocument()
  })

  it('shows total user count', async () => {
    renderPage()
    expect(await screen.findByText('3 total users')).toBeInTheDocument()
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getUsers).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })
})

// ── User table ────────────────────────────────────────────────────────────────

describe('UsersPage — user table', () => {
  it('renders a row per user plus header row', async () => {
    renderPage()
    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(4) // header + 3 users
  })

  it('displays user emails', async () => {
    renderPage()
    expect(await screen.findByText('admin@test.com')).toBeInTheDocument()
    expect(screen.getByText('op@test.com')).toBeInTheDocument()
    expect(screen.getByText('inactive@test.com')).toBeInTheDocument()
  })

  it('shows Active status chip for active users', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    expect(screen.getAllByText('Active').length).toBeGreaterThanOrEqual(2)
  })

  it('shows Inactive status chip for inactive users', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    expect(screen.getAllByText('Inactive').length).toBeGreaterThan(0)
  })

  it('shows role labels in select elements', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    expect(screen.getAllByText('Administrator').length).toBeGreaterThanOrEqual(1)
  })

  it('shows empty state when no users match', async () => {
    vi.mocked(apiClient.api.getUsers).mockResolvedValue({ users: [], totalCount: 0, totalPages: 1 })
    renderPage()
    expect(await screen.findByText(/no users/i)).toBeInTheDocument()
  })
})

// ── Filters ───────────────────────────────────────────────────────────────────

describe('UsersPage — filters', () => {
  it('renders search input', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    expect(screen.getByPlaceholderText(/search by email/i)).toBeInTheDocument()
  })

  it('renders role filter select', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    expect(screen.getByText('All roles')).toBeInTheDocument()
  })

  it('renders status filter select', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    expect(screen.getByText('All statuses')).toBeInTheDocument()
  })

  it('shows Clear button when search is set', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    expect(screen.queryByRole('button', { name: /clear/i })).not.toBeInTheDocument()
    fireEvent.change(screen.getByPlaceholderText(/search by email/i), { target: { value: 'admin' } })
    // Filter change triggers re-query; findByRole waits for data to reload
    expect(await screen.findByRole('button', { name: /clear/i })).toBeInTheDocument()
  })

  it('shows Clear button when role filter is set', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: 'Administrator' } })
    expect(await screen.findByRole('button', { name: /clear/i })).toBeInTheDocument()
  })

  it('shows Clear button when status filter is set', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    fireEvent.change(screen.getAllByRole('combobox')[1], { target: { value: 'true' } })
    expect(await screen.findByRole('button', { name: /clear/i })).toBeInTheDocument()
  })

  it('Clear button resets all filters', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    fireEvent.change(screen.getByPlaceholderText(/search by email/i), { target: { value: 'admin' } })
    const clearBtn = await screen.findByRole('button', { name: /clear/i })
    fireEvent.click(clearBtn)
    // After clear, data reloads; once loaded the Clear button is gone
    await screen.findByText('admin@test.com')
    expect(screen.queryByRole('button', { name: /clear/i })).not.toBeInTheDocument()
  })
})

// ── User actions ──────────────────────────────────────────────────────────────

describe('UsersPage — user actions', () => {
  it('calls deactivateUser when Deactivate button clicked for active user', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    const btns = screen.getAllByRole('button', { name: /deactivate/i })
    fireEvent.click(btns[0])
    await waitFor(() =>
      expect(apiClient.api.deactivateUser).toHaveBeenCalled()
    )
  })

  it('calls activateUser when Activate button clicked for inactive user', async () => {
    renderPage()
    await screen.findByText('inactive@test.com')
    const btn = screen.getByRole('button', { name: /^activate$/i })
    fireEvent.click(btn)
    await waitFor(() =>
      expect(apiClient.api.activateUser).toHaveBeenCalledWith('user-3')
    )
  })

  it('calls updateUserRole when role select changes', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    // combobox[0] = role filter, [1] = status filter, then per-user role selects
    const selects = screen.getAllByRole('combobox')
    fireEvent.change(selects[2], { target: { value: 'Auditor' } }) // first user's role select
    await waitFor(() =>
      expect(apiClient.api.updateUserRole).toHaveBeenCalledWith('user-1', 'Auditor')
    )
  })

  it('calls updateUserProfile when display name input loses focus', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    const inputs = screen.getAllByPlaceholderText('—')
    fireEvent.change(inputs[0], { target: { value: 'Admin Name' } })
    fireEvent.blur(inputs[0])
    await waitFor(() =>
      expect(apiClient.api.updateUserProfile).toHaveBeenCalledWith('user-1', 'Admin Name')
    )
  })

  it('calls updateUserProfile with null when display name is cleared on blur', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    const inputs = screen.getAllByPlaceholderText('—')
    fireEvent.change(inputs[0], { target: { value: '   ' } }) // whitespace only
    fireEvent.blur(inputs[0])
    await waitFor(() =>
      expect(apiClient.api.updateUserProfile).toHaveBeenCalledWith('user-1', null)
    )
  })
})

// ── Export CSV ────────────────────────────────────────────────────────────────

describe('UsersPage — export CSV', () => {
  it('shows Export CSV button', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    expect(screen.getByRole('button', { name: /export csv/i })).toBeInTheDocument()
  })

  it('calls api.exportUsers when Export CSV clicked', async () => {
    const create = vi.fn(() => 'blob:mock-url')
    const revoke = vi.fn()
    vi.stubGlobal('URL', { createObjectURL: create, revokeObjectURL: revoke })
    renderPage()
    await screen.findByText('admin@test.com')
    fireEvent.click(screen.getByRole('button', { name: /export csv/i }))
    await waitFor(() =>
      expect(apiClient.api.exportUsers).toHaveBeenCalled()
    )
  })
})
