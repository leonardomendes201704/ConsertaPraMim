#!/usr/bin/env python3
"""Dashboard live para execucao e monitoramento de load test."""

from __future__ import annotations

import json
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path
from typing import Any

import streamlit as st

try:
    from streamlit_autorefresh import st_autorefresh
except Exception:  # pragma: no cover
    st_autorefresh = None


SCRIPT_DIR = Path(__file__).resolve().parent
RUNNER_PATH = SCRIPT_DIR / "loadtest_runner.py"
CONFIG_PATH = SCRIPT_DIR / "loadtest.config.json"
OUTPUT_DIR = SCRIPT_DIR / "output"


def load_config() -> dict[str, Any]:
    if not CONFIG_PATH.exists():
        return {}
    try:
        return json.loads(CONFIG_PATH.read_text(encoding="utf-8-sig"))
    except ValueError:
        return {}


def load_live_state(path: Path | None) -> dict[str, Any]:
    if path is None or not path.exists():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except ValueError:
        return {}


def tail_text(path: Path | None, max_lines: int = 120) -> str:
    if path is None or not path.exists():
        return ""

    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    return "\n".join(lines[-max_lines:])


def build_runner_command(values: dict[str, Any], state_file: Path) -> list[str]:
    command = [
        sys.executable,
        str(RUNNER_PATH),
        "--config",
        str(CONFIG_PATH),
        "--scenario",
        values["scenario"],
        "--output-dir",
        str(OUTPUT_DIR),
        "--live-state-file",
        str(state_file),
        "--live-refresh-seconds",
        str(values["live_refresh_seconds"]),
    ]

    if values.get("base_url"):
        command.extend(["--base-url", str(values["base_url"]).strip()])
    if int(values.get("vus") or 0) > 0:
        command.extend(["--vus", str(int(values["vus"]))])
    if int(values.get("duration") or 0) > 0:
        command.extend(["--duration", str(int(values["duration"]))])
    if float(values.get("ramp_up") or 0) > 0:
        command.extend(["--ramp-up", str(float(values["ramp_up"]))])
    if int(values.get("think_min") or 0) >= 0:
        command.extend(["--think-min", str(int(values["think_min"]))])
    if int(values.get("think_max") or 0) >= 0:
        command.extend(["--think-max", str(int(values["think_max"]))])
    if float(values.get("timeout") or 0) > 0:
        command.extend(["--timeout", str(float(values["timeout"]))])
    if int(values.get("seed") or 0) > 0:
        command.extend(["--seed", str(int(values["seed"]))])
    if values.get("auth_password"):
        command.extend(["--auth-password", values["auth_password"]])
    if values.get("insecure"):
        command.append("--insecure")
    if values.get("publish_admin"):
        command.append("--publish-admin")
    if values.get("publish_token"):
        command.extend(["--publish-token", values["publish_token"]])
    if values.get("publish_email"):
        command.extend(["--publish-email", values["publish_email"]])
    if values.get("publish_password"):
        command.extend(["--publish-password", values["publish_password"]])

    return command


def ensure_session_defaults() -> None:
    st.session_state.setdefault("runner_process", None)
    st.session_state.setdefault("runner_pid", None)
    st.session_state.setdefault("state_file", None)
    st.session_state.setdefault("log_file", None)
    st.session_state.setdefault("command", [])
    st.session_state.setdefault("run_started_at", None)


def is_process_running() -> bool:
    process = st.session_state.get("runner_process")
    if process is None:
        return False
    return process.poll() is None


def stop_process() -> None:
    process = st.session_state.get("runner_process")
    if process is None:
        return

    if process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()

    st.session_state["runner_process"] = None
    st.session_state["runner_pid"] = None


def start_process(command: list[str], state_file: Path, log_file: Path) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    with log_file.open("w", encoding="utf-8") as log_handle:
        process = subprocess.Popen(  # noqa: S603
            command,
            cwd=str(SCRIPT_DIR),
            stdout=log_handle,
            stderr=subprocess.STDOUT,
        )

    st.session_state["runner_process"] = process
    st.session_state["runner_pid"] = process.pid
    st.session_state["state_file"] = str(state_file)
    st.session_state["log_file"] = str(log_file)
    st.session_state["command"] = command
    st.session_state["run_started_at"] = datetime.now().isoformat(timespec="seconds")


