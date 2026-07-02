import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import MfaPage from '../pages/MfaPage'
import * as apiClient from '../api/client'

vi.mock('qrcode.react', () => ({
  QRCodeSVG: ({ value, role, 'aria-label': ariaLabel }: { value: string; role?: string; 'aria-label'?: string }) =>
    <svg role={role} aria-label={ariaLabel} data-testid="qr-code" data-value={value} />,
}))

vi.mock('../api/client', () => ({
  api: {
    getMfaStatus: vi.fn(),
    setupMfa: vi.fn(),
    verifyMfa: vi.fn(),
    disableMfa: vi.fn(),
  },
}))

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={qc}>
      <MfaPage />
    </QueryClientProvider>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.getMfaStatus).mockResolvedValue({ mfaEnabled: false })
  vi.mocked(apiClient.api.setupMfa).mockResolvedValue({
    secret: 'JBSWY3DPEHPK3PXP',
    qrUri: 'otpauth://totp/ExamShield:user@test.com?secret=JBSWY3DPEHPK3PXP&issuer=ExamShield',
  })
  vi.mocked(apiClient.api.verifyMfa).mockResolvedValue({ mfaEnabled: true })
  vi.mocked(apiClient.api.disableMfa).mockResolvedValue({ mfaEnabled: false })
})

describe('MfaPage', () => {
  it('renders page heading', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { name: /multi-factor/i })).toBeInTheDocument()
  })

  it('shows MFA disabled state initially', async () => {
    renderPage()
    expect(await screen.findByText(/not enabled/i)).toBeInTheDocument()
  })

  it('shows Enable MFA button when MFA is off', async () => {
    renderPage()
    expect(await screen.findByRole('button', { name: /enable mfa/i })).toBeInTheDocument()
  })

  it('calls setupMfa when Enable button clicked', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /enable mfa/i }))
    await waitFor(() => expect(apiClient.api.setupMfa).toHaveBeenCalled())
  })

  it('shows QR code URI after setup', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /enable mfa/i }))
    const matches = await screen.findAllByText(/JBSWY3DPEHPK3PXP/)
    expect(matches.length).toBeGreaterThan(0)
  })

  it('shows code input field after setup', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /enable mfa/i }))
    const matches = await screen.findAllByText(/JBSWY3DPEHPK3PXP/)
    expect(matches.length).toBeGreaterThan(0)
    expect(screen.getByPlaceholderText(/6-digit code/i)).toBeInTheDocument()
  })

  it('calls verifyMfa with entered code', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /enable mfa/i }))
    await screen.findByPlaceholderText(/6-digit code/i)
    fireEvent.change(screen.getByPlaceholderText(/6-digit code/i), { target: { value: '123456' } })
    fireEvent.click(screen.getByRole('button', { name: /verify/i }))
    await waitFor(() =>
      expect(apiClient.api.verifyMfa).toHaveBeenCalledWith('123456')
    )
  })

  it('shows Disable button when MFA is enabled', async () => {
    vi.mocked(apiClient.api.getMfaStatus).mockResolvedValue({ mfaEnabled: true })
    renderPage()
    expect(await screen.findByRole('button', { name: /disable mfa/i })).toBeInTheDocument()
  })

  it('shows loading state initially', () => {
    vi.mocked(apiClient.api.getMfaStatus).mockImplementation(() => new Promise(() => {}))
    renderPage()
    expect(screen.getByRole('status', { name: /loading/i })).toBeInTheDocument()
  })

  it('renders a scannable QR code image after setup', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /enable mfa/i }))
    await screen.findAllByText(/JBSWY3DPEHPK3PXP/)
    expect(screen.getByRole('img', { name: /qr code/i })).toBeInTheDocument()
  })

  it('QR code encodes the otpauth URI', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /enable mfa/i }))
    await screen.findAllByText(/JBSWY3DPEHPK3PXP/)
    const qr = screen.getByTestId('qr-code')
    expect(qr).toHaveAttribute('data-value', expect.stringContaining('otpauth://totp/'))
  })

  it('verify input has a single placeholder', async () => {
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /enable mfa/i }))
    await screen.findAllByText(/JBSWY3DPEHPK3PXP/)
    const inputs = screen.getAllByPlaceholderText(/6-digit code/i)
    expect(inputs).toHaveLength(1)
  })

  it('shows invalid code error when verifyMfa rejects', async () => {
    vi.mocked(apiClient.api.verifyMfa).mockRejectedValue(new Error('invalid'))
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /enable mfa/i }))
    await screen.findByPlaceholderText(/6-digit code/i)
    fireEvent.change(screen.getByPlaceholderText(/6-digit code/i), { target: { value: '000000' } })
    fireEvent.click(screen.getByRole('button', { name: /verify/i }))
    expect(await screen.findByText(/invalid code/i)).toBeInTheDocument()
  })

  it('calls disableMfa when Disable MFA button is clicked', async () => {
    vi.mocked(apiClient.api.getMfaStatus).mockResolvedValue({ mfaEnabled: true })
    renderPage()
    fireEvent.click(await screen.findByRole('button', { name: /disable mfa/i }))
    await waitFor(() => expect(apiClient.api.disableMfa).toHaveBeenCalled())
  })
})
