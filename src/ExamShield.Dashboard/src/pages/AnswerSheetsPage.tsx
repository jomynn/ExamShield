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
    <div className="p-8">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-white">Answer Sheets</h1>
        {data && (
          <span className="text-sm text-muted-foreground">
            {data.totalCount} total
          </span>
        )}
      </div>

      {/* Filters */}
      <div className="flex gap-3 mb-4">
        <input
          value={examIdFilter}
          onChange={e => { setExamIdFilter(e.target.value); handleFilterChange() }}
          placeholder="Filter by Exam ID (UUID)"
          className="rounded border border-[#30363D] bg-[#161B22] px-3 py-1.5 text-sm text-white placeholder-[#8B949E] w-72"
        />
        <select
          value={statusFilter}
          onChange={e => { setStatusFilter(e.target.value); handleFilterChange() }}
          className="rounded border border-[#30363D] bg-[#161B22] px-3 py-1.5 text-sm text-white"
        >
          {STATUSES.map(s => (
            <option key={s} value={s}>{s || 'All statuses'}</option>
          ))}
        </select>
        <input
          value={deviceIdFilter}
          onChange={e => { setDeviceIdFilter(e.target.value); handleFilterChange() }}
          placeholder="Filter by Device ID (UUID)"
          className="rounded border border-[#30363D] bg-[#161B22] px-3 py-1.5 text-sm text-white placeholder-[#8B949E] w-64"
        />
        <input
          value={studentIdFilter}
          onChange={e => { setStudentIdFilter(e.target.value); handleFilterChange() }}
          placeholder="Filter by Student ID (UUID)"
          className="rounded border border-[#30363D] bg-[#161B22] px-3 py-1.5 text-sm text-white placeholder-[#8B949E] w-64"
        />
        {(examIdFilter || statusFilter || deviceIdFilter || studentIdFilter) && (
          <button
            onClick={() => { setExamIdFilter(''); setStatusFilter(''); setDeviceIdFilter(''); setStudentIdFilter(''); setPage(1) }}
            className="text-sm text-[#8B949E] hover:text-white px-2"
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
          className="ml-auto px-3 py-1.5 rounded border border-[#30363D] text-sm text-[#8B949E] hover:text-white hover:border-[#58A6FF]"
        >
          Export CSV
        </button>
      </div>

      {viewingId && (
        <div className="mb-6">
          <div className="flex items-center justify-between mb-3">
            <span className="text-[#8B949E] text-sm font-mono">{viewingId}</span>
            <button
              onClick={() => setViewingId(null)}
              className="text-[#8B949E] hover:text-white text-sm px-3 py-1 rounded border border-[#30363D] hover:border-[#8B949E]"
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

      {isLoading && <div className="p-8 text-center text-[#8B949E]">Loading...</div>}

      {!isLoading && (
        <div className="bg-[#161B22] rounded-xl border border-[#30363D] overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[#30363D] text-[#8B949E] text-left">
                <th className="px-4 py-3">Capture ID</th>
                <th className="px-4 py-3">Student ID</th>
                <th className="px-4 py-3">Exam ID</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Captured At</th>
                <th className="px-4 py-3">Image</th>
              </tr>
            </thead>
            <tbody>
              {(data?.captures ?? []).map(c => (
                <tr
                  key={c.captureId}
                  className="border-b border-[#21262D] hover:bg-[#21262D]/50"
                >
                  <td className="px-4 py-3 font-mono text-xs text-[#8B949E]">
                    {c.captureId.slice(0, 8)}…
                  </td>
                  <td className="px-4 py-3 text-white font-mono text-xs">{c.studentId}</td>
                  <td className="px-4 py-3 text-[#8B949E] font-mono text-xs">
                    {c.examId.slice(0, 8)}…
                  </td>
                  <td className="px-4 py-3">
                    <span className={`text-xs font-semibold px-2 py-0.5 rounded ${STATUS_COLORS[c.status] ?? 'text-gray-400 bg-gray-900/30'}`}>
                      {c.status}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-[#8B949E] text-xs">
                    {new Date(c.capturedAt).toLocaleString()}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex gap-2">
                      {c.storageKey && canViewImage && (
                        <button
                          onClick={() => setViewingId(viewingId === c.captureId ? null : c.captureId)}
                          className="text-xs px-2 py-1 bg-[#21262D] hover:bg-[#30363D] text-[#00BFFF] rounded"
                        >
                          View Image
                        </button>
                      )}
                      {c.storageKey && !canViewImage && (
                        <span className="text-xs px-2 py-1 text-[#8B949E] flex items-center gap-1 select-none" title="Your role does not permit viewing answer sheet images">
                          <svg xmlns="http://www.w3.org/2000/svg" className="h-3 w-3" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                            <path fillRule="evenodd" d="M5 9V7a5 5 0 0110 0v2a2 2 0 012 2v5a2 2 0 01-2 2H5a2 2 0 01-2-2v-5a2 2 0 012-2zm8-2v2H7V7a3 3 0 016 0z" clipRule="evenodd" />
                          </svg>
                          Restricted
                        </span>
                      )}
                      <button
                        onClick={() => setChainId(chainId === c.captureId ? null : c.captureId)}
                        className="text-xs px-2 py-1 bg-[#21262D] hover:bg-[#30363D] text-purple-400 rounded"
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
            <div className="p-8 text-center text-[#8B949E]">No answer sheets match the current filters.</div>
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
        <div className="mt-6 rounded-lg border border-purple-800/40 bg-[#161B22] p-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-sm font-semibold text-purple-300">Chain of Custody</h2>
            <button onClick={() => setChainId(null)} className="text-xs text-muted-foreground hover:text-white">✕ Close</button>
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
