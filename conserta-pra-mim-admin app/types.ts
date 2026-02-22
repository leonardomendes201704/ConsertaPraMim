export type AdminAppView = 'SPLASH' | 'AUTH' | 'HOME';

export type AdminHomeTab = 'dashboard' | 'monitoring' | 'support' | 'settings';

export interface AdminAuthSession {
  userId: string;
  token: string;
  userName: string;
  role: string;
  email: string;
  loggedInAtIso: string;
}
