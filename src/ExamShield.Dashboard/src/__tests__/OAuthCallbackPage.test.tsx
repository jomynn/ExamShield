import { render, screen, waitFor } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import OAuthCallbackPage from '../pages/OAuthCallbackPage'

// Capture navigate calls
const mockNavigate = vi.fn()
vi.mock('react-router-dom', async (importOriginal) => {
  const real = await importOriginal<typeof import('react-router-dom')>()
  return { ...real, useNavigate: () => mockNavigate }
})

function renderAt(search: string) {
  return render(
    <MemoryRouter initialEntries={[`/oauth/callback${search}`]}>
      <Routes>
        <Route path="/oauth/callback" element={<OAuthCallbackPage />} />
      </Routes>
    </MemoryRouter>
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  localStorage.clear()
})

afterEach(() => {
  localStorage.clear()
})

describe('OAuthCallbackPage — loading state', () => {
  it('shows a spinner while processing', () => {
    renderAt('')
    expect(document.querySelector('.animate-spin')).toBeInTheDocument()
  })

  it('shows completing sign-in message', () => {
    renderAt('')
    expect(screen.getByText(/completing sign-in/i)).toBeInTheDocument()
  })
})

describe('OAuthCallbackPage — token flow', () => {
  it('stores token in localStorage when ?token param present', async () => {
    renderAt('?token=jwt.test.token')
    await waitFor(() => expect(localStorage.getItem('auth_token')).toBe('jwt.test.token'))
  })

  it('dispatches auth:token_set event on success', async () => {
    const listener = vi.fn()
    window.addEventListener('auth:token_set', listener)
    renderAt('?token=jwt.test.token')
    await waitFor(() => expect(listener).toHaveBeenCalled())
    window.removeEventListener('auth:token_set', listener)
  })

  it('navigates to / with replace on success', async () => {
    renderAt('?token=jwt.test.token')
    await waitFor(() =>
      expect(mockNavigate).toHaveBeenCalledWith('/', { replace: true })
    )
  })
})

describe('OAuthCallbackPage — error flow', () => {
  it('navigates to /login with encoded error when ?oidc_error present', async () => {
    renderAt('?oidc_error=access_denied')
    await waitFor(() =>
      expect(mockNavigate).toHaveBeenCalledWith(
        expect.stringContaining('/login?error='),
        { replace: true }
      )
    )
  })

  it('includes the encoded oidc_error message in the redirect URL', async () => {
    renderAt('?oidc_error=invalid_token')
    await waitFor(() => {
      const [url] = mockNavigate.mock.calls[0]
      expect(url).toContain('invalid_token')
    })
  })

  it('navigates to /login when no params provided', async () => {
    renderAt('')
    await waitFor(() =>
      expect(mockNavigate).toHaveBeenCalledWith(
        expect.stringContaining('/login'),
        { replace: true }
      )
    )
  })

  it('does not store token in localStorage on error', async () => {
    renderAt('?oidc_error=access_denied')
    await waitFor(() => expect(mockNavigate).toHaveBeenCalled())
    expect(localStorage.getItem('auth_token')).toBeNull()
  })
})
