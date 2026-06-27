import type { RealtimeNotification } from '../../hooks/useNotifications'

const SEVERITY_STYLES: Record<string, string> = {
  Critical: 'border-red-500 bg-red-950/80 text-red-100',
  High:     'border-orange-500 bg-orange-950/80 text-orange-100',
  Warning:  'border-yellow-500 bg-yellow-950/80 text-yellow-100',
  Info:     'border-blue-500 bg-blue-950/80 text-blue-100',
}

const SEVERITY_ICON: Record<string, string> = {
  Critical: '🚨',
  High:     '⚠️',
  Warning:  '⚡',
  Info:     'ℹ️',
}

interface Props {
  notifications: RealtimeNotification[]
  onDismiss: (index: number) => void
}

export default function NotificationToast({ notifications, onDismiss }: Props) {
  if (notifications.length === 0) return null

  return (
    <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 max-w-sm w-full">
      {notifications.slice(0, 5).map((n, i) => (
        <div
          key={i}
          className={`flex items-start gap-3 rounded-lg border px-4 py-3 shadow-lg backdrop-blur-sm ${SEVERITY_STYLES[n.severity] ?? SEVERITY_STYLES.Info}`}
        >
          <span className="text-lg shrink-0">{SEVERITY_ICON[n.severity] ?? 'ℹ️'}</span>
          <div className="flex-1 min-w-0">
            <p className="text-xs font-semibold uppercase tracking-wide opacity-70">
              {n.type.replace(/([A-Z])/g, ' $1').trim()}
            </p>
            <p className="text-sm mt-0.5 break-words">{n.message}</p>
            <p className="text-xs opacity-50 mt-1">
              {new Date(n.occurredAt).toLocaleTimeString()}
            </p>
          </div>
          <button
            onClick={() => onDismiss(i)}
            className="text-lg opacity-50 hover:opacity-100 shrink-0 leading-none"
            aria-label="Dismiss"
          >
            ×
          </button>
        </div>
      ))}
    </div>
  )
}
