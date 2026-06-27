import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useSettings, useUpdateSettings, useTestAlert } from '../hooks/useSettings'
import { api, type NotificationChannelSettingsPayload } from '../api/client'

const SEVERITY_OPTIONS = ['Info', 'Warning', 'High', 'Critical']

const DEFAULT_CHANNELS: NotificationChannelSettingsPayload = {
  emailEnabled: false,   emailRecipients: null,
  slackEnabled: false,   slackWebhookUrl: null,
  lineEnabled: false,    lineNotifyToken: null,
  webhookEnabled: false, webhookUrl: null,
}

function ChannelRow({ label, enabled, onToggle, children }: {
  label: string
  enabled: boolean
  onToggle: (v: boolean) => void
  children?: React.ReactNode
}) {
  return (
    <div className="rounded-lg border border-border p-4 space-y-3">
      <label className="flex items-center gap-3 cursor-pointer">
        <input
          type="checkbox"
          checked={enabled}
          onChange={e => onToggle(e.target.checked)}
          className="h-4 w-4 rounded"
        />
        <span className="text-sm font-medium text-foreground">{label}</span>
      </label>
      {enabled && children}
    </div>
  )
}

function Field({ label, value, onChange, placeholder, type = 'text' }: {
  label: string
  value: string
  onChange: (v: string) => void
  placeholder?: string
  type?: string
}) {
  return (
    <label className="block space-y-1">
      <span className="text-xs text-muted-foreground">{label}</span>
      <input
        type={type}
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        className="w-full rounded border border-border bg-background px-3 py-1.5 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-primary"
      />
    </label>
  )
}

function NotificationChannelSection() {
  const qc = useQueryClient()
  const { data, isLoading } = useQuery({
    queryKey: ['notification-channel-settings'],
    queryFn: api.getNotificationChannelSettings,
  })

  const [channels, setChannels] = useState<NotificationChannelSettingsPayload>(DEFAULT_CHANNELS)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (data) setChannels({
      emailEnabled:   data.emailEnabled,   emailRecipients:  data.emailRecipients,
      slackEnabled:   data.slackEnabled,   slackWebhookUrl:  data.slackWebhookUrl,
      lineEnabled:    data.lineEnabled,    lineNotifyToken:  data.lineNotifyToken,
      webhookEnabled: data.webhookEnabled, webhookUrl:       data.webhookUrl,
    })
  }, [data])

  const { mutate: save, isPending } = useMutation({
    mutationFn: (p: NotificationChannelSettingsPayload) => api.updateNotificationChannelSettings(p),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['notification-channel-settings'] })
      setSaved(true)
      setError('')
      setTimeout(() => setSaved(false), 3000)
    },
    onError: async (err: unknown) => {
      const res = err instanceof Response ? await res?.json?.() : null
      setError(res?.title ?? 'Failed to save notification settings.')
    },
  })

  const set = <K extends keyof NotificationChannelSettingsPayload>(
    key: K, value: NotificationChannelSettingsPayload[K]
  ) => setChannels(prev => ({ ...prev, [key]: value }))

  if (isLoading) return <p className="text-sm text-muted-foreground">Loading…</p>

  return (
    <section className="space-y-4 border-t pt-6">
      <div>
        <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">
          Alert Channels
        </h2>
        <p className="mt-1 text-xs text-muted-foreground">
          Configure where security alerts and tamper notifications are sent.
        </p>
      </div>

      <div className="space-y-3">
        <ChannelRow label="Email" enabled={channels.emailEnabled} onToggle={v => set('emailEnabled', v)}>
          <Field
            label="Recipients (comma-separated)"
            value={channels.emailRecipients ?? ''}
            onChange={v => set('emailRecipients', v || null)}
            placeholder="ops@example.com, security@example.com"
          />
        </ChannelRow>

        <ChannelRow label="Slack" enabled={channels.slackEnabled} onToggle={v => set('slackEnabled', v)}>
          <Field
            label="Webhook URL"
            value={channels.slackWebhookUrl ?? ''}
            onChange={v => set('slackWebhookUrl', v || null)}
            placeholder="https://hooks.slack.com/services/T.../B.../..."
          />
        </ChannelRow>

        <ChannelRow label="LINE Notify" enabled={channels.lineEnabled} onToggle={v => set('lineEnabled', v)}>
          <Field
            label="Access Token"
            value={channels.lineNotifyToken ?? ''}
            onChange={v => set('lineNotifyToken', v || null)}
            placeholder="LINE Notify access token"
            type="password"
          />
        </ChannelRow>

        <ChannelRow label="Generic Webhook" enabled={channels.webhookEnabled} onToggle={v => set('webhookEnabled', v)}>
          <Field
            label="Webhook URL"
            value={channels.webhookUrl ?? ''}
            onChange={v => set('webhookUrl', v || null)}
            placeholder="https://your-service.example.com/notify"
          />
        </ChannelRow>
      </div>

      <div className="flex items-center gap-4">
        <button
          onClick={() => save(channels)}
          disabled={isPending}
          className="px-4 py-2 rounded-lg bg-primary text-primary-foreground text-sm font-medium hover:opacity-90 disabled:opacity-50"
        >
          {isPending ? 'Saving…' : 'Save Channels'}
        </button>
        {saved && <span className="text-sm text-green-500">Channel settings saved.</span>}
        {error && <span className="text-sm text-red-400">{error}</span>}
      </div>
    </section>
  )
}

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

      {/* Alert channel configuration */}
      <NotificationChannelSection />

      {/* Test alert */}
      <section className="space-y-3 border-t pt-6">
        <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider">
          Test Alert
        </h2>
        <p className="text-sm text-muted-foreground">
          Send a test notification to all configured alert channels.
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
