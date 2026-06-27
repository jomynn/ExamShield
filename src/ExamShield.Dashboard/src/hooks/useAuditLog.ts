import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'

export function useAuditLog(page: number, pageSize = 20, captureId?: string, action?: string) {
  return useQuery({
    queryKey: ['audit', page, pageSize, captureId, action],
    queryFn: () => api.getAuditLog({ page, pageSize, captureId, action }),
    placeholderData: prev => prev,
  })
}
