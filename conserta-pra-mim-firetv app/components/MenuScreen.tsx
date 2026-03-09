import React from 'react';
import type { FireTvAuthSession } from '../types';

interface MenuScreenProps {
  session: FireTvAuthSession;
  onOpenLanding: () => void;
  onOpenOperations: () => void;
  onLogout: () => void;
}

const MenuScreen: React.FC<MenuScreenProps> = ({
  session,
  onOpenLanding,
  onOpenOperations,
  onLogout
}) => {
  return (
    <div className="tv-shell tv-shell--centered">
      <div className="tv-menu-card">
        <div className="tv-menu-header">
          <img className="tv-splash-logo" src="/logo-wordmark.png" alt="ConsertaPraMim" />
          <div>
            <p className="tv-eyebrow">Fire TV Dashboard</p>
            <h1>Escolha a visao</h1>
            <p className="tv-supporting-copy">
              Sessao ativa como <strong>{session.userName}</strong>. Escolha qual painel deseja acompanhar em tela cheia.
            </p>
          </div>
        </div>

        <div className="tv-menu-grid">
          <button type="button" className="tv-menu-tile" onClick={onOpenLanding}>
            <span className="tv-menu-tile-tag">Landing</span>
            <strong>Metricas da landing</strong>
            <small>KPIs, heatmap, scrollmap, ranking de elementos e sessoes recentes.</small>
          </button>

          <button type="button" className="tv-menu-tile tv-menu-tile--accent" onClick={onOpenOperations}>
            <span className="tv-menu-tile-tag">Operacao</span>
            <strong>Visao operacional</strong>
            <small>Health checks, servicos, prestadores, mapa operacional, barras diarias e receita.</small>
          </button>
        </div>

        <div className="tv-menu-actions">
          <button type="button" className="tv-secondary-button" onClick={onLogout}>
            Sair
          </button>
        </div>
      </div>
    </div>
  );
};

export default MenuScreen;
