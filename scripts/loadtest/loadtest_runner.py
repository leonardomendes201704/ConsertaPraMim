#!/usr/bin/env python3
"""
ConsertaPraMim API Load Test Runner
"""

from __future__ import annotations

import argparse
import asyncio
import json
import math
import random
import re
import sys
import time
import uuid
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Optional

import httpx


DEFAULT_TIMEOUT_SECONDS = 20.0
DEFAULT_OUTPUT_DIR = Path(__file__).resolve().parent / "output"


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def to_float(value: Any, default: float) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def to_int(value: Any, default: int) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return default


def percentile(values: list[float], p: float) -> float:
    if not values:
        return 0.0
    if p <= 0:
        return min(values)
    if p >= 100:
        return max(values)

    ordered = sorted(values)
    rank = (len(ordered) - 1) * (p / 100.0)
    low = math.floor(rank)
    high = math.ceil(rank)

    if low == high:
        return float(ordered[low])

    low_value = ordered[low]
    high_value = ordered[high]
    fraction = rank - low
    return float(low_value + ((high_value - low_value) * fraction))


def normalize_error_message(raw: str) -> str:
    if not raw:
        return "unknown_error"

    normalized = raw.strip()
    normalized = re.sub(r"[\r\n\t]+", " ", normalized)
    normalized = re.sub(r"\s+", " ", normalized)
    normalized = re.sub(
        r"\b[0-9a-f]{8}\-[0-9a-f]{4}\-[0-9a-f]{4}\-[0-9a-f]{4}\-[0-9a-f]{12}\b",
        "{guid}",
        normalized,
        flags=re.IGNORECASE,
    )
    normalized = re.sub(r"\b\d{2,}\b", "{n}", normalized)

    if len(normalized) > 180:
        normalized = normalized[:177] + "..."

    return normalized


def truncate_text(text: str, max_length: int = 400) -> str:
    if text is None:
        return ""
    if len(text) <= max_length:
        return text
    return text[: max_length - 3] + "..."


def weighted_choice(items: list[dict[str, Any]], rng: random.Random) -> dict[str, Any]:
    total = sum(max(to_float(item.get("weight"), 0.0), 0.0) for item in items)
    if total <= 0:
        return rng.choice(items)

    point = rng.uniform(0, total)
    cumulative = 0.0
    for item in items:
        cumulative += max(to_float(item.get("weight"), 0.0), 0.0)
        if point <= cumulative:
            return item

    return items[-1]


@dataclass
class FailureSample:
    timestamp_utc: str
    client_id: str
    correlation_id: str
    endpoint: str
    method: str
    path: str
    status_code: Optional[int]
    duration_ms: float
    error_type: str
    error_message: str
    request_body: Optional[str]
    response_snippet: Optional[str]


