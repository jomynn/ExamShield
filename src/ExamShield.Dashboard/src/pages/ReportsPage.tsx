import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  PieChart, Pie, Cell, Tooltip, ResponsiveContainer,
  BarChart, Bar, XAxis, YAxis, CartesianGrid,
} from 'recharts'
import { useReportSummary, useExamReport } from '../hooks/useReports'
import { useResults } from '../hooks/useResults'
import { useAuditLog } from '../hooks/useAuditLog'
import { api } from '../api/client'

const C = {
  cyan:   '#22d3ee',
  yellow: '#facc15',
  green:  '#4ade80',
  red:    '#f87171',
  blue:   '#60a5fa',
  violet: '#a78bfa',
  slate:  '#94a3b8',
  orange: '#fb923c',
}

const TOOLTIP_STYLE = {
  backgroundColor: 'rgba(15, 23, 42, 0.92)',
  border: '1px solid rgba(255,255,255,0.10)',
  borderRadius: 12,
  color: '#e2e8f0',
  fontSize: 12,
  backdropFilter: 'blur(12px)',
  boxShadow: '0 8px 32px rgba(0,0,0,0.3)',
}

function ChartCard({
  title, children, className,
}: { title: string; children: React.ReactNode; className?: string }) {
  return (
    <div className={`glass-card p-6 animate-in ${className ?? ''}`}>
      <p className="mb-5 text-sm font-semibold text-foreground">{title}</p>
      {children}
    </div>
  )
}

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
  const { data: securityData } = useQuery({
    queryKey: ['reports-security'],
    queryFn: () => api.getSecurityEvents(100),
  })
  const [examIdInput, setExamIdInput] = useState('')
  const [examId, setExamId] = useState<string | null>(null)
  const { data: examReport, isFetching: examFetching } = useExamReport(examId)

  if (isLoading) return <p>Loading...</p>
  const s = summary!

  const scores = resultsData?.results ?? []
  const audit  = auditData?.entries ?? []
  const events = securityData?.events ?? []

  // ── Derived chart data ────────────────────────────────────────────────────

  const captureStatusData = [
    { name: 'Verified', value: s.captures.verified, color: C.cyan  },
    { name: 'Uploaded', value: s.captures.uploaded, color: C.blue  },
    { name: 'Created',  value: s.captures.created,  color: C.slate },
    { name: 'Tampered', value: s.captures.tampered, color: C.red   },
  ].filter(d => d.value > 0)

  const scoreBuckets = [
    { range: '90–100%', count: scores.filter(r => r.percentage >= 90).length,                              color: C.green  },
    { range: '75–89%',  count: scores.filter(r => r.percentage >= 75 && r.percentage < 90).length,         color: C.cyan   },
    { range: '60–74%',  count: scores.filter(r => r.percentage >= 60 && r.percentage < 75).length,         color: C.yellow },
    { range: '<60%',    count: scores.filter(r => r.percentage < 60).length,                               color: C.red    },
  ]

  const severityColors: Record<string, string> = {
    Critical: C.red, High: C.orange, Warning: C.yellow, Info: C.cyan,
  }
  const securityBarData = ['Critical', 'High', 'Warning', 'Info'].map(sev => ({
    severity: sev,
    count: events.filter(e => e.severity === sev).length,
  }))

  const actionMap: Record<string, number> = {}
  audit.forEach(e => { actionMap[e.action] = (actionMap[e.action] ?? 0) + 1 })
  const auditBarData = Object.entries(actionMap)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 8)
    .map(([action, count]) => ({
      action: action.replace(/([A-Z])/g, ' $1').trim().split(' ').slice(0, 2).join(' '),
      count,
    }))

  const exportResults = () => {
    const rows = [
      ['Score ID', 'Capture ID', 'Exam ID', 'Student ID', 'Correct', 'Total', 'Percentage', 'Scored At'],
      ...scores.map(r => [
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
      ...audit.map(e => [e.id, e.action, e.userId, e.ipAddress, e.occurredAt, e.reason ?? '']),
    ]
    downloadCsv('examshield-audit.csv', rows)
  }

  return (
    <div className="p-6 space-y-6">

      {/* ── Header ─────────────────────────────────────────────────────── */}
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

      {/* ── Row 1: Capture Pipeline + Score Distribution ────────────────── */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">

        <ChartCard title="Capture Pipeline Status">
          {captureStatusData.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">No captures yet</p>
          ) : (
            <div className="flex items-center gap-6">
              <ResponsiveContainer width="52%" height={190}>
                <PieChart>
                  <Pie
                    data={captureStatusData} cx="50%" cy="50%"
                    innerRadius={52} outerRadius={82}
                    paddingAngle={3} dataKey="value"
                    animationBegin={0} animationDuration={800}
                  >
                    {captureStatusData.map(d => (
                      <Cell key={d.name} fill={d.color} stroke="transparent" />
                    ))}
                  </Pie>
                  <Tooltip contentStyle={TOOLTIP_STYLE} formatter={(v) => [v, 'captures']} />
                </PieChart>
              </ResponsiveContainer>
              <div className="flex flex-col gap-2.5">
                {captureStatusData.map(d => (
                  <div key={d.name} className="flex items-center gap-2.5">
                    <span
                      className="h-2.5 w-2.5 rounded-full shrink-0"
                      style={{ background: d.color }}
                    />
                    <span className="text-xs text-muted-foreground w-16">{d.name}</span>
                    <span className="text-xs font-bold text-foreground tabular-nums">{d.value}</span>
                  </div>
                ))}
                <div className="mt-1 pt-2" style={{ borderTop: '1px solid var(--glass-border)' }}>
                  <span className="text-xs text-muted-foreground">Total: </span>
                  <span className="text-xs font-bold text-foreground">{s.captures.total}</span>
                </div>
              </div>
            </div>
          )}
        </ChartCard>

        <ChartCard title="Score Distribution">
          {scores.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">No scores published yet</p>
          ) : (
            <div className="space-y-4">
              <ResponsiveContainer width="100%" height={150}>
                <BarChart data={scoreBuckets} margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" vertical={false} />
                  <XAxis dataKey="range" tick={{ fill: '#94a3b8', fontSize: 11 }} axisLine={false} tickLine={false} />
                  <YAxis allowDecimals={false} tick={{ fill: '#94a3b8', fontSize: 11 }} axisLine={false} tickLine={false} />
                  <Tooltip
                    contentStyle={TOOLTIP_STYLE}
                    formatter={(v) => [v, 'students']}
                    cursor={{ fill: 'rgba(255,255,255,0.04)' }}
                  />
                  <Bar dataKey="count" radius={[6, 6, 0, 0]} maxBarSize={56}>
                    {scoreBuckets.map(d => <Cell key={d.range} fill={d.color} />)}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
              <div className="grid grid-cols-4 gap-2">
                {scoreBuckets.map(b => (
                  <div
                    key={b.range}
                    className="rounded-xl p-2 text-center"
                    style={{ background: 'rgba(255,255,255,0.04)', border: '1px solid var(--glass-border)' }}
                  >
                    <p className="text-[10px] text-muted-foreground mb-0.5">{b.range}</p>
                    <p className="text-lg font-bold tabular-nums" style={{ color: b.color }}>{b.count}</p>
                  </div>
                ))}
              </div>
            </div>
          )}
        </ChartCard>
      </div>

      {/* ── Processing stats ─────────────────────────────────────────────── */}
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

      {/* ── Row 2: Security Events + Audit Activity ─────────────────────── */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">

        <ChartCard title="Security Overview">
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3">
              <StatBlock label="Total Events" value={s.security.totalEvents} />
              <StatBlock label="Critical Events" value={s.security.criticalEvents} />
            </div>
            {events.length > 0 ? (
              <ResponsiveContainer width="100%" height={130}>
                <BarChart data={securityBarData} margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" vertical={false} />
                  <XAxis dataKey="severity" tick={{ fill: '#94a3b8', fontSize: 11 }} axisLine={false} tickLine={false} />
                  <YAxis allowDecimals={false} tick={{ fill: '#94a3b8', fontSize: 11 }} axisLine={false} tickLine={false} />
                  <Tooltip contentStyle={TOOLTIP_STYLE} cursor={{ fill: 'rgba(255,255,255,0.04)' }} />
                  <Bar dataKey="count" name="Events" radius={[6, 6, 0, 0]} maxBarSize={48}>
                    {securityBarData.map(d => (
                      <Cell key={d.severity} fill={severityColors[d.severity]} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <p className="py-2 text-sm text-muted-foreground">No security events to display</p>
            )}
          </div>
        </ChartCard>

        <ChartCard title="Audit Activity by Action">
          {auditBarData.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">No audit entries</p>
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart
                data={auditBarData}
                layout="vertical"
                margin={{ top: 0, right: 8, left: 0, bottom: 0 }}
              >
                <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" horizontal={false} />
                <XAxis
                  type="number" allowDecimals={false}
                  tick={{ fill: '#94a3b8', fontSize: 10 }} axisLine={false} tickLine={false}
                />
                <YAxis
                  type="category" dataKey="action" width={80}
                  tick={{ fill: '#94a3b8', fontSize: 10 }} axisLine={false} tickLine={false}
                />
                <Tooltip contentStyle={TOOLTIP_STYLE} cursor={{ fill: 'rgba(255,255,255,0.04)' }} />
                <Bar dataKey="count" fill={C.blue} radius={[0, 6, 6, 0]} maxBarSize={20} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
      </div>

      {/* ── Per-exam drill-down ──────────────────────────────────────────── */}
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
            {examReport.totalScored > 0 && (() => {
              const examScores = scores.filter(r => r.examId === examReport.examId)
              const examBuckets = [
                { range: '90–100%', count: examScores.filter(r => r.percentage >= 90).length,                              color: C.green  },
                { range: '75–89%',  count: examScores.filter(r => r.percentage >= 75 && r.percentage < 90).length,         color: C.cyan   },
                { range: '60–74%',  count: examScores.filter(r => r.percentage >= 60 && r.percentage < 75).length,         color: C.yellow },
                { range: '<60%',    count: examScores.filter(r => r.percentage < 60).length,                               color: C.red    },
              ]
              return (
                <div>
                  <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-2">
                    Score Distribution
                  </p>
                  <ResponsiveContainer width="100%" height={120}>
                    <BarChart data={examBuckets} margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" vertical={false} />
                      <XAxis dataKey="range" tick={{ fill: '#94a3b8', fontSize: 10 }} axisLine={false} tickLine={false} />
                      <YAxis allowDecimals={false} tick={{ fill: '#94a3b8', fontSize: 10 }} axisLine={false} tickLine={false} />
                      <Tooltip
                        contentStyle={TOOLTIP_STYLE}
                        formatter={(v) => [v, 'students']}
                        cursor={{ fill: 'rgba(255,255,255,0.04)' }}
                      />
                      <Bar dataKey="count" radius={[4, 4, 0, 0]} maxBarSize={48}>
                        {examBuckets.map(d => <Cell key={d.range} fill={d.color} />)}
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              )
            })()}
          </div>
        )}
      </section>
    </div>
  )
}
