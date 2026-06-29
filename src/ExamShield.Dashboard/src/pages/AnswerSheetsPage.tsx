import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import ImageViewer from '../components/ImageViewer'
import Pagination from '../components/Pagination'
import { useChainOfCustody, useFlagCaptureAsTampered } from '../hooks/useCaptures'
import { useAuth } from '../hooks/useAuth'
import { usePermissions } from '../hooks/usePermissions'

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5083'

const STATUSES = ['', 'Created', 'Uploaded', 'Verified', 'Tampered']

const STATUS_COLORS: Record<string, string> = {
  Verified: 'text-green-400 bg-green-900/30',
  Uploaded: 'text-blue-400 bg-blue-900/30',
  Created:  'text-yellow-400 bg-yellow-900/30',
  Tampered: 'text-red-400 bg-red-900/30',
}

export default function AnswerSheetsPage() {
  const { auth } = useAuth()
  const { canViewImage } = usePermissions(auth.role)
  const [page, setPage] = useState(1)
  const [examIdFilter, setExamIdFilter]       = useState('')
  const [statusFilter, setStatusFilter]       = useState('')
  const [deviceIdFilter, setDeviceIdFilter]   = useState('')
  const [studentIdFilter, setStudentIdFilter] = useState('')
  const PAGE_SIZE = 20

  const { data, isLoading } = useQuery({
    queryKey: ['answer-sheets', page, examIdFilter, statusFilter, deviceIdFilter, studentIdFilter],
    queryFn: () => api.getCaptures(
      page, PAGE_SIZE,
      examIdFilter   || undefined,
      statusFilter   || undefined,
      deviceIdFilter || undefined,
      studentIdFilter || undefined
    ),
  })

  const [viewingId,  setViewingId]  = useState<string | null>(null)
  const [chainId,      setChainId]      = useState<string | null>(null)
  const [flagReason,   setFlagReason]   = useState('')
  const [flagError,    setFlagError]    = useState<string | null>(null)
  const { data: chain, isLoading: chainLoading } = useChainOfCustody(chainId)
  const flagTampered = useFlagCaptureAsTampered()

  function handleFilterChange() {
    setPage(1)
  }

  return (
    <div className="space-y-5 pb-4">
      {/* Header */}
      <div className="glass-card px-6 py-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-bold text-foreground">Answer Sheets</h1>
            {data && (
              <p className="text-sm text-muted-foreground mt-0.5">{data.totalCount} total captures</p>
            )}
          </div>
        </div>
      </div>

      {/* Filters */}
      <div className="glass-card p-4">
        <div className="flex flex-wrap gap-3">
        <input
          value={examIdFilter}
          onChange={e => { setExamIdFilter(e.target.value); handleFilterChange() }}
          placeholder="Filter by Exam ID (UUID)"
          className="input-glass w-72"
        />
        <select
          value={statusFilter}
          onChange={e => { setStatusFilter(e.target.value); handleFilterChange() }}
          className="input-glass w-40"
        >
          {STATUSES.map(s => (
            <option key={s} value={s}>{s || 'All statuses'}</option>
          ))}
        </select>
        <input
          value={deviceIdFilter}
          onChange={e => { setDeviceIdFilter(e.target.value); handleFilterChange() }}
          placeholder="Filter by Device ID (UUID)"
          className="input-glass w-56"
        />
        <input
          value={studentIdFilter}
          onChange={e => { setStudentIdFilter(e.target.value); handleFilterChange() }}
          placeholder="Filter by Student ID (UUID)"
          className="input-glass w-56"
        />
        {(examIdFilter || statusFilter || deviceIdFilter || studentIdFilter) && (
          <button
            onClick={() => { setExamIdFilter(''); setStatusFilter(''); setDeviceIdFilter(''); setStudentIdFilter(''); setPage(1) }}
            className="text-sm text-muted-foreground hover:text-foreground px-2 transition-colors"
          >
            Clear
          </button>
        )}
        <button
          onClick={() => api.exportCaptures(examIdFilter || undefined, statusFilter || undefined)
            .then(blob => {
              const url = URL.createObjectURL(blob)
              const a = document.createElement('a')
              a.href = url
              a.download = `captures-${Date.now()}.csv`
              a.click()
              URL.revokeObjectURL(url)
            })}
          className="btn-glass ml-auto text-xs px-4 py-2"
        >
          Export CSV
        </button>
        </div>
      </div>

      {viewingId && (
        <div className="glass-card p-4">
          <div className="flex items-center justify-between mb-3">
            <span className="text-muted-foreground text-sm font-mono">{viewingId}</span>
            <button
              onClick={() => setViewingId(null)}
              className="btn-glass text-xs px-3 py-1.5"
            >
              Close
            </button>
          </div>
          <ImageViewer
            src={`${BASE_URL}/captures/${viewingId}/image`}
            alt="Answer sheet"
          />
        </div>
      )}

      {isLoading && (
        <div role="status" aria-label="Loading" className="glass-card p-12 text-center text-muted-foreground">
          <div className="inline-block h-5 w-5 rounded-full border-2 border-border border-t-primary animate-spin" />
        </div>
      )}

      {!isLoading && (
        <div className="glass-card overflow-hidden">
          <table className="glass-table w-full">
            <thead>
              <tr>
                <th>Capture ID</th>
                <th>Student ID</th>
                <th>Exam ID</th>
                <th>Status</th>
                <th>Captured At</th>
                <th>Image</th>
              </tr>
            </thead>
            <tbody>
              {(data?.captures ?? []).map(c => (
                <tr key={c.captureId}>
                  <td className="font-mono text-xs text-muted-foreground">
                    {c.captureId.slice(0, 8)}…
                  </td>
                  <td className="font-mono text-xs text-foreground">{c.studentId}</td>
                  <td className="font-mono text-xs text-muted-foreground">
                    {c.examId.slice(0, 8)}…
                  </td>
                  <td>
                    <span className={`text-xs font-semibold px-2.5 py-0.5 rounded-full ${STATUS_COLORS[c.status] ?? 'text-muted-foreground bg-muted'}`}>
                      {c.status}
                    </span>
                  </td>
                  <td className="text-xs text-muted-foreground">
                    {new Date(c.capturedAt).toLocaleString()}
                  </td>
                  <td>
                    <div className="flex gap-2">
                      {c.storageKey && canViewImage && (
                        <button
                          onClick={() => setViewingId(viewingId === c.captureId ? null : c.captureId)}
                          className="text-xs px-2.5 py-1 rounded-lg text-primary transition-colors hover:bg-primary/10"
                        >
                          View Image
                        </button>
                      )}
                      {c.storageKey && !canViewImage && (
                        <span className="text-xs px-2 py-1 text-muted-foreground flex items-center gap-1 select-none" title="Your role does not permit viewing answer sheet images">
                          <svg xmlns="http://www.w3.org/2000/svg" className="h-3 w-3" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                            <path fillRule="evenodd" d="M5 9V7a5 5 0 0110 0v2a2 2 0 012 2v5a2 2 0 01-2 2H5a2 2 0 01-2-2v-5a2 2 0 012-2zm8-2v2H7V7a3 3 0 016 0z" clipRule="evenodd" />
                          </svg>
                          Restricted
                        </span>
                      )}
                      <button
                        onClick={() => setChainId(chainId === c.captureId ? null : c.captureId)}
                        className="text-xs px-2.5 py-1 rounded-lg text-violet-400 transition-colors hover:bg-violet-500/10"
                      >
                        Chain
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {(data?.captures ?? []).length === 0 && (
            <div className="p-8 text-center text-muted-foreground">No answer sheets match the current filters.</div>
          )}

          <Pagination
            page={page}
            totalPages={data?.totalPages ?? 1}
            onPageChange={setPage}
          />
        </div>
      )}

      {/* Chain of Custody panel */}
      {chainId && (
        <div className="glass-card p-5" style={{ borderColor: 'rgba(167,139,250,0.3)' }}>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-sm font-semibold text-violet-400">Chain of Custody</h2>
            <button onClick={() => setChainId(null)} className="text-xs text-muted-foreground hover:text-foreground transition-colors">✕ Close</button>
          </div>
          {chainLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
          {chain && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-2 text-xs">
                <div><span className="text-muted-foreground">Status</span><br/><span className="font-mono">{chain.status}</span></div>
                <div><span className="text-muted-foreground">Page</span><br/><span className="font-mono">{chain.pageNumber}</span></div>
                <div className="col-span-2"><span className="text-muted-foreground">Hash</span><br/><span className="font-mono text-[10px] break-all">{chain.hashHex}</span></div>
                {chain.ocrResult && <div><span className="text-muted-foreground">OCR Confidence</span><br/><span className="font-mono">{(chain.ocrResult.overallConfidence * 100).toFixed(1)}%</span></div>}
                {chain.score && <div><span className="text-muted-foreground">Score</span><br/><span className="font-mono">{chain.score.correctAnswers}/{chain.score.totalQuestions} ({chain.score.percentage.toFixed(1)}%)</span></div>}
              </div>
              {chain.status !== 'Tampered' && (
                <div className="border-t border-red-900/30 pt-3 space-y-2">
                  <p className="text-xs font-medium text-red-400">Flag as Tampered</p>
                  <div className="flex gap-2">
                    <input
                      value={flagReason}
                      onChange={e => setFlagReason(e.target.value)}
                      placeholder="Reason for flagging (required)"
                      className="flex-1 rounded border border-red-800/40 px-2 py-1 text-xs bg-background"
                    />
                    <button
                      disabled={!flagReason.trim() || flagTampered.isPending}
                      onClick={() =>
                        flagTampered.mutate(
                          { captureId: chainId!, reason: flagReason },
                          {
                            onSuccess: () => { setFlagReason(''); setFlagError(null) },
                            onError: () => setFlagError('Failed — capture may already be tampered'),
                          }
                        )
                      }
                      className="px-3 py-1 rounded bg-red-900/50 text-red-300 text-xs hover:bg-red-900/70 disabled:opacity-40"
                    >
                      {flagTampered.isPending ? '…' : 'Flag'}
                    </button>
                  </div>
                  {flagError && <p className="text-xs text-red-400">{flagError}</p>}
                </div>
              )}

              <div>
                <p className="text-xs text-muted-foreground mb-2">Audit Trail ({chain.auditTrail.length} events)</p>
                <ol className="relative border-l border-purple-800/40 ml-2 space-y-2">
                  {chain.auditTrail.map((a, i) => (
                    <li key={i} className="ml-4 text-xs">
                      <span className="absolute -left-1.5 mt-0.5 h-3 w-3 rounded-full bg-purple-700" />
                      <span className="font-medium text-purple-300">{a.action}</span>
                      <span className="ml-2 text-muted-foreground">{new Date(a.occurredAt).toLocaleTimeString()}</span>
                      {a.reason && <span className="ml-2 text-muted-foreground">— {a.reason}</span>}
                    </li>
                  ))}
                </ol>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
