import { render, screen, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, it, expect, vi, afterEach } from 'vitest'
import LoginPage from '../pages/LoginPage'

function renderLogin() {
  return render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>
  )
}

describe('LoginPage', () => {
  it('renders email and password fields', () => {
    renderLogin()
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument()
  })

  it('renders a sign-in button', () => {
    renderLogin()
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument()
  })

  it('shows validation error when fields are empty on submit', async () => {
    renderLogin()
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))
    expect(await screen.findByText(/email is required/i)).toBeInTheDocument()
  })

  it('calls onLogin with credentials when form is submitted', async () => {
    const onLogin = vi.fn().mockResolvedValue(undefined)
    render(
      <MemoryRouter>
        <LoginPage onLogin={onLogin} />
      </MemoryRouter>
    )
    await userEvent.type(screen.getByLabelText(/email/i), 'admin@examshield.local')
    await userEvent.type(screen.getByLabelText(/password/i), 'Secret@1234')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))
    expect(onLogin).toHaveBeenCalledWith('admin@examshield.local', 'Secret@1234')
  })
})

describe('LoginPage — MFA step', () => {
  it('shows authenticator code input when requiresMfa is true', () => {
    render(<MemoryRouter><LoginPage requiresMfa /></MemoryRouter>)
    expect(screen.getByLabelText(/authenticator code/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /verify/i })).toBeInTheDocument()
  })

  it('shows error when code field is empty on submit', async () => {
    render(<MemoryRouter><LoginPage requiresMfa /></MemoryRouter>)
    await userEvent.click(screen.getByRole('button', { name: /verify/i }))
    expect(await screen.findByText(/authenticator code is required/i)).toBeInTheDocument()
  })

  it('calls onMfaLogin with the entered code', async () => {
    const onMfaLogin = vi.fn().mockResolvedValue(undefined)
    render(<MemoryRouter><LoginPage requiresMfa onMfaLogin={onMfaLogin} /></MemoryRouter>)
    await userEvent.type(screen.getByLabelText(/authenticator code/i), '123456')
    await userEvent.click(screen.getByRole('button', { name: /verify/i }))
    expect(onMfaLogin).toHaveBeenCalledWith('123456')
  })

  it('shows invalid code error when onMfaLogin throws', async () => {
    const onMfaLogin = vi.fn().mockRejectedValue(new Error('bad code'))
    render(<MemoryRouter><LoginPage requiresMfa onMfaLogin={onMfaLogin} /></MemoryRouter>)
    await userEvent.type(screen.getByLabelText(/authenticator code/i), '000000')
    await userEvent.click(screen.getByRole('button', { name: /verify/i }))
    expect(await screen.findByText(/invalid authenticator code/i)).toBeInTheDocument()
  })
})

describe('LoginPage — login catch branch', () => {
  it('shows invalid credentials error when onLogin rejects', async () => {
    const onLogin = vi.fn().mockRejectedValue(new Error('unauthorized'))
    render(<MemoryRouter><LoginPage onLogin={onLogin} /></MemoryRouter>)
    await userEvent.type(screen.getByLabelText(/email/i), 'admin@examshield.local')
    await userEvent.type(screen.getByLabelText(/password/i), 'wrongpass')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))
    expect(await screen.findByText(/invalid credentials/i)).toBeInTheDocument()
  })
})

describe('LoginPage — OIDC providers', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
    vi.resetModules()
  })

  it('renders SSO buttons for known providers and unknown provider fallback', async () => {
    vi.stubEnv('VITE_OIDC_PROVIDERS', 'google,azure,custom')
    vi.resetModules()
    const { default: LoginPageDyn } = await import('../pages/LoginPage')
    render(<MemoryRouter><LoginPageDyn /></MemoryRouter>)
    expect(screen.getByRole('button', { name: /continue with google/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /continue with microsoft/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /continue with custom/i })).toBeInTheDocument()
  })

  it('clicking an OIDC provider button triggers handleOidcLogin without throwing', async () => {
    vi.stubEnv('VITE_OIDC_PROVIDERS', 'google')
    vi.resetModules()
    const { default: LoginPageDyn } = await import('../pages/LoginPage')
    render(<MemoryRouter><LoginPageDyn /></MemoryRouter>)
    expect(() =>
      fireEvent.click(screen.getByRole('button', { name: /continue with google/i }))
    ).not.toThrow()
  })
})
