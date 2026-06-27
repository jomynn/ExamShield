import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type CreateExamPayload } from '../api/client'

export function useExams(page = 1, pageSize = 50) {
  return useQuery({
    queryKey: ['exams', page, pageSize],
    queryFn: () => api.getExams(page, pageSize),
  })
}

export function useCreateExam() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateExamPayload) => api.createExam(payload),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['exams'] }),
  })
}

export function useActivateExam() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (examId: string) => api.activateExam(examId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['exams'] }),
  })
}

export function useCloseExam() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (examId: string) => api.closeExam(examId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['exams'] }),
  })
}
