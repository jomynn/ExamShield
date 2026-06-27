import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useAuth } from './hooks/useAuth'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import AuditLogPage from './pages/AuditLogPage'
import DeviceManagementPage from './pages/DeviceManagementPage'
import SecurityCenterPage from './pages/SecurityCenterPage'
import PublicVerificationPage from './pages/PublicVerificationPage'
import ManualReviewPage from './pages/ManualReviewPage'
import OcrQueuePage from './pages/OcrQueuePage'
import ResultsPage from './pages/ResultsPage'
import ExaminationsPage from './pages/ExaminationsPage'
import UsersPage from './pages/UsersPage'
import ScoringPage from './pages/ScoringPage'
import CaptureSessionsPage from './pages/CaptureSessionsPage'
import RolesPage from './pages/RolesPage'
import ReportsPage from './pages/ReportsPage'
import SettingsPage from './pages/SettingsPage'
import StudentPortalPage from './pages/StudentPortalPage'
import MfaPage from './pages/MfaPage'
import ForgotPasswordPage from './pages/ForgotPasswordPage'
import AnswerSheetsPage from './pages/AnswerSheetsPage'
import RankingsPage from './pages/RankingsPage'
import SessionManagementPage from './pages/SessionManagementPage'
import AppLayout from './components/layout/AppLayout'
import { useDashboardStats } from './hooks/useDashboardStats'
import { useNotifications } from './hooks/useNotifications'
import NotificationToast from './components/ui/NotificationToast'
import './index.css'

const queryClient = new QueryClient()

function LiveDashboard() {
  const { data, isLoading } = useDashboardStats()
  if (isLoading || !data) return <p className="text-sm text-muted-foreground">Loading…</p>
  return (
    <DashboardPage
      stats={{
        totalCaptures: data.totalCaptures,
        pendingReview: data.pendingReview,
        verifiedToday: data.verifiedToday,
        alertCount: data.activeAlerts,
      }}
    />
  )
}

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth()
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />
}

function NotificationsOverlay() {
  const { notifications, dismiss } = useNotifications()
  return <NotificationToast notifications={notifications} onDismiss={dismiss} />
}

export default function App() {
  const { isAuthenticated, login, completeMfaLogin, requiresMfa, auth } = useAuth()

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route
            path="/login"
            element={isAuthenticated ? <Navigate to="/" replace /> : <LoginPage onLogin={login} onMfaLogin={completeMfaLogin} requiresMfa={requiresMfa} />}
          />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/public/verify" element={<PublicVerificationPage />} />
          <Route
            path="/*"
            element={
              <ProtectedRoute>
                <NotificationsOverlay />
                <AppLayout userName={auth.role ?? 'User'}>
                  <Routes>
                    <Route path="/" element={<LiveDashboard />} />
                    <Route path="/audit" element={<AuditLogPage />} />
                    <Route path="/devices" element={<DeviceManagementPage />} />
                    <Route path="/security" element={<SecurityCenterPage />} />
                    <Route path="/review" element={<ManualReviewPage />} />
                    <Route path="/ocr-queue" element={<OcrQueuePage />} />
                    <Route path="/results" element={<ResultsPage />} />
                    <Route path="/exams" element={<ExaminationsPage />} />
                    <Route path="/users" element={<UsersPage />} />
                    <Route path="/scoring" element={<ScoringPage />} />
                    <Route path="/rankings" element={<RankingsPage />} />
                    <Route path="/captures" element={<CaptureSessionsPage />} />
                    <Route path="/roles" element={<RolesPage />} />
                    <Route path="/reports" element={<ReportsPage />} />
                    <Route path="/settings" element={<SettingsPage />} />
                    <Route path="/sessions" element={<SessionManagementPage />} />
                    <Route path="/mfa" element={<MfaPage />} />
                    <Route path="/answer-sheets" element={<AnswerSheetsPage />} />
                    <Route path="/student" element={<StudentPortalPage />} />
                  </Routes>
                </AppLayout>
              </ProtectedRoute>
            }
          />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  )
}
