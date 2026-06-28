import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, type PendingReviewItem, type ReviewDetailResponse, type OcrAnswer } from '../api/client'
import { AlertTriangle, CheckCircle, Send, ThumbsUp, ThumbsDown, ArrowUpCircle } from 'lucide-react'
import { cn } from '../lib/utils'

const LOW_CONFIDENCE_THRESHOLD = 0.6

const STATUS_VARIANT: Record<string, string> = {
  Pending:   'bg-yellow-500/10 text-yellow-500',
  Completed: 'bg-blue-500/10 text-blue-400',
  Approved:  'bg-green-500/10 text-green-500',
  Rejected:  'bg-red-500/10 text-red-400',
  Escalated: 'bg-orange-500/10 text-orange-400',
}

function confidenceColor(confidence: number) {
  if (confidence >= LOW_CONFIDENCE_THRESHOLD) return 'text-green-500'
  return 'text-yellow-500'
}

function AnswerRow({ answer, selected, onSelect, readOnly }: {
  answer: OcrAnswer
  selected: string
  onSelect?: (text: string) => void
  readOnly?: boolean
}) {
  const isLow = answer.confidence < LOW_CONFIDENCE_THRESHOLD
  const pct = Math.round(answer.confidence * 100)
  return (
    <tr className="border-b border-border last:border-0">
      <td className="px-4 py-3 font-medium text-foreground">Q{answer.questionNumber}</td>
      <td className="px-4 py-3">
        {readOnly ? (
          <span className="font-mono text-sm text-foreground">{selected}</span>
        ) : (
          <input
            type="text"
            value={selected}
            onChange={e => onSelect?.(e.target.value)}
            className="w-24 rounded border border-border bg-card px-2 py-1 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-primary"
            placeholder={answer.text}
          />
        )}
      </td>
      <td className={cn('px-4 py-3 text-sm', confidenceColor(answer.confidence))}>
        {pct}%{isLow && <AlertTriangle className="ml-1 inline h-3 w-3" />}
      </td>
    </tr>
  )
}

