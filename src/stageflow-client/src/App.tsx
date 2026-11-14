import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuth } from './context/AuthContext';
import { MainLayout } from './layouts/MainLayout';
import { LoginPage } from './features/auth/LoginPage';
import { DashboardPage } from './features/dashboard/DashboardPage';
import { OffersPage } from './features/offers/OffersPage';
import { InternshipsPage } from './features/internships/InternshipsPage';
import { ReportsPage } from './features/reports/ReportsPage';
import { DefensesPage } from './features/defenses/DefensesPage';
import { UsersPage } from './features/users/UsersPage';
import { AuditPage } from './features/audit/AuditPage';
import { VerifyConventionPage } from './features/conventions/VerifyConventionPage';
import { BusyIndicator } from '@ui5/webcomponents-react';

interface ProtectedRouteProps {
  children: React.ReactNode;
  allowedRoles?: string[];
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children, allowedRoles }) => {
  const { user, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
        <BusyIndicator active />
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (allowedRoles && !allowedRoles.includes(user.role)) {
    return <Navigate to="/dashboard" replace />;
  }

  return <>{children}</>;
};

export const App: React.FC = () => {
  const { user } = useAuth();

  return (
    <Routes>
      {/* Public routes */}
      <Route path="/login" element={user ? <Navigate to="/dashboard" replace /> : <LoginPage />} />
      <Route path="/verify-convention/:token" element={<VerifyConventionPage />} />

      {/* Protected routes wrapped in MainLayout */}
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <MainLayout />
          </ProtectedRoute>
        }
      >
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<DashboardPage />} />
        <Route path="offers" element={<OffersPage />} />
        <Route path="internships" element={<InternshipsPage />} />
        <Route path="reports" element={<ReportsPage />} />
        <Route path="defenses" element={<DefensesPage />} />
        
        {/* Admin only */}
        <Route
          path="users"
          element={
            <ProtectedRoute allowedRoles={['Admin']}>
              <UsersPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="audit"
          element={
            <ProtectedRoute allowedRoles={['Admin']}>
              <AuditPage />
            </ProtectedRoute>
          }
        />
      </Route>

      {/* Fallback */}
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
};
export default App;
