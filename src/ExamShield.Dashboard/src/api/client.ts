const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5083'

function authHeaders(): HeadersInit {
  const token = localStorage.getItem('auth_token')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...authHeaders(), ...init?.headers },
  })
  if (!res.ok) {
    const text = await res.text()
    throw new Error(`${res.status}: ${text}`)
  }
  return res.json() as Promise<T>
}

export interface LoginResponse { token: string; refreshToken: string; role: string; requiresMfa: boolean }
export interface DashboardStats {
  totalCaptures: number
  pendingReview: number
  verifiedToday: number
  alertCount: number
}
export interface AuditEntry {
  id: string
  action: string
  captureId: string | null
  userId: string
  ipAddress: string
  occurredAt: string
  reason: string | null
  contentHash: string
  serverSignature: string
}
export interface AuditResponse { entries: AuditEntry[]; totalCount: number }

export interface DeviceEntry {
  deviceId: string
  name: string
  status: string
  isActive: boolean
  registeredAt: string
  lastSeenAt: string | null
  blacklistReason: string | null
}
export interface DeviceListResponse { devices: DeviceEntry[] }

export interface SecurityEventEntry {
  id: string
  eventType: string
  severity: string
  message: string
  userId: string | null
  ipAddress: string | null
  captureId: string | null
  occurredAt: string
}
export interface SecurityEventListResponse { events: SecurityEventEntry[] }

export interface AllSessionEntry {
  id: string
  userId: string
  createdAt: string
  expiresAt: string
}
export interface AllSessionsResponse { sessions: AllSessionEntry[] }

export interface LoginHistoryEntry {
  id: string
  eventType: string
  userId: string | null
  ipAddress: string | null
  occurredAt: string
}
export interface LoginHistoryResponse { events: LoginHistoryEntry[] }

