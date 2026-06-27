import { useState } from 'react'
import { useScoringQueue, useScoreCapture, useBatchScore } from '../hooks/useScoring'

export default function ScoringPage() {
  const { data, isLoading } = useScoringQueue()
  const score = useScoreCapture()
  const batch = useBatchScore()

  const [batchExamId, setBatchExamId] = useState('')
  const [batchResult, setBatchResult] = useState<{ scored: number; skipped: number } | null>(null)

  if (isLoading) return <p>Loading...</p>

  const items = data?.items ?? []

  function handleBatch(e: React.FormEvent) {
    e.preventDefault()
    batch.mutate(batchExamId, {
      onSuccess: r => { setBatchResult(r); setBatchExamId('') },
    })
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center gap-3">
        <h1 className="text-2xl font-bold">Scoring</h1>
        <span className="text-sm text-muted-foreground">{items.length} pending</span>
      </div>

      {/* Batch score */}
      <div className="rounded-lg border p-4 space-y-3 max-w-md">
        <h2 className="text-sm font-semibold">Score All for Exam</h2>
        <form onSubmit={handleBatch} className="flex gap-2">
          <input
            value={batchExamId}
            onChange={e => setBatchExamId(e.target.value)}
            placeholder="Exam ID (UUID)"
            required
            className="flex-1 rounded border px-3 py-1.5 text-sm bg-background"
          />
          <button
            type="submit"
            disabled={batch.isPending}
            className="px-4 py-1.5 rounded bg-primary text-primary-foreground text-sm hover:bg-primary/90 disabled:opacity-50"
          >
            {batch.isPending ? 'Scoring…' : 'Score All'}
          </button>
        </form>
        {batchResult && (
          <p className="text-sm text-green-400">
            Scored {batchResult.scored} captures, skipped {batchResult.skipped}.
          </p>
        )}
        {batch.isError && (
          <p className="text-sm text-red-400">{String(batch.error)}</p>
        )}
      </div>

      {/* Individual queue */}
      {items.length === 0 ? (
        <p className="text-muted-foreground">No captures awaiting scoring.</p>
      ) : (
        <table className="w-full text-sm border-collapse">
          <thead>
            <tr className="border-b text-left">
              <th className="py-2 pr-4">Capture ID</th>
              <th className="py-2 pr-4">Exam ID</th>
              <th className="py-2 pr-4">Confidence</th>
              <th className="py-2 pr-4">Processed At</th>
              <th className="py-2" />
            </tr>
          </thead>
          <tbody>
            {items.map(item => (
              <tr key={item.captureId} className="border-b hover:bg-muted/30">
                <td className="py-2 pr-4 font-mono text-xs">{item.captureId.slice(0, 8)}…</td>
                <td className="py-2 pr-4 font-mono text-xs">{item.examId.slice(0, 8)}…</td>
                <td className="py-2 pr-4">
                  <span className={item.overallConfidence >= 0.85 ? 'text-green-400' : 'text-yellow-400'}>
                    {Math.round(item.overallConfidence * 100)}%
                  </span>
                </td>
                <td className="py-2 pr-4 text-muted-foreground text-xs">
                  {new Date(item.completedAt).toLocaleString()}
                </td>
                <td className="py-2">
                  <button
                    onClick={() => score.mutate(item.captureId)}
                    disabled={score.isPending}
                    className="px-3 py-1 text-xs rounded bg-primary text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
                  >
                    Score
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
