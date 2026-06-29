import { useQueryClient, useQuery } from '@tanstack/react-query'
import {
  ShieldCheck, Clock, CheckCircle, AlertTriangle,
  BookOpen, BarChart2, Cpu, Layers, RefreshCw,
  TrendingUp, Users, Activity,
} from 'lucide-react'
import {
  PieChart, Pie, Cell, Tooltip, ResponsiveContainer,
  BarChart, Bar, XAxis, YAxis, CartesianGrid,
  RadarChart, Radar, PolarGrid, PolarAngleAxis,
} from 'recharts'
import { api } from '../api/client'
import { useDashboardStats } from '../hooks/useDashboardStats'

// ── Colour palette ──────────────────────────────────────────────────────────
const C = {
  cyan:   '#22d3ee',
  yellow: '#facc15',
  green:  '#4ade80',
  red:    '#f87171',
  blue:   '#60a5fa',
  violet: '#a78bfa',
  slate:  '#94a3b8',
  orange: '#fb923c',
  primary: '#4F8EF7',
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

// ── Glass Stat Card ──────────────────────────────────────────────────────────
function StatCard({
  label, value, sub, icon: Icon, accent, accentBg, loading = false,
}: {
  label: string; value: string | number; sub?: string
  icon: React.ElementType; accent: string; accentBg: string; loading?: boolean
}) {
  return (
    <div className="glass-card p-5 flex flex-col gap-3 animate-in">
      <div className="flex items-start justify-between">
        <div className="flex flex-col gap-1">
          <p className="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground">
            {label}
          </p>
          {loading ? (
            <div className="h-9 w-24 animate-pulse rounded-xl bg-muted/50" />
          ) : (
            <p className="text-3xl font-bold tabular-nums" style={{ color: accent }}>
              {typeof value === 'number' ? value.toLocaleString() : value}
            </p>
          )}
        </div>
        <div
          className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl"
          style={{ background: accentBg }}
        >
          <Icon className="h-5 w-5" style={{ color: accent }} />
        </div>
      </div>
      {sub && (
        <p className="text-xs text-muted-foreground leading-relaxed">{sub}</p>
      )}
    </div>
  )
}

// ── Glass Chart Card ─────────────────────────────────────────────────────────
function ChartCard({
  title, children, className,
}: {
  title: string; children: React.ReactNode; className?: string
}) {
  return (
    <div className={`glass-card p-6 animate-in ${className ?? ''}`}>
      <p className="mb-5 text-sm font-semibold text-foreground">{title}</p>
      {children}
    </div>
  )
}

// ── Severity badge ────────────────────────────────────────────────────────────
function SeverityBadge({ severity }: { severity: string }) {
  const map: Record<string, { bg: string; color: string }> = {
    Critical: { bg: 'rgba(239,68,68,0.15)',   color: '#f87171' },
    High:     { bg: 'rgba(251,146,60,0.15)',   color: '#fb923c' },
    Warning:  { bg: 'rgba(250,204,21,0.15)',   color: '#facc15' },
    Info:     { bg: 'rgba(34,211,238,0.15)',   color: '#22d3ee' },
  }
  const s = map[severity] ?? { bg: 'rgba(148,163,184,0.15)', color: '#94a3b8' }
  return (
    <span
      className="rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide"
      style={{ background: s.bg, color: s.color, border: `1px solid ${s.color}22` }}
    >
      {severity}
    </span>
  )
}

// ── Action icon ───────────────────────────────────────────────────────────────
function ActionIcon({ action }: { action: string }) {
  const iconMap: Record<string, { icon: React.ElementType; color: string }> = {
    HashVerified:          { icon: ShieldCheck, color: C.cyan    },
    ScoreGenerated:        { icon: BarChart2,   color: C.violet  },
    ResultPublished:       { icon: TrendingUp,  color: C.green   },
    ManualReviewCompleted: { icon: CheckCircle, color: C.green   },
    ManualReviewStarted:   { icon: Clock,       color: C.yellow  },
    OCRCompleted:          { icon: Cpu,         color: C.blue    },
    ImageUploaded:         { icon: Layers,      color: C.blue    },
    CaptureRegistered:     { icon: ShieldCheck, color: C.slate   },
    DeviceRegistered:      { icon: Activity,    color: C.slate   },
    UserCreated:           { icon: Users,       color: C.slate   },
  }
  const { icon: Icon, color } = iconMap[action] ?? { icon: Activity, color: C.slate }
  return <Icon className="h-3.5 w-3.5 shrink-0" style={{ color }} />
}

// ── Main page ─────────────────────────────────────────────────────────────────
export default function DashboardPage() {
  const qc = useQueryClient()

  const { data: stats, isLoading: statsLoading, dataUpdatedAt } = useDashboardStats()

  const { data: statistics } = useQuery({
    queryKey: ['statistics'],
    queryFn: api.getStatistics,
    refetchInterval: 60_000,
  })

  const { data: capturesData } = useQuery({
    queryKey: ['captures-dash'],
    queryFn: () => api.getCaptures(1, 200),
    refetchInterval: 30_000,
  })

  const { data: examsData } = useQuery({
    queryKey: ['exams-dash'],
    queryFn: () => api.getExams(1, 100),
    refetchInterval: 60_000,
  })

  const { data: devicesData } = useQuery({
    queryKey: ['devices-dash'],
    queryFn: api.getDevices,
    refetchInterval: 60_000,
  })

  const { data: securityData } = useQuery({
    queryKey: ['security-dash'],
    queryFn: () => api.getSecurityEvents(50),
    refetchInterval: 30_000,
  })

  const { data: auditData } = useQuery({
    queryKey: ['audit-dash'],
    queryFn: () => api.getAuditLog({ pageSize: 8 }),
    refetchInterval: 30_000,
  })

  const { data: resultsData } = useQuery({
    queryKey: ['results-dash'],
    queryFn: () => api.getResults(),
    refetchInterval: 60_000,
  })

  // ── Derived data ─────────────────────────────────────────────────────────
  const captures = capturesData?.captures ?? []
  const exams    = examsData?.exams ?? []
  const devices  = devicesData?.devices ?? []
  const events   = securityData?.events ?? []
  const audit    = auditData?.entries ?? []
  const scores   = resultsData?.results ?? []

  const captureStatusData = ['Created', 'Uploaded', 'Verified', 'Tampered'].map(status => ({
    name: status,
    value: captures.filter(c => c.status === status).length,
  })).filter(d => d.value > 0)

  const captureColors: Record<string, string> = {
    Created: C.slate, Uploaded: C.blue, Verified: C.cyan, Tampered: C.red,
  }

  const severityOrder = ['Critical', 'High', 'Warning', 'Info']
  const securityBarData = severityOrder.map(sev => ({
    severity: sev,
    count: events.filter(e => e.severity === sev).length,
  }))
  const severityColors: Record<string, string> = {
    Critical: C.red, High: C.orange, Warning: C.yellow, Info: C.cyan,
  }

  const examStatusData = ['Active', 'Closed', 'Draft'].map(status => ({
    status,
    count: exams.filter(e => e.status === status).length,
  }))
  const examStatusColors: Record<string, string> = {
    Active: C.cyan, Closed: C.violet, Draft: C.slate,
  }

  const scoreBuckets = [
    { range: '90–100%', count: scores.filter(s => s.percentage >= 90).length },
    { range: '75–89%',  count: scores.filter(s => s.percentage >= 75 && s.percentage < 90).length },
    { range: '60–74%',  count: scores.filter(s => s.percentage >= 60 && s.percentage < 75).length },
    { range: '<60%',    count: scores.filter(s => s.percentage < 60).length },
  ]

  const scoreBarData = [...scores]
    .sort((a, b) => b.percentage - a.percentage)
    .slice(0, 10)
    .map((s, i) => ({ label: `#${i + 1}`, pct: Math.round(s.percentage) }))

  const activeDevices  = devices.filter(d => d.isActive).length
  const pendingDevices = devices.filter(d => d.status === 'Pending').length
  const activeExams    = exams.filter(e => e.status === 'Active').length
  const updatedAt      = dataUpdatedAt ? new Date(dataUpdatedAt).toLocaleTimeString() : '—'

  const refreshAll = () => {
    qc.invalidateQueries({ queryKey: ['dashboard-stats'] })
    qc.invalidateQueries({ queryKey: ['statistics'] })
    qc.invalidateQueries({ queryKey: ['captures-dash'] })
    qc.invalidateQueries({ queryKey: ['exams-dash'] })
    qc.invalidateQueries({ queryKey: ['devices-dash'] })
    qc.invalidateQueries({ queryKey: ['security-dash'] })
    qc.invalidateQueries({ queryKey: ['audit-dash'] })
    qc.invalidateQueries({ queryKey: ['results-dash'] })
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="space-y-5 pb-4">

      {/* ── Hero header ──────────────────────────────────────── */}
      <div className="glass-card px-7 py-5">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold text-gradient">Dashboard</h1>
            <p className="mt-0.5 text-xs text-muted-foreground">
              Live exam monitoring · Last updated: <span className="text-foreground font-medium">{updatedAt}</span>
            </p>
          </div>
          <div className="flex items-center gap-3">
            {(stats?.activeAlerts ?? 0) > 0 && (
              <span className="flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold"
                style={{
                  background: 'rgba(239,68,68,0.12)',
                  color: '#f87171',
                  border: '1px solid rgba(239,68,68,0.2)',
                }}
              >
                <AlertTriangle className="h-3.5 w-3.5" />
                {stats!.activeAlerts} active alert{stats!.activeAlerts !== 1 ? 's' : ''}
              </span>
            )}
            <button
              onClick={refreshAll}
              className="btn-glass text-xs gap-1.5 py-2 px-4"
            >
              <RefreshCw className="h-3.5 w-3.5 stroke-[1.75]" />
              Refresh
            </button>
          </div>
        </div>
      </div>

      {/* ── Row 1: Primary KPI cards ──────────────────────────── */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <StatCard
          label="Total Captures"
          value={stats?.totalCaptures ?? 0}
          icon={ShieldCheck}
          accent={C.cyan}
          accentBg="rgba(34,211,238,0.12)"
          loading={statsLoading}
          sub={`${captures.filter(c => c.status === 'Verified').length} verified`}
        />
        <StatCard
          label="Pending Review"
          value={stats?.pendingReview ?? 0}
          icon={Clock}
          accent={C.yellow}
          accentBg="rgba(250,204,21,0.12)"
          loading={statsLoading}
          sub="awaiting manual check"
        />
        <StatCard
          label="Verified Today"
          value={stats?.verifiedToday ?? 0}
          icon={CheckCircle}
          accent={C.green}
          accentBg="rgba(74,222,128,0.12)"
          loading={statsLoading}
          sub="hash + signature OK"
        />
        <StatCard
          label="Active Alerts"
          value={stats?.activeAlerts ?? 0}
          icon={AlertTriangle}
          accent={stats?.activeAlerts ? C.red : C.slate}
          accentBg={stats?.activeAlerts ? 'rgba(248,113,113,0.12)' : 'rgba(148,163,184,0.12)'}
          loading={statsLoading}
          sub="critical events <24h"
        />
      </div>

      {/* ── Row 2: Secondary KPI cards ────────────────────────── */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <StatCard
          label="Exams"
          value={exams.length}
          icon={BookOpen}
          accent={C.blue}
          accentBg="rgba(96,165,250,0.12)"
          sub={`${activeExams} active`}
        />
        <StatCard
          label="Avg Score"
          value={statistics ? `${statistics.averagePercentage.toFixed(1)}%` : '—'}
          icon={BarChart2}
          accent={C.violet}
          accentBg="rgba(167,139,250,0.12)"
          sub={`${statistics?.totalPapersScored ?? 0} papers scored`}
        />
        <StatCard
          label="Active Devices"
          value={activeDevices}
          icon={Cpu}
          accent={C.orange}
          accentBg="rgba(251,146,60,0.12)"
          sub={`${pendingDevices} pending approval`}
        />
        <StatCard
          label="OCR Queue"
          value={captures.filter(c => c.status === 'Uploaded').length}
          icon={Layers}
          accent={C.slate}
          accentBg="rgba(148,163,184,0.12)"
          sub="captures awaiting OCR"
        />
      </div>

      {/* ── Row 3: Capture Pipeline + Security Severity ────────── */}
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
                    {captureStatusData.map(entry => (
                      <Cell key={entry.name} fill={captureColors[entry.name]} stroke="transparent" />
                    ))}
                  </Pie>
                  <Tooltip contentStyle={TOOLTIP_STYLE} formatter={(v) => [v, 'captures']} />
                </PieChart>
              </ResponsiveContainer>
              <div className="flex flex-col gap-2.5">
                {captureStatusData.map(entry => (
                  <div key={entry.name} className="flex items-center gap-2.5">
                    <span
                      className="h-2.5 w-2.5 rounded-full shrink-0"
                      style={{ background: captureColors[entry.name] }}
                    />
                    <span className="text-xs text-muted-foreground w-16">{entry.name}</span>
                    <span className="text-xs font-bold text-foreground tabular-nums">{entry.value}</span>
                  </div>
                ))}
                <div className="mt-1 pt-2" style={{ borderTop: '1px solid var(--glass-border)' }}>
                  <span className="text-xs text-muted-foreground">Total: </span>
                  <span className="text-xs font-bold text-foreground">{captures.length}</span>
                </div>
              </div>
            </div>
          )}
        </ChartCard>

        <ChartCard title="Security Threat Breakdown">
          {events.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">No security events</p>
          ) : (
            <ResponsiveContainer width="100%" height={190}>
              <BarChart data={securityBarData} margin={{ top: 4, right: 8, left: -16, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" vertical={false} />
                <XAxis dataKey="severity" tick={{ fill: '#94a3b8', fontSize: 11 }} axisLine={false} tickLine={false} />
                <YAxis allowDecimals={false} tick={{ fill: '#94a3b8', fontSize: 11 }} axisLine={false} tickLine={false} />
                <Tooltip contentStyle={TOOLTIP_STYLE} cursor={{ fill: 'rgba(255,255,255,0.04)' }} />
                <Bar dataKey="count" name="Events" radius={[6, 6, 0, 0]} maxBarSize={48}>
                  {securityBarData.map(entry => (
                    <Cell key={entry.severity} fill={severityColors[entry.severity]} />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
      </div>

      {/* ── Row 4: Score Distribution + Exam Overview ─────────── */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">

        <ChartCard title="Score Distribution">
          {scoreBarData.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">No scores published yet</p>
          ) : (
            <div className="space-y-4">
              <div className="grid grid-cols-4 gap-2">
                {scoreBuckets.map(b => (
                  <div
                    key={b.range}
                    className="rounded-2xl p-2.5 text-center"
                    style={{ background: 'rgba(255,255,255,0.04)', border: '1px solid var(--glass-border)' }}
                  >
                    <p className="text-[10px] text-muted-foreground leading-tight mb-0.5">{b.range}</p>
                    <p className="text-xl font-bold text-foreground">{b.count}</p>
                  </div>
                ))}
              </div>
              <ResponsiveContainer width="100%" height={120}>
                <BarChart data={scoreBarData} margin={{ top: 4, right: 4, left: -20, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" vertical={false} />
                  <XAxis dataKey="label" tick={{ fill: '#94a3b8', fontSize: 10 }} axisLine={false} tickLine={false} />
                  <YAxis domain={[0, 100]} tick={{ fill: '#94a3b8', fontSize: 10 }} axisLine={false} tickLine={false} />
                  <Tooltip contentStyle={TOOLTIP_STYLE} formatter={(v) => [`${v}%`, 'Score']} cursor={{ fill: 'rgba(255,255,255,0.04)' }} />
                  <Bar dataKey="pct" name="Score %" radius={[4, 4, 0, 0]} maxBarSize={28}>
                    {scoreBarData.map(entry => (
                      <Cell key={entry.label}
                        fill={entry.pct >= 90 ? C.green : entry.pct >= 70 ? C.cyan : entry.pct >= 50 ? C.yellow : C.red}
                      />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
              <div className="flex items-center justify-between text-xs text-muted-foreground px-1">
                <span>High: <span className="font-semibold" style={{ color: C.cyan }}>{statistics?.highestScore ?? '—'}</span></span>
                <span>Avg: <span className="font-semibold" style={{ color: C.violet }}>{statistics?.averagePercentage.toFixed(1) ?? '—'}%</span></span>
                <span>Low: <span className="font-semibold" style={{ color: C.yellow }}>{statistics?.lowestScore ?? '—'}</span></span>
              </div>
            </div>
          )}
        </ChartCard>

        <ChartCard title="Exam Overview">
          {exams.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">No exams yet</p>
          ) : (
            <div className="space-y-4">
              <div className="flex gap-3">
                {examStatusData.map(d => (
                  <div
                    key={d.status}
                    className="flex-1 rounded-2xl p-3 text-center"
                    style={{
                      background: examStatusColors[d.status] + '12',
                      border: `1px solid ${examStatusColors[d.status]}25`,
                    }}
                  >
                    <p className="text-[10px] text-muted-foreground mb-0.5">{d.status}</p>
                    <p className="text-2xl font-bold" style={{ color: examStatusColors[d.status] }}>{d.count}</p>
                  </div>
                ))}
              </div>
              <div className="space-y-1.5 max-h-[150px] overflow-y-auto pr-1">
                {[...exams].reverse().map(exam => (
                  <div
                    key={exam.examId}
                    className="flex items-center justify-between rounded-xl px-3 py-2.5"
                    style={{
                      background: 'rgba(255,255,255,0.03)',
                      border: '1px solid var(--glass-border)',
                    }}
                  >
                    <div className="min-w-0 flex-1">
                      <p className="text-xs font-medium text-foreground truncate">{exam.name}</p>
                      <p className="text-[10px] text-muted-foreground">{exam.totalQuestions} questions</p>
                    </div>
                    <span
                      className="ml-3 shrink-0 rounded-full px-2.5 py-0.5 text-[10px] font-semibold"
                      style={{
                        background: examStatusColors[exam.status] + '18',
                        color: examStatusColors[exam.status],
                        border: `1px solid ${examStatusColors[exam.status]}30`,
                      }}
                    >
                      {exam.status}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </ChartCard>
      </div>

      {/* ── Row 5: Activity Feed + Threat Radar ───────────────── */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">

        <div className="lg:col-span-2">
          <ChartCard title="Recent Activity">
            {audit.length === 0 ? (
              <p className="py-4 text-sm text-muted-foreground">No audit entries</p>
            ) : (
              <div className="space-y-0">
                {audit.slice(0, 8).map((entry, i) => (
                  <div
                    key={entry.id ?? i}
                    className="flex items-start gap-3 py-3"
                    style={{
                      borderBottom: i < Math.min(audit.length, 8) - 1 ? '1px solid var(--glass-border)' : 'none',
                    }}
                  >
                    <div
                      className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-xl"
                      style={{ background: 'rgba(255,255,255,0.06)', border: '1px solid var(--glass-border)' }}
                    >
                      <ActionIcon action={entry.action} />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="text-xs font-medium text-foreground">
                        {entry.action.replace(/([A-Z])/g, ' $1').trim()}
                      </p>
                      <p className="text-[10px] text-muted-foreground truncate mt-0.5">
                        {entry.userId === 'system' ? 'System' : entry.userId.substring(0, 8) + '…'}
                        {' · '}{entry.ipAddress}
                      </p>
                    </div>
                    <p className="shrink-0 text-[10px] text-muted-foreground tabular-nums">
                      {new Date(entry.occurredAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                    </p>
                  </div>
                ))}
              </div>
            )}
          </ChartCard>
        </div>

        <ChartCard title="Threat Radar">
          {events.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">No events</p>
          ) : (
            (() => {
              const typeMap: Record<string, number> = {}
              events.forEach(e => { typeMap[e.eventType] = (typeMap[e.eventType] ?? 0) + 1 })
              const radarData = Object.entries(typeMap)
                .sort((a, b) => b[1] - a[1])
                .slice(0, 6)
                .map(([type, count]) => ({
                  subject: type.replace(/([A-Z])/g, ' $1').trim().split(' ').slice(0, 2).join(' '),
                  count,
                }))
              return (
                <ResponsiveContainer width="100%" height={220}>
                  <RadarChart data={radarData} margin={{ top: 0, right: 20, left: 20, bottom: 0 }}>
                    <PolarGrid stroke="rgba(255,255,255,0.08)" />
                    <PolarAngleAxis dataKey="subject" tick={{ fill: '#94a3b8', fontSize: 9 }} />
                    <Radar name="Count" dataKey="count" stroke={C.primary} fill={C.primary} fillOpacity={0.18} strokeWidth={2} />
                    <Tooltip contentStyle={TOOLTIP_STYLE} />
                  </RadarChart>
                </ResponsiveContainer>
              )
            })()
          )}
        </ChartCard>
      </div>
    </div>
  )
}
