import React, { useEffect, useState } from 'react';
import { checkFireTvApiHealth, FireTvAuthApiError, loginFireTvAdmin } from '../services/auth';
import type { FireTvAuthSession } from '../types';

interface LoginScreenProps {
  onLoginSuccess: (session: FireTvAuthSession) => void;
}

const DEFAULT_EMAIL = String(import.meta.env.VITE_DEFAULT_LOGIN_EMAIL || '').trim();
const DEFAULT_PASSWORD = String(import.meta.env.VITE_DEFAULT_LOGIN_PASSWORD || '');

const LoginScreen: React.FC<LoginScreenProps> = ({ onLoginSuccess }) => {
  const [email, setEmail] = useState(DEFAULT_EMAIL);
  const [password, setPassword] = useState(DEFAULT_PASSWORD);
  const [checking, setChecking] = useState(true);
  const [apiAvailable, setApiAvailable] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    let mounted = true;
    void (async () => {
      const available = await checkFireTvApiHealth();
      if (!mounted) {
        return;
      }

      setApiAvailable(available);
      setChecking(false);
      if (!available) {
        setErrorMessage('API indisponivel no momento. Verifique a conectividade do dispositivo.');
      }
    })();

    return () => {
      mounted = false;
    };
  }, []);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!apiAvailable) {
      setErrorMessage('API indisponivel no momento.');
      return;
    }

    setErrorMessage('');
    setIsSubmitting(true);

    try {
      const session = await loginFireTvAdmin(email, password);
      onLoginSuccess(session);
    } catch (error) {
      if (error instanceof FireTvAuthApiError) {
        setErrorMessage(error.message);
      } else {
        setErrorMessage('Falha inesperada ao autenticar.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="tv-shell tv-shell--centered">
      <div className="tv-login-card">
        <img className="tv-login-logo" src="/logo-wordmark.png" alt="ConsertaPraMim" />
        <p className="tv-eyebrow">Fire TV Dashboard</p>
        <h1>Paineis executivos</h1>
        <p className="tv-login-copy">Entre com uma conta Admin para abrir o menu central e escolher entre a landing e a visao operacional.</p>

        <form className="tv-login-form" onSubmit={handleSubmit}>
          <label className="tv-field">
            <span>E-mail</span>
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
              required
            />
          </label>

          <label className="tv-field">
            <span>Senha</span>
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
              required
            />
          </label>

          {checking ? <div className="tv-status">Verificando disponibilidade da API...</div> : null}
          {errorMessage ? <div className="tv-error">{errorMessage}</div> : null}

          <button type="submit" className="tv-primary-button" disabled={isSubmitting || checking}>
            {isSubmitting ? 'Autenticando...' : 'Entrar'}
          </button>
        </form>
      </div>
    </div>
  );
};

export default LoginScreen;
