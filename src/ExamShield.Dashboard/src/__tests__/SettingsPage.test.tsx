import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import SettingsPage from '../pages/SettingsPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getSettings: vi.fn(),
    updateSettings: vi.fn(),
  },
}))

const mockSettings = {
  ocrConfidenceThreshold: 0.85,
  notificationsEnabled: true,
  notificationSeverity: 'High',
  accessTokenExpiryMinutes: 60,
  refreshTokenExpiryDays: 7,
}

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <SettingsPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getSettings).mockResolvedValue(mockSettings)
  vi.mocked(apiClient.api.updateSettings).mockResolvedValue(mockSettings)
})

describe('SettingsPage', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /settings/i })).toBeInTheDocument()
  })

  it('shows OCR confidence threshold section', async () => {
    renderPage()
    const els = await screen.findAllByText(/ocr confidence/i)
    expect(els.length).toBeGreaterThanOrEqual(1)
  })

  it('shows notifications toggle', async () => {
    renderPage()
    await screen.findAllByText(/ocr confidence/i)
    expect(screen.getByLabelText(/notifications enabled/i)).toBeInTheDocument()
  })

  it('shows access token expiry field', async () => {
    renderPage()
    await screen.findAllByText(/ocr confidence/i)
    expect(screen.getByLabelText(/access token expiry/i)).toBeInTheDocument()
  })

  it('pre-fills fields with current settings', async () => {
    renderPage()
    const input = await screen.findByLabelText(/access token expiry/i)
    expect(input).toHaveValue(60)
  })

  it('shows Save button', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: /save settings/i })).toBeInTheDocument()
  })

  it('calls updateSettings on save', async () => {
    renderPage()
    await screen.findByRole('button', { name: /save settings/i })
    fireEvent.click(screen.getByRole('button', { name: /save settings/i }))
    await waitFor(() =>
      expect(apiClient.api.updateSettings).toHaveBeenCalled()
    )
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getSettings).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('shows success message after save', async () => {
    renderPage()
    await screen.findByRole('button', { name: /save settings/i })
    fireEvent.click(screen.getByRole('button', { name: /save settings/i }))
    expect(await screen.findByText(/saved/i)).toBeInTheDocument()
  })
})
