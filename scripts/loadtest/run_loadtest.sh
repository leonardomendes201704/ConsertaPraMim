#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUNNER="$SCRIPT_DIR/loadtest_runner.py"
CONFIG="$SCRIPT_DIR/loadtest.config.json"
OUTPUT_DIR="$SCRIPT_DIR/output"

SCENARIO="${1:-smoke}"

python "$RUNNER" --config "$CONFIG" --scenario "$SCENARIO" --output-dir "$OUTPUT_DIR"

