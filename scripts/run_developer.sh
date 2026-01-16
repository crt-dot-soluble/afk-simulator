#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${BASH_VERSION:-}" ]]; then
  exec bash "$0" "$@"
fi

NO_BROWSER="${NO_BROWSER:-0}"
SCRIPT_PATH="${BASH_SOURCE[0]}"
SCRIPT_DIR="$(cd "$(dirname "${SCRIPT_PATH}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

SERVER_PID=""
CLIENT_PID=""
POPPED=0

cleanup() {
  set +e
  if [[ -n "${CLIENT_PID}" ]] && kill -0 "${CLIENT_PID}" 2>/dev/null; then
    kill "${CLIENT_PID}" 2>/dev/null || true
    wait "${CLIENT_PID}" 2>/dev/null || true
  fi

  if [[ -n "${SERVER_PID}" ]] && kill -0 "${SERVER_PID}" 2>/dev/null; then
    kill "${SERVER_PID}" 2>/dev/null || true
    wait "${SERVER_PID}" 2>/dev/null || true
  fi

  if [[ ${POPPED} -eq 0 ]]; then
    popd >/dev/null 2>&1 || true
    POPPED=1
  fi
}

trap cleanup EXIT INT TERM

pushd "${REPO_ROOT}" >/dev/null

log() {
  printf '[run_developer] %s\n' "$1"
}

open_url() {
  local url="$1"
  if command -v xdg-open >/dev/null 2>&1; then
    xdg-open "${url}" >/dev/null 2>&1 || true
  elif command -v open >/dev/null 2>&1; then
    open "${url}" >/dev/null 2>&1 || true
  elif command -v wslview >/dev/null 2>&1; then
    wslview "${url}" >/dev/null 2>&1 || true
  elif command -v powershell.exe >/dev/null 2>&1; then
    powershell.exe -NoLogo -NoProfile -Command "Start-Process '${url}'" >/dev/null 2>&1 || true
  elif command -v cmd.exe >/dev/null 2>&1; then
    cmd.exe /c start "" "${url}" >/dev/null 2>&1 || true
  else
    if command -v python3 >/dev/null 2>&1; then
      python3 -c "import sys, webbrowser; webbrowser.open(sys.argv[1])" "${url}" >/dev/null 2>&1 || true
    elif command -v python >/dev/null 2>&1; then
      python -c "import sys, webbrowser; webbrowser.open(sys.argv[1])" "${url}" >/dev/null 2>&1 || true
    else
      printf 'Open %s\n' "${url}"
    fi
  fi
}

log "Launching Engine.Server (developer mode)..."
dotnet run --project "${REPO_ROOT}/src/Engine.Server" &
SERVER_PID=$!
sleep 3

log "Launching Engine.Client (Mission Control)..."
dotnet run --project "${REPO_ROOT}/src/Engine.Client" &
CLIENT_PID=$!
sleep 4

if [[ "${NO_BROWSER}" == "0" ]]; then
  log "Opening Mission Control surfaces..."
  open_url "https://localhost:7061/"
  open_url "https://localhost:7061/devtools"
fi

log "Press Ctrl+C to stop both hosts."
wait "${SERVER_PID}"
wait "${CLIENT_PID}"
