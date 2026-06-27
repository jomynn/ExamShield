import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, UpdateSettingsPayload } from '../api/client'

export function useSettings() {
  return useQuery({
    queryKey: ['settings'],
    queryFn: () => api.getSettings(),
  })
}

export function useUpdateSettings() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdateSettingsPayload) => api.updateSettings(payload),
    onSuccess: data => qc.setQueryData(['settings'], data),
  })
}

export function useTestAlert() {
  return useMutation({
    mutationFn: () => api.testAlert(),
  })
}
