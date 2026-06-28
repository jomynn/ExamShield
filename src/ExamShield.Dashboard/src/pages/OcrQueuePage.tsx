import { useState } from 'react'
import { useOcrQueue, useTriggerOcr, useBatchOcr } from '../hooks/useOcrQueue'

export default function OcrQueuePage() {
  const { data, isLoading } = useOcrQueue()
  const trigger = useTriggerOcr()
  const batch   = useBatchOcr()

  const [batchExamId, setBatchExamId] = useState('')
  const [batchResult, setBatchResult] = useState<{ queued: number; skipped: number } | null>(null)

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
        <h1 className="text-2xl font-bold">OCR Queue</h1>
        <span className="text-sm text-muted-foreground">{items.length} pending</span>
      </div>

      {/* Batch trigger */}
      <div className="rounded-lg border p-4 space-y-3 max-w-md">
        <h2 className="text-sm font-semibold">Process All for Exam</h2>
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
            {batch.isPending ? 'Processing…' : 'Process All'}
          </button>
        </form>
        {batchResult && (
          <p className="text-sm text-green-400">
            Queued {batchResult.queued} captures, skipped {batchResult.skipped}.
          </p>
        )}
        {batch.isError && (
          <p className="text-sm text-red-400">{String(batch.error)}</p>
        )}
      </div>

      {/* Individual queue */}
      {items.length === 0 ? (
        <p className="text-muted-foreground">No captures awaiting OCR processing.</p>
      ) : (
        <table className="w-full text-sm border-collapse">
          <thead>
            <tr className="border-b text-left">
              <th className="py-2 pr-4">Capture ID</th>
              <th className="py-2 pr-4">Exam ID</th>
              <th className="py-2 pr-4">Student ID</th>
              <th className="py-2 pr-4">Uploaded At</th>
              <th className="py-2" />
            </tr>
          </thead>
          <tbody>
            {items.map(item => (
              <tr key={item.captureId} className="border-b hover:bg-muted/30">
                <td className="py-2 pr-4 font-mono text-xs">{item.captureId.length > 8 ? `${item.captureId.slice(0, 8)}…` : item.captureId}</td>
                <td className="py-2 pr-4 font-mono text-xs">{item.examId.length > 8 ? `${item.examId.slice(0, 8)}…` : item.examId}</td>
                <td className="py-2 pr-4 font-mono text-xs">{item.studentId.length > 8 ? `${item.studentId.slice(0, 8)}…` : item.studentId}</td>
                <td className="py-2 pr-4 text-xs">{new Date(item.uploadedAt).toLocaleString()}</td>
                <td className="py-2">
                  <button
                    onClick={() => trigger.mutate(item.captureId)}
                    disabled={trigger.isPending}
                    className="px-3 py-1 text-xs rounded bg-primary text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
                  >
                    Trigger OCR
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
