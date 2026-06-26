#!/usr/bin/env bash
# XIX_DL_BOT installer — interactive setup + Docker build/run.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/<you>/XIX-DL-BOT/main/install.sh | bash
#   ./install.sh                          # interactive
#   ./install.sh --non-interactive        # use existing .env without prompting
#   ./install.sh --no-start               # set up but do not start the container
#
set -euo pipefail

# --------- colors ---------
if [ -t 1 ]; then
  C_BOLD=$'\033[1m'; C_DIM=$'\033[2m'; C_GREEN=$'\033[32m'; C_RED=$'\033[31m'
  C_YEL=$'\033[33m'; C_CYAN=$'\033[36m'; C_RESET=$'\033[0m'
else
  C_BOLD=""; C_DIM=""; C_GREEN=""; C_RED=""; C_YEL=""; C_CYAN=""; C_RESET=""
fi

log()  { printf "${C_CYAN}==>${C_RESET} %s\n" "$*"; }
ok()   { printf "${C_GREEN}✓${C_RESET}  %s\n" "$*"; }
warn() { printf "${C_YEL}!${C_RESET}  %s\n" "$*"; }
err()  { printf "${C_RED}✗${C_RESET}  %s\n" "$*" >&2; }

# --------- args ---------
INTERACTIVE=1
DO_START=1
REPO_URL="${REPO_URL:-https://github.com/REPLACE_ME/XIX-DL-BOT.git}"

while [ "${1:-}" != "" ]; do
  case "$1" in
    --non-interactive) INTERACTIVE=0 ;;
    --no-start)        DO_START=0 ;;
    --repo)            shift; REPO_URL="$1" ;;
    -h|--help)
      sed -n '2,8p' "$0" | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) err "Unknown arg: $1"; exit 1 ;;
  esac
  shift
done

# --------- prerequisites ---------
need() { command -v "$1" >/dev/null 2>&1 || { err "Missing dependency: $1"; exit 1; }; }

log "Checking prerequisites…"
need git
need docker
if docker compose version >/dev/null 2>&1; then
  DC="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
  DC="docker-compose"
else
  err "Docker Compose plugin not found. Install Docker Desktop or the compose plugin."
  exit 1
fi
ok "git, docker, $DC present"

# --------- clone or update ---------
TARGET_DIR="${TARGET_DIR:-XIX-DL-BOT}"
if [ -d "$TARGET_DIR/.git" ]; then
  log "Repo already cloned at ./$TARGET_DIR — pulling latest"
  ( cd "$TARGET_DIR" && git pull --ff-only )
elif [ -f "./XIX-DL-BOT.sln" ]; then
  TARGET_DIR="."
  log "Running from inside the repo"
else
  log "Cloning $REPO_URL → ./$TARGET_DIR"
  git clone "$REPO_URL" "$TARGET_DIR"
fi
cd "$TARGET_DIR"

# --------- prompts ---------
ENV_FILE=".env"
[ -f "$ENV_FILE" ] || cp .env.example "$ENV_FILE"

read_existing() {
  # read VALUE for KEY=VALUE from .env (returns empty if missing/empty)
  local key="$1"
  grep -E "^${key}=" "$ENV_FILE" 2>/dev/null | tail -1 | cut -d= -f2- || true
}

set_env() {
  local key="$1" val="$2"
  # Escape & for sed
  local safe; safe=$(printf '%s' "$val" | sed -e 's/[\/&]/\\&/g')
  if grep -qE "^${key}=" "$ENV_FILE"; then
    sed -i.bak -E "s/^${key}=.*/${key}=${safe}/" "$ENV_FILE" && rm -f "${ENV_FILE}.bak"
  else
    printf '%s=%s\n' "$key" "$val" >> "$ENV_FILE"
  fi
}

