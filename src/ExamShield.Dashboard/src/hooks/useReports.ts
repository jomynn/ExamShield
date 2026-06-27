import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'

export function useReportSummary() {
  return useQuery({
    queryKey: ['report-summary'],
    queryFn: () => api.getReportSummary(),
  })
}

export function useExamReport(examId: string | null) {
  return useQuery({
    queryKey: ['exam-report', examId],
    queryFn: () => api.getExamReport(examId!),
    enabled: examId !== null,
  })
}
