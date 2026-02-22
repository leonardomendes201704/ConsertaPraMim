#!/usr/bin/env python3
"""Dispara push de resumo para o app admin via endpoint interno da API.

Uso rapido:
  python scripts/send_admin_summary_push.py \
    --api-base http://187.77.48.150:5193 \
    --token "<DEPLOY_WEBHOOK_TOKEN>" \
    --summary "Backend e apps mobile atualizados com suporte multi-device."
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Envia push de resumo para admins no ConsertaPraMim."
    )
    parser.add_argument(
        "--api-base",
        default=os.getenv("CPM_API_BASE_URL", "").strip(),
        help="Base da API (ex.: http://187.77.48.150:5193). Pode usar env CPM_API_BASE_URL.",
    )
    parser.add_argument(
        "--token",
        default=os.getenv("CPM_DEPLOY_NOTIFICATIONS_WEBHOOK_TOKEN", "").strip(),
        help="Token do header X-Deploy-Token. Pode usar env CPM_DEPLOY_NOTIFICATIONS_WEBHOOK_TOKEN.",
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
        default=os.getenv("CPM_ADMIN_SUMMARY_ACTION_URL", "").strip(),
        help="URL opcional para abrir ao tocar na notificacao.",
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

    try:
        api_base = normalize_api_base(args.api_base)
    except ValueError as ex:
        print(f"[ERRO] {ex}", file=sys.stderr)
        return 2

    token = (args.token or "").strip()
    if not token:
        print("[ERRO] token nao informado. Use --token ou env CPM_DEPLOY_NOTIFICATIONS_WEBHOOK_TOKEN.", file=sys.stderr)
        return 2

    summary = read_summary(args)
    if not summary:
        print("[ERRO] summary vazio. Use --summary ou --summary-file.", file=sys.stderr)
        return 2

    if args.dry_run:
        print(json.dumps(
            {
                "endpoint": f"{api_base}/api/internal/deploy/admin-summary",
                "payload": {
                    "title": args.title.strip() or "Resumo de entrega",
                    "summary": summary,
                    "source": (args.source or "codex").strip(),
                    **({"actionUrl": args.action_url.strip()} if args.action_url.strip() else {}),
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
        action_url=args.action_url.strip(),
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
