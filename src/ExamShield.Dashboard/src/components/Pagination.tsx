interface PaginationProps {
  page: number
  totalPages: number
  onPageChange: (page: number) => void
}

export default function Pagination({ page, totalPages, onPageChange }: PaginationProps) {
  if (totalPages <= 1) return null

  return (
    <div className="flex items-center justify-between px-4 py-3" style={{ borderTop: '1px solid var(--glass-border)' }}>
      <p className="text-xs text-muted-foreground">
        {`Page ${page} of ${totalPages}`}
      </p>
      <div className="flex gap-2">
        <button
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1}
          className="btn-glass text-xs px-3 py-1.5 disabled:opacity-40 disabled:cursor-not-allowed"
        >
          ← Previous
        </button>
        <button
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages}
          className="btn-glass text-xs px-3 py-1.5 disabled:opacity-40 disabled:cursor-not-allowed"
        >
          Next →
        </button>
      </div>
    </div>
  )
}
