import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import ForgotPasswordPage from '../pages/ForgotPasswordPage'
import * as apiClient from '../api/client'

vi.mock('../api/client', () => ({
  api: {
    forgotPassword: vi.fn(),
    resetPassword: vi.fn(),
  },
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')
  return { ...actual }
})

function renderForgotPage(search = '') {
  return render(
    <MemoryRouter initialEntries={[`/forgot-password${search}`]}>
      <ForgotPasswordPage />
    </MemoryRouter>
  )
}

beforeEach(() => {
  vi.mocked(apiClient.api.forgotPassword).mockResolvedValue(undefined)
  vi.mocked(apiClient.api.resetPassword).mockResolvedValue(undefined)
})

describe('ForgotPasswordPage — request form', () => {
  it('renders email input and submit button', () => {
    renderForgotPage()
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /send reset link/i })).toBeInTheDocument()
  })

  it('submit button is disabled when email is empty', () => {
    renderForgotPage()
    expect(screen.getByRole('button', { name: /send reset link/i })).toBeDisabled()
  })

  it('enables submit button once email is entered', () => {
    renderForgotPage()
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'a@b.com' } })
    expect(screen.getByRole('button', { name: /send reset link/i })).not.toBeDisabled()
  })

  it('calls api.forgotPassword with the entered email', async () => {
    renderForgotPage()
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'user@example.com' } })
    fireEvent.click(screen.getByRole('button', { name: /send reset link/i }))
    await waitFor(() =>
      expect(apiClient.api.forgotPassword).toHaveBeenCalledWith('user@example.com')
    )
  })

  it('shows confirmation message after submit', async () => {
    renderForgotPage()
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'user@example.com' } })
    fireEvent.click(screen.getByRole('button', { name: /send reset link/i }))
    expect(await screen.findByText(/reset link has been sent/i)).toBeInTheDocument()
  })

  it('shows confirmation even when API call fails (no user enumeration)', async () => {
    vi.mocked(apiClient.api.forgotPassword).mockRejectedValue(new Error('not found'))
    renderForgotPage()
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'nope@example.com' } })
    fireEvent.click(screen.getByRole('button', { name: /send reset link/i }))
    expect(await screen.findByText(/reset link has been sent/i)).toBeInTheDocument()
  })

  it('renders Back to Sign In link', () => {
    renderForgotPage()
    expect(screen.getByRole('link', { name: /back to sign in/i })).toBeInTheDocument()
  })
})

describe('ForgotPasswordPage — reset form (token present)', () => {
  it('renders new password fields when token is in URL', () => {
    renderForgotPage('?token=abc123')
    expect(screen.getByLabelText(/new password/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/confirm password/i)).toBeInTheDocument()
  })

  it('submit button is disabled when passwords are empty', () => {
    renderForgotPage('?token=abc123')
    expect(screen.getByRole('button', { name: /set new password/i })).toBeDisabled()
  })

  it('shows mismatch warning when passwords differ', () => {
    renderForgotPage('?token=abc123')
    fireEvent.change(screen.getByLabelText(/new password/i), { target: { value: 'NewPass1!' } })
    fireEvent.change(screen.getByLabelText(/confirm password/i), { target: { value: 'Different1!' } })
    expect(screen.getByText(/do not match/i)).toBeInTheDocument()
  })

  it('calls api.resetPassword with token and new password', async () => {
    renderForgotPage('?token=tok123')
    fireEvent.change(screen.getByLabelText(/new password/i), { target: { value: 'NewPass1!' } })
    fireEvent.change(screen.getByLabelText(/confirm password/i), { target: { value: 'NewPass1!' } })
    fireEvent.click(screen.getByRole('button', { name: /set new password/i }))
    await waitFor(() =>
      expect(apiClient.api.resetPassword).toHaveBeenCalledWith('tok123', 'NewPass1!')
    )
  })

  it('shows success message after password reset', async () => {
    renderForgotPage('?token=tok123')
    fireEvent.change(screen.getByLabelText(/new password/i), { target: { value: 'NewPass1!' } })
    fireEvent.change(screen.getByLabelText(/confirm password/i), { target: { value: 'NewPass1!' } })
    fireEvent.click(screen.getByRole('button', { name: /set new password/i }))
    expect(await screen.findByText(/password updated/i)).toBeInTheDocument()
  })

  it('shows error message when token is invalid', async () => {
    vi.mocked(apiClient.api.resetPassword).mockRejectedValue(new Error('invalid token'))
    renderForgotPage('?token=expired')
    fireEvent.change(screen.getByLabelText(/new password/i), { target: { value: 'NewPass1!' } })
    fireEvent.change(screen.getByLabelText(/confirm password/i), { target: { value: 'NewPass1!' } })
    fireEvent.click(screen.getByRole('button', { name: /set new password/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent(/invalid or has already been used/i)
  })
})
