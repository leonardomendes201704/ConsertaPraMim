import React, { useEffect, useState } from 'react';
import DashboardScreen from './components/DashboardScreen';
import LoginScreen from './components/LoginScreen';
import MenuScreen from './components/MenuScreen';
import OperationsDashboardScreen from './components/OperationsDashboardScreen';
import SplashScreen from './components/SplashScreen';
import { clearFireTvSession, loadFireTvSession, saveFireTvSession } from './services/auth';
import type { FireTvAppView, FireTvAuthSession } from './types';

const SPLASH_DELAY_MS = 900;
const TV_BASE_WIDTH = 1920;
const TV_BASE_HEIGHT = 1080;
const MIN_TV_SCALE = 0.35;

const App: React.FC = () => {
  const [view, setView] = useState<FireTvAppView>('SPLASH');
  const [session, setSession] = useState<FireTvAuthSession | null>(null);
  const isDashboardView = view === 'LANDING_DASHBOARD' || view === 'OPERATIONS_DASHBOARD';

  useEffect(() => {
    const updateTvScale = () => {
      const widthScale = window.innerWidth / TV_BASE_WIDTH;
      const heightScale = window.innerHeight / TV_BASE_HEIGHT;
      const nextScale = Math.max(MIN_TV_SCALE, Math.min(widthScale, heightScale));
      const scaledWidth = TV_BASE_WIDTH * nextScale;
      const scaledHeight = TV_BASE_HEIGHT * nextScale;
      const offsetX = (window.innerWidth - scaledWidth) / 2;
      const offsetY = (window.innerHeight - scaledHeight) / 2;

      document.documentElement.style.setProperty('--tv-scale', nextScale.toFixed(4));
      document.documentElement.style.setProperty('--tv-offset-x', `${offsetX.toFixed(2)}px`);
      document.documentElement.style.setProperty('--tv-offset-y', `${offsetY.toFixed(2)}px`);
    };

    updateTvScale();
    window.addEventListener('resize', updateTvScale);
    window.addEventListener('orientationchange', updateTvScale);

    return () => {
      window.removeEventListener('resize', updateTvScale);
      window.removeEventListener('orientationchange', updateTvScale);
    };
  }, []);

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

  useEffect(() => {
    if (!isDashboardView) {
      return;
    }

    const goBackToMenu = () => {
      setView('MENU');
    };

    const handleKeyboardBack = (event: KeyboardEvent) => {
      const key = event.key;
      const keyCode = event.keyCode;
      const isBackKey =
        key === 'Escape' ||
        key === 'Backspace' ||
        key === 'BrowserBack' ||
        key === 'GoBack' ||
        keyCode === 4 ||
        keyCode === 8 ||
        keyCode === 27 ||
        keyCode === 166 ||
        keyCode === 461;

      if (!isBackKey) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();
      goBackToMenu();
    };

    window.addEventListener('keydown', handleKeyboardBack, true);

    let backListener: { remove: () => Promise<void> } | null = null;
    let cancelled = false;

    const setupNativeBackListener = async () => {
      try {
        const module = await import('@capacitor/app');
        if (cancelled) {
          return;
        }

        backListener = await module.App.addListener('backButton', () => {
          goBackToMenu();
        });
      } catch {
        // Sem plugin nativo no contexto web/preview: fallback de teclado continua ativo.
      }
    };

    void setupNativeBackListener();

    return () => {
      cancelled = true;
      window.removeEventListener('keydown', handleKeyboardBack, true);
      if (backListener) {
        void backListener.remove();
      }
    };
  }, [isDashboardView]);

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

  let content: React.ReactNode;

  if (view === 'SPLASH') {
    content = <SplashScreen />;
  } else if (view === 'AUTH' || !session) {
    content = <LoginScreen onLoginSuccess={handleLoginSuccess} />;
  } else if (view === 'MENU') {
    content = (
      <MenuScreen
        session={session}
        onOpenLanding={() => setView('LANDING_DASHBOARD')}
        onOpenOperations={() => setView('OPERATIONS_DASHBOARD')}
        onLogout={handleLogout}
      />
    );
  } else if (view === 'OPERATIONS_DASHBOARD') {
    content = (
      <OperationsDashboardScreen
        session={session}
        onBack={() => setView('MENU')}
        onLogout={handleLogout}
      />
    );
  } else {
    content = (
      <DashboardScreen
        session={session}
        onBack={() => setView('MENU')}
        onLogout={handleLogout}
      />
    );
  }

  return (
    <div className="tv-fixed-viewport">
      <div className="tv-fixed-stage">{content}</div>
    </div>
  );
};

export default App;
