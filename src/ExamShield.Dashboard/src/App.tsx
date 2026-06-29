import { useEffect } from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useAuth } from './hooks/useAuth'
import { useSetupStatus } from './hooks/useSetupStatus'
import SetupPage from './pages/SetupPage'
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
import ReviewRequestsPage from './pages/ReviewRequestsPage'
import PipelineDemoPage from './pages/PipelineDemoPage'
import ProfilePage from './pages/ProfilePage'
import ChangePasswordPage from './pages/ChangePasswordPage'
import OAuthCallbackPage from './pages/OAuthCallbackPage'
import AppLayout from './components/layout/AppLayout'
import { useNotifications } from './hooks/useNotifications'
import NotificationToast from './components/ui/NotificationToast'
import './index.css'

const queryClient = new QueryClient()

function LiveDashboard() {
  return <DashboardPage />
}

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth()
  const { status } = useSetupStatus()
  if (status?.isFirstRun) return <Navigate to="/setup" replace />
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />
}

function NotificationsOverlay() {
  const { notifications, dismiss } = useNotifications()
  return <NotificationToast notifications={notifications} onDismiss={dismiss} />
}

export default function App() {
  const { isAuthenticated, login, completeMfaLogin, requiresMfa, auth, logout, email, expiresAt, hasMfa } = useAuth()

  useEffect(() => {
    const handle = () => logout()
    window.addEventListener('auth:expired', handle)
    return () => window.removeEventListener('auth:expired', handle)
  }, [logout])

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/setup" element={<SetupPage />} />
          <Route
            path="/login"
            element={isAuthenticated ? <Navigate to="/" replace /> : <LoginPage onLogin={login} onMfaLogin={completeMfaLogin} requiresMfa={requiresMfa} />}
          />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/auth/callback" element={<OAuthCallbackPage />} />
          <Route path="/public/verify" element={<PublicVerificationPage />} />
          <Route
            path="/*"
            element={
              <ProtectedRoute>
                <NotificationsOverlay />
                <AppLayout
                  userName={auth.role ?? 'User'}
                  userEmail={email}
                  hasMfa={hasMfa}
                  expiresAt={expiresAt}
                  onLogout={logout}
                >
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
                    <Route path="/review-requests" element={<ReviewRequestsPage />} />
                    <Route path="/pipeline" element={<PipelineDemoPage />} />
                    <Route path="/profile" element={<ProfilePage />} />
                    <Route path="/change-password" element={<ChangePasswordPage />} />
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
