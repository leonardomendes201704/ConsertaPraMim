import React from 'react';

const SplashScreen: React.FC = () => {
  return (
    <div className="tv-shell tv-shell--centered">
      <div className="tv-splash-card">
        <img className="tv-splash-logo" src="/logo-wordmark.png" alt="ConsertaPraMim" />
        <p className="tv-eyebrow">Fire TV Dashboard</p>
        <h1>Preparando analytics da landing</h1>
        <p>Carregando sessao local e configurando o dashboard operacional.</p>
      </div>
    </div>
  );
};

export default SplashScreen;