function ReviewDetailPanel({ reviewId, detail, onDone }: {
  reviewId: string
  detail: ReviewDetailResponse
  onDone: () => void
}) {
  const qc = useQueryClient()
  const [overrides, setOverrides] = useState<Record<number, string>>(() =>
    Object.fromEntries(detail.ocrAnswers.map(a => [a.questionNumber, a.text]))
  )
  const [reasonMode, setReasonMode] = useState<'reject' | 'escalate' | null>(null)
  const [reason, setReason] = useState('')

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['reviews'] })
    qc.invalidateQueries({ queryKey: ['review', reviewId] })
    onDone()
  }

  const { mutate: submit, isPending: isSubmitting } = useMutation({
    mutationFn: (answers: { questionNumber: number; text: string }[]) =>
      api.submitReview(reviewId, answers),
    onSuccess: invalidate,
  })

  const { mutate: approve, isPending: isApproving } = useMutation({
    mutationFn: () => api.approveReview(reviewId),
    onSuccess: invalidate,
  })

  const { mutate: reject, isPending: isRejecting } = useMutation({
    mutationFn: (r: string) => api.rejectReview(reviewId, r),
    onSuccess: invalidate,
  })

  const { mutate: escalate, isPending: isEscalating } = useMutation({
    mutationFn: (r: string) => api.escalateReview(reviewId, r),
    onSuccess: invalidate,
  })

  const isBusy = isSubmitting || isApproving || isRejecting || isEscalating
  const isPending   = detail.status === 'Pending'
  const isCompleted = detail.status === 'Completed'
  const isTerminal  = !isPending && !isCompleted

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-foreground">OCR Answers</h2>
        <div className="flex items-center gap-3">
          <span className={cn('rounded-full px-2 py-0.5 text-xs font-medium', STATUS_VARIANT[detail.status] ?? 'bg-muted text-muted-foreground')}>
            {detail.status}
          </span>
          <span className="text-xs text-muted-foreground font-mono truncate max-w-[180px]">{detail.captureId}</span>
        </div>
      </div>

      <div className="overflow-hidden rounded-xl border border-border">
        <table className="w-full text-sm">
          <thead className="bg-muted/50">
            <tr>
              {['Question', 'Answer', 'Confidence'].map(h => (
                <th key={h} className="px-4 py-2 text-left text-xs font-medium text-muted-foreground">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {detail.ocrAnswers.map(a => (
              <AnswerRow
                key={a.questionNumber}
                answer={a}
                selected={overrides[a.questionNumber] ?? a.text}
                onSelect={isPending ? v => setOverrides(prev => ({ ...prev, [a.questionNumber]: v })) : undefined}
                readOnly={!isPending}
              />
            ))}
          </tbody>
        </table>
      </div>

      {/* Reviewer submit */}
      {isPending && (
        <button
          onClick={() => submit(detail.ocrAnswers.map(a => ({ questionNumber: a.questionNumber, text: overrides[a.questionNumber] ?? a.text })))}
          disabled={isBusy}
          className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
        >
          <Send className="h-4 w-4" />
          {isSubmitting ? 'Submitting…' : 'Submit Review'}
        </button>
      )}

      {/* Supervisor actions */}
      {isCompleted && (
        <div className="space-y-3">
          {reasonMode ? (
            <div className="space-y-2">
              <input
                autoFocus
                value={reason}
                onChange={e => setReason(e.target.value)}
                placeholder={reasonMode === 'reject' ? 'Rejection reason…' : 'Escalation reason…'}
                className="w-full rounded-lg border border-border bg-card px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-primary"
              />
              <div className="flex gap-2">
                <button
                  onClick={() => {
                    if (!reason.trim()) return
                    reasonMode === 'reject' ? reject(reason) : escalate(reason)
                    setReason('')
                    setReasonMode(null)
                  }}
                  disabled={isBusy || !reason.trim()}
                  className={cn(
                    'rounded-lg px-4 py-2 text-sm font-medium text-white hover:opacity-90 disabled:opacity-50',
                    reasonMode === 'reject' ? 'bg-red-600' : 'bg-orange-500'
                  )}
                >
                  {isBusy ? 'Saving…' : (reasonMode === 'reject' ? 'Confirm Reject' : 'Confirm Escalate')}
                </button>
                <button
                  onClick={() => { setReasonMode(null); setReason('') }}
                  className="rounded-lg border border-border px-4 py-2 text-sm text-muted-foreground hover:bg-muted/40"
                >
                  Cancel
                </button>
              </div>
            </div>
          ) : (
            <div className="flex gap-2">
              <button
                onClick={() => approve()}
                disabled={isBusy}
                className="inline-flex items-center gap-2 rounded-lg bg-green-600 px-4 py-2 text-sm font-medium text-white hover:opacity-90 disabled:opacity-50"
              >
                <ThumbsUp className="h-4 w-4" />
                {isApproving ? 'Approving…' : 'Approve'}
              </button>
              <button
                onClick={() => setReasonMode('reject')}
                disabled={isBusy}
                className="inline-flex items-center gap-2 rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white hover:opacity-90 disabled:opacity-50"
              >
                <ThumbsDown className="h-4 w-4" />
                Reject
              </button>
              <button
                onClick={() => setReasonMode('escalate')}
                disabled={isBusy}
                className="inline-flex items-center gap-2 rounded-lg bg-orange-500 px-4 py-2 text-sm font-medium text-white hover:opacity-90 disabled:opacity-50"
              >
                <ArrowUpCircle className="h-4 w-4" />
                Escalate
              </button>
            </div>
          )}
        </div>
      )}

      {isTerminal && (
        <p className="text-sm text-muted-foreground">
          This review is <span className="font-medium">{detail.status.toLowerCase()}</span> and requires no further action.
        </p>
      )}
    </div>
  )
}

export default function ManualReviewPage() {
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const { data: listData, isLoading } = useQuery({
    queryKey: ['reviews'],
    queryFn: api.getPendingReviews,
    refetchInterval: 15_000,
  })

  const { data: detail } = useQuery({
    queryKey: ['review', selectedId],
    queryFn: () => api.getReviewDetail(selectedId!),
    enabled: !!selectedId,
  })

  const pendingCount = listData?.reviews.length ?? 0

  return (
    <div className="flex h-full gap-6">
      {/* Left: pending list */}
      <div className="w-80 shrink-0 space-y-4">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold text-foreground">Manual Review</h1>
          {listData && (
            <span className="rounded-full bg-yellow-500/10 px-2 py-0.5 text-xs font-medium text-yellow-500">
              {pendingCount} pending
            </span>
          )}
        </div>

        {isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}

        {listData && (
          <div className="overflow-hidden rounded-xl border border-border">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="px-4 py-2 text-left text-xs font-medium text-muted-foreground">Capture</th>
                  <th className="px-4 py-2 text-left text-xs font-medium text-muted-foreground">Created</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {listData.reviews.map((r: PendingReviewItem) => (
                  <tr
                    key={r.reviewId}
                    onClick={() => setSelectedId(r.reviewId)}
                    className={cn(
                      'cursor-pointer transition-colors hover:bg-muted/40',
                      selectedId === r.reviewId && 'bg-primary/10'
                    )}
                  >
                    <td className="px-4 py-3 font-mono text-xs text-foreground">{r.captureId.length > 8 ? `${r.captureId.slice(0, 8)}…` : r.captureId}</td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">
                      {new Date(r.createdAt).toLocaleDateString()}
                    </td>
                  </tr>
                ))}
                {listData.reviews.length === 0 && (
                  <tr>
                    <td colSpan={2} className="px-4 py-6 text-center text-muted-foreground">
                      <CheckCircle className="mx-auto mb-1 h-5 w-5 text-green-500" />
                      All reviews complete
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Right: detail panel */}
      <div className="flex-1">
        {detail && selectedId ? (
          <ReviewDetailPanel
            reviewId={selectedId}
            detail={detail}
            onDone={() => setSelectedId(null)}
          />
        ) : (
          <div className="flex h-40 items-center justify-center rounded-xl border border-dashed border-border text-muted-foreground">
            Select a review to begin
          </div>
        )}
      </div>
    </div>
  )
}