def scenario_defaults(config: dict[str, Any], scenario: str) -> dict[str, Any]:
    scenarios = config.get("scenarios") if isinstance(config.get("scenarios"), dict) else {}
    values = scenarios.get(scenario) if isinstance(scenarios.get(scenario), dict) else {}
    return {
        "vus": int(values.get("vus", 20)),
        "duration": int(values.get("durationSeconds", 60)),
        "ramp_up": float(values.get("rampUpSeconds", 5)),
        "think_min": int(values.get("thinkTimeMinMs", 150)),
        "think_max": int(values.get("thinkTimeMaxMs", 800)),
    }


def render_metrics(state: dict[str, Any]) -> None:
    run = state.get("run", {}) if isinstance(state.get("run"), dict) else {}
    summary = state.get("summary", {}) if isinstance(state.get("summary"), dict) else {}
    latency = state.get("latencyMs", {}) if isinstance(state.get("latencyMs"), dict) else {}

    col1, col2, col3, col4, col5 = st.columns(5)
    col1.metric("Total requests", summary.get("totalRequests", 0))
    col2.metric("RPS atual", summary.get("rpsCurrent", 0))
    col3.metric("RPS medio", summary.get("rpsAvg", 0))
    col4.metric("p95 (ms)", latency.get("p95", 0))
    col5.metric("Erros (%)", summary.get("errorRatePercent", 0))

    progress = float(run.get("progressPercent", 0.0) or 0.0)
    st.progress(min(max(progress / 100.0, 0.0), 1.0), text=f"Progresso: {progress:.2f}%")


def render_timeseries(state: dict[str, Any]) -> None:
    timeseries = state.get("timeseries", {}) if isinstance(state.get("timeseries"), dict) else {}
    per_second = timeseries.get("requestsPerSecond", [])
    if not isinstance(per_second, list) or not per_second:
        st.info("Aguardando dados de throughput...")
        return

    rows = []
    for item in per_second:
        if isinstance(item, dict):
            rows.append({
                "Segundo": int(item.get("second", 0)),
                "RPS": int(item.get("requests", 0)),
            })

    st.line_chart(rows, x="Segundo", y="RPS", use_container_width=True)


def render_tables(state: dict[str, Any]) -> None:
    status_codes = state.get("statusCodes", []) if isinstance(state.get("statusCodes"), list) else []
    top_hits = state.get("topEndpointsByHits", []) if isinstance(state.get("topEndpointsByHits"), list) else []
    top_p95 = state.get("topEndpointsByP95", []) if isinstance(state.get("topEndpointsByP95"), list) else []

    col_left, col_right = st.columns(2)

    with col_left:
        st.subheader("Status codes")
        st.dataframe(status_codes[:12], use_container_width=True, hide_index=True)

        st.subheader("Top endpoints por hits")
        st.dataframe(top_hits[:12], use_container_width=True, hide_index=True)

    with col_right:
        st.subheader("Top endpoints por p95")
        st.dataframe(top_p95[:12], use_container_width=True, hide_index=True)


def render_artifacts(state: dict[str, Any]) -> None:
    artifacts = state.get("artifacts", {}) if isinstance(state.get("artifacts"), dict) else {}
    if not artifacts:
        st.caption("Artifacts ainda nao disponiveis.")
        return

    st.subheader("Relatorios gerados")
    st.code("\n".join([f"{key}: {value}" for key, value in artifacts.items()]))


