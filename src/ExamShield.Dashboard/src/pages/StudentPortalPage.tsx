import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { FileDown } from 'lucide-react'
import { api } from '../api/client'

function pctColor(p: number) {
  if (p >= 80) return 'text-green-500'
  if (p >= 60) return 'text-yellow-500'
  return 'text-red-500'
}

export default function StudentPortalPage() {
  const [inputId, setInputId] = useState('')
  const [studentId, setStudentId] = useState<string | null>(null)
  const [disputeCaptureId, setDisputeCaptureId] = useState('')
  const [disputeReason, setDisputeReason] = useState('')
  const [disputeSuccess, setDisputeSuccess] = useState(false)

  const submitReview = useMutation({
    mutationFn: ({ captureId, reason }: { captureId: string; reason: string }) =>
      api.submitReviewRequest(studentId!, captureId, reason),
    onSuccess: () => {
      setDisputeSuccess(true)
      setDisputeCaptureId('')
      setDisputeReason('')
    },
  })

  const { data, isFetching } = useQuery({
    queryKey: ['student-results', studentId],
    queryFn: () => api.getStudentResults(studentId!),
    enabled: studentId !== null,
  })

  const handleLookUp = () => {
    const trimmed = inputId.trim()
    if (trimmed) setStudentId(trimmed)
  }

  const handleDownloadCertificate = async (captureId: string, examName: string) => {
    const blob = await api.downloadCertificate(captureId)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `certificate-${examName.replace(/\s+/g, '-')}.pdf`
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="p-6 space-y-6 max-w-3xl">
      <h1 className="text-2xl font-bold">Student Portal</h1>

      {/* Search */}
      <div className="flex gap-2">
        <input
          type="text"
          placeholder="Enter Student ID (UUID)"
          value={inputId}
          onChange={e => setInputId(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && handleLookUp()}
          className="flex-1 rounded border border-border bg-background px-3 py-2 text-sm text-foreground"
        />
        <button
          onClick={handleLookUp}
          disabled={isFetching}
          className="px-4 py-2 text-sm rounded bg-primary text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
        >
          {isFetching ? 'Looking up…' : 'Look Up'}
        </button>
      </div>

      {/* Dispute / Review Request */}
      {studentId && (
        <div className="rounded-lg border border-border p-4 space-y-3 max-w-lg">
          <h2 className="font-semibold text-sm">Submit a Review Request</h2>
          {disputeSuccess && (
            <p className="text-xs text-green-500">
              Review request submitted successfully.
            </p>
          )}
          <input
            type="text"
            placeholder="Capture ID (UUID)"
            value={disputeCaptureId}
            onChange={e => { setDisputeCaptureId(e.target.value); setDisputeSuccess(false) }}
            className="w-full rounded border border-border bg-background px-3 py-2 text-sm"
          />
          <textarea
            placeholder="Reason for dispute (e.g. OCR misread question 5)"
            value={disputeReason}
            onChange={e => setDisputeReason(e.target.value)}
            rows={3}
            className="w-full rounded border border-border bg-background px-3 py-2 text-sm resize-none"
          />
          <button
            onClick={() => submitReview.mutate({ captureId: disputeCaptureId, reason: disputeReason })}
            disabled={submitReview.isPending || !disputeCaptureId || !disputeReason}
            className="px-4 py-2 text-sm rounded bg-primary text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
          >
            {submitReview.isPending ? 'Submitting…' : 'Submit Request'}
          </button>
        </div>
      )}

      {/* Results */}
      {data && (
        <div className="space-y-4">
          <div>
            <p className="text-xs text-muted-foreground">Student ID</p>
            <p className="font-mono text-sm text-foreground">{data.studentId}</p>
          </div>

          {data.results.length === 0 ? (
            <p className="text-muted-foreground text-sm">No scored results found for this student.</p>
          ) : (
            <table className="w-full text-sm border-collapse">
              <thead>
                <tr className="border-b text-left">
                  <th className="py-2 pr-4">Exam</th>
                  <th className="py-2 pr-4 text-right">Score</th>
                  <th className="py-2 pr-4 text-right">%</th>
                  <th className="py-2 pr-4">Verified</th>
                  <th className="py-2 pr-4">Scored At</th>
                  <th className="py-2 pr-4">Hash</th>
                  <th className="py-2" />
                </tr>
              </thead>
              <tbody>
                {data.results.map(r => (
                  <tr key={r.scoreId} className="border-b hover:bg-muted/20">
                    <td className="py-2 pr-4 font-medium text-foreground">{r.examName}</td>
                    <td className="py-2 pr-4 text-right text-muted-foreground">
                      {r.correctAnswers}/{r.totalQuestions}
                    </td>
                    <td className={`py-2 pr-4 text-right font-bold ${pctColor(r.percentage)}`}>
                      {r.percentage.toFixed(1)}%
                    </td>
                    <td className="py-2 pr-4">
                      {r.isVerified ? (
                        <span className="text-xs font-semibold text-green-500">Verified ✓</span>
                      ) : (
                        <span className="text-xs text-muted-foreground">—</span>
                      )}
                    </td>
                    <td className="py-2 pr-4 text-xs text-muted-foreground">
                      {new Date(r.scoredAt).toLocaleDateString()}
                    </td>
                    <td className="py-2 pr-4 font-mono text-[10px] text-muted-foreground truncate max-w-[120px]">
                      {r.hashHex.substring(0, 16)}…
                    </td>
                    <td className="py-2">
                      <button
                        title="Download PDF certificate"
                        onClick={() => handleDownloadCertificate(r.captureId, r.examName)}
                        className="inline-flex items-center gap-1 rounded px-2 py-1 text-xs text-cyan-400 hover:bg-cyan-400/10 transition-colors"
                      >
                        <FileDown className="h-3 w-3" />
                        PDF
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  )
}
