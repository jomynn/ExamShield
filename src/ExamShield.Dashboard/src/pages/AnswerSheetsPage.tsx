import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import ImageViewer from '../components/ImageViewer'
import Pagination from '../components/Pagination'

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5083'

const STATUSES = ['', 'Created', 'Uploaded', 'Verified', 'Tampered']

const STATUS_COLORS: Record<string, string> = {
  Verified: 'text-green-400 bg-green-900/30',
  Uploaded: 'text-blue-400 bg-blue-900/30',
  Created:  'text-yellow-400 bg-yellow-900/30',
  Tampered: 'text-red-400 bg-red-900/30',
}

export default function AnswerSheetsPage() {
  const [page, setPage] = useState(1)
  const [examIdFilter, setExamIdFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const PAGE_SIZE = 20

  const { data, isLoading } = useQuery({
    queryKey: ['answer-sheets', page, examIdFilter, statusFilter],
    queryFn: () => api.getCaptures(
      page, PAGE_SIZE,
      examIdFilter || undefined,
      statusFilter || undefined
    ),
  })

  const [viewingId, setViewingId] = useState<string | null>(null)

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
        {(examIdFilter || statusFilter) && (
          <button
            onClick={() => { setExamIdFilter(''); setStatusFilter(''); setPage(1) }}
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
                    {c.storageKey && (
                      <button
                        onClick={() => setViewingId(viewingId === c.captureId ? null : c.captureId)}
                        className="text-xs px-2 py-1 bg-[#21262D] hover:bg-[#30363D] text-[#00BFFF] rounded"
                      >
                        View Image
                      </button>
                    )}
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
    </div>
  )
}