@dataclass
class MetricsCollector:
    started_epoch: float
    max_failure_samples: int = 10

    total_requests: int = 0
    successful_requests: int = 0
    failed_requests: int = 0
    total_duration_ms: list[float] = field(default_factory=list)

    status_counts: Counter = field(default_factory=Counter)
    exception_counts: Counter = field(default_factory=Counter)
    endpoint_hits: Counter = field(default_factory=Counter)
    endpoint_errors: Counter = field(default_factory=Counter)
    endpoint_durations: dict[str, list[float]] = field(default_factory=lambda: defaultdict(list))

    requests_per_second: Counter = field(default_factory=Counter)

    error_catalog_counts: Counter = field(default_factory=Counter)
    error_catalog_endpoints: dict[str, set[str]] = field(default_factory=lambda: defaultdict(set))

    failure_samples: list[FailureSample] = field(default_factory=list)

    def record(
        self,
        *,
        endpoint_key: str,
        status_code: Optional[int],
        duration_ms: float,
        timestamp_epoch: float,
        error_type: Optional[str] = None,
        error_message: Optional[str] = None,
        failure_sample: Optional[FailureSample] = None,
    ) -> None:
        self.total_requests += 1
        self.endpoint_hits[endpoint_key] += 1
        self.endpoint_durations[endpoint_key].append(duration_ms)
        self.total_duration_ms.append(duration_ms)

        second_bucket = int(max(0, math.floor(timestamp_epoch - self.started_epoch)))
        self.requests_per_second[second_bucket] += 1

        if status_code is not None:
            self.status_counts[status_code] += 1

        is_failure = False
        if status_code is not None and status_code >= 400:
            is_failure = True
        if error_type:
            is_failure = True

        if is_failure:
            self.failed_requests += 1
            self.endpoint_errors[endpoint_key] += 1
            normalized = normalize_error_message(error_message or error_type or "request_failed")
            self.error_catalog_counts[normalized] += 1
            self.error_catalog_endpoints[normalized].add(endpoint_key)

            if error_type:
                self.exception_counts[error_type] += 1

            if failure_sample and len(self.failure_samples) < self.max_failure_samples:
                self.failure_samples.append(failure_sample)
        else:
            self.successful_requests += 1

    def build_report(
        self,
        *,
        run_id: str,
        scenario_name: str,
        started_at_utc: str,
        finished_at_utc: str,
        duration_seconds: float,
        base_url: str,
        scenario_config: dict[str, Any],
        resolved_endpoints: list[dict[str, Any]],
    ) -> dict[str, Any]:
        total = self.total_requests
        duration = max(duration_seconds, 0.001)

        avg_rps = total / duration
        peak_rps = max(self.requests_per_second.values(), default=0)

        min_latency = min(self.total_duration_ms) if self.total_duration_ms else 0.0
        avg_latency = (sum(self.total_duration_ms) / len(self.total_duration_ms)) if self.total_duration_ms else 0.0
        max_latency = max(self.total_duration_ms) if self.total_duration_ms else 0.0

        p50 = percentile(self.total_duration_ms, 50)
        p95 = percentile(self.total_duration_ms, 95)
        p99 = percentile(self.total_duration_ms, 99)

        error_rate = (self.failed_requests / total * 100.0) if total else 0.0

        status_breakdown = []
        for status_code, count in self.status_counts.most_common():
            pct = (count / total * 100.0) if total else 0.0
            status_breakdown.append(
                {
                    "statusCode": status_code,
                    "count": count,
                    "percentage": round(pct, 2),
                }
            )

        endpoint_stats = []
        for endpoint_key, hits in self.endpoint_hits.items():
            durations = self.endpoint_durations.get(endpoint_key, [])
            errors = self.endpoint_errors.get(endpoint_key, 0)
            endpoint_stats.append(
                {
                    "endpoint": endpoint_key,
                    "hits": hits,
                    "errors": errors,
                    "errorRatePercent": round((errors / hits * 100.0) if hits else 0.0, 2),
                    "avgLatencyMs": round(sum(durations) / len(durations), 2) if durations else 0.0,
                    "p95LatencyMs": round(percentile(durations, 95), 2) if durations else 0.0,
                }
            )

        top_by_hits = sorted(endpoint_stats, key=lambda x: x["hits"], reverse=True)[:10]
        top_by_p95 = sorted(endpoint_stats, key=lambda x: x["p95LatencyMs"], reverse=True)[:10]

        top_errors = []
        for message, count in self.error_catalog_counts.most_common(10):
            top_errors.append(
                {
                    "message": message,
                    "count": count,
                    "endpoints": sorted(self.error_catalog_endpoints.get(message, set())),
                }
            )

        failures = [
            {
                "timestampUtc": sample.timestamp_utc,
                "clientId": sample.client_id,
                "correlationId": sample.correlation_id,
                "endpoint": sample.endpoint,
                "method": sample.method,
                "path": sample.path,
                "statusCode": sample.status_code,
                "durationMs": round(sample.duration_ms, 2),
                "errorType": sample.error_type,
                "errorMessage": sample.error_message,
                "requestBody": sample.request_body,
                "responseSnippet": sample.response_snippet,
            }
            for sample in self.failure_samples
        ]

        return {
            "runId": run_id,
            "scenario": scenario_name,
            "baseUrl": base_url,
            "startedAtUtc": started_at_utc,
            "finishedAtUtc": finished_at_utc,
            "durationSeconds": round(duration_seconds, 2),
            "summary": {
                "totalRequests": total,
                "successfulRequests": self.successful_requests,
                "failedRequests": self.failed_requests,
                "errorRatePercent": round(error_rate, 2),
                "rpsAvg": round(avg_rps, 2),
                "rpsPeak": peak_rps,
            },
            "latencyMs": {
                "min": round(min_latency, 2),
                "avg": round(avg_latency, 2),
                "max": round(max_latency, 2),
                "p50": round(p50, 2),
                "p95": round(p95, 2),
                "p99": round(p99, 2),
            },
            "statusCodes": status_breakdown,
            "exceptions": [{"type": key, "count": value} for key, value in self.exception_counts.most_common()],
            "topEndpointsByHits": top_by_hits,
            "topEndpointsByP95": top_by_p95,
            "topErrors": top_errors,
            "failureSamples": failures,
            "scenarioConfig": scenario_config,
            "resolvedEndpoints": resolved_endpoints,
        }


