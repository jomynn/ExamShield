import { useState } from 'react'
import { useReportSummary, useExamReport } from '../hooks/useReports'
import { useResults } from '../hooks/useResults'
import { useAuditLog } from '../hooks/useAuditLog'
import { api } from '../api/client'

function StatBlock({ label, value, sub }: { label: string; value: string | number; sub?: string }) {
  return (
    <div className="rounded-lg border border-border bg-card p-5">
      <p className="text-xs text-muted-foreground uppercase tracking-wider">{label}</p>
      <p className="mt-1 text-3xl font-bold text-foreground">{value}</p>
      {sub && <p className="mt-0.5 text-xs text-muted-foreground">{sub}</p>}
    </div>
  )
}

function downloadCsv(filename: string, rows: string[][]) {
  const csv = rows.map(r => r.map(c => `"${String(c).replace(/"/g, '""')}"`).join(',')).join('\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

export default function ReportsPage() {
  const { data: summary, isLoading } = useReportSummary()
  const { data: resultsData } = useResults()
  const { data: auditData } = useAuditLog(1, 1000)
  const [examIdInput, setExamIdInput] = useState('')
  const [examId, setExamId] = useState<string | null>(null)
  const { data: examReport, isFetching: examFetching } = useExamReport(examId)

  if (isLoading) return <p>Loading...</p>

  const s = summary!

  const exportResults = () => {
    const rows = [
      ['Score ID', 'Capture ID', 'Exam ID', 'Student ID', 'Correct', 'Total', 'Percentage', 'Scored At'],
      ...(resultsData?.results ?? []).map(r => [
        r.scoreId, r.captureId, r.examId, r.studentId,
        String(r.correctAnswers), String(r.totalQuestions),
        `${r.percentage.toFixed(1)}%`, r.scoredAt,
      ]),
    ]
    downloadCsv('examshield-results.csv', rows)
  }

  const exportAudit = () => {
    const rows = [
      ['ID', 'Action', 'User ID', 'IP Address', 'Occurred At', 'Reason'],
      ...(auditData?.entries ?? []).map(e => [
        e.id, e.action, e.userId, e.ipAddress, e.occurredAt, e.reason ?? '',
      ]),
    ]
    downloadCsv('examshield-audit.csv', rows)
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Reports</h1>
          <p className="text-xs text-muted-foreground mt-1">
            Generated {new Date(s.generatedAt).toLocaleString()}
          </p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={exportResults}
            className="px-4 py-2 text-sm rounded border border-primary text-primary hover:bg-primary/10"
          >
            Export Results
          </button>
          <button
            onClick={exportAudit}
            className="px-4 py-2 text-sm rounded border border-border text-muted-foreground hover:bg-muted"
          >
            Export Audit
          </button>
        </div>
      </div>

      {/* Capture stats */}
      <section className="space-y-2">
        <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">Captures</h2>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
          <StatBlock label="Total" value={s.captures.total} />
          <StatBlock label="Verified" value={s.captures.verified} />
          <StatBlock label="Uploaded" value={s.captures.uploaded} />
          <StatBlock label="Created" value={s.captures.created} />
          <StatBlock label="Tampered" value={s.captures.tampered} />
        </div>
      </section>

      {/* OCR + Scoring stats */}
      <section className="space-y-2">
        <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">Processing</h2>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <StatBlock label="OCR Processed" value={s.ocr.totalProcessed} />
          <StatBlock
            label="Avg OCR Confidence"
            value={`${(s.ocr.averageConfidence * 100).toFixed(1)}%`}
          />
          <StatBlock label="Total Scored" value={s.scores.totalScored} />
          <StatBlock
            label="Avg Score"
            value={`${s.scores.averagePercentage.toFixed(1)}%`}
            sub={`High: ${s.scores.highestPercentage.toFixed(1)}%  Low: ${s.scores.lowestPercentage.toFixed(1)}%`}
          />
        </div>
      </section>

      {/* Security stats */}
      <section className="space-y-2">
        <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">Security</h2>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <StatBlock label="Security Events" value={s.security.totalEvents} />
          <StatBlock label="Critical Events" value={s.security.criticalEvents} />
        </div>
      </section>

      {/* Per-exam drill-down */}
      <section className="space-y-3">
        <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">Per-Exam Report</h2>
        <div className="flex gap-2 max-w-md">
          <input
            type="text"
            placeholder="Exam ID (UUID)"
            value={examIdInput}
            onChange={e => setExamIdInput(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && setExamId(examIdInput.trim() || null)}
            className="flex-1 rounded border border-border bg-background px-3 py-2 text-sm"
          />
          <button
            onClick={() => setExamId(examIdInput.trim() || null)}
            disabled={examFetching}
            className="px-4 py-2 text-sm rounded bg-primary text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
          >
            {examFetching ? 'Loading…' : 'Load'}
          </button>
        </div>

        {examReport && (
          <div className="rounded-lg border border-border p-4 space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-semibold text-lg">{examReport.examName}</h3>
                <p className="text-xs text-muted-foreground">
                  {examReport.examStatus} · {examReport.totalQuestions} questions ·
                  Generated {new Date(examReport.generatedAt).toLocaleString()}
                </p>
              </div>
              <button
                onClick={async () => {
                  const blob = await api.exportExamReportCsv(examReport.examId)
                  const url = URL.createObjectURL(blob)
                  const a = document.createElement('a')
                  a.href = url
                  a.download = `exam-report-${examReport.examId}.csv`
                  a.click()
                  URL.revokeObjectURL(url)
                }}
                className="px-3 py-1.5 text-xs rounded border border-border text-muted-foreground hover:bg-muted"
              >
                Export CSV
              </button>
            </div>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <StatBlock label="Captures" value={examReport.totalCaptures} />
              <StatBlock label="Verified" value={examReport.verifiedCaptures} />
              <StatBlock label="Tampered" value={examReport.tamperedCaptures} />
              <StatBlock label="Review Requests" value={examReport.totalReviewRequests} />
            </div>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <StatBlock label="OCR Processed" value={examReport.totalOcrProcessed} />
              <StatBlock
                label="Avg OCR Confidence"
                value={`${(examReport.ocrAverageConfidence * 100).toFixed(1)}%`}
              />
              <StatBlock label="Low Confidence" value={examReport.lowConfidenceCount} />
              <StatBlock
                label="Avg Score"
                value={`${examReport.averageScorePercentage.toFixed(1)}%`}
                sub={`High ${examReport.highestScorePercentage.toFixed(1)}% · Low ${examReport.lowestScorePercentage.toFixed(1)}%`}
              />
            </div>
          </div>
        )}
      </section>
    </div>
  )
}
