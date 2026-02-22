import React, { useEffect, useState } from 'react';
import AppShell from './components/AppShell';
import SplashScreen from './components/SplashScreen';
import type { AdminAppView } from './types';

const SPLASH_DELAY_MS = 900;

const App: React.FC = () => {
  const [view, setView] = useState<AdminAppView>('SPLASH');

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setView('AUTH');
    }, SPLASH_DELAY_MS);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, []);

  if (view === 'SPLASH') {
    return <SplashScreen />;
  }

  return <AppShell initialView={view} />;
};

export default App;
