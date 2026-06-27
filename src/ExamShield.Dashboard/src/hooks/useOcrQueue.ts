import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'

export function useOcrQueue() {
  return useQuery({
    queryKey: ['ocr-queue'],
    queryFn: () => api.getOcrQueue(),
    refetchInterval: 30_000,
  })
}

export function useTriggerOcr() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (captureId: string) => api.triggerOcr(captureId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ocr-queue'] }),
  })
}

export function useBatchOcr() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (examId: string) => api.triggerBatchOcr(examId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ocr-queue'] }),
  })
}
