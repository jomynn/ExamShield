import { useState } from 'react'
import { ShieldCheck, ShieldX, Search, Loader2 } from 'lucide-react'
import { api, type PublicVerifyResponse } from '../api/client'

type VerifyState = { status: 'idle' } | { status: 'loading' } | { status: 'success'; data: PublicVerifyResponse } | { status: 'error'; message: string }

function ResultPanel({ data }: { data: PublicVerifyResponse }) {
  return (
    <div className={`rounded-xl border p-6 space-y-4 ${data.isValid ? 'border-green-500/40 bg-green-500/5' : 'border-red-500/40 bg-red-500/5'}`}>
      <div className="flex items-center gap-3">
        {data.isValid
          ? <ShieldCheck className="h-8 w-8 text-green-500" />
          : <ShieldX className="h-8 w-8 text-red-500" />
        }
        <span className={`text-xl font-bold ${data.isValid ? 'text-green-500' : 'text-red-500'}`}>
          {data.isValid ? 'Verified — Authentic' : 'Tampered — Integrity Failed'}
        </span>
      </div>

      <dl className="grid grid-cols-2 gap-2 text-sm">
        <dt className="text-muted-foreground">Capture ID</dt>
        <dd className="font-mono text-foreground truncate">{data.captureId}</dd>

        <dt className="text-muted-foreground">Hash</dt>
        <dd className={data.hashValid ? 'text-green-500' : 'text-red-500'}>
          {data.hashValid ? '✓ Valid' : '✗ Mismatch'}
        </dd>

        <dt className="text-muted-foreground">Signature</dt>
        <dd className={data.signatureValid ? 'text-green-500' : 'text-red-500'}>
          {data.signatureValid ? '✓ Valid' : '✗ Invalid'}
        </dd>

        {data.capturedAt && (
          <>
            <dt className="text-muted-foreground">Captured</dt>
            <dd className="text-foreground">{new Date(data.capturedAt).toLocaleString()}</dd>
          </>
        )}
      </dl>
    </div>
  )
}

export default function PublicVerificationPage() {
  const [input, setInput] = useState('')
  const [mode, setMode] = useState<'captureId' | 'hash'>('captureId')
  const [state, setState] = useState<VerifyState>({ status: 'idle' })

  async function handleVerify() {
    if (!input.trim()) return
    setState({ status: 'loading' })
    try {
      const data = mode === 'hash'
        ? await api.publicVerifyByHash(input.trim())
        : await api.publicVerify(input.trim())
      setState({ status: 'success', data })
    } catch (err) {
      setState({ status: 'error', message: err instanceof Error ? err.message : 'Verification failed.' })
    }
  }

  return (
    <div className="mx-auto max-w-xl space-y-6 py-8">
      <div className="space-y-1">
        <h1 className="text-3xl font-bold text-foreground">Verify Answer Sheet</h1>
        <p className="text-muted-foreground">
          Enter a Capture ID or SHA-256 hash to verify authenticity and digital signature.
        </p>
      </div>

      <div className="flex gap-2 mb-1">
        {(['captureId', 'hash'] as const).map(m => (
          <button
            key={m}
            onClick={() => { setMode(m); setInput(''); setState({ status: 'idle' }) }}
            className={`px-3 py-1 rounded text-xs ${mode === m ? 'bg-primary text-primary-foreground' : 'border border-border text-muted-foreground hover:text-foreground'}`}
          >
            {m === 'captureId' ? 'Capture ID' : 'SHA-256 Hash'}
          </button>
        ))}
      </div>

      <div className="flex gap-2">
        <input
          type="text"
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && handleVerify()}
          placeholder={mode === 'hash' ? 'SHA-256 hash (64 hex chars)' : 'Capture ID (UUID)'}
          className="flex-1 rounded-lg border border-border bg-card px-4 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary"
        />
        <button
          onClick={handleVerify}
          disabled={state.status === 'loading'}
          className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
        >
          {state.status === 'loading'
            ? <Loader2 className="h-4 w-4 animate-spin" />
            : <Search className="h-4 w-4" />
          }
          Verify
        </button>
      </div>

      {state.status === 'error' && (
        <div role="alert" className="rounded-lg border border-red-500/40 bg-red-500/5 px-4 py-3 text-sm text-red-500">
          {state.message}
        </div>
      )}

      {state.status === 'success' && <ResultPanel data={state.data} />}
    </div>
  )
}
