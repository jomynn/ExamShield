import { useState } from 'react'
import { useExams, useCreateExam, useActivateExam, useCloseExam } from '../hooks/useExams'
import StatusChip from '../components/ui/StatusChip'
import Pagination from '../components/Pagination'

const STATUS_VARIANT: Record<string, 'success' | 'warning' | 'muted'> = {
  Active: 'success',
  Draft:  'warning',
  Closed: 'muted',
}

const PAGE_SIZE = 20

export default function ExaminationsPage() {
  const [page, setPage] = useState(1)
  const { data, isLoading } = useExams(page, PAGE_SIZE)
  const create = useCreateExam()
  const activate = useActivateExam()
  const close = useCloseExam()
  const [showForm, setShowForm] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [totalQuestions, setTotalQuestions] = useState(50)

  if (isLoading) return <p>Loading...</p>

  const exams = data?.exams ?? []

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    create.mutate(
      { name, description, totalQuestions },
      {
        onSuccess: () => {
          setShowForm(false)
          setName('')
          setDescription('')
          setTotalQuestions(50)
        },
      }
    )
  }

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">
          Examinations{data ? ` (${data.totalCount})` : ''}
        </h1>
        <button
          onClick={() => setShowForm(v => !v)}
          className="px-4 py-2 rounded bg-primary text-primary-foreground text-sm hover:bg-primary/90"
        >
          Create Exam
        </button>
      </div>

      {showForm && (
        <form onSubmit={handleSubmit} className="rounded-lg border p-4 space-y-3 max-w-md">
          <div className="space-y-1">
            <label htmlFor="exam-name" className="text-sm font-medium">
              Exam Name
            </label>
            <input
              id="exam-name"
              value={name}
              onChange={e => setName(e.target.value)}
              required
              className="w-full rounded border px-3 py-2 text-sm"
              placeholder="e.g. Mathematics Final 2026"
            />
          </div>
          <div className="space-y-1">
            <label htmlFor="exam-description" className="text-sm font-medium">
              Description
            </label>
            <input
              id="exam-description"
              value={description}
              onChange={e => setDescription(e.target.value)}
              className="w-full rounded border px-3 py-2 text-sm"
            />
          </div>
          <div className="space-y-1">
            <label htmlFor="total-questions" className="text-sm font-medium">
              Total Questions
            </label>
            <input
              id="total-questions"
              type="number"
              min={1}
              value={totalQuestions}
              onChange={e => setTotalQuestions(Number(e.target.value))}
              required
              className="w-full rounded border px-3 py-2 text-sm"
            />
          </div>
          <button
            type="submit"
            disabled={create.isPending}
            className="px-4 py-2 rounded bg-primary text-primary-foreground text-sm hover:bg-primary/90 disabled:opacity-50"
          >
            Create
          </button>
        </form>
      )}

      {exams.length === 0 ? (
        <p className="text-muted-foreground">No exams configured yet.</p>
      ) : (
        <div className="rounded-lg border border-border overflow-hidden">
          <table className="w-full text-sm border-collapse">
            <thead>
              <tr className="border-b text-left bg-muted/20">
                <th className="py-2 px-4">Name</th>
                <th className="py-2 px-4">Status</th>
                <th className="py-2 px-4">Questions</th>
                <th className="py-2 px-4">Created</th>
                <th className="py-2 px-4">Actions</th>
              </tr>
            </thead>
            <tbody>
              {exams.map(exam => (
                <tr key={exam.examId} className="border-b hover:bg-muted/30">
                  <td className="py-2 px-4 font-medium">{exam.name}</td>
                  <td className="py-2 px-4">
                    <StatusChip variant={STATUS_VARIANT[exam.status] ?? 'muted'} label={exam.status} />
                  </td>
                  <td className="py-2 px-4">{exam.totalQuestions}</td>
                  <td className="py-2 px-4 text-muted-foreground">
                    {new Date(exam.createdAt).toLocaleDateString()}
                  </td>
                  <td className="py-2 px-4">
                    <div className="flex gap-2">
                      {exam.status === 'Draft' && (
                        <button
                          onClick={() => activate.mutate(exam.examId)}
                          disabled={activate.isPending}
                          className="px-3 py-1 rounded text-xs bg-green-600 text-white hover:bg-green-700 disabled:opacity-50"
                        >
                          Activate
                        </button>
                      )}
                      {exam.status === 'Active' && (
                        <button
                          onClick={() => close.mutate(exam.examId)}
                          disabled={close.isPending}
                          className="px-3 py-1 rounded text-xs bg-amber-600 text-white hover:bg-amber-700 disabled:opacity-50"
                        >
                          Close
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <Pagination
            page={page}
            totalPages={data?.totalPages ?? 1}
            onPageChange={setPage}
          />
        </div>
      )}
    </div>
  )
}
