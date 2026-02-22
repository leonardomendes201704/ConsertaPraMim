import React, { useEffect, useState } from 'react';
import AppShell from './components/AppShell';
import Auth from './components/Auth';
import SplashScreen from './components/SplashScreen';
import {
  clearAdminAuthSession,
  loadAdminAuthSession,
  saveAdminAuthSession
} from './services/auth';
import type { AdminAppView, AdminAuthSession } from './types';

const SPLASH_DELAY_MS = 900;

const App: React.FC = () => {
  const [view, setView] = useState<AdminAppView>('SPLASH');
  const [authSession, setAuthSession] = useState<AdminAuthSession | null>(null);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      const storedSession = loadAdminAuthSession();
      if (storedSession) {
        setAuthSession(storedSession);
        setView('HOME');
        return;
      }

      setView('AUTH');
    }, SPLASH_DELAY_MS);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, []);

  const handleLoginSuccess = (session: AdminAuthSession) => {
    saveAdminAuthSession(session);
    setAuthSession(session);
    setView('HOME');
  };

  const handleLogout = () => {
    clearAdminAuthSession();
    setAuthSession(null);
    setView('AUTH');
  };

  if (view === 'SPLASH') {
    return <SplashScreen />;
  }

  if (view === 'AUTH' || !authSession) {
    return <Auth onLoginSuccess={handleLoginSuccess} />;
  }

  return <AppShell session={authSession} onLogout={handleLogout} />;
};

export default App;