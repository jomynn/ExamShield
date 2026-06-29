import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { KeyRound, Shield, ShieldCheck, ShieldOff } from 'lucide-react'

type SetupData = { secret: string; qrUri: string }

export default function MfaPage() {
  const qc = useQueryClient()
  const [setup, setSetup] = useState<SetupData | null>(null)
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data: status, isLoading } = useQuery({
    queryKey: ['mfa-status'],
    queryFn: api.getMfaStatus,
  })

  const setupMut = useMutation({
    mutationFn: api.setupMfa,
    onSuccess: (data) => setSetup(data),
  })

  const verifyMut = useMutation({
    mutationFn: (c: string) => api.verifyMfa(c),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['mfa-status'] }); setSetup(null) },
    onError: () => setError('Invalid code. Please try again.'),
  })

  const disableMut = useMutation({
    mutationFn: api.disableMfa,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['mfa-status'] }),
  })

  if (isLoading) {
    return (
      <div role="status" aria-label="Loading" className="p-8 text-center text-muted-foreground">
        <div className="inline-block h-5 w-5 rounded-full border-2 border-border border-t-primary animate-spin" />
      </div>
    )
  }

  return (
    <div className="space-y-5 pb-4">
      {/* Header */}
      <div className="glass-card px-6 py-4">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-2xl"
            style={{ background: 'rgba(79,142,247,0.12)' }}>
            <KeyRound className="h-5 w-5 text-primary stroke-[1.75]" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-foreground">Multi-Factor Authentication</h1>
            <p className="text-sm text-muted-foreground">Protect your account with TOTP</p>
          </div>
        </div>
      </div>

      {/* Status card */}
      <div className="max-w-xl">
        <div className="glass-card p-6 space-y-5">
          <div className="flex items-center gap-3">
            <div className={`flex h-10 w-10 items-center justify-center rounded-2xl ${
              status?.mfaEnabled
                ? 'bg-green-500/12'
                : 'bg-yellow-500/12'
            }`}>
              {status?.mfaEnabled
                ? <ShieldCheck className="h-5 w-5 text-green-400" />
                : <Shield className="h-5 w-5 text-yellow-400" />
              }
            </div>
            <div>
              <p className="text-sm font-semibold text-foreground">
                {status?.mfaEnabled ? 'MFA Enabled' : 'MFA Not Enabled'}
              </p>
              <p className="text-xs text-muted-foreground">
                {status?.mfaEnabled
                  ? 'Your account is protected with TOTP.'
                  : 'Your account does not have MFA enabled.'}
              </p>
            </div>
            <span
              className="ml-auto rounded-full px-3 py-1 text-xs font-semibold"
              style={status?.mfaEnabled
                ? { background: 'rgba(74,222,128,0.12)', color: '#4ade80', border: '1px solid rgba(74,222,128,0.2)' }
                : { background: 'rgba(250,204,21,0.12)', color: '#facc15', border: '1px solid rgba(250,204,21,0.2)' }
              }
            >
              {status?.mfaEnabled ? 'Active' : 'Inactive'}
            </span>
          </div>

          {!status?.mfaEnabled && !setup && (
            <button
              onClick={() => setupMut.mutate()}
              disabled={setupMut.isPending}
              className="btn-primary"
            >
              {setupMut.isPending ? 'Generating…' : 'Enable MFA'}
            </button>
          )}

          {status?.mfaEnabled && (
            <button
              onClick={() => disableMut.mutate()}
              disabled={disableMut.isPending}
              className="btn-danger"
            >
              <ShieldOff className="h-3.5 w-3.5" />
              {disableMut.isPending ? 'Disabling…' : 'Disable MFA'}
            </button>
          )}
        </div>

        {/* Setup flow */}
        {setup && (
          <div className="glass-card p-6 mt-4 space-y-4">
            <p className="text-sm font-semibold text-foreground">Scan in your authenticator app</p>

            <div className="rounded-2xl p-3 font-mono text-xs break-all"
              style={{ background: 'rgba(255,255,255,0.03)', border: '1px solid var(--glass-border)' }}>
              <p className="text-muted-foreground mb-1 text-[10px] uppercase tracking-wider">Secret</p>
              <p className="text-foreground">{setup.secret}</p>
            </div>

            <div className="rounded-2xl p-3 font-mono text-[10px] break-all"
              style={{ background: 'rgba(255,255,255,0.03)', border: '1px solid var(--glass-border)' }}>
              <p className="text-muted-foreground mb-1 text-[10px] uppercase tracking-wider">QR URI</p>
              <p className="text-muted-foreground">{setup.qrUri}</p>
            </div>

            <div className="space-y-2 pt-1">
              <label htmlFor="totp-code" className="block text-sm font-medium text-foreground">
                Enter the 6-digit code to confirm
              </label>
              <input
                id="totp-code"
                type="text"
                maxLength={6}
                inputMode="numeric"
                placeholder="000000"
                value={code}
                onChange={(e) => { setCode(e.target.value.replace(/\D/g, '')); setError(null) }}
                className="input-glass text-center font-mono tracking-[0.5em] text-xl"
              placeholder="6-digit code"
              />
              {error && (
                <p className="rounded-xl px-3 py-2 text-sm text-red-400"
                  style={{ background: 'rgba(239,68,68,0.10)', border: '1px solid rgba(239,68,68,0.2)' }}>
                  {error}
                </p>
              )}
              <button
                onClick={() => verifyMut.mutate(code)}
                disabled={code.length !== 6 || verifyMut.isPending}
                className="btn-primary w-full py-2.5 mt-1"
              >
                {verifyMut.isPending ? 'Verifying…' : 'Verify & Enable'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
