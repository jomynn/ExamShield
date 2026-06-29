import { useState } from 'react'
import { Zap, Mail, Lock, Hash } from 'lucide-react'
import { useSearchParams } from 'react-router-dom'

const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

// Providers listed here are shown as SSO buttons.
// Set VITE_OIDC_PROVIDERS="google,azure" to enable them.
const OIDC_PROVIDERS: { id: string; label: string }[] = (
  import.meta.env.VITE_OIDC_PROVIDERS ?? ''
)
  .split(',')
  .map((s: string) => s.trim())
  .filter(Boolean)
  .map((id: string) => ({ id, label: id === 'google' ? 'Google' : id === 'azure' ? 'Microsoft' : id }))

function handleOidcLogin(provider: string) {
  const returnUrl = `${window.location.origin}/auth/callback`
  window.location.href =
    `${API_BASE}/auth/external/${provider}/redirect?return_url=${encodeURIComponent(returnUrl)}`
}

interface LoginPageProps {
  onLogin?: (email: string, password: string) => Promise<void>
  onMfaLogin?: (code: string) => Promise<void>
  requiresMfa?: boolean
}

export default function LoginPage({ onLogin, onMfaLogin, requiresMfa }: LoginPageProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [mfaCode, setMfaCode] = useState('')
  const [searchParams] = useSearchParams()
  const oidcError = searchParams.get('error')
  const [error, setError] = useState<string | null>(oidcError)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)

    if (requiresMfa) {
      if (!mfaCode) { setError('Authenticator code is required'); return }
      setLoading(true)
      try { await onMfaLogin?.(mfaCode) }
      catch { setError('Invalid authenticator code') }
      finally { setLoading(false) }
      return
    }

    if (!email)    { setError('Email is required'); return }
    if (!password) { setError('Password is required'); return }

    setLoading(true)
    try { await onLogin?.(email, password) }
    catch { setError('Invalid credentials') }
    finally { setLoading(false) }
  }

  const logoBlock = (
    <div className="flex flex-col items-center gap-4">
      <div className="flex h-14 w-14 items-center justify-center rounded-3xl bg-gradient-to-br from-primary to-[#78A6FF] shadow-glow-blue">
        <Zap className="h-7 w-7 text-white" strokeWidth={2} />
      </div>
      <div className="text-center">
        <h1 className="text-2xl font-bold text-gradient">ExamShield</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          {requiresMfa ? 'Two-factor authentication' : 'Sign in to your account'}
        </p>
      </div>
    </div>
  )

  if (requiresMfa) {
    return (
      <div className="min-h-screen aurora-bg flex items-center justify-center p-4">
        <div className="w-full max-w-sm animate-in">
          <div className="glass-lg rounded-3xl p-8 space-y-6">
            {logoBlock}

            <div className="glass-divider" />

            <form onSubmit={handleSubmit} noValidate className="space-y-4">
              <div className="space-y-1.5">
                <label htmlFor="mfa-code" className="block text-sm font-medium text-foreground">
                  Authenticator Code
                </label>
                <div className="relative">
                  <Hash className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground stroke-[1.75]" />
                  <input
                    id="mfa-code"
                    type="text"
                    inputMode="numeric"
                    maxLength={6}
                    autoComplete="one-time-code"
                    value={mfaCode}
                    onChange={e => setMfaCode(e.target.value.replace(/\D/g, ''))}
                    className="input-glass pl-10 text-center text-xl tracking-[0.5em] font-mono"
                    placeholder="000000"
                    autoFocus
                  />
                </div>
              </div>

              {error && (
                <p role="alert" className="rounded-xl px-3 py-2 text-sm text-red-400"
                  style={{ background: 'rgba(239,68,68,0.10)', border: '1px solid rgba(239,68,68,0.2)' }}>
                  {error}
                </p>
              )}

              <button type="submit" disabled={loading} className="btn-primary w-full py-3">
                {loading ? (
                  <span className="flex items-center gap-2">
                    <span className="h-4 w-4 rounded-full border-2 border-white/30 border-t-white animate-spin" />
                    Verifying…
                  </span>
                ) : 'Verify Code'}
              </button>
            </form>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen aurora-bg flex items-center justify-center p-4">
      <div className="w-full max-w-sm animate-in">
        <div className="glass-lg rounded-3xl p-8 space-y-6">
          {logoBlock}

          <div className="glass-divider" />

          <form onSubmit={handleSubmit} noValidate className="space-y-4">
            <div className="space-y-1.5">
              <label htmlFor="email" className="block text-sm font-medium text-foreground">
                Email address
              </label>
              <div className="relative">
                <Mail className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground stroke-[1.75]" />
                <input
                  id="email"
                  type="email"
                  autoComplete="email"
                  value={email}
                  onChange={e => setEmail(e.target.value)}
                  className="input-glass pl-10"
                  placeholder="admin@examshield.local"
                  autoFocus
                />
              </div>
            </div>

            <div className="space-y-1.5">
              <label htmlFor="password" className="block text-sm font-medium text-foreground">
                Password
              </label>
              <div className="relative">
                <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground stroke-[1.75]" />
                <input
                  id="password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  className="input-glass pl-10"
                  placeholder="••••••••"
                />
              </div>
            </div>

            {error && (
              <p role="alert" className="rounded-xl px-3 py-2 text-sm text-red-400"
                style={{ background: 'rgba(239,68,68,0.10)', border: '1px solid rgba(239,68,68,0.2)' }}>
                {error}
              </p>
            )}

            <button type="submit" disabled={loading} className="btn-primary w-full py-3 mt-2">
              {loading ? (
                <span className="flex items-center gap-2">
                  <span className="h-4 w-4 rounded-full border-2 border-white/30 border-t-white animate-spin" />
                  Signing in…
                </span>
              ) : 'Sign In'}
            </button>

            <p className="text-center text-sm text-muted-foreground pt-1">
              <a href="/forgot-password" className="text-primary hover:underline font-medium">
                Forgot password?
              </a>
            </p>
          </form>

          {OIDC_PROVIDERS.length > 0 && (
            <>
              <div className="relative flex items-center">
                <div className="flex-grow border-t border-border" />
                <span className="mx-3 text-xs text-muted-foreground">or</span>
                <div className="flex-grow border-t border-border" />
              </div>
              <div className="space-y-2">
                {OIDC_PROVIDERS.map(p => (
                  <button
                    key={p.id}
                    type="button"
                    onClick={() => handleOidcLogin(p.id)}
                    className="w-full rounded-xl border border-border bg-background/60 px-4 py-2.5 text-sm font-medium text-foreground hover:bg-muted/30 transition-colors"
                  >
                    Continue with {p.label}
                  </button>
                ))}
              </div>
            </>
          )}
        </div>

        <p className="mt-6 text-center text-xs text-muted-foreground">
          Protected by ExamShield · AES-256-GCM encryption
        </p>
      </div>
    </div>
  )
}
