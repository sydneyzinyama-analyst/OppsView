import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAuth } from './auth/RequireAuth'
import { AppLayout } from './components/AppLayout'
import { ApplicationsPage } from './pages/ApplicationsPage'
import { AuthPage } from './pages/AuthPage'
import { MatchPage } from './pages/MatchPage'
import { RemindersPage } from './pages/RemindersPage'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<AuthPage mode="login" />} />
      <Route path="/register" element={<AuthPage mode="register" />} />
      <Route
        path="/"
        element={
          <RequireAuth>
            <AppLayout>
              <ApplicationsPage />
            </AppLayout>
          </RequireAuth>
        }
      />
      <Route
        path="/match"
        element={
          <RequireAuth>
            <AppLayout>
              <MatchPage />
            </AppLayout>
          </RequireAuth>
        }
      />
      <Route
        path="/reminders"
        element={
          <RequireAuth>
            <AppLayout>
              <RemindersPage />
            </AppLayout>
          </RequireAuth>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
