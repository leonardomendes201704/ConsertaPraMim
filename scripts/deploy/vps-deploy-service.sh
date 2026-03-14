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
  echo "Uso: $0 [repo_dir] <api|web-cpmfull|web-admin|web-client|web-provider|web-telegrambridge|mobile-webview-client|mobile-webview-provider|mobile-webview-admin>"
  echo "Ou:  $0 <api|web-cpmfull|web-admin|web-client|web-provider|web-telegrambridge|mobile-webview-client|mobile-webview-provider|mobile-webview-admin>"
  exit 1
fi

declare -A COMPOSE_FILES=(
  [api]="Backend/docker-compose.vps.api.yml"
  [web-cpmfull]="Backend/docker-compose.vps.web-cpmfull.yml"
  [web-admin]="Backend/docker-compose.vps.web-admin.yml"
  [web-client]="Backend/docker-compose.vps.web-client.yml"
  [web-provider]="Backend/docker-compose.vps.web-provider.yml"
  [web-telegrambridge]="Backend/docker-compose.vps.web-telegrambridge.yml"
  [mobile-webview-client]="Backend/docker-compose.vps.mobile-webview-client.yml"
  [mobile-webview-provider]="Backend/docker-compose.vps.mobile-webview-provider.yml"
  [mobile-webview-admin]="Backend/docker-compose.vps.mobile-webview-admin.yml"
)

declare -A CONTAINER_SUFFIXES=(
  [api]="api"
  [web-cpmfull]="cpmfull"
  [web-admin]="admin"
  [web-client]="cliente"
  [web-provider]="prestador"
  [web-telegrambridge]="telegrambridge"
  [mobile-webview-client]="app-cliente"
  [mobile-webview-provider]="app-prestador"
  [mobile-webview-admin]="app-admin"
)

declare -A PROJECT_SUFFIXES=(
  [api]="api"
  [web-cpmfull]="cpmfull"
  [web-admin]="admin"
  [web-client]="cliente"
  [web-provider]="prestador"
  [web-telegrambridge]="telegrambridge"
  [mobile-webview-client]="app-cliente"
  [mobile-webview-provider]="app-prestador"
  [mobile-webview-admin]="app-admin"
)

declare -A LEGACY_CONTAINER_SUFFIXES=(
  [api]="api"
  [web-cpmfull]="landing"
  [web-admin]="web-admin"
  [web-client]="web-client"
  [web-provider]="web-provider"
  [web-telegrambridge]="telegrambridge"
  [mobile-webview-client]="mobile-webview-client"
  [mobile-webview-provider]="mobile-webview-provider"
  [mobile-webview-admin]="mobile-webview-admin"
)

if [[ -z "${COMPOSE_FILES[$TARGET_SERVICE]+x}" ]]; then
  echo "Servico invalido: '$TARGET_SERVICE'."
  echo "Servicos suportados: api, web-cpmfull, web-admin, web-client, web-provider, web-telegrambridge, mobile-webview-client, mobile-webview-provider, mobile-webview-admin"
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

# Resolve prefixo do container sem executar/sourcar o .env (evita quebra por caracteres especiais em secrets).
CONTAINER_PREFIX_VALUE="$(grep -E '^CONTAINER_PREFIX=' "$ENV_FILE" | head -n1 | cut -d'=' -f2- || true)"
CONTAINER_PREFIX_VALUE="${CONTAINER_PREFIX_VALUE%\"}"
CONTAINER_PREFIX_VALUE="${CONTAINER_PREFIX_VALUE#\"}"
CONTAINER_PREFIX_VALUE="${CONTAINER_PREFIX_VALUE%\'}"
CONTAINER_PREFIX_VALUE="${CONTAINER_PREFIX_VALUE#\'}"
CONTAINER_PREFIX_VALUE="${CONTAINER_PREFIX_VALUE:-cpm-prd}"
TARGET_CONTAINER_NAME="${CONTAINER_PREFIX_VALUE}-${CONTAINER_SUFFIXES[$TARGET_SERVICE]}"
LEGACY_CONTAINER_NAME="${CONTAINER_PREFIX_VALUE}-${LEGACY_CONTAINER_SUFFIXES[$TARGET_SERVICE]}"
LEGACY_PREFIX_VALUE=""
if [[ "$CONTAINER_PREFIX_VALUE" == "cpm-prd" ]]; then
  LEGACY_PREFIX_VALUE="cpm"
elif [[ "$CONTAINER_PREFIX_VALUE" == "cpm-hml" ]]; then
  LEGACY_PREFIX_VALUE="cpm-dev"
fi
COMPOSE_PROJECT_NAME_VALUE="${CONTAINER_PREFIX_VALUE}-${PROJECT_SUFFIXES[$TARGET_SERVICE]}"
COMPOSE_CMD=(docker compose -p "$COMPOSE_PROJECT_NAME_VALUE" -f "$COMPOSE_FILE" --env-file "$ENV_FILE")

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
if ! "${COMPOSE_CMD[@]}" build; then
  echo "[${TARGET_SERVICE}] Build padrao falhou. Executando fallback com limpeza de cache e --no-cache..."
  docker builder prune -f >/dev/null 2>&1 || true
  "${COMPOSE_CMD[@]}" build --no-cache
fi

declare -a CLEANUP_CONTAINERS=("$TARGET_CONTAINER_NAME")
if [[ "$LEGACY_CONTAINER_NAME" != "$TARGET_CONTAINER_NAME" ]]; then
  CLEANUP_CONTAINERS+=("$LEGACY_CONTAINER_NAME")
fi
if [[ -n "$LEGACY_PREFIX_VALUE" ]]; then
  CLEANUP_CONTAINERS+=("${LEGACY_PREFIX_VALUE}-${CONTAINER_SUFFIXES[$TARGET_SERVICE]}")
  CLEANUP_CONTAINERS+=("${LEGACY_PREFIX_VALUE}-${LEGACY_CONTAINER_SUFFIXES[$TARGET_SERVICE]}")
fi

for container_name in "${CLEANUP_CONTAINERS[@]}"; do
  if docker ps -a --format '{{.Names}}' | grep -Fxq "$container_name"; then
    echo "[${TARGET_SERVICE}] Removendo container existente '$container_name' para evitar conflito de nome/porta..."
    docker rm -f "$container_name" >/dev/null 2>&1 || true
  fi
done

"${COMPOSE_CMD[@]}" up -d --no-build --remove-orphans

echo "[${TARGET_SERVICE}] [5/5] Status final:"
"${COMPOSE_CMD[@]}" ps

echo "[${TARGET_SERVICE}] Deploy finalizado."
