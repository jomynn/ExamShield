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

export function useAnswerKey(examId: string | null) {
  return useQuery({
    queryKey: ['answer-key', examId],
    queryFn: () => api.getAnswerKey(examId!),
    enabled: !!examId,
    retry: false,
  })
}

export function useSetAnswerKey() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ examId, answers }: { examId: string; answers: Record<number, string> }) =>
      api.setAnswerKey(examId, answers),
    onSuccess: (_data, vars) => qc.invalidateQueries({ queryKey: ['answer-key', vars.examId] }),
  })
}

export function useExamCandidates(examId: string | null) {
  return useQuery({
    queryKey: ['exam-candidates', examId],
    queryFn: () => api.getExamCandidates(examId!),
    enabled: !!examId,
  })
}

export function useEnrollStudent() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ examId, studentId }: { examId: string; studentId: string }) =>
      api.enrollStudent(examId, studentId),
    onSuccess: (_data, vars) => qc.invalidateQueries({ queryKey: ['exam-candidates', vars.examId] }),
  })
}
