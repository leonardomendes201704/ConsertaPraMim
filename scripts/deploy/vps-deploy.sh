#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${1:-$HOME/ConsertaPraMimWeb}"
COMPOSE_FILE="Backend/docker-compose.vps.yml"
ENV_FILE="Backend/.env.vps"
DOCKER_NETWORK="conserta_net"
MSSQL_CONTAINER_NAME="${MSSQL_CONTAINER_NAME:-mssql}"

cd "$REPO_DIR"

if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "Arquivo nao encontrado: $COMPOSE_FILE"
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  cp Backend/.env.vps.example "$ENV_FILE"
  echo "Arquivo $ENV_FILE criado a partir de .env.vps.example."
  echo "Edite as credenciais e execute novamente."
  exit 1
fi

echo "[1/5] Atualizando codigo..."
git pull --rebase

echo "[2/5] Garantindo rede docker $DOCKER_NETWORK..."
docker network inspect "$DOCKER_NETWORK" >/dev/null 2>&1 || docker network create "$DOCKER_NETWORK"

echo "[3/5] Conectando container SQL '$MSSQL_CONTAINER_NAME' na rede (se necessario)..."
docker network connect "$DOCKER_NETWORK" "$MSSQL_CONTAINER_NAME" >/dev/null 2>&1 || true

echo "[4/5] Build + deploy dos containers..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --build --remove-orphans

echo "[5/5] Status final:"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" ps

echo "Deploy finalizado."