class VuSession:
    def __init__(
        self,
        *,
        vu_index: int,
        scenario_name: str,
        base_url: str,
        scenario_cfg: dict[str, Any],
        global_cfg: dict[str, Any],
        endpoints: list[dict[str, Any]],
        metrics: MetricsCollector,
        timeout_seconds: float,
        insecure_tls: bool,
        random_seed: int,
    ) -> None:
        self.vu_index = vu_index
        self.scenario_name = scenario_name
        self.base_url = base_url.rstrip("/")
        self.scenario_cfg = scenario_cfg
        self.global_cfg = global_cfg
        self.endpoints = endpoints
        self.metrics = metrics
        self.rng = random.Random((random_seed * 10000) + vu_index)

        self.client_id = f"LT-{scenario_name.upper()}-{vu_index:04d}"
        self.tenant_id = self._pick_tenant_id()

        self.auth_cfg = global_cfg.get("auth", {}) or {}
        self.auth_enabled = bool(self.auth_cfg.get("enabled"))
        self.account = self._pick_account()
        self.access_token: Optional[str] = None

        self.state: dict[str, Any] = {
            "orderIds": [],
        }

        self.http_client = httpx.AsyncClient(
            timeout=httpx.Timeout(timeout_seconds),
            verify=not insecure_tls,
            limits=httpx.Limits(max_keepalive_connections=50, max_connections=100),
        )

    def _pick_account(self) -> Optional[dict[str, Any]]:
        accounts = self.auth_cfg.get("accounts") or []
        if not accounts:
            return None
        index = (self.vu_index - 1) % len(accounts)
        return accounts[index]

    def _pick_tenant_id(self) -> Optional[str]:
        tenant_ids = self.global_cfg.get("tenantIds") or []
        if not tenant_ids:
            return None
        return tenant_ids[(self.vu_index - 1) % len(tenant_ids)]

    async def close(self) -> None:
        await self.http_client.aclose()

    async def ensure_login(self, force: bool = False) -> bool:
        if not self.auth_enabled:
            return False
        if self.access_token and not force:
            return True
        if not self.account:
            return False

        login_path = self.auth_cfg.get("loginPath") or "/api/auth/login"
        login_url = self.base_url + login_path
        payload = {
            "email": self.account.get("email"),
            "password": self.account.get("password"),
        }

        correlation_id = str(uuid.uuid4())
        headers = {
            "Content-Type": "application/json",
            "X-Client-Id": self.client_id,
            "X-Correlation-Id": correlation_id,
        }
        if self.tenant_id:
            headers["X-Tenant-Id"] = self.tenant_id

        start = time.perf_counter()
        timestamp = time.time()

        try:
            response = await self.http_client.post(login_url, json=payload, headers=headers)
            duration_ms = (time.perf_counter() - start) * 1000.0

            token_field = self.auth_cfg.get("tokenField") or "token"
            token_value = None
            error_message = ""

            if response.status_code < 400:
                try:
                    response_json = response.json()
                    token_value = response_json.get(token_field)
                except ValueError:
                    token_value = None

            if not token_value:
                error_message = truncate_text(response.text or "login_failed")
                sample = FailureSample(
                    timestamp_utc=utc_now_iso(),
                    client_id=self.client_id,
                    correlation_id=correlation_id,
                    endpoint="auth.login",
                    method="POST",
                    path=login_path,
                    status_code=response.status_code,
                    duration_ms=duration_ms,
                    error_type="login_error",
                    error_message=normalize_error_message(error_message),
                    request_body=json.dumps(payload, ensure_ascii=False),
                    response_snippet=truncate_text(response.text or "", 260),
                )
                self.metrics.record(
                    endpoint_key="auth.login",
                    status_code=response.status_code,
                    duration_ms=duration_ms,
                    timestamp_epoch=timestamp,
                    error_type="login_error",
                    error_message=error_message,
                    failure_sample=sample,
                )
                return False

            self.access_token = str(token_value)
            self.metrics.record(
                endpoint_key="auth.login",
                status_code=response.status_code,
                duration_ms=duration_ms,
                timestamp_epoch=timestamp,
            )
            return True
        except httpx.TimeoutException as exc:
            duration_ms = (time.perf_counter() - start) * 1000.0
            message = f"timeout: {exc}"
            sample = FailureSample(
                timestamp_utc=utc_now_iso(),
                client_id=self.client_id,
                correlation_id=correlation_id,
                endpoint="auth.login",
                method="POST",
                path=login_path,
                status_code=None,
                duration_ms=duration_ms,
                error_type="timeout",
                error_message=normalize_error_message(message),
                request_body=json.dumps(payload, ensure_ascii=False),
                response_snippet="",
            )
            self.metrics.record(
                endpoint_key="auth.login",
                status_code=None,
                duration_ms=duration_ms,
                timestamp_epoch=timestamp,
                error_type="timeout",
                error_message=message,
                failure_sample=sample,
            )
            return False
        except Exception as exc:
            duration_ms = (time.perf_counter() - start) * 1000.0
            message = f"exception: {type(exc).__name__}: {exc}"
            sample = FailureSample(
                timestamp_utc=utc_now_iso(),
                client_id=self.client_id,
                correlation_id=correlation_id,
                endpoint="auth.login",
                method="POST",
                path=login_path,
                status_code=None,
                duration_ms=duration_ms,
                error_type=type(exc).__name__,
                error_message=normalize_error_message(message),
                request_body=json.dumps(payload, ensure_ascii=False),
                response_snippet="",
            )
            self.metrics.record(
                endpoint_key="auth.login",
                status_code=None,
                duration_ms=duration_ms,
                timestamp_epoch=timestamp,
                error_type=type(exc).__name__,
                error_message=message,
                failure_sample=sample,
            )
            return False

    def _build_headers(self, correlation_id: str, endpoint: dict[str, Any], has_body: bool) -> dict[str, str]:
        headers = {
            "Accept": "application/json",
            "X-Client-Id": self.client_id,
            "X-Correlation-Id": correlation_id,
        }
        if has_body:
            headers["Content-Type"] = "application/json"

        default_headers = self.global_cfg.get("defaultHeaders") or {}
        for key, value in default_headers.items():
            headers[str(key)] = str(value)

        if self.tenant_id:
            headers["X-Tenant-Id"] = self.tenant_id

        auth_mode = str(endpoint.get("auth") or "none").lower()
        if auth_mode == "bearer" and self.access_token:
            headers["Authorization"] = f"Bearer {self.access_token}"

        custom_headers = endpoint.get("headers") or {}
        for key, value in custom_headers.items():
            headers[str(key)] = str(value)

        return headers

    def _resolve_path(self, endpoint: dict[str, Any], inject_invalid: bool) -> tuple[str, str]:
        template_path = str(endpoint.get("path") or "/")
        path_to_use = template_path

        if inject_invalid and endpoint.get("invalidPath"):
            path_to_use = str(endpoint.get("invalidPath"))

        if "{orderId}" in path_to_use:
            order_ids = self.state.get("orderIds") or []
            if order_ids:
                selected_order_id = order_ids[self.rng.randrange(0, len(order_ids))]
                path_to_use = path_to_use.replace("{orderId}", str(selected_order_id))
            else:
                fallback_path = endpoint.get("fallbackPath")
                if fallback_path:
                    path_to_use = str(fallback_path)
                else:
                    path_to_use = path_to_use.replace("{orderId}", "00000000-0000-0000-0000-000000000000")

        if not path_to_use.startswith("/"):
            path_to_use = "/" + path_to_use

        return template_path, path_to_use

    def _resolve_body(self, endpoint: dict[str, Any], inject_invalid: bool) -> Optional[dict[str, Any]]:
        body = endpoint.get("bodyTemplate")
        invalid_body = endpoint.get("invalidBodyTemplate")

        if inject_invalid and invalid_body is not None:
            body = invalid_body

        if body is None:
            return None

        if isinstance(body, dict):
            return json.loads(json.dumps(body))
        return None

    def _capture_response_state(self, endpoint: dict[str, Any], response_json: Any) -> None:
        capture_mode = endpoint.get("capture")
        if capture_mode == "client_order_ids" and isinstance(response_json, dict):
            order_ids: list[str] = []
            for key in ("openOrders", "finalizedOrders"):
                items = response_json.get(key)
                if isinstance(items, list):
                    for item in items:
                        if isinstance(item, dict) and item.get("id"):
                            order_ids.append(str(item.get("id")))

            if order_ids:
                unique_ids = sorted(set(order_ids))
                self.state["orderIds"] = unique_ids

    async def execute_request(self, endpoint: dict[str, Any]) -> None:
        inject_error_rate = to_float(self.scenario_cfg.get("errorInjectionRatePercent"), 0.0)
        should_inject_invalid = inject_error_rate > 0 and self.rng.uniform(0, 100) <= inject_error_rate

        method = str(endpoint.get("method") or "GET").upper()
        template_path, resolved_path = self._resolve_path(endpoint, inject_invalid=should_inject_invalid)
        body = self._resolve_body(endpoint, inject_invalid=should_inject_invalid)

        auth_mode = str(endpoint.get("auth") or "none").lower()
        if auth_mode == "bearer" and self.auth_enabled:
            await self.ensure_login(force=False)

        correlation_id = str(uuid.uuid4())
        headers = self._build_headers(correlation_id, endpoint, has_body=(body is not None))

        url = self.base_url + resolved_path
        endpoint_key = f"{method} {template_path}"

        start = time.perf_counter()
        timestamp = time.time()

        try:
            response = await self.http_client.request(method, url, headers=headers, json=body)
            duration_ms = (time.perf_counter() - start) * 1000.0

            response_text = response.text or ""
            normalized_message = normalize_error_message(response_text)

            failure_sample = None
            if response.status_code >= 400:
                failure_sample = FailureSample(
                    timestamp_utc=utc_now_iso(),
                    client_id=self.client_id,
                    correlation_id=correlation_id,
                    endpoint=endpoint_key,
                    method=method,
                    path=resolved_path,
                    status_code=response.status_code,
                    duration_ms=duration_ms,
                    error_type=f"http_{response.status_code}",
                    error_message=normalized_message,
                    request_body=(json.dumps(body, ensure_ascii=False) if body is not None else None),
                    response_snippet=truncate_text(response_text, 300),
                )

            self.metrics.record(
                endpoint_key=endpoint_key,
                status_code=response.status_code,
                duration_ms=duration_ms,
                timestamp_epoch=timestamp,
                error_type=(f"http_{response.status_code}" if response.status_code >= 400 else None),
                error_message=(response_text if response.status_code >= 400 else None),
                failure_sample=failure_sample,
            )

            if response.status_code == 401 and auth_mode == "bearer" and self.auth_enabled:
                await self.ensure_login(force=True)

            if response.status_code < 400 and endpoint.get("capture"):
                try:
                    data = response.json()
                    self._capture_response_state(endpoint, data)
                except ValueError:
                    pass

        except httpx.TimeoutException as exc:
            duration_ms = (time.perf_counter() - start) * 1000.0
            message = f"timeout: {exc}"
            sample = FailureSample(
                timestamp_utc=utc_now_iso(),
                client_id=self.client_id,
                correlation_id=correlation_id,
                endpoint=endpoint_key,
                method=method,
                path=resolved_path,
                status_code=None,
                duration_ms=duration_ms,
                error_type="timeout",
                error_message=normalize_error_message(message),
                request_body=(json.dumps(body, ensure_ascii=False) if body is not None else None),
                response_snippet="",
            )
            self.metrics.record(
                endpoint_key=endpoint_key,
                status_code=None,
                duration_ms=duration_ms,
                timestamp_epoch=timestamp,
                error_type="timeout",
                error_message=message,
                failure_sample=sample,
            )
        except Exception as exc:
            duration_ms = (time.perf_counter() - start) * 1000.0
            message = f"exception: {type(exc).__name__}: {exc}"
            sample = FailureSample(
                timestamp_utc=utc_now_iso(),
                client_id=self.client_id,
                correlation_id=correlation_id,
                endpoint=endpoint_key,
                method=method,
                path=resolved_path,
                status_code=None,
                duration_ms=duration_ms,
                error_type=type(exc).__name__,
                error_message=normalize_error_message(message),
                request_body=(json.dumps(body, ensure_ascii=False) if body is not None else None),
                response_snippet="",
            )
            self.metrics.record(
                endpoint_key=endpoint_key,
                status_code=None,
                duration_ms=duration_ms,
                timestamp_epoch=timestamp,
                error_type=type(exc).__name__,
                error_message=message,
                failure_sample=sample,
            )


