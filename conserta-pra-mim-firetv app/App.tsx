import React, { useEffect, useState } from 'react';
import DashboardScreen from './components/DashboardScreen';
import LoginScreen from './components/LoginScreen';
import MenuScreen from './components/MenuScreen';
import OperationsDashboardScreen from './components/OperationsDashboardScreen';
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
        setView('MENU');
        return;
      }

      setView('AUTH');
    }, SPLASH_DELAY_MS);

    return () => window.clearTimeout(timerId);
  }, []);

  const handleLoginSuccess = (nextSession: FireTvAuthSession) => {
    saveFireTvSession(nextSession);
    setSession(nextSession);
    setView('MENU');
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

  if (view === 'MENU') {
    return (
      <MenuScreen
        session={session}
        onOpenLanding={() => setView('LANDING_DASHBOARD')}
        onOpenOperations={() => setView('OPERATIONS_DASHBOARD')}
        onLogout={handleLogout}
      />
    );
  }

  if (view === 'OPERATIONS_DASHBOARD') {
    return (
      <OperationsDashboardScreen
        session={session}
        onBack={() => setView('MENU')}
        onLogout={handleLogout}
      />
    );
  }

  return (
    <DashboardScreen
      session={session}
      onBack={() => setView('MENU')}
      onLogout={handleLogout}
    />
  );
};

export default App;
