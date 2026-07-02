import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import SettingsPage from '../pages/SettingsPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    getSettings: vi.fn(),
    updateSettings: vi.fn(),
    getNotificationChannelSettings: vi.fn(),
    updateNotificationChannelSettings: vi.fn(),
    testAlert: vi.fn(),
  },
}))

const mockSettings = {
  ocrConfidenceThreshold: 0.85,
  notificationsEnabled: true,
  notificationSeverity: 'High',
  accessTokenExpiryMinutes: 60,
  refreshTokenExpiryDays: 7,
}

const mockChannels = {
  emailEnabled: false,   emailRecipients: null,
  slackEnabled: false,   slackWebhookUrl: null,
  lineEnabled: false,    lineNotifyToken: null,
  webhookEnabled: false, webhookUrl: null,
  updatedAt: '2026-06-29T00:00:00Z',
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
  vi.mocked(apiClient.api.getNotificationChannelSettings).mockResolvedValue(mockChannels)
  vi.mocked(apiClient.api.updateNotificationChannelSettings).mockResolvedValue({ ...mockChannels, emailEnabled: true })
  vi.mocked(apiClient.api.testAlert).mockResolvedValue({ sent: true, error: null })
})

describe('SettingsPage — core settings', () => {
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

describe('SettingsPage — notification channels', () => {
  it('shows Alert Channels section heading', async () => {
    renderPage()
    expect(await screen.findByText(/alert channels/i)).toBeInTheDocument()
  })

  it('shows Email, Slack, LINE and Webhook channel toggles', async () => {
    renderPage()
    expect(await screen.findByText(/alert channels/i))
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/slack/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/line notify/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/generic webhook/i)).toBeInTheDocument()
  })

  it('shows recipients field when Email is toggled on', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/alert channels/i)
    await user.click(screen.getByLabelText(/email/i))
    expect(await screen.findByPlaceholderText(/ops@example\.com/i)).toBeInTheDocument()
  })

  it('shows webhook URL field when Slack is toggled on', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/alert channels/i)
    await user.click(screen.getByLabelText(/slack/i))
    expect(await screen.findByPlaceholderText(/hooks\.slack\.com/i)).toBeInTheDocument()
  })

  it('calls updateNotificationChannelSettings when Save Channels is clicked', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: /save channels/i })).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /save channels/i }))
    await waitFor(() =>
      expect(apiClient.api.updateNotificationChannelSettings).toHaveBeenCalled()
    )
  })

  it('shows channel settings saved confirmation', async () => {
    renderPage()
    await screen.findByRole('button', { name: /save channels/i })
    fireEvent.click(screen.getByRole('button', { name: /save channels/i }))
    expect(await screen.findByText(/channel settings saved/i)).toBeInTheDocument()
  })

  it('shows Send Test Alert button', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: /send test alert/i })).toBeInTheDocument()
  })

  it('calls testAlert when Send Test Alert is clicked', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /send test alert/i }))
    await waitFor(() => expect(apiClient.api.testAlert).toHaveBeenCalled())
  })

  it('shows success message after test alert is sent', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /send test alert/i }))
    expect(await screen.findByText(/test alert sent successfully/i)).toBeInTheDocument()
  })

  it('shows failure message when test alert returns sent=false', async () => {
    vi.mocked(apiClient.api.testAlert).mockResolvedValue({ sent: false, error: 'SMTP unreachable' })
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /send test alert/i }))
    expect(await screen.findByText(/smtp unreachable/i)).toBeInTheDocument()
  })

  it('shows failure message when test alert request rejects', async () => {
    vi.mocked(apiClient.api.testAlert).mockRejectedValue(new Error('network error'))
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /send test alert/i }))
    expect(await screen.findByText(/request failed/i)).toBeInTheDocument()
  })

  it('shows LINE Notify token field when LINE Notify is toggled on', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/alert channels/i)
    await user.click(screen.getByLabelText(/line notify/i))
    expect(await screen.findByPlaceholderText(/LINE Notify access token/i)).toBeInTheDocument()
  })

  it('shows Generic Webhook URL field when Generic Webhook is toggled on', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/alert channels/i)
    await user.click(screen.getByLabelText(/generic webhook/i))
    expect(await screen.findByPlaceholderText(/your-service\.example\.com/i)).toBeInTheDocument()
  })

  it('typing in email recipients field updates the value', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/alert channels/i)
    await user.click(screen.getByLabelText(/email/i))
    const recipientsInput = await screen.findByPlaceholderText(/ops@example\.com/i)
    await user.type(recipientsInput, 'sec@example.com')
    expect(recipientsInput).toHaveValue('sec@example.com')
  })

  it('typing in Slack webhook URL field updates the value', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/alert channels/i)
    await user.click(screen.getByLabelText(/slack/i))
    const input = await screen.findByPlaceholderText(/hooks\.slack\.com/i)
    await user.type(input, 'https://hooks.slack.com/services/T1/B1/x')
    expect(input).toHaveValue('https://hooks.slack.com/services/T1/B1/x')
  })

  it('typing in LINE Notify token field updates the value', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/alert channels/i)
    await user.click(screen.getByLabelText(/line notify/i))
    const input = await screen.findByPlaceholderText(/LINE Notify access token/i)
    await user.type(input, 'my-line-token')
    expect(input).toHaveValue('my-line-token')
  })

  it('typing in Generic Webhook URL field updates the value', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText(/alert channels/i)
    await user.click(screen.getByLabelText(/generic webhook/i))
    const input = await screen.findByPlaceholderText(/your-service\.example\.com/i)
    await user.type(input, 'https://example.com/notify')
    expect(input).toHaveValue('https://example.com/notify')
  })

  it('shows error message when saving notification channels fails', async () => {
    vi.mocked(apiClient.api.updateNotificationChannelSettings).mockRejectedValue(
      new Error('Network failure')
    )
    renderPage()
    await screen.findByRole('button', { name: /save channels/i })
    fireEvent.click(screen.getByRole('button', { name: /save channels/i }))
    expect(await screen.findByText(/failed to save notification settings/i)).toBeInTheDocument()
  })
})