async def run_scenario(
    *,
    scenario_name: str,
    base_url: str,
    scenario_cfg: dict[str, Any],
    global_cfg: dict[str, Any],
    endpoints: list[dict[str, Any]],
    timeout_seconds: float,
    insecure_tls: bool,
    random_seed: int,
) -> dict[str, Any]:
    vus = to_int(scenario_cfg.get("vus"), 10)
    duration_seconds = max(to_int(scenario_cfg.get("durationSeconds"), 30), 1)
    ramp_up_seconds = max(to_float(scenario_cfg.get("rampUpSeconds"), 0.0), 0.0)

    think_min_ms = max(to_int(scenario_cfg.get("thinkTimeMinMs"), 100), 0)
    think_max_ms = max(to_int(scenario_cfg.get("thinkTimeMaxMs"), 600), think_min_ms)

    started_epoch = time.time()
    started_utc = utc_now_iso()

    metrics = MetricsCollector(started_epoch=started_epoch)

    stop_at = time.perf_counter() + duration_seconds

    async def vu_worker(vu_index: int) -> None:
        session = VuSession(
            vu_index=vu_index,
            scenario_name=scenario_name,
            base_url=base_url,
            scenario_cfg=scenario_cfg,
            global_cfg=global_cfg,
            endpoints=endpoints,
            metrics=metrics,
            timeout_seconds=timeout_seconds,
            insecure_tls=insecure_tls,
            random_seed=random_seed,
        )

        try:
            if ramp_up_seconds > 0 and vus > 1:
                delay = (ramp_up_seconds / max(vus - 1, 1)) * (vu_index - 1)
                await asyncio.sleep(delay)

            while time.perf_counter() < stop_at:
                endpoint = weighted_choice(endpoints, session.rng)
                await session.execute_request(endpoint)

                think_ms = session.rng.randint(think_min_ms, think_max_ms)
                await asyncio.sleep(think_ms / 1000.0)
        finally:
            await session.close()

    workers = [asyncio.create_task(vu_worker(index)) for index in range(1, vus + 1)]
    await asyncio.gather(*workers)

    finished_utc = utc_now_iso()
    elapsed_seconds = max(time.time() - started_epoch, 0.0)

    return metrics.build_report(
        run_id=str(uuid.uuid4()),
        scenario_name=scenario_name,
        started_at_utc=started_utc,
        finished_at_utc=finished_utc,
        duration_seconds=elapsed_seconds,
        base_url=base_url,
        scenario_config=scenario_cfg,
        resolved_endpoints=endpoints,
    )


