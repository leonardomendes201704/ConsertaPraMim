#!/usr/bin/env python3
"""
Build dos APKs Android (cliente e prestador) em um comando.

Uso basico:
    python scripts/build_apks.py

Exemplo com API explicita:
    python scripts/build_apks.py --api-base-url http://192.168.0.196:5193
"""

from __future__ import annotations

import argparse
import hashlib
import os
import shutil
import socket
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Iterable


@dataclass(frozen=True)
class AppConfig:
    name: str
    folder: str
    output_debug_name: str
    output_compat_name: str


APPS: tuple[AppConfig, ...] = (
    AppConfig(
        name="Cliente",
        folder="conserta-pra-mim app",
        output_debug_name="ConsertaPraMim-Cliente-debug.apk",
        output_compat_name="ConsertaPraMim-Cliente-compat.apk",
    ),
    AppConfig(
        name="Prestador",
        folder="conserta-pra-mim-provider app",
        output_debug_name="ConsertaPraMim-Prestador-debug.apk",
        output_compat_name="ConsertaPraMim-Prestador-compat.apk",
    ),
)


def repo_root() -> Path:
    return Path(__file__).resolve().parents[1]


def print_step(message: str) -> None:
    now = datetime.now().strftime("%H:%M:%S")
    print(f"[{now}] {message}")


def run_command(
    command: list[str], cwd: Path | None = None, env: dict[str, str] | None = None
) -> None:
    command_str = " ".join(command)
    print_step(f"Executando: {command_str}")
    subprocess.run(command, cwd=str(cwd) if cwd else None, check=True, env=env)


def detect_local_ip() -> str:
    # Descobre IP local de saida sem enviar trafego real.
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
        s.connect(("8.8.8.8", 80))
        return s.getsockname()[0]


def choose_sdk_root(explicit_sdk: str | None) -> Path:
    candidates: list[Path] = []
    if explicit_sdk:
        candidates.append(Path(explicit_sdk))

    # Prioriza SDK local do usuario (normalmente gravavel e sem problema de permissao).
    candidates.extend(
        [
            Path.home() / "AndroidSdk",
            Path.home() / "AppData" / "Local" / "Android" / "Sdk",
        ]
    )

    # Variaveis de ambiente sao consideradas, mas com menor prioridade que SDK local.
    for env_var in ("ANDROID_SDK_ROOT", "ANDROID_HOME"):
        env_value = os.environ.get(env_var)
        if env_value:
            candidates.append(Path(env_value))

    candidates.append(Path(r"C:\Program Files (x86)\Android\android-sdk"))

    for candidate in candidates:
        if (candidate / "platform-tools").exists() and (candidate / "build-tools").exists():
            return candidate
    raise FileNotFoundError(
        "Nao foi possivel localizar Android SDK. "
        "Informe com --sdk-root ou configure ANDROID_SDK_ROOT."
    )


def escape_local_properties_path(path: Path) -> str:
    text = str(path)
    text = text.replace("\\", r"\\")
    text = text.replace(":", r"\:")
    return text


def write_android_env(app_dir: Path, api_base_url: str) -> None:
    env_file = app_dir / ".env.android"
    env_file.write_text(f"VITE_API_BASE_URL={api_base_url}\n", encoding="ascii")


def write_local_properties(app_dir: Path, sdk_root: Path) -> None:
    local_properties = app_dir / "android" / "local.properties"
    local_properties.write_text(
        f"sdk.dir={escape_local_properties_path(sdk_root)}\n",
        encoding="ascii",
    )


def build_android_debug(app_dir: Path, env: dict[str, str]) -> None:
    run_command(["npm.cmd", "run", "android:apk:debug"], cwd=app_dir, env=env)


