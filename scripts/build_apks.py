#!/usr/bin/env python3
"""
Build dos APKs Android (cliente, prestador e admin) em um comando.

Uso basico:
    python scripts/build_apks.py

Exemplo com API explicita (apenas para confirmar o mesmo valor padrao):
    python scripts/build_apks.py --api-base-url http://187.77.48.150:5193

Exemplo com publicacao automatica por SFTP:
    python scripts/build_apks.py --api-base-url http://187.77.48.150:5193 ^
        --publish-sftp-host 187.77.48.150 --publish-sftp-user root --publish-sftp-dir /var/www/apks ^
        --publish-sftp-key C:/Users/devcr/.ssh/my-repository
"""

from __future__ import annotations

import argparse
import hashlib
import os
import shlex
import shutil
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Iterable

VPS_API_BASE_URL = "http://187.77.48.150:5193"


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
    AppConfig(
        name="Admin",
        folder="conserta-pra-mim-admin app",
        output_debug_name="ConsertaPraMim-Admin-debug.apk",
        output_compat_name="ConsertaPraMim-Admin-compat.apk",
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


def build_ssh_args(
    port: int,
    key_path: str | None,
    insecure: bool,
) -> tuple[list[str], list[str]]:
    ssh_base = ["ssh", "-p", str(port)]
    scp_base = ["scp", "-P", str(port)]

    if key_path:
        ssh_base.extend(["-i", key_path])
        scp_base.extend(["-i", key_path])

    if insecure:
        insecure_options = ["-o", "StrictHostKeyChecking=no", "-o", "UserKnownHostsFile=/dev/null"]
        ssh_base.extend(insecure_options)
        scp_base.extend(insecure_options)

    return ssh_base, scp_base


def publish_files_via_sftp(
    files: list[Path],
    host: str,
    user: str,
    remote_dir: str,
    port: int,
    key_path: str | None,
    insecure: bool,
) -> None:
    normalized_remote_dir = remote_dir.strip()
    if not normalized_remote_dir:
        raise ValueError("Diretorio remoto SFTP vazio.")

    ssh_base, scp_base = build_ssh_args(port=port, key_path=key_path, insecure=insecure)
    target = f"{user}@{host}"

    print_step(f"Preparando pasta remota: {normalized_remote_dir}")
    run_command(ssh_base + [target, f"mkdir -p {shlex.quote(normalized_remote_dir)}"])

    destination = f"{target}:{normalized_remote_dir.rstrip('/')}/"
    for file_path in files:
        print_step(f"Publicando: {file_path.name}")
        run_command(scp_base + [str(file_path), destination])


def normalize_base_url(value: str) -> str:
    return value.strip().rstrip("/")


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


def locate_keytool() -> str:
    keytool_cmd = "keytool.exe" if os.name == "nt" else "keytool"
    resolved = shutil.which(keytool_cmd) or shutil.which("keytool")
    if resolved:
        return resolved

    java_home = os.environ.get("JAVA_HOME")
    if java_home:
        candidate = Path(java_home) / "bin" / keytool_cmd
        if candidate.exists():
            return str(candidate)

    raise FileNotFoundError(
        "Nao foi possivel localizar o keytool (JDK). "
        "No CI, adicione actions/setup-java antes do build dos APKs."
    )


def create_debug_keystore(keystore: Path) -> None:
    keytool = locate_keytool()
    keystore.parent.mkdir(parents=True, exist_ok=True)
    maybe_remove(keystore)

    run_command(
        [
            keytool,
            "-genkeypair",
            "-v",
            "-keystore",
            str(keystore),
            "-storepass",
            "android",
            "-alias",
            "androiddebugkey",
            "-keypass",
            "android",
            "-keyalg",
            "RSA",
            "-keysize",
            "2048",
            "-validity",
            "10000",
            "-dname",
            "CN=Android Debug,O=Android,C=US",
        ]
    )


def ensure_debug_keystore() -> Path:
    keystore = Path.home() / ".android" / "debug.keystore"
    if not keystore.exists():
        print_step(
            f"Debug keystore nao encontrado em: {keystore}. "
            "Gerando automaticamente..."
        )
        create_debug_keystore(keystore)

    if not keystore.exists():
        raise FileNotFoundError(
            f"Falha ao gerar debug keystore em: {keystore}."
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


def write_checksums(output_dir: Path, files: Iterable[Path], api_base_url: str) -> Path:
    lines = [
        f"Gerado em: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"API_BASE_URL: {api_base_url}",
        "",
    ]
    for file_path in files:
        lines.append(f"{file_path.name} SHA256: {sha256(file_path)}")
    checksum_file = output_dir / "SHA256.txt"
    checksum_file.write_text("\n".join(lines) + "\n", encoding="ascii")
    return checksum_file


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Build automatizado dos APKs ConsertaPraMim (cliente, prestador e admin)."
    )
    parser.add_argument(
        "--api-base-url",
        dest="api_base_url",
        default=None,
        help=(
            "Mantido por compatibilidade. Este script compila sempre com a VPS "
            f"({VPS_API_BASE_URL})."
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
    parser.add_argument(
        "--publish-sftp-host",
        dest="publish_sftp_host",
        default=None,
        help="Host da VPS para publicar os APKs via SFTP (ex.: 187.77.48.150).",
    )
    parser.add_argument(
        "--publish-sftp-port",
        dest="publish_sftp_port",
        type=int,
        default=22,
        help="Porta do SSH/SFTP (padrao: 22).",
    )
    parser.add_argument(
        "--publish-sftp-user",
        dest="publish_sftp_user",
        default=None,
        help="Usuario SSH/SFTP da VPS (ex.: root).",
    )
    parser.add_argument(
        "--publish-sftp-dir",
        dest="publish_sftp_dir",
        default=None,
        help="Diretorio remoto para publicar os APKs (ex.: /var/www/apks).",
    )
    parser.add_argument(
        "--publish-sftp-key",
        dest="publish_sftp_key",
        default=None,
        help="Caminho da chave privada SSH para publicar (opcional).",
    )
    parser.add_argument(
        "--publish-sftp-insecure",
        dest="publish_sftp_insecure",
        action="store_true",
        help="Desabilita validacao de host key (nao recomendado, apenas setup inicial).",
    )
    args = parser.parse_args()

    root = repo_root()
    output_dir = Path(args.output_dir) if args.output_dir else (root / "apk-output")
    output_dir.mkdir(parents=True, exist_ok=True)

    requested_api_base_url = normalize_base_url(args.api_base_url) if args.api_base_url else None
    if requested_api_base_url and requested_api_base_url != normalize_base_url(VPS_API_BASE_URL):
        raise ValueError(
            f"--api-base-url invalido ({requested_api_base_url}). "
            f"Use obrigatoriamente {VPS_API_BASE_URL}."
        )

    api_base_url = VPS_API_BASE_URL
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

    checksum_file = write_checksums(output_dir, generated_files, api_base_url)
    publish_files = [*generated_files, checksum_file]

    if args.publish_sftp_host:
        if not args.publish_sftp_user:
            raise ValueError("Use --publish-sftp-user junto com --publish-sftp-host.")
        if not args.publish_sftp_dir:
            raise ValueError("Use --publish-sftp-dir junto com --publish-sftp-host.")

        print_step(
            "Publicacao SFTP habilitada: "
            f"{args.publish_sftp_user}@{args.publish_sftp_host}:{args.publish_sftp_dir}"
        )
        publish_files_via_sftp(
            files=publish_files,
            host=args.publish_sftp_host,
            user=args.publish_sftp_user,
            remote_dir=args.publish_sftp_dir,
            port=args.publish_sftp_port,
            key_path=args.publish_sftp_key,
            insecure=args.publish_sftp_insecure,
        )
        print_step("Publicacao SFTP concluida.")

    print_step("Build finalizado com sucesso.")
    print_step("Arquivos gerados:")
    for file_path in generated_files:
        print(f"  - {file_path}")
    print(f"  - {checksum_file}")
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