def print_report(report: dict[str, Any]) -> None:
    summary = report.get("summary", {})
    latency = report.get("latencyMs", {})

    print("\n=== Load Test Summary ===")
    print(f"Run ID: {report.get('runId')}")
    print(f"Scenario: {report.get('scenario')} | Base URL: {report.get('baseUrl')}")
    print(f"Started: {report.get('startedAtUtc')} | Finished: {report.get('finishedAtUtc')}")
    print(f"Duration: {report.get('durationSeconds')} s")

    print("\n-- Requests --")
    print(f"Total: {summary.get('totalRequests', 0)}")
    print(f"Success: {summary.get('successfulRequests', 0)}")
    print(f"Failed: {summary.get('failedRequests', 0)} ({summary.get('errorRatePercent', 0)}%)")
    print(f"RPS avg: {summary.get('rpsAvg', 0)} | RPS peak: {summary.get('rpsPeak', 0)}")

    print("\n-- Latency --")
    print(
        f"min/avg/max: {latency.get('min', 0)} / {latency.get('avg', 0)} / {latency.get('max', 0)} ms"
    )
    print(
        f"p50/p95/p99: {latency.get('p50', 0)} / {latency.get('p95', 0)} / {latency.get('p99', 0)} ms"
    )

    print("\n-- Errors by Status --")
    status_codes = report.get("statusCodes", [])
    if not status_codes:
        print("(none)")
    else:
        for item in status_codes:
            print(f"{item.get('statusCode')}: {item.get('count')} ({item.get('percentage')}%)")

    exceptions = report.get("exceptions", [])
    if exceptions:
        print("\n-- Exceptions/Timeouts --")
        for item in exceptions:
            print(f"{item.get('type')}: {item.get('count')}")

    print("\n-- Top Endpoints by Hits --")
    top_hits = report.get("topEndpointsByHits", [])
    if not top_hits:
        print("(none)")
    else:
        for item in top_hits[:10]:
            print(
                f"{item.get('endpoint')}: hits={item.get('hits')} "
                f"p95={item.get('p95LatencyMs')}ms errorRate={item.get('errorRatePercent')}%"
            )

    print("\n-- Top Endpoints by p95 --")
    top_p95 = report.get("topEndpointsByP95", [])
    if not top_p95:
        print("(none)")
    else:
        for item in top_p95[:10]:
            print(
                f"{item.get('endpoint')}: p95={item.get('p95LatencyMs')}ms "
                f"hits={item.get('hits')} errorRate={item.get('errorRatePercent')}%"
            )

    print("\n-- Top Errors --")
    top_errors = report.get("topErrors", [])
    if not top_errors:
        print("(none)")
    else:
        for item in top_errors:
            endpoints = ", ".join(item.get("endpoints", []))
            print(f"{item.get('count')}x {item.get('message')} | endpoints: {endpoints}")

    print("\n-- Failure Samples (up to 10) --")
    samples = report.get("failureSamples", [])
    if not samples:
        print("(none)")
    else:
        for sample in samples[:10]:
            print(
                f"[{sample.get('timestampUtc')}] {sample.get('method')} {sample.get('path')} "
                f"status={sample.get('statusCode')} corr={sample.get('correlationId')} "
                f"error={sample.get('errorMessage')}"
            )


