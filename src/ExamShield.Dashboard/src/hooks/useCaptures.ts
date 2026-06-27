import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'

export function useCaptures(
  page = 1, pageSize = 50,
  examId?: string, status?: string,
  deviceId?: string, studentId?: string
) {
  return useQuery({
    queryKey: ['captures', page, pageSize, examId, status, deviceId, studentId],
    queryFn: () => api.getCaptures(page, pageSize, examId, status, deviceId, studentId),
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

export function useFlagCaptureAsTampered() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ captureId, reason }: { captureId: string; reason: string }) =>
      api.flagCaptureAsTampered(captureId, reason),
    onSuccess: (_data, vars) => {
      qc.invalidateQueries({ queryKey: ['chain-of-custody', vars.captureId] })
      qc.invalidateQueries({ queryKey: ['captures'] })
    },
  })
}
