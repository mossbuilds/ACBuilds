#!/usr/bin/env bash
# One-time root install of the ACBuilds self-heal timer. Idempotent - safe to re-run (e.g. after a rebuilt box).
# Run as root:   bash install-heal.sh [hopper-token]
# The hopper token is only written if given on stdin (see below) or as $1; without one, heal.sh's rung 6 (the
# hopper alert) logs "skipped" and does nothing until 'echo TOKEN > /opt/acbuilds/.hopper_token; chmod 600 ...' is
# done by hand.
set -euo pipefail
APP=/opt/acbuilds
RAW=https://raw.githubusercontent.com/mossbuilds/ACBuilds/main/deploy/vps

command -v systemctl >/dev/null 2>&1 || { echo "ERROR: systemctl not found"; exit 1; }
command -v visudo >/dev/null 2>&1 || { echo "ERROR: visudo not found"; exit 1; }
SYSTEMCTL_BIN=$(command -v systemctl)
REBOOT_BIN=$(command -v reboot || echo /usr/sbin/reboot)
JOURNALCTL_BIN=$(command -v journalctl)
[ -x "$SYSTEMCTL_BIN" ] || { echo "ERROR: systemctl not executable at $SYSTEMCTL_BIN"; exit 1; }
[ -x "$REBOOT_BIN" ] || { echo "ERROR: reboot not found/executable at $REBOOT_BIN (expected /usr/sbin/reboot)"; exit 1; }
[ -x "$JOURNALCTL_BIN" ] || { echo "ERROR: journalctl not executable at $JOURNALCTL_BIN"; exit 1; }
if [ "$SYSTEMCTL_BIN" != "/usr/bin/systemctl" ] || [ "$REBOOT_BIN" != "/usr/sbin/reboot" ] || [ "$JOURNALCTL_BIN" != "/usr/bin/journalctl" ]; then
  echo "ERROR: a binary path differs from the sudoers rule below - refusing to install a rule that would grant nothing."
  echo "  systemctl=$SYSTEMCTL_BIN reboot=$REBOOT_BIN journalctl=$JOURNALCTL_BIN"
  echo "  expected  systemctl=/usr/bin/systemctl reboot=/usr/sbin/reboot journalctl=/usr/bin/journalctl"
  exit 1
fi
id acbuilds >/dev/null 2>&1 || { echo "ERROR: user acbuilds does not exist - run bootstrap.sh first"; exit 1; }

echo "== fetching heal.sh and heal_lib.py"
curl -fsSL "$RAW/heal.sh" -o "$APP/heal.sh"
curl -fsSL "$RAW/heal_lib.py" -o "$APP/heal_lib.py"
chown acbuilds:acbuilds "$APP/heal.sh" "$APP/heal_lib.py"; chmod 755 "$APP/heal.sh"; chmod 644 "$APP/heal_lib.py"

echo "== installing acb-heal.service / acb-heal.timer"
cat > /etc/systemd/system/acb-heal.service <<UNIT
[Unit]
Description=ACBuilds self-heal check (one pass)
After=docker.service
Wants=docker.service

[Service]
Type=oneshot
User=acbuilds
WorkingDirectory=$APP
ExecStart=$APP/heal.sh
UNIT

cat > /etc/systemd/system/acb-heal.timer <<UNIT
[Unit]
Description=Run ACBuilds self-heal every 3 minutes

[Timer]
OnBootSec=3min
OnUnitActiveSec=3min
Unit=acb-heal.service

[Install]
WantedBy=timers.target
UNIT

systemctl daemon-reload
systemctl enable --now acb-heal.timer

echo "== sudoers: acbuilds may restart docker, reboot, and vacuum the journal (nothing else)"
SUDOERS_TMP=$(mktemp)
{
  echo "acbuilds ALL=(root) NOPASSWD: /usr/bin/systemctl restart docker"
  echo "acbuilds ALL=(root) NOPASSWD: /usr/sbin/reboot"
  echo "acbuilds ALL=(root) NOPASSWD: /usr/bin/journalctl --vacuum-size=200M"
} > "$SUDOERS_TMP"
visudo -cf "$SUDOERS_TMP"
install -o root -g root -m 440 "$SUDOERS_TMP" /etc/sudoers.d/acbuilds-heal
rm -f "$SUDOERS_TMP"

echo "== hopper alert token"
TOKEN="${1:-}"
if [ -z "$TOKEN" ] && [ ! -t 0 ]; then
  read -r TOKEN || true
fi
if [ -n "$TOKEN" ]; then
  printf '%s' "$TOKEN" > "$APP/.hopper_token"
  chown acbuilds:acbuilds "$APP/.hopper_token"; chmod 600 "$APP/.hopper_token"
  echo "wrote $APP/.hopper_token (600, owner acbuilds)"
else
  echo "no token given - rung 6 hopper alerts will log 'skipped' until $APP/.hopper_token exists (600, owner acbuilds)"
fi

echo "== done. acb-heal.timer runs heal.sh every 3 minutes as acbuilds. Maintenance switch: touch $APP/heal.disabled"
echo "   Check status any time with:  sudo -u acbuilds $APP/heal.sh --status"
