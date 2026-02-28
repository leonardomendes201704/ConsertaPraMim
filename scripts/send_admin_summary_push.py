#!/usr/bin/env python3
"""Dispara push de resumo para o app admin via endpoint interno da API.

Uso rapido:
  python scripts/send_admin_summary_push.py \
    --config scripts/send_admin_summary_push.example.json \
    --summary "Backend e apps mobile atualizados com suporte multi-device."

Inicializacao do arquivo local padrao:
  python scripts/send_admin_summary_push.py \
    --init-config \
    --api-base http://187.77.48.150:5193 \
    --token "<DEPLOY_WEBHOOK_TOKEN>"
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path


DEFAULT_CONFIG_PATHS = (
    Path.home() / ".codex" / "consertapramim" / "push-config.json",
    Path(__file__).resolve().with_name("send_admin_summary_push.local.json"),
)
DEFAULT_CONFIG_TEMPLATE = {
    "apiBase": "http://187.77.48.150:5193",
    "token": "replace-with-your-webhook-token",
    "actionUrl": "http://187.77.48.150:5151/AdminHome",
}


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Envia push de resumo para admins no ConsertaPraMim."
    )
    parser.add_argument(
        "--api-base",
        default="",
        help="Base da API (ex.: http://187.77.48.150:5193). Tem prioridade sobre arquivo local.",
    )
    parser.add_argument(
        "--token",
        default="",
        help="Token do header X-Deploy-Token. Tem prioridade sobre arquivo local.",
    )
    parser.add_argument(
        "--config",
        default="",
        help=(
            "Arquivo JSON de configuracao. Se omitido, procura automaticamente em: "
            + ", ".join(str(path) for path in DEFAULT_CONFIG_PATHS)
        ),
    )
    parser.add_argument(
        "--init-config",
        action="store_true",
        help="Cria automaticamente o JSON de configuracao local (nao sobrescreve arquivo existente).",
    )
    parser.add_argument(
        "--title",
        default="Resumo de entrega",
        help="Titulo da notificacao push.",
    )
    parser.add_argument(
        "--summary",
        default="",
        help="Resumo a ser enviado no corpo da notificacao.",
    )
    parser.add_argument(
        "--summary-file",
        default="",
        help="Arquivo texto com o resumo. Se informado, tem prioridade sobre --summary.",
    )
    parser.add_argument(
        "--action-url",
        default="",
        help="URL opcional para abrir ao tocar na notificacao. Tem prioridade sobre arquivo local.",
    )
    parser.add_argument(
        "--source",
        default="codex",
        help="Identificador de origem (default: codex).",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=20,
        help="Timeout da requisicao HTTP em segundos (default: 20).",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Mostra payload final sem enviar para API.",
    )
    return parser


def read_summary(args: argparse.Namespace) -> str:
    if args.summary_file:
        with open(args.summary_file, "r", encoding="utf-8") as handle:
            value = handle.read().strip()
            if value:
                return value
    return args.summary.strip()


def normalize_config_path(raw: str) -> Path:
    return Path(raw).expanduser().resolve()


def load_config_file(path: Path) -> dict[str, str]:
    with path.open("r", encoding="utf-8") as handle:
        payload = json.load(handle)

    if not isinstance(payload, dict):
        raise ValueError(f"arquivo de configuracao invalido: '{path}' deve conter um objeto JSON.")

    return {
        "apiBase": str(payload.get("apiBase", "") or "").strip(),
        "token": str(payload.get("token", "") or "").strip(),
        "actionUrl": str(payload.get("actionUrl", "") or "").strip(),
    }


def resolve_local_config(explicit_path: str) -> tuple[dict[str, str], str]:
    if explicit_path:
        config_path = normalize_config_path(explicit_path)
        if not config_path.is_file():
            raise ValueError(f"arquivo de configuracao nao encontrado: {config_path}")
        return load_config_file(config_path), str(config_path)

    for candidate in DEFAULT_CONFIG_PATHS:
        if candidate.is_file():
            return load_config_file(candidate), str(candidate)

    return {}, ""


def build_config_payload(
    args: argparse.Namespace,
    loaded_config: dict[str, str],
    *,
    include_defaults: bool,
) -> dict[str, str]:
    api_base = (
        (args.api_base or "").strip()
        or loaded_config.get("apiBase", "")
        or os.getenv("CPM_API_BASE_URL", "").strip()
        or (DEFAULT_CONFIG_TEMPLATE["apiBase"] if include_defaults else "")
    )
    token = (
        (args.token or "").strip()
        or loaded_config.get("token", "")
        or os.getenv("CPM_DEPLOY_NOTIFICATIONS_WEBHOOK_TOKEN", "").strip()
        or (DEFAULT_CONFIG_TEMPLATE["token"] if include_defaults else "")
    )
    action_url = (
        (args.action_url or "").strip()
        or loaded_config.get("actionUrl", "")
        or os.getenv("CPM_ADMIN_SUMMARY_ACTION_URL", "").strip()
        or (DEFAULT_CONFIG_TEMPLATE["actionUrl"] if include_defaults else "")
    )
    return {
        "apiBase": api_base,
        "token": token,
        "actionUrl": action_url,
    }


def initialize_config_file(target_path: Path, payload: dict[str, str]) -> None:
    target_path.parent.mkdir(parents=True, exist_ok=True)
    with target_path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


def normalize_api_base(raw: str) -> str:
    value = (raw or "").strip().rstrip("/")
    if not value:
        raise ValueError("api-base nao informado.")
    if not (value.startswith("http://") or value.startswith("https://")):
        raise ValueError("api-base deve iniciar com http:// ou https://.")
    return value


def send_push(
    api_base: str,
    token: str,
    title: str,
    summary: str,
    source: str,
    action_url: str,
    timeout: int,
) -> dict:
    endpoint = f"{api_base}/api/internal/deploy/admin-summary"
    payload = {
        "title": title,
        "summary": summary,
        "source": source,
    }
    if action_url:
        payload["actionUrl"] = action_url

    body = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        endpoint,
        data=body,
        method="POST",
        headers={
            "Content-Type": "application/json",
            "X-Deploy-Token": token,
        },
    )

    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            response_body = response.read().decode("utf-8", errors="replace")
            return {
                "ok": True,
                "status": response.status,
                "body": response_body,
                "endpoint": endpoint,
                "payload": payload,
            }
    except urllib.error.HTTPError as ex:
        response_body = ex.read().decode("utf-8", errors="replace")
        return {
            "ok": False,
            "status": ex.code,
            "body": response_body,
            "endpoint": endpoint,
            "payload": payload,
        }
    except urllib.error.URLError as ex:
        return {
            "ok": False,
            "status": 0,
            "body": str(ex),
            "endpoint": endpoint,
            "payload": payload,
        }


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    should_skip_missing_config = bool(args.init_config and args.config)

    try:
        if should_skip_missing_config and not normalize_config_path(args.config).exists():
            config_values, config_source = {}, ""
        else:
            config_values, config_source = resolve_local_config(args.config)
    except ValueError as ex:
        print(f"[ERRO] {ex}", file=sys.stderr)
        return 2

    if args.init_config:
        target_path = (
            normalize_config_path(args.config)
            if args.config
            else DEFAULT_CONFIG_PATHS[0]
        )

        if target_path.exists():
            print(
                f"[ERRO] arquivo de configuracao ja existe: {target_path}. Remova ou use outro caminho com --config.",
                file=sys.stderr,
            )
            return 2

        payload = build_config_payload(args, config_values, include_defaults=True)
        initialize_config_file(target_path, payload)
        print(f"[OK] Arquivo de configuracao criado em: {target_path}")
        print(json.dumps(payload, ensure_ascii=False, indent=2))
        return 0

    resolved_config = build_config_payload(args, config_values, include_defaults=False)
    api_base = resolved_config["apiBase"]

    try:
        api_base = normalize_api_base(api_base)
    except ValueError as ex:
        print(
            f"[ERRO] {ex} Use --api-base, --config ou crie um arquivo local em {DEFAULT_CONFIG_PATHS[0]}",
            file=sys.stderr,
        )
        return 2

    token = resolved_config["token"]
    if not token:
        print(
            "[ERRO] token nao informado. Use --token, --config ou arquivo local padrao.",
            file=sys.stderr,
        )
        return 2

    summary = read_summary(args)
    if not summary:
        print("[ERRO] summary vazio. Use --summary ou --summary-file.", file=sys.stderr)
        return 2

    action_url = resolved_config["actionUrl"]

    if args.dry_run:
        print(json.dumps(
            {
                "endpoint": f"{api_base}/api/internal/deploy/admin-summary",
                "configSource": config_source or None,
                "payload": {
                    "title": args.title.strip() or "Resumo de entrega",
                    "summary": summary,
                    "source": (args.source or "codex").strip(),
                    **({"actionUrl": action_url} if action_url else {}),
                },
            },
            ensure_ascii=False,
            indent=2,
        ))
        return 0

    result = send_push(
        api_base=api_base,
        token=token,
        title=(args.title.strip() or "Resumo de entrega"),
        summary=summary,
        source=(args.source or "codex").strip(),
        action_url=action_url,
        timeout=max(1, int(args.timeout)),
    )

    if result["ok"]:
        print(f"[OK] Push enviado. status={result['status']}")
        print(result["body"])
        return 0

    print(f"[ERRO] Falha ao enviar push. status={result['status']}", file=sys.stderr)
    print(result["body"], file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