ask() {
  # ask "Label" KEY [default] [validator-regex]
  local label="$1" key="$2" def="${3:-}" re="${4:-}"
  local current; current="$(read_existing "$key")"
  local placeholder="${current:-$def}"
  local input=""
  while true; do
    if [ -n "$placeholder" ]; then
      printf "${C_BOLD}%s${C_RESET} ${C_DIM}[%s]${C_RESET}: " "$label" "$placeholder"
    else
      printf "${C_BOLD}%s${C_RESET}: " "$label"
    fi
    IFS= read -r input || true
    [ -z "$input" ] && input="$placeholder"
    if [ -n "$re" ] && ! [[ "$input" =~ $re ]]; then
      err "Invalid value. Expected pattern: $re"
      continue
    fi
    if [ -z "$input" ]; then
      err "Value required."
      continue
    fi
    set_env "$key" "$input"
    return 0
  done
}

ask_choice() {
  # ask_choice "Label" KEY "opt1 opt2 opt3" default
  local label="$1" key="$2" opts="$3" def="${4:-}"
  local current; current="$(read_existing "$key")"
  local placeholder="${current:-$def}"
  while true; do
    printf "${C_BOLD}%s${C_RESET} (%s) ${C_DIM}[%s]${C_RESET}: " "$label" "$opts" "$placeholder"
    local input=""; IFS= read -r input || true
    [ -z "$input" ] && input="$placeholder"
    for o in $opts; do
      if [ "$o" = "$input" ]; then set_env "$key" "$input"; return 0; fi
    done
    err "Pick one of: $opts"
  done
}

if [ "$INTERACTIVE" = "1" ]; then
  echo
  printf "${C_BOLD}━━━ XIX_DL_BOT installer ━━━${C_RESET}\n"
  printf "${C_DIM}Press Enter to keep the value shown in brackets.${C_RESET}\n\n"

  ask         "Bot token (from @BotFather)"      "BOT_TOKEN"            ""                       '^[0-9]+:[A-Za-z0-9_-]+$'
  ask         "Bot username (no @)"              "BOT_USERNAME"         "XIX_DL_BOT"             '^[A-Za-z0-9_]{5,32}$'
  ask         "Superadmin Telegram user id"      "BOT_SUPERADMIN_TG_ID" ""                       '^[0-9]+$'
  ask         "Storage Channel chat id (-100…)"  "BOT_STORAGE_CHANNEL_ID" ""                     '^-100[0-9]+$'
  ask_choice  "Update mode"                      "BOT_UPDATE_MODE"      "polling webhook"        "polling"

  if [ "$(read_existing BOT_UPDATE_MODE)" = "webhook" ]; then
    ask "Webhook public URL"   "BOT_WEBHOOK_URL"    "" '^https://.+'
    ask "Webhook secret token" "BOT_WEBHOOK_SECRET" "" '^[A-Za-z0-9_-]{8,}$'
    ask "Webhook listen addr"  "BOT_WEBHOOK_LISTEN" "http://0.0.0.0:8080" '^https?://.+'
  fi

  # Inside the container these MUST point at /app paths.
  set_env BOT_DB_PATH                "/app/data/bot.db"
  set_env BOT_BACKUP_DIR             "/app/backups"
  set_env BOT_LOG_DIR                "/app/logs"
  set_env BOT_WELCOME_MESSAGE_PATH   "/app/welcome.txt"

  echo
  ok "Wrote $ENV_FILE"
else
  log "Non-interactive mode: using existing $ENV_FILE"
fi

# --------- build & start ---------
log "Building Docker image…"
$DC build

if [ "$DO_START" = "1" ]; then
  log "Starting container…"
  $DC up -d
  echo
  ok "Bot is running."
  printf "  ${C_DIM}logs:   ${C_RESET}%s logs -f bot\n" "$DC"
  printf "  ${C_DIM}stop:   ${C_RESET}%s down\n" "$DC"
  printf "  ${C_DIM}status: ${C_RESET}%s ps\n" "$DC"
  echo
  printf "Next: open ${C_BOLD}https://t.me/%s${C_RESET} and send ${C_BOLD}/admin${C_RESET}.\n" "$(read_existing BOT_USERNAME)"
else
  ok "Setup complete. Start later with: $DC up -d"
fi