def save_reports(report: dict[str, Any], output_dir: Path) -> tuple[Path, Path]:
    output_dir.mkdir(parents=True, exist_ok=True)

    run_id = report.get("runId") or str(uuid.uuid4())
    json_path = output_dir / f"loadtest-report-{run_id}.json"
    txt_path = output_dir / f"loadtest-summary-{run_id}.txt"

    json_text = json.dumps(report, indent=2, ensure_ascii=False)
    json_path.write_text(json_text, encoding="utf-8")

    lines = [
        f"Run ID: {report.get('runId')}",
        f"Scenario: {report.get('scenario')}",
        f"Base URL: {report.get('baseUrl')}",
        f"Started: {report.get('startedAtUtc')}",
        f"Finished: {report.get('finishedAtUtc')}",
        f"Duration(s): {report.get('durationSeconds')}",
        "",
        f"Total requests: {report.get('summary', {}).get('totalRequests', 0)}",
        f"RPS avg: {report.get('summary', {}).get('rpsAvg', 0)}",
        f"RPS peak: {report.get('summary', {}).get('rpsPeak', 0)}",
        f"Error rate: {report.get('summary', {}).get('errorRatePercent', 0)}%",
        "",
        "Top endpoints by hits:",
    ]

    for item in report.get("topEndpointsByHits", []):
        lines.append(
            f"- {item.get('endpoint')} | hits={item.get('hits')} "
            f"p95={item.get('p95LatencyMs')}ms err={item.get('errorRatePercent')}%"
        )

    lines.append("\nTop errors:")
    for item in report.get("topErrors", []):
        lines.append(f"- {item.get('count')}x {item.get('message')}")

    txt_path.write_text("\n".join(lines), encoding="utf-8")

    latest_json = output_dir / "loadtest-report-latest.json"
    latest_txt = output_dir / "loadtest-summary-latest.txt"
    latest_json.write_text(json_text, encoding="utf-8")
    latest_txt.write_text("\n".join(lines), encoding="utf-8")

    html_text = render_html_report(report)
    html_path = output_dir / f"loadtest-report-{run_id}.html"
    html_path.write_text(html_text, encoding="utf-8")
    (output_dir / "loadtest-report-latest.html").write_text(html_text, encoding="utf-8")

    return json_path, txt_path