def sha256(file_path: Path) -> str:
    digest = hashlib.sha256()
    with file_path.open("rb") as fp:
        for chunk in iter(lambda: fp.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def parse_version(name: str) -> tuple[int, ...]:
    parts: list[int] = []
    for token in name.split("."):
        try:
            parts.append(int(token))
        except ValueError:
            parts.append(0)
    return tuple(parts)


def latest_build_tools_dir(sdk_root: Path) -> Path:
    build_tools_root = sdk_root / "build-tools"
    if not build_tools_root.exists():
        raise FileNotFoundError(
            f"Pasta de build-tools nao encontrada em: {build_tools_root}"
        )
    candidates = [d for d in build_tools_root.iterdir() if d.is_dir()]
    if not candidates:
        raise FileNotFoundError(
            f"Nenhuma versao de build-tools encontrada em: {build_tools_root}"
        )
    return sorted(candidates, key=lambda d: parse_version(d.name), reverse=True)[0]


def maybe_remove(path: Path) -> None:
    if path.exists():
        path.unlink()


def ensure_debug_keystore() -> Path:
    keystore = Path.home() / ".android" / "debug.keystore"
    if not keystore.exists():
        raise FileNotFoundError(
            f"Debug keystore nao encontrado em: {keystore}. "
            "Abra o Android Studio uma vez para gerar automaticamente."
        )
    return keystore


def create_compat_apk(
    build_tools: Path, debug_keystore: Path, source_apk: Path, compat_apk: Path
) -> None:
    zipalign = build_tools / "zipalign.exe"
    apksigner = build_tools / "apksigner.bat"
    if not zipalign.exists() or not apksigner.exists():
        raise FileNotFoundError(
            f"zipalign/apksigner nao encontrados em: {build_tools}. "
            "Verifique a instalacao do Android SDK Build-Tools."
        )

    aligned_apk = compat_apk.with_name(compat_apk.stem + "-aligned.apk")
    maybe_remove(aligned_apk)
    maybe_remove(compat_apk)

    run_command(
        [
            str(zipalign),
            "-f",
            "-p",
            "4",
            str(source_apk),
            str(aligned_apk),
        ]
    )
    run_command(
        [
            str(apksigner),
            "sign",
            "--ks",
            str(debug_keystore),
            "--ks-key-alias",
            "androiddebugkey",
            "--ks-pass",
            "pass:android",
            "--key-pass",
            "pass:android",
            "--v1-signing-enabled",
            "true",
            "--v2-signing-enabled",
            "true",
            "--out",
            str(compat_apk),
            str(aligned_apk),
        ]
    )
    run_command([str(apksigner), "verify", "--verbose", str(compat_apk)])
    maybe_remove(aligned_apk)


def copy_file(source: Path, target: Path) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)


def write_checksums(output_dir: Path, files: Iterable[Path], api_base_url: str) -> None:
    lines = [
        f"Gerado em: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"API_BASE_URL: {api_base_url}",
        "",
    ]
    for file_path in files:
        lines.append(f"{file_path.name} SHA256: {sha256(file_path)}")
    (output_dir / "SHA256.txt").write_text("\n".join(lines) + "\n", encoding="ascii")


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Build automatizado dos APKs ConsertaPraMim (cliente e prestador)."
    )
    parser.add_argument(
        "--api-base-url",
        dest="api_base_url",
        default=None,
        help=(
            "Base da API para compilar os apps (ex.: http://192.168.0.196:5193). "
            "Se omitido, usa http://<IP_LOCAL>:5193."
        ),
    )
    parser.add_argument(
        "--sdk-root",
        dest="sdk_root",
        default=None,
        help="Caminho do Android SDK (opcional).",
    )
    parser.add_argument(
        "--output-dir",
        dest="output_dir",
        default=None,
        help="Pasta de saida dos APKs (padrao: <repo>/apk-output).",
    )
    args = parser.parse_args()

    root = repo_root()
    output_dir = Path(args.output_dir) if args.output_dir else (root / "apk-output")
    output_dir.mkdir(parents=True, exist_ok=True)

    local_ip = detect_local_ip()
    api_base_url = args.api_base_url or f"http://{local_ip}:5193"
    sdk_root = choose_sdk_root(args.sdk_root)
    build_tools = latest_build_tools_dir(sdk_root)
    debug_keystore = ensure_debug_keystore()
    build_env = os.environ.copy()
    build_env["ANDROID_SDK_ROOT"] = str(sdk_root)
    build_env["ANDROID_HOME"] = str(sdk_root)

    print_step(f"Repo: {root}")
    print_step(f"SDK: {sdk_root}")
    print_step(f"Build-tools: {build_tools.name}")
    print_step(f"API base URL: {api_base_url}")
    print_step(f"Saida: {output_dir}")

    generated_files: list[Path] = []
    for app in APPS:
        app_dir = root / app.folder
        debug_source = app_dir / "android" / "app" / "build" / "outputs" / "apk" / "debug" / "app-debug.apk"
        debug_target = output_dir / app.output_debug_name
        compat_target = output_dir / app.output_compat_name

        print_step(f"--- {app.name}: preparando build ---")
        write_android_env(app_dir, api_base_url)
        write_local_properties(app_dir, sdk_root)
        build_android_debug(app_dir, build_env)

        if not debug_source.exists():
            raise FileNotFoundError(f"APK debug nao encontrado: {debug_source}")

        print_step(f"--- {app.name}: copiando debug APK ---")
        copy_file(debug_source, debug_target)
        generated_files.append(debug_target)

        print_step(f"--- {app.name}: gerando APK compat ---")
        create_compat_apk(build_tools, debug_keystore, debug_target, compat_target)
        generated_files.append(compat_target)

    write_checksums(output_dir, generated_files, api_base_url)

    print_step("Build finalizado com sucesso.")
    print_step("Arquivos gerados:")
    for file_path in generated_files:
        print(f"  - {file_path}")
    print(f"  - {output_dir / 'SHA256.txt'}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except subprocess.CalledProcessError as error:
        print_step(f"Falha no comando (exit={error.returncode}): {error.cmd}")
        raise
    except Exception as error:  # noqa: BLE001
        print_step(f"Erro: {error}")
        raise
