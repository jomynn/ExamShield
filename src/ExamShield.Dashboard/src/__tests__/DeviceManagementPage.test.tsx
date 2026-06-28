import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import DeviceManagementPage from '../pages/DeviceManagementPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getDevices: vi.fn(),
    disableDevice: vi.fn(),
    enableDevice: vi.fn(),
  },
}))

const mockDevices = [
  {
    deviceId: 'dev-1',
    name: 'Scanner-01',
    status: 'Approved',
    isActive: true,
    registeredAt: '2026-06-01T08:00:00Z',
    lastSeenAt: null,
    blacklistReason: null,
  },
  {
    deviceId: 'dev-2',
    name: 'Scanner-02',
    status: 'Disabled',
    isActive: false,
    registeredAt: '2026-06-02T09:00:00Z',
    lastSeenAt: null,
    blacklistReason: null,
  },
]

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <DeviceManagementPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getDevices).mockResolvedValue({ devices: mockDevices })
  vi.mocked(apiClient.api.disableDevice).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.enableDevice).mockResolvedValue(undefined)
})

describe('DeviceManagementPage', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /device management/i })).toBeInTheDocument()
  })

  it('renders a row for each device', async () => {
    renderPage()
    const rows = await screen.findAllByRole('row')
    expect(rows).toHaveLength(3) // header + 2 rows
  })

  it('displays device names', async () => {
    renderPage()
    expect(await screen.findByText('Scanner-01')).toBeInTheDocument()
    expect(await screen.findByText('Scanner-02')).toBeInTheDocument()
  })

  it('shows Disable button for active device', async () => {
    renderPage()
    await screen.findByText('Scanner-01')
    const disableButtons = screen.getAllByRole('button', { name: /disable/i })
    expect(disableButtons.length).toBeGreaterThan(0)
  })

  it('shows Enable button for inactive device', async () => {
    renderPage()
    await screen.findByText('Scanner-02')
    const enableButtons = screen.getAllByRole('button', { name: /enable/i })
    expect(enableButtons.length).toBeGreaterThan(0)
  })

  it('calls disableDevice when Disable clicked', async () => {
    renderPage()
    await screen.findByText('Scanner-01')
    const btn = screen.getAllByRole('button', { name: /disable/i })[0]
    fireEvent.click(btn)
    await waitFor(() => expect(apiClient.api.disableDevice).toHaveBeenCalledWith('dev-1'))
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getDevices).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })
})
