import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'

export function useUsers(page = 1, pageSize = 50, search?: string, role?: string) {
  return useQuery({
    queryKey: ['users', page, pageSize, search, role],
    queryFn: () => api.getUsers(page, pageSize, search, role),
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
