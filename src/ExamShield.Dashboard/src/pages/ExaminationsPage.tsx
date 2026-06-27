import { useState } from 'react'
import {
  useExams, useCreateExam, useActivateExam, useCloseExam,
  useAnswerKey, useSetAnswerKey, useExamCandidates, useEnrollStudent,
  useUnenrollStudent, useExamSubmissionStatus,
} from '../hooks/useExams'
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
  const setKey = useSetAnswerKey()

  const [showForm, setShowForm] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [totalQuestions, setTotalQuestions] = useState(50)

  const [keyExamId, setKeyExamId] = useState<string | null>(null)
  const [keyExamTotalQ, setKeyExamTotalQ] = useState(0)
  const [keyAnswers, setKeyAnswers] = useState<Record<number, string>>({})
  const { data: existingKey } = useAnswerKey(keyExamId)

  const [enrollExamId, setEnrollExamId] = useState<string | null>(null)
  const [newStudentId, setNewStudentId] = useState('')
  const { data: candidatesData } = useExamCandidates(enrollExamId)
  const { data: statusData }    = useExamSubmissionStatus(enrollExamId)
  const enroll   = useEnrollStudent()
  const unenroll = useUnenrollStudent()

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

  function openKeyModal(examId: string, totalQ: number) {
    setKeyExamId(examId)
    setKeyExamTotalQ(totalQ)
    setKeyAnswers({})
  }

  function handleSaveKey() {
    if (!keyExamId) return
    setKey.mutate({ examId: keyExamId, answers: keyAnswers }, {
      onSuccess: () => setKeyExamId(null),
    })
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
            <label htmlFor="exam-name" className="text-sm font-medium">Exam Name</label>
            <input
              id="exam-name" value={name} onChange={e => setName(e.target.value)} required
              className="w-full rounded border px-3 py-2 text-sm"
              placeholder="e.g. Mathematics Final 2026"
            />
          </div>
          <div className="space-y-1">
            <label htmlFor="exam-description" className="text-sm font-medium">Description</label>
            <input
              id="exam-description" value={description} onChange={e => setDescription(e.target.value)}
              className="w-full rounded border px-3 py-2 text-sm"
            />
          </div>
          <div className="space-y-1">
            <label htmlFor="total-questions" className="text-sm font-medium">Total Questions</label>
            <input
              id="total-questions" type="number" min={1}
              value={totalQuestions} onChange={e => setTotalQuestions(Number(e.target.value))} required
              className="w-full rounded border px-3 py-2 text-sm"
            />
          </div>
          <button
            type="submit" disabled={create.isPending}
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
                        <>
                          <button
                            onClick={() => openKeyModal(exam.examId, exam.totalQuestions)}
                            className="px-3 py-1 rounded text-xs bg-blue-600 text-white hover:bg-blue-700"
                          >
                            Answer Key
                          </button>
                          <button
                            onClick={() => { setEnrollExamId(exam.examId); setNewStudentId('') }}
                            className="px-3 py-1 rounded text-xs bg-teal-600 text-white hover:bg-teal-700"
                          >
                            Students
                          </button>
                          <button
                            onClick={() => close.mutate(exam.examId)}
                            disabled={close.isPending}
                            className="px-3 py-1 rounded text-xs bg-amber-600 text-white hover:bg-amber-700 disabled:opacity-50"
                          >
                            Close
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <Pagination page={page} totalPages={data?.totalPages ?? 1} onPageChange={setPage} />
        </div>
      )}

      {/* Answer Key Modal */}
      {keyExamId && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-background rounded-xl border p-6 w-full max-w-lg space-y-4 max-h-[80vh] overflow-y-auto">
            <h2 className="text-lg font-semibold">
              {existingKey ? 'Answer Key (read-only — already set)' : 'Set Answer Key'}
            </h2>

            {existingKey ? (
              <div className="space-y-2">
                {Object.entries(existingKey.answers).map(([q, a]) => (
                  <div key={q} className="flex items-center gap-3 text-sm">
                    <span className="w-24 text-muted-foreground">Question {q}</span>
                    <span className="font-mono font-bold">{a}</span>
                  </div>
                ))}
              </div>
            ) : (
              <div className="space-y-2">
                {Array.from({ length: keyExamTotalQ }, (_, i) => i + 1).map(q => (
                  <div key={q} className="flex items-center gap-3">
                    <label className="w-24 text-sm text-muted-foreground">Question {q}</label>
                    <input
                      value={keyAnswers[q] ?? ''}
                      onChange={e => setKeyAnswers(prev => ({ ...prev, [q]: e.target.value.toUpperCase() }))}
                      maxLength={1}
                      className="w-16 rounded border px-2 py-1 text-sm font-mono text-center uppercase"
                      placeholder="A"
                    />
                  </div>
                ))}
              </div>
            )}

            <div className="flex gap-2 pt-2">
              {!existingKey && (
                <button
                  onClick={handleSaveKey}
                  disabled={setKey.isPending || Object.keys(keyAnswers).length === 0}
                  className="px-4 py-2 rounded bg-primary text-primary-foreground text-sm hover:bg-primary/90 disabled:opacity-50"
                >
                  {setKey.isPending ? 'Saving…' : 'Save Answer Key'}
                </button>
              )}
              <button
                onClick={() => setKeyExamId(null)}
                className="px-4 py-2 rounded border text-sm hover:bg-muted/30"
              >
                {existingKey ? 'Close' : 'Cancel'}
              </button>
            </div>

            {setKey.isError && (
              <p className="text-sm text-red-500">{String(setKey.error)}</p>
            )}
          </div>
        </div>
      )}

      {/* Enrollment Modal */}
      {enrollExamId && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-background rounded-xl border p-6 w-full max-w-md space-y-4 max-h-[80vh] overflow-y-auto">
            <h2 className="text-lg font-semibold">Enrolled Students</h2>

            {statusData && statusData.totalEnrolled > 0 && (
              <div className="space-y-1">
                <div className="flex justify-between text-xs text-muted-foreground">
                  <span>Submissions</span>
                  <span>{statusData.submitted} / {statusData.totalEnrolled}</span>
                </div>
                <div className="w-full h-2 rounded-full bg-muted overflow-hidden">
                  <div
                    className="h-2 rounded-full bg-green-500 transition-all"
                    style={{ width: `${(statusData.submitted / statusData.totalEnrolled) * 100}%` }}
                  />
                </div>
                {statusData.missing > 0 && (
                  <p className="text-xs text-amber-400">{statusData.missing} student(s) have not submitted yet.</p>
                )}
              </div>
            )}

            <form
              onSubmit={e => {
                e.preventDefault()
                enroll.mutate({ examId: enrollExamId, studentId: newStudentId }, {
                  onSuccess: () => setNewStudentId(''),
                })
              }}
              className="flex gap-2"
            >
              <input
                value={newStudentId}
                onChange={e => setNewStudentId(e.target.value)}
                placeholder="Student ID (UUID)"
                required
                className="flex-1 rounded border px-3 py-1.5 text-sm bg-background"
              />
              <button
                type="submit"
                disabled={enroll.isPending}
                className="px-4 py-1.5 rounded bg-teal-600 text-white text-sm hover:bg-teal-700 disabled:opacity-50"
              >
                Enroll
              </button>
            </form>

            {enroll.isError && (
              <p className="text-sm text-red-400">{String(enroll.error)}</p>
            )}

            <div className="space-y-1 max-h-64 overflow-y-auto">
              {(candidatesData?.candidates ?? []).length === 0
                ? <p className="text-sm text-muted-foreground">No students enrolled yet.</p>
                : (candidatesData?.candidates ?? []).map(c => {
                  const sub = statusData?.students.find(s => s.studentId === c.studentId)
                  return (
                    <div key={c.studentId} className="flex items-center justify-between text-sm py-1 border-b gap-2">
                      <span className="font-mono text-xs flex-1 truncate">{c.studentId}</span>
                      <span className={`text-xs px-1.5 py-0.5 rounded ${sub?.hasSubmitted ? 'bg-green-900/40 text-green-400' : 'bg-amber-900/40 text-amber-400'}`}>
                        {sub?.hasSubmitted ? (sub.captureStatus ?? 'Submitted') : 'Missing'}
                      </span>
                      {!sub?.hasSubmitted && (
                        <button
                          onClick={() => unenroll.mutate({ examId: enrollExamId!, studentId: c.studentId })}
                          disabled={unenroll.isPending}
                          className="text-xs text-red-400 hover:text-red-300 disabled:opacity-40"
                          title="Remove from exam"
                        >
                          ✕
                        </button>
                      )}
                    </div>
                  )
                })
              }
            </div>

            <button
              onClick={() => setEnrollExamId(null)}
              className="px-4 py-2 rounded border text-sm hover:bg-muted/30"
            >
              Close
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
