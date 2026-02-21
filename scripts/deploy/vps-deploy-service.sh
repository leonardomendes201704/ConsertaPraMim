#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="$HOME/ConsertaPraMimWeb"
TARGET_SERVICE=""
ENV_FILE="Backend/.env.vps"
DOCKER_NETWORK="conserta_net"
MSSQL_CONTAINER_NAME="${MSSQL_CONTAINER_NAME:-mssql}"
MSSQL_HOST_ALIAS="${MSSQL_HOST_ALIAS:-mssql}"

if [[ $# -eq 1 ]]; then
  TARGET_SERVICE="$1"
elif [[ $# -eq 2 ]]; then
  REPO_DIR="$1"
  TARGET_SERVICE="$2"
fi

if [[ -z "$TARGET_SERVICE" ]]; then
  echo "Uso: $0 [repo_dir] <api|web-admin|web-client|web-provider>"
  echo "Ou:  $0 <api|web-admin|web-client|web-provider>"
  exit 1
fi

declare -A COMPOSE_FILES=(
  [api]="Backend/docker-compose.vps.api.yml"
  [web-admin]="Backend/docker-compose.vps.web-admin.yml"
  [web-client]="Backend/docker-compose.vps.web-client.yml"
  [web-provider]="Backend/docker-compose.vps.web-provider.yml"
)

if [[ -z "${COMPOSE_FILES[$TARGET_SERVICE]+x}" ]]; then
  echo "Servico invalido: '$TARGET_SERVICE'."
  echo "Servicos suportados: api, web-admin, web-client, web-provider"
  exit 1
fi

COMPOSE_FILE="${COMPOSE_FILES[$TARGET_SERVICE]}"

cd "$REPO_DIR"

if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "Arquivo compose nao encontrado: $COMPOSE_FILE"
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  cp Backend/.env.vps.example "$ENV_FILE"
  echo "Arquivo $ENV_FILE criado a partir de .env.vps.example."
  echo "Edite as credenciais e execute novamente."
  exit 1
fi

echo "[${TARGET_SERVICE}] [1/5] Atualizando codigo..."
if [[ "${SKIP_GIT_PULL:-0}" == "1" || "${GITHUB_ACTIONS:-false}" == "true" ]]; then
  echo "[${TARGET_SERVICE}] Pulando git pull (execucao em CI/self-hosted runner)."
else
  git pull --rebase
fi

echo "[${TARGET_SERVICE}] [2/5] Garantindo rede docker $DOCKER_NETWORK..."
docker network inspect "$DOCKER_NETWORK" >/dev/null 2>&1 || docker network create "$DOCKER_NETWORK"

if [[ "$TARGET_SERVICE" == "api" ]]; then
  echo "[${TARGET_SERVICE}] [3/5] Conectando SQL '$MSSQL_CONTAINER_NAME' na rede como alias '$MSSQL_HOST_ALIAS' (se necessario)..."
  docker network disconnect "$DOCKER_NETWORK" "$MSSQL_CONTAINER_NAME" >/dev/null 2>&1 || true
  docker network connect --alias "$MSSQL_HOST_ALIAS" "$DOCKER_NETWORK" "$MSSQL_CONTAINER_NAME" >/dev/null 2>&1 || true
else
  echo "[${TARGET_SERVICE}] [3/5] Sem dependencia direta de SQL para deploy deste servico."
fi

echo "[${TARGET_SERVICE}] [4/5] Build + deploy..."
if ! docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --build --remove-orphans; then
  echo "[${TARGET_SERVICE}] Build padrao falhou. Executando fallback com limpeza de cache e --no-cache..."
  docker builder prune -f >/dev/null 2>&1 || true
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" build --no-cache
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --remove-orphans
fi

echo "[${TARGET_SERVICE}] [5/5] Status final:"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" ps

echo "[${TARGET_SERVICE}] Deploy finalizado."