describe('SettingsPage — form field interactions', () => {
  it('OCR confidence slider change updates displayed percentage', async () => {
    renderPage()
    const slider = await screen.findByLabelText(/ocr confidence threshold/i)
    fireEvent.change(slider, { target: { value: '0.70' } })
    expect(screen.getByText('70%')).toBeInTheDocument()
  })

  it('notifications enabled checkbox can be toggled off', async () => {
    renderPage()
    const checkbox = await screen.findByLabelText(/notifications enabled/i)
    expect(checkbox).toBeChecked()
    fireEvent.click(checkbox)
    expect(checkbox).not.toBeChecked()
  })

  it('notification severity select can be changed', async () => {
    renderPage()
    const select = await screen.findByLabelText(/notification severity/i)
    fireEvent.change(select, { target: { value: 'Critical' } })
    expect(select).toHaveValue('Critical')
  })

  it('access token expiry field can be changed', async () => {
    renderPage()
    const input = await screen.findByLabelText(/access token expiry/i)
    fireEvent.change(input, { target: { value: '30' } })
    expect(input).toHaveValue(30)
  })

  it('refresh token expiry field can be changed', async () => {
    renderPage()
    const input = await screen.findByLabelText(/refresh token expiry/i)
    fireEvent.change(input, { target: { value: '14' } })
    expect(input).toHaveValue(14)
  })

  it('updated values are submitted when Save Settings is clicked', async () => {
    renderPage()
    const input = await screen.findByLabelText(/access token expiry/i)
    fireEvent.change(input, { target: { value: '30' } })
    fireEvent.click(screen.getByRole('button', { name: /save settings/i }))
    await waitFor(() =>
      expect(apiClient.api.updateSettings).toHaveBeenCalledWith(
        expect.objectContaining({ accessTokenExpiryMinutes: 30 })
      )
    )
  })
})
