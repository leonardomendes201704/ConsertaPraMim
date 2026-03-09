import React, { useEffect, useState } from 'react';
import DashboardScreen from './components/DashboardScreen';
import LoginScreen from './components/LoginScreen';
import SplashScreen from './components/SplashScreen';
import { clearFireTvSession, loadFireTvSession, saveFireTvSession } from './services/auth';
import type { FireTvAppView, FireTvAuthSession } from './types';

const SPLASH_DELAY_MS = 900;

const App: React.FC = () => {
  const [view, setView] = useState<FireTvAppView>('SPLASH');
  const [session, setSession] = useState<FireTvAuthSession | null>(null);

  useEffect(() => {
    const timerId = window.setTimeout(() => {
      const stored = loadFireTvSession();
      if (stored) {
        setSession(stored);
        setView('DASHBOARD');
        return;
      }

      setView('AUTH');
    }, SPLASH_DELAY_MS);

    return () => window.clearTimeout(timerId);
  }, []);

  const handleLoginSuccess = (nextSession: FireTvAuthSession) => {
    saveFireTvSession(nextSession);
    setSession(nextSession);
    setView('DASHBOARD');
  };

  const handleLogout = () => {
    clearFireTvSession();
    setSession(null);
    setView('AUTH');
  };

  if (view === 'SPLASH') {
    return <SplashScreen />;
  }

  if (view === 'AUTH' || !session) {
    return <LoginScreen onLoginSuccess={handleLoginSuccess} />;
  }

  return <DashboardScreen session={session} onLogout={handleLogout} />;
};

export default App;
