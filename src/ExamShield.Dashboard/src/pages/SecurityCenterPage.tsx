import { useState, useMemo } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid,
} from 'recharts'
import { api, type SecurityEventEntry, type AllSessionEntry } from '../api/client'
import StatusChip from '../components/ui/StatusChip'
import type { StatusVariant } from '../components/ui/StatusChip'
import { ShieldAlert } from 'lucide-react'

function severityVariant(severity: string): StatusVariant {
  switch (severity.toLowerCase()) {
    case 'critical': return 'danger'
    case 'high':     return 'warning'
    case 'warning':  return 'warning'
    case 'info':     return 'info'
    default:         return 'muted'
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

// Build hourly event-count buckets for the last 24 h from a list of events
function buildTimelineBuckets(events: SecurityEventEntry[]) {
  const buckets: Record<string, { label: string; Critical: number; High: number; Warning: number; Info: number }> = {}

  const now = Date.now()
  for (let i = 23; i >= 0; i--) {
    const d = new Date(now - i * 3_600_000)
    const key = `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:00`
    buckets[key] = { label: key, Critical: 0, High: 0, Warning: 0, Info: 0 }
  }

  for (const ev of events) {
    const d = new Date(ev.occurredAt)
    if (now - d.getTime() > 24 * 3_600_000) continue
    const key = `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:00`
    if (!buckets[key]) continue
    const sev = ev.severity as keyof typeof buckets[string]
    if (sev in buckets[key]) (buckets[key][sev] as number)++
  }

  return Object.values(buckets)
}

function EventRow({ event }: { event: SecurityEventEntry }) {
  return (
    <tr>
      <td className="px-4 py-3 font-medium text-foreground">{event.eventType}</td>
      <td className="px-4 py-3">
        <StatusChip label={event.severity} variant={severityVariant(event.severity)} />
      </td>
      <td className="px-4 py-3 text-sm text-muted-foreground max-w-xs truncate" title={event.message}>
        {event.message}
      </td>
      <td className="px-4 py-3 text-sm text-muted-foreground font-mono text-xs">{event.ipAddress ?? '—'}</td>
      <td className="px-4 py-3 text-sm text-muted-foreground text-xs">{formatDate(event.occurredAt)}</td>
    </tr>
  )
}

function SessionRow({ session }: { session: AllSessionEntry }) {
  return (
    <tr>
      <td className="px-4 py-3 font-mono text-xs text-muted-foreground">{session.userId}</td>
      <td className="px-4 py-3 text-sm text-muted-foreground text-xs">{new Date(session.createdAt).toLocaleString()}</td>
      <td className="px-4 py-3 text-sm text-muted-foreground text-xs">{new Date(session.expiresAt).toLocaleString()}</td>
    </tr>
  )
}

const SEVERITIES = ['', 'Info', 'Warning', 'High', 'Critical']
const AREA_COLORS = { Critical: '#f87171', High: '#fb923c', Warning: '#facc15', Info: '#38bdf8' }

export default function SecurityCenterPage() {
  const [severity, setSeverity] = useState('')
  const [loginFrom, setLoginFrom] = useState('')
  const [loginTo, setLoginTo]     = useState('')

  const { data, isLoading, isError } = useQuery({
    queryKey: ['security-events', severity],
    queryFn: () => api.getSecurityEvents(100, severity || undefined),
    refetchInterval: 30_000,
  })

  const { data: sessionsData } = useQuery({
    queryKey: ['all-active-sessions'],
    queryFn: () => api.getAllActiveSessions(),
    refetchInterval: 60_000,
  })

  const { data: loginData } = useQuery({
    queryKey: ['login-history', loginFrom, loginTo],
    queryFn: () => api.getLoginHistory(200, loginFrom || undefined, loginTo || undefined),
    refetchInterval: 60_000,
  })

  const criticalCount = data?.events.filter(e => e.severity === 'Critical').length ?? 0
  const timelineBuckets = useMemo(
    () => buildTimelineBuckets(data?.events ?? []),
    [data],
  )

  return (
    <div className="space-y-5 pb-4">
      {/* Header */}
      <div className="glass-card px-6 py-4">
        <div className="flex items-center justify-between flex-wrap gap-3">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-2xl"
              style={{ background: 'rgba(248,113,113,0.12)' }}>
              <ShieldAlert className="h-5 w-5 text-red-400 stroke-[1.75]" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-foreground">Security Center</h1>
              <p className="text-sm text-muted-foreground">Real-time threat monitoring</p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            {data && criticalCount > 0 && (
              <span className="inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold"
                style={{ background: 'rgba(239,68,68,0.12)', color: '#f87171', border: '1px solid rgba(239,68,68,0.2)' }}>
                {criticalCount} critical
              </span>
            )}
            <select
              value={severity}
              onChange={e => setSeverity(e.target.value)}
              className="input-glass w-36 text-xs py-2"
            >
              {SEVERITIES.map(s => (
                <option key={s} value={s}>{s || 'All Severities'}</option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {/* Threat Timeline Chart */}
      <div className="glass-card p-5">
        <p className="mb-4 text-sm font-semibold text-foreground">
          Threat Timeline
          <span className="ml-2 text-xs font-normal text-muted-foreground">last 24 hours by severity</span>
        </p>
        <div style={{ height: 200 }}>
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={timelineBuckets} margin={{ top: 4, right: 8, left: -24, bottom: 0 }}>
              <defs>
                {Object.entries(AREA_COLORS).map(([key, color]) => (
                  <linearGradient key={key} id={`grad-${key}`} x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%"  stopColor={color} stopOpacity={0.3} />
                    <stop offset="95%" stopColor={color} stopOpacity={0}   />
                  </linearGradient>
                ))}
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" />
              <XAxis
                dataKey="label"
                tick={{ fontSize: 9, fill: 'var(--color-muted-foreground)' }}
                tickLine={false} axisLine={false}
                interval={3}
              />
              <YAxis
                allowDecimals={false}
                tick={{ fontSize: 9, fill: 'var(--color-muted-foreground)' }}
                tickLine={false} axisLine={false}
              />
              <Tooltip
                contentStyle={{
                  background: 'var(--glass-bg)', border: '1px solid var(--glass-border)',
                  borderRadius: 8, fontSize: 11,
                }}
                labelStyle={{ color: 'var(--color-foreground)', marginBottom: 4 }}
              />
              {Object.entries(AREA_COLORS).map(([key, color]) => (
                <Area
                  key={key}
                  type="monotone"
                  dataKey={key}
                  stroke={color}
                  strokeWidth={1.5}
                  fill={`url(#grad-${key})`}
                  dot={false}
                  activeDot={{ r: 3, fill: color }}
                />
              ))}
            </AreaChart>
          </ResponsiveContainer>
        </div>
        <div className="mt-3 flex flex-wrap gap-4">
          {Object.entries(AREA_COLORS).map(([key, color]) => (
            <span key={key} className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <span className="inline-block h-2 w-2 rounded-full" style={{ background: color }} />
              {key}
            </span>
          ))}
        </div>
      </div>

      {isLoading && (
        <div role="status" aria-label="Loading" className="glass-card p-12 text-center text-muted-foreground">
          <div className="inline-block h-5 w-5 rounded-full border-2 border-border border-t-primary animate-spin" />
        </div>
      )}
      {isError && (
        <div className="glass-card p-4 text-sm text-red-400"
          style={{ border: '1px solid rgba(239,68,68,0.2)' }}>
          Failed to load security events.
        </div>
      )}

      {/* Security Events */}
      {data && (
        <div className="glass-card overflow-hidden">
          <div className="px-4 py-3" style={{ borderBottom: '1px solid var(--glass-border)' }}>
            <p className="text-sm font-semibold text-foreground">
              Security Events
              <span className="ml-2 text-xs font-normal text-muted-foreground">({data.events.length})</span>
            </p>
          </div>
          <table className="glass-table w-full" data-testid="security-events-table">
            <thead>
              <tr>
                {['Event Type', 'Severity', 'Message', 'IP', 'Time'].map(h => (
                  <th key={h}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {data.events.map(event => (
                <EventRow key={event.id} event={event} />
              ))}
              {data.events.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-muted-foreground">
                    No security events recorded.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Active Sessions */}
      {sessionsData && (
        <div className="glass-card overflow-hidden">
          <div className="px-4 py-3" style={{ borderBottom: '1px solid var(--glass-border)' }}>
            <p className="text-sm font-semibold text-foreground">
              Active Sessions
              <span className="ml-2 text-xs font-normal text-muted-foreground">({sessionsData.sessions.length})</span>
            </p>
          </div>
          <table className="glass-table w-full">
            <thead>
              <tr>
                {['User ID', 'Created', 'Expires'].map(h => (
                  <th key={h}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {sessionsData.sessions.map(s => <SessionRow key={s.id} session={s} />)}
              {sessionsData.sessions.length === 0 && (
                <tr>
                  <td colSpan={3} className="px-4 py-8 text-center text-muted-foreground">
                    No active sessions.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Login History */}
      <div className="glass-card overflow-hidden">
        <div className="flex flex-wrap items-center gap-3 px-4 py-3" style={{ borderBottom: '1px solid var(--glass-border)' }}>
          <p className="text-sm font-semibold text-foreground">Login History</p>
          <input
            type="datetime-local"
            title="From"
            value={loginFrom}
            onChange={e => setLoginFrom(e.target.value)}
            className="input-glass w-auto text-xs py-1.5"
          />
          <input
            type="datetime-local"
            title="To"
            value={loginTo}
            onChange={e => setLoginTo(e.target.value)}
            className="input-glass w-auto text-xs py-1.5"
          />
          {(loginFrom || loginTo) && (
            <button
              onClick={() => { setLoginFrom(''); setLoginTo('') }}
              className="text-xs text-muted-foreground hover:text-foreground transition-colors"
            >
              Clear
            </button>
          )}
        </div>
        <table className="glass-table w-full">
          <thead>
            <tr>
              {['Event', 'User', 'IP', 'Time'].map(h => (
                <th key={h}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {(loginData?.events ?? []).map(e => (
              <tr key={e.id}>
                <td className="text-sm text-foreground">{e.eventType}</td>
                <td className="text-sm text-muted-foreground font-mono text-xs">{e.userId ?? '—'}</td>
                <td className="text-sm text-muted-foreground font-mono text-xs">{e.ipAddress ?? '—'}</td>
                <td className="text-xs text-muted-foreground">{new Date(e.occurredAt).toLocaleString()}</td>
              </tr>
            ))}
            {(loginData?.events.length ?? 0) === 0 && (
              <tr>
                <td colSpan={4} className="px-4 py-8 text-center text-muted-foreground">
                  No login events in range.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