def render_html_report(report: dict[str, Any]) -> str:
    summary = report.get("summary", {})
    latency = report.get("latencyMs", {})
    statuses = report.get("statusCodes", [])
    top_hits = report.get("topEndpointsByHits", [])
    top_errors = report.get("topErrors", [])
    failures = report.get("failureSamples", [])

    def rows_for_status() -> str:
        if not statuses:
            return "<tr><td colspan='3'>(none)</td></tr>"
        return "".join(
            f"<tr><td>{item.get('statusCode')}</td><td>{item.get('count')}</td><td>{item.get('percentage')}%</td></tr>"
            for item in statuses
        )

    def rows_for_top_hits() -> str:
        if not top_hits:
            return "<tr><td colspan='5'>(none)</td></tr>"
        return "".join(
            "<tr>"
            f"<td>{item.get('endpoint')}</td>"
            f"<td>{item.get('hits')}</td>"
            f"<td>{item.get('p95LatencyMs')} ms</td>"
            f"<td>{item.get('errorRatePercent')}%</td>"
            f"<td>{item.get('avgLatencyMs')} ms</td>"
            "</tr>"
            for item in top_hits
        )

    def rows_for_errors() -> str:
        if not top_errors:
            return "<tr><td colspan='3'>(none)</td></tr>"
        return "".join(
            "<tr>"
            f"<td>{item.get('message')}</td>"
            f"<td>{item.get('count')}</td>"
            f"<td>{', '.join(item.get('endpoints', []))}</td>"
            "</tr>"
            for item in top_errors
        )

    def rows_for_failures() -> str:
        if not failures:
            return "<tr><td colspan='7'>(none)</td></tr>"
        return "".join(
            "<tr>"
            f"<td>{item.get('timestampUtc')}</td>"
            f"<td>{item.get('method')}</td>"
            f"<td>{item.get('path')}</td>"
            f"<td>{item.get('statusCode')}</td>"
            f"<td>{item.get('correlationId')}</td>"
            f"<td>{item.get('errorType')}</td>"
            f"<td>{truncate_text(str(item.get('errorMessage') or ''), 140)}</td>"
            "</tr>"
            for item in failures
        )

    return f"""<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>Load Test Report - {report.get('runId')}</title>
  <style>
    body {{ font-family: Arial, sans-serif; margin: 20px; color: #1f2937; }}
    h1, h2 {{ margin: 0 0 12px; }}
    .grid {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 10px; margin-bottom: 16px; }}
    .card {{ border: 1px solid #d1d5db; border-radius: 8px; padding: 10px; background: #f9fafb; }}
    table {{ width: 100%; border-collapse: collapse; margin: 12px 0 20px; font-size: 13px; }}
    th, td {{ border: 1px solid #d1d5db; padding: 6px 8px; text-align: left; vertical-align: top; }}
    th {{ background: #f3f4f6; }}
    code {{ background: #f3f4f6; padding: 2px 4px; border-radius: 4px; }}
  </style>
</head>
<body>
  <h1>Load Test Report</h1>
  <p><strong>RunId:</strong> <code>{report.get('runId')}</code></p>
  <p><strong>Scenario:</strong> {report.get('scenario')} | <strong>BaseUrl:</strong> {report.get('baseUrl')}</p>
  <p><strong>Inicio:</strong> {report.get('startedAtUtc')} | <strong>Fim:</strong> {report.get('finishedAtUtc')} | <strong>DuraÃ§Ã£o:</strong> {report.get('durationSeconds')}s</p>

  <div class="grid">
    <div class="card"><strong>Total requests</strong><br/>{summary.get('totalRequests', 0)}</div>
    <div class="card"><strong>Sucesso</strong><br/>{summary.get('successfulRequests', 0)}</div>
    <div class="card"><strong>Falhas</strong><br/>{summary.get('failedRequests', 0)} ({summary.get('errorRatePercent', 0)}%)</div>
    <div class="card"><strong>RPS mÃ©dio/pico</strong><br/>{summary.get('rpsAvg', 0)} / {summary.get('rpsPeak', 0)}</div>
    <div class="card"><strong>LatÃªncia p50</strong><br/>{latency.get('p50', 0)} ms</div>
    <div class="card"><strong>LatÃªncia p95/p99</strong><br/>{latency.get('p95', 0)} / {latency.get('p99', 0)} ms</div>
  </div>

  <h2>Status codes</h2>
  <table>
    <thead><tr><th>Status</th><th>Contagem</th><th>%</th></tr></thead>
    <tbody>{rows_for_status()}</tbody>
  </table>

  <h2>Top endpoints por hits</h2>
  <table>
    <thead><tr><th>Endpoint</th><th>Hits</th><th>P95</th><th>Error rate</th><th>MÃ©dia</th></tr></thead>
    <tbody>{rows_for_top_hits()}</tbody>
  </table>

  <h2>Top erros</h2>
  <table>
    <thead><tr><th>Mensagem normalizada</th><th>Contagem</th><th>Endpoints</th></tr></thead>
    <tbody>{rows_for_errors()}</tbody>
  </table>

  <h2>Amostras de falha</h2>
  <table>
    <thead><tr><th>Timestamp</th><th>MÃ©todo</th><th>Path</th><th>Status</th><th>CorrelationId</th><th>Tipo</th><th>Erro</th></tr></thead>
    <tbody>{rows_for_failures()}</tbody>
  </table>
</body>
</html>"""


def load_config(config_path: Path) -> dict[str, Any]:
    if not config_path.exists():
        raise FileNotFoundError(f"Arquivo de configuracao nao encontrado: {config_path}")

    text = config_path.read_text(encoding="utf-8-sig")
    data = json.loads(text)

    if not isinstance(data, dict):
        raise ValueError("Arquivo de configuracao invalido. Esperado objeto JSON.")

    return data


