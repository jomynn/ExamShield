import { useState, useEffect } from 'react'
import { useSettings, useUpdateSettings, useTestAlert } from '../hooks/useSettings'

const SEVERITY_OPTIONS = ['Info', 'Warning', 'High', 'Critical']

export default function SettingsPage() {
  const { data, isLoading } = useSettings()
  const update    = useUpdateSettings()
  const testAlert = useTestAlert()
  const [saved, setSaved]             = useState(false)
  const [alertResult, setAlertResult] = useState<{ sent: boolean; error: string | null } | null>(null)

  const [form, setForm] = useState({
    ocrConfidenceThreshold: 0.85,
    notificationsEnabled: true,
    notificationSeverity: 'High',
    accessTokenExpiryMinutes: 60,
    refreshTokenExpiryDays: 7,
  })

  useEffect(() => {
    if (data) setForm(data)
  }, [data])

  if (isLoading) return <p>Loading...</p>

  const set = <K extends keyof typeof form>(key: K, value: typeof form[K]) =>
    setForm(f => ({ ...f, [key]: value }))

  const handleSave = async () => {
    await update.mutateAsync(form)
    setSaved(true)
    setTimeout(() => setSaved(false), 3000)
  }

  return (
    <div className="p-6 space-y-8 max-w-2xl">
      <h1 className="text-2xl font-bold">Settings</h1>

      {/* OCR */}
      <section className="space-y-4">
        <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">
          OCR Processing
        </h2>
        <label className="block space-y-1">
          <span className="text-sm text-foreground">OCR Confidence Threshold</span>
          <div className="flex items-center gap-3">
            <input
              id="ocr-confidence"
              aria-label="OCR Confidence Threshold"
              type="range"
              min={0.5}
              max={1.0}
              step={0.01}
              value={form.ocrConfidenceThreshold}
              onChange={e => set('ocrConfidenceThreshold', parseFloat(e.target.value))}
              className="flex-1"
            />
            <span className="w-12 text-sm font-mono text-right">
              {(form.ocrConfidenceThreshold * 100).toFixed(0)}%
            </span>
          </div>
          <p className="text-xs text-muted-foreground">
            Captures with OCR confidence below this threshold are routed to manual review.
          </p>
        </label>
      </section>

      {/* Notifications */}
      <section className="space-y-4">
        <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">
          Notifications
        </h2>
        <label className="flex items-center gap-3">
          <input
            id="notifications-enabled"
            aria-label="Notifications Enabled"
            type="checkbox"
            checked={form.notificationsEnabled}
            onChange={e => set('notificationsEnabled', e.target.checked)}
            className="h-4 w-4 rounded"
          />
          <span className="text-sm text-foreground">Notifications Enabled</span>
        </label>
        <label className="block space-y-1">
          <span className="text-sm text-foreground">Minimum Alert Severity</span>
          <select
            aria-label="Notification Severity"
            value={form.notificationSeverity}
            onChange={e => set('notificationSeverity', e.target.value)}
            className="w-full rounded border border-border bg-background px-3 py-2 text-sm text-foreground"
          >
            {SEVERITY_OPTIONS.map(s => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </label>
      </section>

      {/* Auth */}
      <section className="space-y-4">
        <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">
          Authentication
        </h2>
        <label className="block space-y-1">
          <span className="text-sm text-foreground">Access Token Expiry (minutes)</span>
          <input
            id="access-token-expiry"
            aria-label="Access Token Expiry"
            type="number"
            min={5}
            max={1440}
            value={form.accessTokenExpiryMinutes}
            onChange={e => set('accessTokenExpiryMinutes', parseInt(e.target.value, 10))}
            className="w-full rounded border border-border bg-background px-3 py-2 text-sm text-foreground"
          />
        </label>
        <label className="block space-y-1">
          <span className="text-sm text-foreground">Refresh Token Expiry (days)</span>
          <input
            id="refresh-token-expiry"
            aria-label="Refresh Token Expiry"
            type="number"
            min={1}
            max={90}
            value={form.refreshTokenExpiryDays}
            onChange={e => set('refreshTokenExpiryDays', parseInt(e.target.value, 10))}
            className="w-full rounded border border-border bg-background px-3 py-2 text-sm text-foreground"
          />
        </label>
      </section>

      <div className="flex items-center gap-4">
        <button
          onClick={handleSave}
          disabled={update.isPending}
          className="px-6 py-2 rounded bg-primary text-primary-foreground font-medium text-sm hover:bg-primary/90 disabled:opacity-50"
        >
          {update.isPending ? 'Saving…' : 'Save Settings'}
        </button>
        {saved && (
          <span className="text-sm text-green-500">Settings saved successfully.</span>
        )}
      </div>

      <section className="space-y-3 border-t pt-6">
        <h2 className="text-base font-semibold">Alert Channels</h2>
        <p className="text-sm text-muted-foreground">
          Send a test notification to all configured alert channels (Slack, Teams, Email, LINE Notify, Webhook).
        </p>
        <div className="flex items-center gap-4">
          <button
            onClick={() =>
              testAlert.mutate(undefined, {
                onSuccess: r => setAlertResult(r),
                onError: () => setAlertResult({ sent: false, error: 'Request failed' }),
              })
            }
            disabled={testAlert.isPending}
            className="px-4 py-2 rounded border text-sm font-medium hover:bg-muted disabled:opacity-50"
          >
            {testAlert.isPending ? 'Sending…' : 'Send Test Alert'}
          </button>
          {alertResult && (
            <span className={`text-sm ${alertResult.sent ? 'text-green-500' : 'text-red-400'}`}>
              {alertResult.sent
                ? 'Test alert sent successfully.'
                : `Failed: ${alertResult.error ?? 'unknown error'}`}
            </span>
          )}
        </div>
      </section>
    </div>
  )
}
