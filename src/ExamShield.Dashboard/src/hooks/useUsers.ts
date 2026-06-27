import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'

export function useUsers(page = 1, pageSize = 50, search?: string, role?: string, isActive?: boolean) {
  return useQuery({
    queryKey: ['users', page, pageSize, search, role, isActive],
    queryFn: () => api.getUsers(page, pageSize, search, role, isActive),
  })
}

export function useUserDetail(userId: string | null) {
  return useQuery({
    queryKey: ['user', userId],
    queryFn: () => api.getUserById(userId!),
    enabled: userId !== null,
  })
}

export function useUpdateUserProfile() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ userId, displayName }: { userId: string; displayName: string | null }) =>
      api.updateUserProfile(userId, displayName),
    onSuccess: (_data, { userId }) => {
      qc.invalidateQueries({ queryKey: ['users'] })
      qc.invalidateQueries({ queryKey: ['user', userId] })
    },
  })
}

export function useUpdateUserRole() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: string }) =>
      api.updateUserRole(userId, role),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
  })
}

export function useDeactivateUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) => api.deactivateUser(userId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
  })
}

export function useActivateUser() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (userId: string) => api.activateUser(userId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
  })
}
