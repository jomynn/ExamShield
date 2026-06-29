import { cn } from '../../lib/utils'

export type StatusVariant = 'success' | 'danger' | 'warning' | 'info' | 'muted'

interface StatusChipProps {
  label: string
  variant: StatusVariant
}

const variantStyles: Record<StatusVariant, { dot: string; pill: string }> = {
  success: {
    dot:  'bg-green-400 shadow-[0_0_6px_rgba(74,222,128,0.6)]',
    pill: 'bg-green-500/12 text-green-400 border border-green-500/20',
  },
  danger: {
    dot:  'bg-red-400 shadow-[0_0_6px_rgba(248,113,113,0.6)]',
    pill: 'bg-red-500/12 text-red-400 border border-red-500/20',
  },
  warning: {
    dot:  'bg-yellow-400 shadow-[0_0_6px_rgba(250,204,21,0.6)]',
    pill: 'bg-yellow-500/12 text-yellow-400 border border-yellow-500/20',
  },
  info: {
    dot:  'bg-blue-400 shadow-[0_0_6px_rgba(96,165,250,0.5)]',
    pill: 'bg-blue-500/12 text-blue-400 border border-blue-500/20',
  },
  muted: {
    dot:  'bg-muted-foreground/60',
    pill: 'bg-muted text-muted-foreground border border-border',
  },
}

export default function StatusChip({ label, variant }: StatusChipProps) {
  const s = variantStyles[variant]
  return (
    <span className={cn(
      'inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium',
      s.pill
    )}>
      <span className={cn('h-1.5 w-1.5 rounded-full shrink-0', s.dot)} />
      {label}
    </span>
  )
}
