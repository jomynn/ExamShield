import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'

export function useDevices() {
  return useQuery({
    queryKey: ['devices'],
    queryFn: api.getDevices,
  })
}

export function useDisableDevice() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.disableDevice(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['devices'] }),
  })
}

export function useEnableDevice() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.enableDevice(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['devices'] }),
  })
}

export function useDeviceHeartbeat() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.deviceHeartbeat(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['devices'] }),
  })
}
