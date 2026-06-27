import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type ReviewRequestItem } from '../api/client'
import { CheckCircle2, XCircle, Clock } from 'lucide-react'
import { cn } from '../lib/utils'

const STATUS_STYLES: Record<string, string> = {
  Pending:     'bg-yellow-500/10 text-yellow-400',
  UnderReview: 'bg-blue-500/10 text-blue-400',
  Resolved:    'bg-green-500/10 text-green-500',
  Rejected:    'bg-red-500/10 text-red-400',
}

const STATUS_TABS = [
  { label: 'All',          value: undefined        },
  { label: 'Pending',      value: 'Pending'        },
  { label: 'Under Review', value: 'UnderReview'    },
  { label: 'Resolved',     value: 'Resolved'       },
  { label: 'Rejected',     value: 'Rejected'       },
]

function ActionPanel({ rr, onDone }: { rr: ReviewRequestItem; onDone: () => void }) {
  const qc = useQueryClient()
  const [note, setNote] = useState('')
  const [mode, setMode] = useState<'idle' | 'resolve' | 'reject'>('idle')

  const resolve = useMutation({
    mutationFn: () => api.resolveReviewRequest(rr.reviewRequestId, note),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['admin-review-requests'] }); onDone() },
  })
  const reject = useMutation({
    mutationFn: () => api.rejectReviewRequest(rr.reviewRequestId, note),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['admin-review-requests'] }); onDone() },
  })

  return (
    <div className="mt-3 space-y-2">
      {mode !== 'idle' && (
        <textarea
          value={note}
          onChange={e => setNote(e.target.value)}
          placeholder={mode === 'resolve' ? 'Resolution note…' : 'Rejection reason…'}
          rows={2}
          className="w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary"
        />
      )}
      <div className="flex gap-2">
        {mode === 'idle' && (
          <>
            <button onClick={() => setMode('resolve')}
              className="flex items-center gap-1 rounded-lg border border-green-500 px-3 py-1 text-xs text-green-500 hover:bg-green-500/10">
              <CheckCircle2 className="h-3 w-3" /> Resolve
            </button>
            <button onClick={() => setMode('reject')}
              className="flex items-center gap-1 rounded-lg border border-red-500 px-3 py-1 text-xs text-red-400 hover:bg-red-500/10">
              <XCircle className="h-3 w-3" /> Reject
            </button>
          </>
        )}
        {mode === 'resolve' && (
          <>
            <button onClick={() => resolve.mutate()} disabled={!note.trim() || resolve.isPending}
              className="rounded-lg bg-green-500 px-3 py-1 text-xs text-white disabled:opacity-50">
              {resolve.isPending ? 'Saving…' : 'Confirm Resolve'}
            </button>
            <button onClick={() => { setMode('idle'); setNote('') }}
              className="text-xs text-muted-foreground hover:text-foreground">Cancel</button>
          </>
        )}
        {mode === 'reject' && (
          <>
            <button onClick={() => reject.mutate()} disabled={!note.trim() || reject.isPending}
              className="rounded-lg bg-red-500 px-3 py-1 text-xs text-white disabled:opacity-50">
              {reject.isPending ? 'Saving…' : 'Confirm Reject'}
            </button>
            <button onClick={() => { setMode('idle'); setNote('') }}
              className="text-xs text-muted-foreground hover:text-foreground">Cancel</button>
          </>
        )}
      </div>
    </div>
  )
}

function RequestCard({ rr }: { rr: ReviewRequestItem }) {
  const [expanded, setExpanded] = useState(false)
  const isPending = rr.status === 'Pending' || rr.status === 'UnderReview'

  return (
    <div className="rounded-xl border border-border bg-card p-4">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 flex-wrap">
            <span className={cn('rounded-full px-2 py-0.5 text-xs font-medium',
              STATUS_STYLES[rr.status] ?? 'bg-muted text-muted-foreground')}>
              {rr.status}
            </span>
            <span className="font-mono text-xs text-muted-foreground">
              {rr.reviewRequestId.slice(0, 8)}…
            </span>
            <span className="text-xs text-muted-foreground">
              {new Date(rr.createdAt).toLocaleString()}
            </span>
          </div>
          <p className="mt-2 text-sm text-foreground">{rr.reason}</p>
          <p className="mt-1 font-mono text-xs text-muted-foreground">
            Capture: {rr.captureId.slice(0, 8)}… · Student: {rr.studentId.slice(0, 8)}…
          </p>
          {rr.resolutionNote && (
            <p className="mt-1 text-xs text-muted-foreground">Note: {rr.resolutionNote}</p>
          )}
        </div>
        {isPending && (
          <button onClick={() => setExpanded(v => !v)}
            className="shrink-0 rounded-lg border border-border px-3 py-1 text-xs text-foreground hover:bg-muted">
            {expanded ? 'Close' : 'Act'}
          </button>
        )}
      </div>
      {expanded && <ActionPanel rr={rr} onDone={() => setExpanded(false)} />}
    </div>
  )
}

export default function ReviewRequestsPage() {
  const [activeTab, setActiveTab] = useState<string | undefined>('Pending')

  const { data, isLoading } = useQuery({
    queryKey: ['admin-review-requests', activeTab],
    queryFn: () => api.getAllReviewRequests(activeTab),
  })

  const items = data?.items ?? []
  const pendingCount = items.filter(r => r.status === 'Pending').length

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-foreground">Review Requests</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Student-submitted requests to re-evaluate OCR results.
        </p>
      </div>

      <div className="flex gap-1 rounded-lg border border-border bg-muted/30 p-1 w-fit">
        {STATUS_TABS.map(tab => (
          <button
            key={tab.label}
            onClick={() => setActiveTab(tab.value)}
            className={cn(
              'rounded-md px-3 py-1.5 text-xs font-medium transition-colors',
              activeTab === tab.value
                ? 'bg-card text-foreground shadow-sm'
                : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {tab.label}
            {tab.value === 'Pending' && pendingCount > 0 && activeTab !== 'Pending' && (
              <span className="ml-1.5 rounded-full bg-yellow-500/20 px-1.5 text-yellow-400">
                {pendingCount}
              </span>
            )}
          </button>
        ))}
      </div>

      {isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}

      {!isLoading && items.length === 0 && (
        <div className="flex h-32 items-center justify-center rounded-xl border border-dashed border-border text-muted-foreground">
          <Clock className="mr-2 h-4 w-4" />
          No {activeTab?.toLowerCase() ?? ''} review requests.
        </div>
      )}

      <div className="space-y-3">
        {items.map(rr => <RequestCard key={rr.reviewRequestId} rr={rr} />)}
      </div>
    </div>
  )
}
