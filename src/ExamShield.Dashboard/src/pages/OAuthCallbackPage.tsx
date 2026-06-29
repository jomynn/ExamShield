import { useEffect, useRef } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'

// Landing page for the OIDC authorization_code callback.
// The API redirects here with ?token=<jwt> or ?oidc_error=<message>.
export default function OAuthCallbackPage() {
  const [params] = useSearchParams()
  const navigate  = useNavigate()
  const handled   = useRef(false)

  useEffect(() => {
    if (handled.current) return
    handled.current = true

    const token = params.get('token')
    const error = params.get('oidc_error')

    if (token) {
      localStorage.setItem('auth_token', token)
      window.dispatchEvent(new Event('auth:token_set'))
      navigate('/', { replace: true })
    } else {
      navigate(`/login?error=${encodeURIComponent(error ?? 'OIDC login failed')}`, { replace: true })
    }
  }, [params, navigate])

  return (
    <div className="min-h-screen aurora-bg flex items-center justify-center">
      <div className="flex flex-col items-center gap-4 text-muted-foreground">
        <div className="h-6 w-6 rounded-full border-2 border-border border-t-primary animate-spin" />
        <p className="text-sm">Completing sign-in…</p>
      </div>
    </div>
  )
}