def main() -> None:
    st.set_page_config(page_title="CPM Load Test Live", layout="wide")
    ensure_session_defaults()

    config = load_config()
    scenario_names = sorted((config.get("scenarios") or {}).keys()) if isinstance(config.get("scenarios"), dict) else ["smoke"]
    scenario = st.sidebar.selectbox("Scenario", scenario_names, index=0)
    defaults = scenario_defaults(config, scenario)

    st.sidebar.markdown("### Parametros")
    base_url = st.sidebar.text_input("Base URL override", value=str(config.get("baseUrl") or ""))
    vus = st.sidebar.number_input("VUs", min_value=1, value=int(defaults["vus"]))
    duration = st.sidebar.number_input("Duration (s)", min_value=1, value=int(defaults["duration"]))
    ramp_up = st.sidebar.number_input("Ramp-up (s)", min_value=0.0, value=float(defaults["ramp_up"]))
    think_min = st.sidebar.number_input("Think min (ms)", min_value=0, value=int(defaults["think_min"]))
    think_max = st.sidebar.number_input("Think max (ms)", min_value=int(think_min), value=int(max(defaults["think_max"], think_min)))
    timeout = st.sidebar.number_input("Timeout (s)", min_value=1.0, value=20.0)
    seed = st.sidebar.number_input("Seed", min_value=1, value=42)
    live_refresh_seconds = st.sidebar.number_input("Live refresh (s)", min_value=0.3, value=1.0, step=0.1)

    insecure = st.sidebar.checkbox("Insecure TLS", value=False)
    auth_password = st.sidebar.text_input("Auth password override", value="", type="password")

    st.sidebar.markdown("### Publicacao admin (opcional)")
    publish_admin = st.sidebar.checkbox("Publicar no admin", value=False)
    publish_token = st.sidebar.text_input("Publish token", value="", type="password")
    publish_email = st.sidebar.text_input("Publish email", value="")
    publish_password = st.sidebar.text_input("Publish password", value="", type="password")

    values = {
        "scenario": scenario,
        "base_url": base_url,
        "vus": int(vus),
        "duration": int(duration),
        "ramp_up": float(ramp_up),
        "think_min": int(think_min),
        "think_max": int(think_max),
        "timeout": float(timeout),
        "seed": int(seed),
        "live_refresh_seconds": float(live_refresh_seconds),
        "insecure": insecure,
        "auth_password": auth_password,
        "publish_admin": publish_admin,
        "publish_token": publish_token,
        "publish_email": publish_email,
        "publish_password": publish_password,
    }

    st.title("Load Test Live Dashboard")
    st.caption("Inicie um cenario e acompanhe throughput, latencia, erros e progresso em tempo real.")

    running = is_process_running()

    action_col1, action_col2 = st.columns([1, 1])
    with action_col1:
        if st.button("Iniciar run", type="primary", disabled=running):
            session_id = datetime.now().strftime("%Y%m%d-%H%M%S")
            state_file = OUTPUT_DIR / f"loadtest-live-{session_id}.json"
            log_file = OUTPUT_DIR / f"loadtest-live-{session_id}.log"
            command = build_runner_command(values, state_file)
            start_process(command, state_file, log_file)
            st.rerun()

    with action_col2:
        if st.button("Parar run", disabled=not running):
            stop_process()
            st.rerun()

    process = st.session_state.get("runner_process")
    process_status = "idle"
    process_exit_code = None
    if process is not None:
        process_exit_code = process.poll()
        if process_exit_code is None:
            process_status = "running"
        elif process_exit_code == 0:
            process_status = "success"
        else:
            process_status = f"error ({process_exit_code})"

    st.write(f"Status do processo: **{process_status}**")
    if st.session_state.get("runner_pid"):
        st.caption(f"PID: {st.session_state['runner_pid']} | Started: {st.session_state.get('run_started_at')}")

    if st.session_state.get("command"):
        st.code(" ".join(st.session_state["command"]))

    state_file_value = st.session_state.get("state_file")
    state_file = Path(state_file_value) if state_file_value else None
    state = load_live_state(state_file)

    if running and st_autorefresh is not None:
        st_autorefresh(interval=int(max(live_refresh_seconds, 0.3) * 1000), key="live-refresh")

    if state:
        render_metrics(state)
        st.subheader("Throughput")
        render_timeseries(state)
        render_tables(state)
        render_artifacts(state)
    else:
        st.info("Nenhum snapshot live disponivel ainda.")

    st.subheader("Log do runner")
    log_file_value = st.session_state.get("log_file")
    log_text = tail_text(Path(log_file_value) if log_file_value else None)
    st.text(log_text or "Sem logs no momento.")

    if not running and process is not None and process.poll() is not None:
        st.caption("Execucao finalizada. Voce pode iniciar um novo run.")


if __name__ == "__main__":
    main()
