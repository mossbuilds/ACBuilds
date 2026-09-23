#!/usr/bin/env bash
# ACBuilds self-heal. Runs ON the VPS as 'acbuilds', fired every 3 minutes by acb-heal.timer (see install-heal.sh).
# One run = check the stack, then AT MOST ONE remediation action, then exit - the timer calls it again in 3 minutes,
# which gives each fix time to take effect before anything escalates further. All the check/ladder/state logic lives
# in heal_lib.py (stdlib python3, for the JSON state file); this script only handles the two things python must not
# do itself: the maintenance skip-switches, and the deploy-lock handoff around a rollback (deploy.sh takes the same
# lock, so this script must not still be holding it when deploy.sh runs).
#
#   heal.sh              one heal pass
#   heal.sh --dry-run    run the checks, print what action would be taken, change nothing
#   heal.sh --status     print the state file human-readably
set -euo pipefail
APP=/opt/acbuilds
LIB="$APP/heal_lib.py"
LOCK="$APP/.deploy.lock"
LOG="$APP/heal.log"

log() {
  echo "$(date -u +%FT%TZ) $*" >> "$LOG"
  if [ "$(wc -l < "$LOG" 2>/dev/null || echo 0)" -gt 5000 ]; then
    tail -n 5000 "$LOG" > "$LOG.trim" && mv "$LOG.trim" "$LOG"
  fi
}

DRY_RUN=0
case "${1:-}" in
  --status) exec python3 "$LIB" status ;;
  --dry-run) DRY_RUN=1 ;;
  "") ;;
  *) echo "usage: heal.sh [--dry-run|--status]"; exit 2 ;;
esac

if [ -e "$APP/heal.disabled" ]; then
  log "skipped: $APP/heal.disabled present (maintenance switch)"
  exit 0
fi

cd "$APP"
exec 9>"$LOCK"
if ! flock -n 9; then
  log "skipped: deploy lock held (a deploy is running)"
  exit 0
fi

LIB_ARGS=(run)
[ "$DRY_RUN" = 1 ] && LIB_ARGS+=(--dry-run)

set +e
OUT=$(python3 "$LIB" "${LIB_ARGS[@]}" 2>&1)
RC=$?
set -e

echo "$OUT"
log "$(echo "$OUT" | tail -1)"

if [ "$RC" -ne 42 ] && [ "$RC" -ne 0 ]; then
  log "heal_lib.py run exited $RC: $OUT"
  exit "$RC"
fi

if [ "$RC" -eq 42 ]; then
  VERSION=$(echo "$OUT" | grep '^ROLLBACK_NEEDED' | awk '{print $2}')
  if [ -z "$VERSION" ]; then
    log "ERROR: rollback requested but no version parsed from: $OUT"
    exit 1
  fi
  if [ "$DRY_RUN" = 1 ]; then
    log "dry-run: would run deploy.sh --rollback $VERSION here"
    exit 0
  fi
  # release the deploy lock: deploy.sh --rollback takes it itself, and heal.sh must not be holding it when that runs
  exec 9>&-
  log "rung 3: releasing lock, running deploy.sh --rollback $VERSION"
  if "$APP/deploy.sh" --rollback "$VERSION" >> "$LOG" 2>&1; then
    RESULT_FLAG=--ok
  else
    RESULT_FLAG=--fail
  fi
  # reacquire the lock (best-effort; another deploy may have started meanwhile) just to serialize the state update
  exec 9>"$LOCK"
  flock -n 9 || true
  python3 "$LIB" rollback-result "$RESULT_FLAG" --version "$VERSION" >> "$LOG" 2>&1 || true
fi

exit 0
