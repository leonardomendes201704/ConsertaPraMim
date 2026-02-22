import React, { useEffect, useState } from 'react';
import AppShell from './components/AppShell';
import Auth from './components/Auth';
import SplashScreen from './components/SplashScreen';
import {
  clearAdminAuthSession,
  loadAdminAuthSession,
  saveAdminAuthSession
} from './services/auth';
import {
  initializeAdminPushNotifications,
  teardownAdminPushNotifications,
  unregisterAdminPushNotifications
} from './services/pushNotifications';
import type { AdminAppView, AdminAuthSession } from './types';

const SPLASH_DELAY_MS = 900;
const PUSH_TOAST_HIDE_MS = 6000;

interface AdminPushToast {
  id: string;
  title: string;
  body: string;
  createdAtIso: string;
}

function buildToastId(): string {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

function formatToastTimestamp(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return 'agora';
  }

  return parsed.toLocaleTimeString('pt-BR', {
    hour: '2-digit',
    minute: '2-digit'
  });
}

const App: React.FC = () => {
  const [view, setView] = useState<AdminAppView>('SPLASH');
  const [authSession, setAuthSession] = useState<AdminAuthSession | null>(null);
  const [pushToast, setPushToast] = useState<AdminPushToast | null>(null);
  const [pushEventsVersion, setPushEventsVersion] = useState(0);

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
    const currentToken = authSession?.token;
    if (currentToken) {
      void unregisterAdminPushNotifications(currentToken);
    } else {
      void teardownAdminPushNotifications();
    }

    clearAdminAuthSession();
    setAuthSession(null);
    setPushToast(null);
    setView('AUTH');
  };

  useEffect(() => {
    if (!authSession?.token) {
      return;
    }

    void initializeAdminPushNotifications(authSession.token, {
      onForegroundNotification: (payload) => {
        setPushToast({
          id: payload.id || buildToastId(),
          title: payload.title,
          body: payload.body,
          createdAtIso: payload.createdAtIso || new Date().toISOString()
        });
        setPushEventsVersion((current) => current + 1);
      },
      onNotificationAction: (payload) => {
        setPushToast({
          id: payload.id || buildToastId(),
          title: payload.title,
          body: payload.body,
          createdAtIso: payload.createdAtIso || new Date().toISOString()
        });
        setPushEventsVersion((current) => current + 1);
      },
      onError: (message) => {
        console.warn(`[AdminPush] ${message}`);
      }
    });

    return () => {
      void teardownAdminPushNotifications();
    };
  }, [authSession?.token]);

  useEffect(() => {
    if (!pushToast) {
      return;
    }

    const timerId = window.setTimeout(() => {
      setPushToast(null);
    }, PUSH_TOAST_HIDE_MS);

    return () => {
      window.clearTimeout(timerId);
    };
  }, [pushToast]);

  if (view === 'SPLASH') {
    return <SplashScreen />;
  }

  if (view === 'AUTH' || !authSession) {
    return <Auth onLoginSuccess={handleLoginSuccess} />;
  }

  return (
    <>
      <AppShell session={authSession} onLogout={handleLogout} pushEventsVersion={pushEventsVersion} />
      {pushToast ? (
        <div className="pointer-events-none fixed inset-x-0 bottom-24 z-40 px-4">
          <div className="mx-auto max-w-lg rounded-2xl border border-blue-200 bg-white/95 p-4 shadow-xl backdrop-blur">
            <div className="flex items-start justify-between gap-3">
              <div className="flex min-w-0 items-start gap-3">
                <span className="material-symbols-outlined text-[22px] text-blue-600">notifications</span>
                <div className="min-w-0">
                  <p className="truncate text-sm font-semibold text-slate-900">{pushToast.title}</p>
                  <p className="mt-1 line-clamp-2 text-xs text-slate-600">{pushToast.body}</p>
                  <p className="mt-1 text-[11px] text-slate-400">{formatToastTimestamp(pushToast.createdAtIso)}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setPushToast(null)}
                className="pointer-events-auto rounded-md p-1 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600"
                aria-label="Fechar toast"
              >
                <span className="material-symbols-outlined text-[18px]">close</span>
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
};

export default App;
