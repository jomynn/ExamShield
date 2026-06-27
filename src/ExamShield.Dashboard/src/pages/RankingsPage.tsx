import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api, type ExamItem, type RankingEntry, type ExamStatisticsResponse } from '../api/client'
import { Trophy, Medal, Users, TrendingUp, CheckCircle } from 'lucide-react'
import { cn } from '../lib/utils'

const RANK_STYLE: Record<number, string> = {
  1: 'text-yellow-400 font-bold',
  2: 'text-slate-400 font-bold',
  3: 'text-amber-600 font-bold',
}

const RANK_ICON: Record<number, React.ReactNode> = {
  1: <Trophy className="h-4 w-4 text-yellow-400" />,
  2: <Medal className="h-4 w-4 text-slate-400" />,
  3: <Medal className="h-4 w-4 text-amber-600" />,
}

function PercentageBar({ value }: { value: number }) {
  const pct = Math.round(value)
  const color = pct >= 80 ? 'bg-green-500' : pct >= 60 ? 'bg-blue-500' : pct >= 40 ? 'bg-yellow-500' : 'bg-red-500'
  return (
    <div className="flex items-center gap-2">
      <div className="h-2 w-24 overflow-hidden rounded-full bg-muted">
        <div className={cn('h-full rounded-full transition-all', color)} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-sm tabular-nums text-foreground">{pct}%</span>
    </div>
  )
}

function RankingsTable({ rows, totalQuestions }: { rows: RankingEntry[]; totalQuestions: number }) {
  if (rows.length === 0) {
    return (
      <div className="flex h-32 items-center justify-center rounded-xl border border-dashed border-border text-muted-foreground">
        No scores submitted yet.
      </div>
    )
  }

  return (
    <div className="overflow-hidden rounded-xl border border-border">
      <table className="w-full text-sm">
        <thead className="bg-muted/50">
          <tr>
            {['Rank', 'Student', 'Score', 'Percentage'].map(h => (
              <th key={h} className="px-4 py-2 text-left text-xs font-medium text-muted-foreground">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {rows.map((r, i) => (
            <tr key={i} className={cn('transition-colors', r.rank <= 3 && 'bg-muted/20')}>
              <td className="px-4 py-3">
                <span className={cn('flex items-center gap-1.5', RANK_STYLE[r.rank] ?? 'text-muted-foreground')}>
                  {RANK_ICON[r.rank] ?? null}
                  #{r.rank}
                </span>
              </td>
              <td className="px-4 py-3 font-mono text-xs text-foreground">{r.studentId.slice(0, 8)}…</td>
              <td className="px-4 py-3 text-foreground">
                {r.correctAnswers} / {totalQuestions}
              </td>
              <td className="px-4 py-3">
                <PercentageBar value={r.percentage} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function StatStrip({ stats }: { stats: ExamStatisticsResponse }) {
  const grades = ['A', 'B', 'C', 'D', 'F']
  const gradeColors: Record<string, string> = {
    A: 'text-green-500', B: 'text-blue-400', C: 'text-yellow-400', D: 'text-orange-400', F: 'text-red-400',
  }
  return (
    <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
      <div className="rounded-xl border border-border bg-card p-4">
        <div className="flex items-center gap-2 text-muted-foreground mb-1">
          <Users className="h-4 w-4" /><span className="text-xs">Students</span>
        </div>
        <p className="text-2xl font-bold text-foreground">{stats.totalStudents}</p>
      </div>
      <div className="rounded-xl border border-border bg-card p-4">
        <div className="flex items-center gap-2 text-muted-foreground mb-1">
          <TrendingUp className="h-4 w-4" /><span className="text-xs">Average</span>
        </div>
        <p className="text-2xl font-bold text-foreground">{stats.averagePercentage.toFixed(1)}%</p>
      </div>
      <div className="rounded-xl border border-border bg-card p-4">
        <div className="flex items-center gap-2 text-muted-foreground mb-1">
          <CheckCircle className="h-4 w-4" /><span className="text-xs">Pass Rate</span>
        </div>
        <p className="text-2xl font-bold text-green-500">{stats.passRate.toFixed(1)}%</p>
      </div>
      <div className="rounded-xl border border-border bg-card p-4">
        <div className="flex items-center gap-2 text-muted-foreground mb-2">
          <span className="text-xs font-medium">Grade Distribution</span>
        </div>
        <div className="flex gap-2 flex-wrap">
          {grades.map(g => (
            <span key={g} className={cn('text-sm font-semibold', gradeColors[g])}>
              {g}:{stats.gradeDistribution[g] ?? 0}
            </span>
          ))}
        </div>
      </div>
    </div>
  )
}

export default function RankingsPage() {
  const [selectedExamId, setSelectedExamId] = useState<string>('')

  const { data: examsData, isLoading: examsLoading } = useQuery({
    queryKey: ['exams'],
    queryFn: () => api.getExams(1, 100),
  })

  const { data: rankingsData, isLoading: rankingsLoading } = useQuery({
    queryKey: ['rankings', selectedExamId],
    queryFn: () => api.getExamRankings(selectedExamId),
    enabled: !!selectedExamId,
  })

  const { data: statsData } = useQuery({
    queryKey: ['exam-statistics', selectedExamId],
    queryFn: () => api.getExamStatistics(selectedExamId),
    enabled: !!selectedExamId,
  })

  const selectedExam = examsData?.exams.find((e: ExamItem) => e.examId === selectedExamId)

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Score Rankings</h1>
          {selectedExam && (
            <p className="mt-0.5 text-sm text-muted-foreground">{selectedExam.name}</p>
          )}
        </div>

        <select
          value={selectedExamId}
          onChange={e => setSelectedExamId(e.target.value)}
          className="rounded-lg border border-border bg-card px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-primary"
        >
          <option value="">Select exam…</option>
          {examsData?.exams.map((e: ExamItem) => (
            <option key={e.examId} value={e.examId}>{e.name}</option>
          ))}
        </select>
      </div>

      {!selectedExamId && (
        <div className="flex h-40 items-center justify-center rounded-xl border border-dashed border-border text-muted-foreground">
          {examsLoading ? 'Loading exams…' : 'Select an exam to view rankings.'}
        </div>
      )}

      {selectedExamId && (
        rankingsLoading ? (
          <p className="text-sm text-muted-foreground">Loading rankings…</p>
        ) : (
          <>
            {statsData && statsData.totalStudents > 0 && <StatStrip stats={statsData} />}
            {rankingsData && rankingsData.rankings.length > 0 && (
              <div className="grid grid-cols-3 gap-4">
                {[1, 2, 3].map(medal => {
                  const entry = rankingsData.rankings.find(r => r.rank === medal)
                  return (
                    <div key={medal} className="rounded-xl border border-border bg-card p-4 text-center">
                      <div className="mb-2 flex justify-center">{RANK_ICON[medal]}</div>
                      <p className={cn('text-lg font-bold', RANK_STYLE[medal])}>{entry ? `${Math.round(entry.percentage)}%` : '—'}</p>
                      <p className="text-xs text-muted-foreground">{entry ? `${entry.correctAnswers}/${entry.totalQuestions} correct` : 'No entry'}</p>
                    </div>
                  )
                })}
              </div>
            )}

            <RankingsTable
              rows={rankingsData?.rankings ?? []}
              totalQuestions={selectedExam?.totalQuestions ?? 0}
            />
          </>
        )
      )}
    </div>
  )
}
