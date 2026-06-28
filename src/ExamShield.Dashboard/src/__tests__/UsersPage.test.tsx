import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import UsersPage from '../pages/UsersPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getUsers: vi.fn(),
    updateUserRole: vi.fn(),
    deactivateUser: vi.fn(),
  },
}))

const mockUsers = [
  {
    userId: 'user-1',
    email: 'admin@test.com',
    role: 'Administrator',
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
  },
  {
    userId: 'user-2',
    email: 'invigilator@test.com',
    role: 'Operator',
    isActive: true,
    createdAt: '2026-02-01T00:00:00Z',
  },
  {
    userId: 'user-3',
    email: 'inactive@test.com',
    role: 'Operator',
    isActive: false,
    createdAt: '2026-03-01T00:00:00Z',
  },
]

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <UsersPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getUsers).mockResolvedValue({ users: mockUsers })
  vi.mocked(apiClient.api.updateUserRole).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.deactivateUser).mockResolvedValue(undefined)
})

describe('UsersPage', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /users/i })).toBeInTheDocument()
  })

  it('renders a row per user', async () => {
    renderPage()
    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(4) // header + 3 users
  })

  it('displays user emails', async () => {
    renderPage()
    expect(await screen.findByText('admin@test.com')).toBeInTheDocument()
    expect(await screen.findByText('invigilator@test.com')).toBeInTheDocument()
  })

  it('shows active / inactive status', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    const activeChips = screen.getAllByText('Active')
    expect(activeChips.length).toBeGreaterThanOrEqual(2)
    expect(screen.getAllByText('Inactive').length).toBeGreaterThan(0)
  })

  it('shows role labels', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    const adminMatches = screen.getAllByText('Administrator')
    expect(adminMatches.length).toBeGreaterThanOrEqual(1)
  })

  it('calls deactivateUser when deactivate button clicked', async () => {
    renderPage()
    await screen.findByText('admin@test.com')
    const btns = screen.getAllByRole('button', { name: /deactivate/i })
    fireEvent.click(btns[0])
    await waitFor(() =>
      expect(apiClient.api.deactivateUser).toHaveBeenCalled()
    )
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getUsers).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('shows empty state when no users', async () => {
    vi.mocked(apiClient.api.getUsers).mockResolvedValue({ users: [] })
    renderPage()
    expect(await screen.findByText(/no users/i)).toBeInTheDocument()
  })
})
