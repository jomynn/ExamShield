import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'

export function useCaptures() {
  return useQuery({
    queryKey: ['captures'],
    queryFn: () => api.getCaptures(),
    refetchInterval: 30_000,
  })
}

export function useVerifyCapture() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (captureId: string) => api.verifyCapture(captureId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['captures'] }),
  })
}

export function useChainOfCustody(captureId: string | null) {
  return useQuery({
    queryKey: ['chain-of-custody', captureId],
    queryFn: () => api.getChainOfCustody(captureId!),
    enabled: !!captureId,
  })
}
