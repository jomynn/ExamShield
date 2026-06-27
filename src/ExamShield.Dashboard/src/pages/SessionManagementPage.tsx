import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, type SessionItem } from '../api/client'
import { LogOut, ShieldAlert } from 'lucide-react'

function timeAgo(iso: string) {
  const diff = Date.now() - new Date(iso).getTime()
  const mins = Math.floor(diff / 60_000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins}m ago`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs}h ago`
  return `${Math.floor(hrs / 24)}d ago`
}

function isExpired(iso: string) {
  return new Date(iso).getTime() < Date.now()
}

export default function SessionManagementPage() {
  const qc = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['sessions'],
    queryFn: api.getSessions,
    refetchInterval: 30_000,
  })

  const { mutate: revoke, isPending: isRevoking, variables: revokingId } = useMutation({
    mutationFn: (sessionId: string) => api.revokeSession(sessionId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['sessions'] }),
  })

  const { mutate: revokeAll, isPending: isRevokingAll } = useMutation({
    mutationFn: () => api.revokeAllSessions(),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['sessions'] }),
  })

  const sessions = data?.sessions ?? []
  const active   = sessions.filter(s => !isExpired(s.expiresAt))
  const expired  = sessions.filter(s => isExpired(s.expiresAt))

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Active Sessions</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">
            {active.length} active · {expired.length} expired
          </p>
        </div>
        {active.length > 1 && (
          <button
            onClick={() => revokeAll()}
            disabled={isRevokingAll}
            className="inline-flex items-center gap-2 rounded-lg border border-red-500/40 bg-red-500/10 px-3 py-1.5 text-sm font-medium text-red-400 hover:bg-red-500/20 disabled:opacity-50"
          >
            <ShieldAlert className="h-4 w-4" />
            {isRevokingAll ? 'Revoking…' : 'Revoke all'}
          </button>
        )}
      </div>

      {isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}

      {!isLoading && sessions.length === 0 && (
        <div className="flex h-32 items-center justify-center rounded-xl border border-dashed border-border text-muted-foreground">
          No sessions found.
        </div>
      )}

      {active.length > 0 && (
        <Section title="Active">
          {active.map(s => (
            <SessionRow
              key={s.id}
              session={s}
              onRevoke={() => revoke(s.id)}
              isRevoking={isRevoking && revokingId === s.id}
            />
          ))}
        </Section>
      )}

      {expired.length > 0 && (
        <Section title="Expired">
          {expired.map(s => (
            <SessionRow key={s.id} session={s} expired />
          ))}
        </Section>
      )}
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="space-y-2">
      <h2 className="text-xs font-medium uppercase tracking-wider text-muted-foreground">{title}</h2>
      <div className="overflow-hidden rounded-xl border border-border divide-y divide-border">
        {children}
      </div>
    </div>
  )
}

function SessionRow({ session, onRevoke, isRevoking, expired }: {
  session: SessionItem
  onRevoke?: () => void
  isRevoking?: boolean
  expired?: boolean
}) {
  return (
    <div className="flex items-center justify-between px-4 py-3">
      <div className="space-y-0.5">
        <p className="font-mono text-xs text-foreground">{session.id.slice(0, 16)}…</p>
        <p className="text-xs text-muted-foreground">
          Started {timeAgo(session.createdAt)} ·{' '}
          {expired
            ? `Expired ${timeAgo(session.expiresAt)}`
            : `Expires ${new Date(session.expiresAt).toLocaleTimeString()}`}
        </p>
      </div>

      {!expired && onRevoke && (
        <button
          onClick={onRevoke}
          disabled={isRevoking}
          className="inline-flex items-center gap-1.5 rounded-lg border border-border px-3 py-1.5 text-xs text-muted-foreground hover:border-red-500/40 hover:text-red-400 disabled:opacity-50 transition-colors"
        >
          <LogOut className="h-3 w-3" />
          {isRevoking ? 'Revoking…' : 'Revoke'}
        </button>
      )}
    </div>
  )
}
