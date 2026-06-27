import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import ImageViewer from '../components/ImageViewer'
import Pagination from '../components/Pagination'

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5083'

const STATUS_COLORS: Record<string, string> = {
  Verified: 'text-green-400 bg-green-900/30',
  Uploaded: 'text-blue-400 bg-blue-900/30',
  Created:  'text-yellow-400 bg-yellow-900/30',
  Tampered: 'text-red-400 bg-red-900/30',
}

export default function AnswerSheetsPage() {
  const [page, setPage] = useState(1)
  const PAGE_SIZE = 20

  const { data, isLoading } = useQuery({
    queryKey: ['answer-sheets', page],
    queryFn: () => api.getCaptures(page, PAGE_SIZE),
  })

  const [viewingId, setViewingId] = useState<string | null>(null)

  if (isLoading) {
    return <div className="p-8 text-center text-[#8B949E]">Loading...</div>
  }

  const captures = data?.captures ?? []

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
            {captures.map(c => (
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

        {captures.length === 0 && (
          <div className="p-8 text-center text-[#8B949E]">No answer sheets yet.</div>
        )}

        <Pagination
          page={page}
          totalPages={data?.totalPages ?? 1}
          onPageChange={setPage}
        />
      </div>
    </div>
  )
}
