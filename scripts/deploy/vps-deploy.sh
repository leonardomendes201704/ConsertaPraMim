#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="${1:-$HOME/ConsertaPraMimWeb}"
ENV_FILE="Backend/.env.vps"
DEPLOY_SERVICE_SCRIPT="scripts/deploy/vps-deploy-service.sh"
SERVICES=("api" "web-admin" "web-client" "web-provider")
ORIGINAL_SKIP_GIT_PULL="${SKIP_GIT_PULL:-0}"

cd "$REPO_DIR"

if [[ ! -f "$DEPLOY_SERVICE_SCRIPT" ]]; then
  echo "Script nao encontrado: $DEPLOY_SERVICE_SCRIPT"
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  cp Backend/.env.vps.example "$ENV_FILE"
  echo "Arquivo $ENV_FILE criado a partir de .env.vps.example."
  echo "Edite as credenciais e execute novamente."
  exit 1
fi

chmod +x "$DEPLOY_SERVICE_SCRIPT"

for index in "${!SERVICES[@]}"; do
  service="${SERVICES[$index]}"
  if [[ "$index" -eq 0 ]]; then
    export SKIP_GIT_PULL="$ORIGINAL_SKIP_GIT_PULL"
  else
    export SKIP_GIT_PULL="1"
  fi

  "$DEPLOY_SERVICE_SCRIPT" "$REPO_DIR" "$service"
done

echo "Deploy finalizado."
