import type { AdminAuthSession } from '../types';

const SESSION_STORAGE_KEY = 'cpm.admin.auth.session';

export function loadAdminAuthSession(): AdminAuthSession | null {
  try {
    const raw = window.localStorage.getItem(SESSION_STORAGE_KEY);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw) as AdminAuthSession;
    if (!parsed?.token || !parsed?.userId) {
      return null;
    }

    return parsed;
  } catch {
    return null;
  }
}

export function saveAdminAuthSession(session: AdminAuthSession): void {
  window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
}

export function clearAdminAuthSession(): void {
  window.localStorage.removeItem(SESSION_STORAGE_KEY);
}