export const api = {
  login: (email: string, password: string) =>
    request<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  mfaLogin: (email: string, password: string, code: string) =>
    request<LoginResponse>('/auth/mfa/login', {
      method: 'POST',
      body: JSON.stringify({ email, password, code }),
    }),

  refreshToken: (refreshToken: string) =>
    request<LoginResponse>('/auth/refresh', {
      method: 'POST',
      body: JSON.stringify({ refreshToken }),
    }),

  logout: (refreshToken: string) =>
    request<void>('/auth/logout', {
      method: 'POST',
      body: JSON.stringify({ refreshToken }),
    }),

  getAuditLog: (params?: { captureId?: string; page?: number; pageSize?: number; action?: string }) => {
    const q = new URLSearchParams()
    if (params?.captureId) q.set('captureId', params.captureId)
    if (params?.page)      q.set('page', String(params.page))
    if (params?.pageSize)  q.set('pageSize', String(params.pageSize))
    if (params?.action)    q.set('action', params.action)
    return request<AuditResponse>(`/audit?${q}`)
  },

  getDevices: () => request<DeviceListResponse>('/devices'),

  approveDevice: (id: string) =>
    request<void>(`/devices/${id}/approve`, { method: 'PUT' }),

  disableDevice: (id: string) =>
    request<void>(`/devices/${id}/disable`, { method: 'PUT' }),

  enableDevice: (id: string) =>
    request<void>(`/devices/${id}/enable`, { method: 'PUT' }),

  blacklistDevice: (id: string, reason: string) =>
    request<void>(`/devices/${id}/blacklist`, {
      method: 'PUT',
      body: JSON.stringify({ reason }),
    }),

  deviceHeartbeat: (id: string) =>
    request<{ deviceId: string; lastSeenAt: string }>(`/devices/${id}/heartbeat`, { method: 'POST' }),

  getSecurityEvents: (limit = 100, severity?: string) => {
    const params = new URLSearchParams({ limit: String(limit) })
    if (severity) params.set('severity', severity)
    return request<SecurityEventListResponse>(`/security/events?${params}`)
  },

  getAllActiveSessions: (userId?: string) => {
    const query = userId ? `?userId=${userId}` : ''
    return request<AllSessionsResponse>(`/security/sessions${query}`)
  },

  getDashboardStats: () =>
    request<DashboardStatsResponse>('/dashboard/stats'),

  publicVerify: (captureId: string) =>
    request<PublicVerifyResponse>(`/public/verify?captureId=${encodeURIComponent(captureId)}`),

  publicVerifyByHash: (hashHex: string) =>
    request<PublicVerifyResponse>(`/public/verify?hashHex=${encodeURIComponent(hashHex)}`),

  getCaptures: (page = 1, pageSize = 50, examId?: string, status?: string,
                deviceId?: string, studentId?: string) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (examId)    params.set('examId', examId)
    if (status)    params.set('status', status)
    if (deviceId)  params.set('deviceId', deviceId)
    if (studentId) params.set('studentId', studentId)
    return request<CaptureListResponse>(`/captures?${params}`)
  },

  verifyCapture: (captureId: string) =>
    request<VerifyCaptureResponse>(`/public/verify?captureId=${encodeURIComponent(captureId)}`),

  getChainOfCustody: (captureId: string) =>
    request<ChainOfCustodyResult>(`/captures/${captureId}/chain-of-custody`),

  flagCaptureAsTampered: (captureId: string, reason: string) =>
    request<void>(`/captures/${captureId}/flag-tampered`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),

  exportCaptures: (examId?: string, status?: string) => {
    const params = new URLSearchParams()
    if (examId) params.set('examId', examId)
    if (status) params.set('status', status)
    const query = params.toString() ? `?${params}` : ''
    return fetch(`${BASE_URL}/captures/export${query}`, {
      headers: { ...authHeaders() },
    }).then(r => r.blob())
  },

  getUsers: (page = 1, pageSize = 50, search?: string, role?: string) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (search) params.append('search', search)
    if (role) params.append('role', role)
    return request<UserListResponse>(`/users/?${params}`)
  },

  exportUsers: (search?: string, role?: string) => {
    const params = new URLSearchParams()
    if (search) params.set('search', search)
    if (role) params.set('role', role)
    const query = params.toString() ? `?${params}` : ''
    return fetch(`${BASE_URL}/users/export${query}`, {
      headers: { ...authHeaders() },
    }).then(r => r.blob())
  },

  getUserById: (userId: string) =>
    request<UserDetail>(`/users/${userId}`),

  updateUserProfile: (userId: string, displayName: string | null) =>
    request<void>(`/users/${userId}`, {
      method: 'PUT',
      body: JSON.stringify({ displayName }),
    }),

  updateUserRole: (userId: string, role: string) =>
    request<void>(`/users/${userId}/role`, {
      method: 'PUT',
      body: JSON.stringify({ role }),
    }),

  deactivateUser: (userId: string) =>
    request<void>(`/users/${userId}/deactivate`, { method: 'PUT' }),

  activateUser: (userId: string) =>
    request<void>(`/users/${userId}/activate`, { method: 'PUT' }),

  getExams: (page = 1, pageSize = 50, search?: string, status?: string,
             scheduledFrom?: string, scheduledTo?: string) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (search)        params.append('search', search)
    if (status)        params.append('status', status)
    if (scheduledFrom) params.append('scheduledFrom', scheduledFrom)
    if (scheduledTo)   params.append('scheduledTo', scheduledTo)
    return request<ExamListResponse>(`/exams/?${params}`)
  },

  exportExams: (search?: string, status?: string) => {
    const params = new URLSearchParams()
    if (search) params.set('search', search)
    if (status) params.set('status', status)
    const query = params.toString() ? `?${params}` : ''
    return fetch(`${BASE_URL}/exams/export${query}`, {
      headers: { ...authHeaders() },
    }).then(r => r.blob())
  },

  createExam: (payload: CreateExamPayload) =>
    request<ExamItem>('/exams/', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  deleteExam: (examId: string) =>
    request<void>(`/exams/${examId}`, { method: 'DELETE' }),

  updateExam: (examId: string, payload: {
    name: string; description?: string;
    scheduledAt?: string | null; endsAt?: string | null;
  }) =>
    request<void>(`/exams/${examId}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  submitReviewRequest: (studentId: string, captureId: string, reason: string) =>
    request<{ reviewRequestId: string }>('/student/review-request', {
      method: 'POST',
      body: JSON.stringify({ captureId, studentId, reason }),
    }),

  getStudentReviewRequests: (studentId: string) =>
    request<{ items: ReviewRequestItem[] }>(`/student/review-requests?studentId=${studentId}`),

  getAllReviewRequests: (status?: string) =>
    request<{ items: ReviewRequestItem[] }>(`/admin/review-requests${status ? `?status=${status}` : ''}`),

  resolveReviewRequest: (id: string, note: string) =>
    request<void>(`/student/review-requests/${id}/resolve`, {
      method: 'PUT',
      body: JSON.stringify({ note }),
    }),

  rejectReviewRequest: (id: string, note: string) =>
    request<void>(`/student/review-requests/${id}/reject`, {
      method: 'PUT',
      body: JSON.stringify({ note }),
    }),

  activateExam: (examId: string) =>
    request<void>(`/exams/${examId}/activate`, { method: 'PUT' }),

  closeExam: (examId: string) =>
    request<void>(`/exams/${examId}/close`, { method: 'PUT' }),

  setAnswerKey: (examId: string, answers: Record<number, string>) =>
    request<void>(`/exams/${examId}/answer-key`, {
      method: 'POST',
      body: JSON.stringify({ answers }),
    }),

  getAnswerKey: (examId: string) =>
    request<{ examId: string; answers: Record<string, string>; createdAt: string }>(
      `/exams/${examId}/answer-key`
    ),

  enrollStudent: (examId: string, studentId: string) =>
    request<void>(`/exams/${examId}/students`, {
      method: 'POST',
      body: JSON.stringify({ studentId }),
    }),

  getExamCandidates: (examId: string) =>
    request<ExamCandidatesResponse>(`/exams/${examId}/students`),

  unenrollStudent: (examId: string, studentId: string) =>
    request<void>(`/exams/${examId}/students/${studentId}`, { method: 'DELETE' }),

  bulkEnrollStudents: (examId: string, studentIds: string[]) =>
    request<{ enrolled: number; alreadyEnrolled: number; total: number }>(
      `/exams/${examId}/students/bulk`, {
        method: 'POST',
        body: JSON.stringify({ studentIds: studentIds.map(id => id.trim()).filter(Boolean) }),
      }),

  getExamSubmissionStatus: (examId: string) =>
    request<ExamSubmissionStatusResponse>(`/exams/${examId}/submission-status`),

  getScoringQueue: () => request<ScoringQueueResponse>('/score/queue'),

  scoreCapture: (captureId: string) =>
    request<ScoreCaptureResponse>('/score', {
      method: 'POST',
      body: JSON.stringify({ captureId }),
    }),

  batchScore: (examId: string) =>
    request<{ examId: string; scored: number; skipped: number }>('/score/batch', {
      method: 'POST',
      body: JSON.stringify({ examId }),
    }),

  getStudentResults: (studentId: string) =>
    request<StudentResultsResponse>(`/student/results?studentId=${encodeURIComponent(studentId)}`),

  getSettings: () => request<SettingsResponse>('/settings/'),

  updateSettings: (payload: UpdateSettingsPayload) =>
    request<SettingsResponse>('/settings/', { method: 'PUT', body: JSON.stringify(payload) }),

  testAlert: () =>
    request<{ sent: boolean; error: string | null }>('/settings/alert/test', { method: 'POST' }),

  getNotificationChannelSettings: () =>
    request<NotificationChannelSettingsResponse>('/settings/notifications'),

  updateNotificationChannelSettings: (payload: NotificationChannelSettingsPayload) =>
    request<NotificationChannelSettingsResponse>('/settings/notifications', {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  getReportSummary: () => request<ReportSummaryResponse>('/reports/summary'),

  exportAuditLog: (params?: { captureId?: string; from?: string; to?: string }) => {
    const qs = new URLSearchParams()
    if (params?.captureId) qs.set('captureId', params.captureId)
    if (params?.from) qs.set('from', params.from)
    if (params?.to) qs.set('to', params.to)
    const query = qs.toString() ? `?${qs}` : ''
    return fetch(`${BASE_URL}/audit/export${query}`, {
      headers: { ...authHeaders() },
    }).then(r => r.blob())
  },

  getExamReport: (examId: string) =>
    request<ExamReportResponse>(`/reports/exam/${examId}`),

  exportExamReportCsv: (examId: string) =>
    fetch(`${BASE_URL}/reports/exam/${examId}/export`, {
      headers: { ...authHeaders() },
    }).then(r => r.blob()),

  exportScores: (examId?: string) => {
    const query = examId ? `?examId=${examId}` : ''
    return fetch(`${BASE_URL}/score/export${query}`, {
      headers: { ...authHeaders() },
    }).then(r => r.blob())
  },

  getRoles: () => request<RoleListResponse>('/roles'),

  getResults: () => request<GetResultsResponse>('/results'),

  getStatistics: () => request<StatisticsResponse>('/statistics'),

  getMfaStatus: () => request<MfaStatusResponse>('/auth/mfa/status'),
  setupMfa: () => request<MfaSetupResponse>('/auth/mfa/setup', { method: 'POST' }),
  verifyMfa: (code: string) =>
    request<MfaStatusResponse>('/auth/mfa/verify', { method: 'POST', body: JSON.stringify({ code }) }),
  disableMfa: () =>
    request<MfaStatusResponse>('/auth/mfa/', { method: 'DELETE' }),

  getOcrQueue: () => request<OcrQueueResponse>('/ocr/queue'),

  triggerOcr: (captureId: string) =>
    request<void>(`/ocr/${captureId}`, { method: 'POST' }),

  triggerBatchOcr: (examId: string) =>
    request<{ examId: string; queued: number; skipped: number }>('/ocr/batch', {
      method: 'POST',
      body: JSON.stringify({ examId }),
    }),

  getPendingReviews: () => request<PendingReviewsResponse>('/reviews'),

  getReviewDetail: (reviewId: string) => request<ReviewDetailResponse>(`/reviews/${reviewId}`),

  submitReview: (reviewId: string, answers: ReviewedAnswerPayload[]) =>
    request<void>(`/reviews/${reviewId}/submit`, {
      method: 'POST',
      body: JSON.stringify({ answers }),
    }),

  approveReview: (reviewId: string) =>
    request<void>(`/reviews/${reviewId}/approve`, { method: 'PUT' }),

  rejectReview: (reviewId: string, reason: string) =>
    request<void>(`/reviews/${reviewId}/reject`, {
      method: 'PUT',
      body: JSON.stringify({ reason }),
    }),

  escalateReview: (reviewId: string, reason: string) =>
    request<void>(`/reviews/${reviewId}/escalate`, {
      method: 'PUT',
      body: JSON.stringify({ reason }),
    }),

  forgotPassword: (email: string) =>
    request<void>('/auth/password/forgot', {
      method: 'POST',
      body: JSON.stringify({ email }),
    }),

  resetPassword: (token: string, newPassword: string) =>
    request<void>('/auth/password/reset', {
      method: 'POST',
      body: JSON.stringify({ token, newPassword }),
    }),

  getLoginHistory: (limit = 100) =>
    request<LoginHistoryResponse>(`/security/login-history?limit=${limit}`),

  changePassword: (currentPassword: string, newPassword: string) =>
    request<void>('/auth/password/change', {
      method: 'POST',
      body: JSON.stringify({ currentPassword, newPassword }),
    }),

  getProfile: () => request<ProfileResponse>('/auth/profile'),

  getSessions: () => request<SessionsResponse>('/auth/sessions'),

  revokeSession: (sessionId: string) =>
    request<void>(`/auth/sessions/${sessionId}`, { method: 'DELETE' }),

  revokeAllSessions: () =>
    request<void>('/auth/sessions', { method: 'DELETE' }),

  getExamRankings: (examId: string) =>
    request<ExamRankingsResponse>(`/score/rankings/${examId}`),

  getExamStatistics: (examId: string) =>
    request<ExamStatisticsResponse>(`/score/exams/${examId}/statistics`),
}

export interface CaptureItem {
  captureId: string
  examId: string
  studentId: string
  deviceId: string
  status: string
  capturedAt: string
  storageKey: string | null
}
export interface CaptureListResponse {
  captures: CaptureItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface VerifyCaptureResponse {
  isValid: boolean
  hashValid: boolean
  signatureValid: boolean
}

export interface UserItem {
  userId: string
  email: string
  role: string
  isActive: boolean
  createdAt: string
}

export interface UserDetail extends UserItem {
  displayName: string | null
  mfaEnabled: boolean
}
export interface UserListResponse {
  users: UserItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ReviewRequestItem {
  reviewRequestId: string
  studentId: string
  captureId: string
  reason: string
  status: string
  resolutionNote: string | null
  createdAt: string
}

export interface ExamItem {
  examId: string
  name: string
  description: string | null
  status: string
  totalQuestions: number
  createdAt: string
  scheduledAt: string | null
  endsAt: string | null
}
export interface ExamListResponse {
  exams: ExamItem[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface RankingEntry {
  rank: number
  studentId: string
  correctAnswers: number
  totalQuestions: number
  percentage: number
}

export interface ExamRankingsResponse {
  examId: string
  rankings: RankingEntry[]
}

export interface ExamStatisticsResponse {
  examId: string
  totalStudents: number
  averagePercentage: number
  highestPercentage: number
  lowestPercentage: number
  passRate: number
  gradeDistribution: Record<string, number>
}

export interface CreateExamPayload {
  name: string
  description: string
  totalQuestions: number
  scheduledAt?: string | null
  endsAt?: string | null
}

export interface OcrQueueItem {
  captureId: string
  examId: string
  studentId: string
  uploadedAt: string
}
export interface OcrQueueResponse { items: OcrQueueItem[] }

export interface PendingReviewItem {
  reviewId: string
  captureId: string
  ocrResultId: string
  createdAt: string
}
export interface PendingReviewsResponse { reviews: PendingReviewItem[] }

export interface OcrAnswer {
  questionNumber: number
  text: string
  confidence: number
}
export interface ReviewDetailResponse {
  reviewId: string
  captureId: string
  ocrResultId: string
  status: string
  ocrAnswers: OcrAnswer[]
  createdAt: string
}

export interface ReviewedAnswerPayload {
  questionNumber: number
  text: string
}

export interface DashboardStatsResponse {
  totalCaptures: number
  pendingReview: number
  verifiedToday: number
  activeAlerts: number
}

export interface ScoreResultItem {
  scoreId: string
  captureId: string
  examId: string
  studentId: string
  correctAnswers: number
  totalQuestions: number
  percentage: number
  scoredAt: string
}
export interface GetResultsResponse { results: ScoreResultItem[] }

export interface ScoringQueueItem {
  captureId: string
  examId: string
  ocrResultId: string
  ocrStatus: string
  overallConfidence: number
  completedAt: string
}
export interface ScoringQueueResponse { items: ScoringQueueItem[] }

export interface ScoreCaptureResponse {
  scoreId: string
  correctAnswers: number
  totalQuestions: number
  percentage: number
}

export interface StatisticsResponse {
  totalPapersScored: number
  averagePercentage: number
  highestScore: number
  lowestScore: number
}

export interface StudentResultItem {
  scoreId: string
  captureId: string
  examId: string
  examName: string
  correctAnswers: number
  totalQuestions: number
  percentage: number
  scoredAt: string
  hashHex: string
  isVerified: boolean
}
export interface StudentResultsResponse {
  studentId: string
  results: StudentResultItem[]
}

export interface SettingsResponse {
  ocrConfidenceThreshold: number
  notificationsEnabled: boolean
  notificationSeverity: string
  accessTokenExpiryMinutes: number
  refreshTokenExpiryDays: number
}
export type UpdateSettingsPayload = SettingsResponse

export interface NotificationChannelSettingsResponse {
  emailEnabled: boolean
  emailRecipients: string | null
  slackEnabled: boolean
  slackWebhookUrl: string | null
  lineEnabled: boolean
  lineNotifyToken: string | null
  webhookEnabled: boolean
  webhookUrl: string | null
  updatedAt: string
}
export type NotificationChannelSettingsPayload = Omit<NotificationChannelSettingsResponse, 'updatedAt'>

export interface ReportSummaryResponse {
  generatedAt: string
  captures: { total: number; created: number; uploaded: number; verified: number; tampered: number }
  ocr: { totalProcessed: number; averageConfidence: number }
  scores: { totalScored: number; averagePercentage: number; highestPercentage: number; lowestPercentage: number }
  security: { totalEvents: number; criticalEvents: number }
}

export interface ExamReportResponse {
  examId: string
  examName: string
  examStatus: string
  totalQuestions: number
  generatedAt: string
  totalCaptures: number
  uploadedCaptures: number
  verifiedCaptures: number
  tamperedCaptures: number
  totalOcrProcessed: number
  ocrAverageConfidence: number
  lowConfidenceCount: number
  totalScored: number
  averageScorePercentage: number
  highestScorePercentage: number
  lowestScorePercentage: number
  totalReviewRequests: number
}

export interface ExamCandidateItem { studentId: string; enrolledAt: string }
export interface ExamCandidatesResponse { examId: string; candidates: ExamCandidateItem[] }

export interface StudentSubmissionItem { studentId: string; hasSubmitted: boolean; captureStatus: string | null }
export interface ExamSubmissionStatusResponse {
  examId: string
  totalEnrolled: number
  submitted: number
  missing: number
  students: StudentSubmissionItem[]
}

export interface RoleItem {
  roleName: string
  displayName: string
  description: string
  permissions: string[]
}
export interface RoleListResponse { roles: RoleItem[] }

export interface MfaStatusResponse { mfaEnabled: boolean }
export interface MfaSetupResponse { secret: string; qrUri: string }

export interface ProfileResponse { email: string; role: string; mfaEnabled: boolean }
export interface SessionItem { id: string; createdAt: string; expiresAt: string }
export interface SessionsResponse { sessions: SessionItem[] }

export interface PublicVerifyResponse {
  captureId: string
  isValid: boolean
  hashValid: boolean
  signatureValid: boolean
  isUploaded: boolean
  capturedAt: string | null
}

export interface ChainAuditEntry { action: string; userId: string; occurredAt: string; reason: string | null }
export interface ChainOcrInfo { ocrResultId: string; overallConfidence: number; answerCount: number; completedAt: string }
export interface ChainScoreInfo { scoreId: string; correctAnswers: number; totalQuestions: number; percentage: number; scoredAt: string }
export interface ChainReviewInfo { reviewId: string; status: string; requestedAt: string }
export interface ChainOfCustodyResult {
  captureId: string; examId: string; studentId: string; deviceId: string
  pageNumber: number; hashHex: string; status: string; capturedAt: string; storageKey: string | null
  ocrResult: ChainOcrInfo | null
  score: ChainScoreInfo | null
  reviews: ChainReviewInfo[]
  auditTrail: ChainAuditEntry[]
}
