import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
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

function EventRow({ event }: { event: SecurityEventEntry }) {
  return (
    <tr className="hover:bg-muted/30 transition-colors">
      <td className="px-4 py-3 font-medium text-foreground">{event.eventType}</td>
      <td className="px-4 py-3">
        <StatusChip label={event.severity} variant={severityVariant(event.severity)} />
      </td>
      <td className="px-4 py-3 text-sm text-muted-foreground max-w-xs truncate" title={event.message}>
        {event.message}
      </td>
      <td className="px-4 py-3 text-sm text-muted-foreground">{event.ipAddress ?? '—'}</td>
      <td className="px-4 py-3 text-sm text-muted-foreground">{formatDate(event.occurredAt)}</td>
    </tr>
  )
}

function SessionRow({ session }: { session: AllSessionEntry }) {
  return (
    <tr className="hover:bg-muted/30 transition-colors">
      <td className="px-4 py-2 font-mono text-xs text-muted-foreground">{session.userId}</td>
      <td className="px-4 py-2 text-sm text-muted-foreground">{new Date(session.createdAt).toLocaleString()}</td>
      <td className="px-4 py-2 text-sm text-muted-foreground">{new Date(session.expiresAt).toLocaleString()}</td>
    </tr>
  )
}

const SEVERITIES = ['', 'Info', 'Warning', 'High', 'Critical']

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

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <ShieldAlert className="h-6 w-6 text-red-500" />
          <h1 className="text-2xl font-bold text-foreground">Security Center</h1>
        </div>
        <div className="flex items-center gap-3">
          {data && criticalCount > 0 && (
            <span className="inline-flex items-center rounded-full bg-red-500/15 px-3 py-1 text-sm font-semibold text-red-500">
              {criticalCount} critical
            </span>
          )}
          <select
            value={severity}
            onChange={e => setSeverity(e.target.value)}
            className="rounded border border-border px-2 py-1 text-xs bg-background text-foreground"
          >
            {SEVERITIES.map(s => (
              <option key={s} value={s}>{s || 'All Severities'}</option>
            ))}
          </select>
        </div>
      </div>

      {isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
      {isError   && <p className="text-sm text-red-500">Failed to load security events.</p>}

      {data && (
        <div className="overflow-hidden rounded-xl border border-border">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                {['Event Type', 'Severity', 'Message', 'IP', 'Time'].map(h => (
                  <th key={h} className="px-4 py-3 text-left font-medium text-muted-foreground">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
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
      {sessionsData && (
        <div className="space-y-2">
          <h2 className="text-lg font-semibold text-foreground">
            Active Sessions ({sessionsData.sessions.length})
          </h2>
          <div className="overflow-hidden rounded-xl border border-border">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  {['User ID', 'Created', 'Expires'].map(h => (
                    <th key={h} className="px-4 py-3 text-left font-medium text-muted-foreground">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
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
        </div>
      )}
      <div className="space-y-2">
        <div className="flex items-center gap-3">
          <h2 className="text-lg font-semibold text-foreground">Login History</h2>
          <input
            type="datetime-local"
            title="From"
            value={loginFrom}
            onChange={e => setLoginFrom(e.target.value)}
            className="rounded border border-border bg-background px-2 py-1 text-xs text-foreground"
          />
          <input
            type="datetime-local"
            title="To"
            value={loginTo}
            onChange={e => setLoginTo(e.target.value)}
            className="rounded border border-border bg-background px-2 py-1 text-xs text-foreground"
          />
          {(loginFrom || loginTo) && (
            <button
              onClick={() => { setLoginFrom(''); setLoginTo('') }}
              className="text-xs text-muted-foreground hover:text-foreground"
            >Clear</button>
          )}
        </div>
        <div className="overflow-hidden rounded-xl border border-border">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                {['Event', 'User', 'IP', 'Time'].map(h => (
                  <th key={h} className="px-4 py-3 text-left font-medium text-muted-foreground">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {(loginData?.events ?? []).map(e => (
                <tr key={e.id}>
                  <td className="px-4 py-2">{e.eventType}</td>
                  <td className="px-4 py-2 text-muted-foreground">{e.userId ?? '—'}</td>
                  <td className="px-4 py-2 text-muted-foreground">{e.ipAddress ?? '—'}</td>
                  <td className="px-4 py-2 text-muted-foreground">{new Date(e.occurredAt).toLocaleString()}</td>
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
    </div>
  )
}