def resolve_scenario(config: dict[str, Any], scenario_name: str) -> dict[str, Any]:
    scenarios = config.get("scenarios")
    if not isinstance(scenarios, dict):
        raise ValueError("Configuracao invalida: campo 'scenarios' ausente.")

    scenario = scenarios.get(scenario_name)
    if not isinstance(scenario, dict):
        available = ", ".join(sorted(scenarios.keys()))
        raise ValueError(f"Cenario '{scenario_name}' nao encontrado. Disponiveis: {available}")

    return scenario


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="ConsertaPraMim API Load Test Runner")
    parser.add_argument("--config", default=str(Path(__file__).resolve().parent / "loadtest.config.json"), help="Caminho do arquivo de configuracao JSON")
    parser.add_argument("--scenario", default="smoke", help="Nome do cenario em loadtest.config.json")
    parser.add_argument("--base-url", default=None, help="Sobrescreve baseUrl da configuracao")
    parser.add_argument("--vus", type=int, default=None, help="Sobrescreve quantidade de clientes virtuais (VUs)")
    parser.add_argument("--duration", type=int, default=None, help="Sobrescreve durationSeconds")
    parser.add_argument("--ramp-up", type=float, default=None, help="Sobrescreve rampUpSeconds")
    parser.add_argument("--think-min", type=int, default=None, help="Sobrescreve thinkTimeMinMs")
    parser.add_argument("--think-max", type=int, default=None, help="Sobrescreve thinkTimeMaxMs")
    parser.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT_SECONDS, help="Timeout HTTP por request")
    parser.add_argument("--insecure", action="store_true", help="Desabilita validacao TLS (self-signed em dev)")
    parser.add_argument("--seed", type=int, default=42, help="Seed para reproducibilidade")
    parser.add_argument("--output-dir", default=str(DEFAULT_OUTPUT_DIR), help="Diretorio de saida dos relatorios")
    parser.add_argument("--auth-password", default=None, help="Sobrescreve senha de todas as contas de auth no config")
    return parser.parse_args()


async def main_async(args: argparse.Namespace) -> int:
    config_path = Path(args.config).resolve()
    config = load_config(config_path)

    scenario_cfg = resolve_scenario(config, args.scenario)

    if args.base_url:
        config["baseUrl"] = args.base_url
    base_url = str(config.get("baseUrl") or "").rstrip("/")
    if not base_url:
        raise ValueError("baseUrl nao informado no config nem via --base-url")

    if args.vus is not None:
        scenario_cfg["vus"] = args.vus
    if args.duration is not None:
        scenario_cfg["durationSeconds"] = args.duration
    if args.ramp_up is not None:
        scenario_cfg["rampUpSeconds"] = args.ramp_up
    if args.think_min is not None:
        scenario_cfg["thinkTimeMinMs"] = args.think_min
    if args.think_max is not None:
        scenario_cfg["thinkTimeMaxMs"] = args.think_max

    if args.auth_password:
        auth_cfg = config.get("auth") or {}
        accounts = auth_cfg.get("accounts") or []
        for account in accounts:
            account["password"] = args.auth_password

    endpoints = config.get("endpoints")
    if not isinstance(endpoints, list) or not endpoints:
        raise ValueError("Configuracao invalida: endpoints precisa ser uma lista nao vazia.")

    print("=== ConsertaPraMim Load Test ===")
    print(f"Config: {config_path}")
    print(f"Scenario: {args.scenario}")
    print(f"Base URL: {base_url}")
    print(
        f"VUs: {scenario_cfg.get('vus')} | Duration(s): {scenario_cfg.get('durationSeconds')} | "
        f"RampUp(s): {scenario_cfg.get('rampUpSeconds', 0)}"
    )
    print(
        f"Think(ms): {scenario_cfg.get('thinkTimeMinMs')}..{scenario_cfg.get('thinkTimeMaxMs')} | "
        f"Error Injection(%): {scenario_cfg.get('errorInjectionRatePercent', 0)}"
    )

    report = await run_scenario(
        scenario_name=args.scenario,
        base_url=base_url,
        scenario_cfg=scenario_cfg,
        global_cfg=config,
        endpoints=endpoints,
        timeout_seconds=max(args.timeout, 1.0),
        insecure_tls=args.insecure,
        random_seed=args.seed,
    )

    print_report(report)

    output_dir = Path(args.output_dir).resolve()
    json_path, txt_path = save_reports(report, output_dir)
    print("\nRelatorios gerados:")
    print(f"- JSON: {json_path}")
    print(f"- TXT:  {txt_path}")
    print(f"- Latest JSON: {output_dir / 'loadtest-report-latest.json'}")
    print(f"- Latest HTML: {output_dir / 'loadtest-report-latest.html'}")

    return 0


def main() -> int:
    try:
        args = parse_args()
        return asyncio.run(main_async(args))
    except KeyboardInterrupt:
        print("\nExecucao interrompida pelo usuario.")
        return 130
    except Exception as exc:
        print(f"\nErro: {type(exc).__name__}: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

