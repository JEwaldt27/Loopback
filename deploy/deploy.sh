#!/usr/bin/env bash
# Server-side deploy for Loopback: install a staged publish build and restart
# the service. Safe to re-run. Configure via env vars (defaults shown).
#
#   APP_DIR   where the app is installed   (default: $HOME/lineflowapp)
#   STAGING   where the build was copied   (default: $HOME/loopback-staging)
#   SERVICE   systemd unit name            (default: lineflow)
#
# Usage:  bash ~/deploy-loopback.sh
#   or with overrides:  APP_DIR=/opt/loopback SERVICE=loopback bash ~/deploy-loopback.sh
set -euo pipefail

APP_DIR="${APP_DIR:-$HOME/lineflowapp}"
STAGING="${STAGING:-$HOME/loopback-staging}"
SERVICE="${SERVICE:-lineflow}"

if [ ! -d "$STAGING" ]; then
  echo "Staging dir not found: $STAGING" >&2
  echo "Copy a publish build there first (run deploy/deploy.ps1 on your dev box)." >&2
  exit 1
fi

echo "==> Installing $STAGING -> $APP_DIR"
mkdir -p "$APP_DIR"
# Note: users.json (your accounts + password hashes) lives only in APP_DIR and
# is not part of the published build, so this copy never overwrites it.
cp -r "$STAGING"/* "$APP_DIR"/

echo "==> Restarting $SERVICE (sudo)"
sudo systemctl restart "$SERVICE"

sleep 1
echo "==> Status"
if systemctl is-active --quiet "$SERVICE"; then
  echo "$SERVICE is active."
  sudo journalctl -u "$SERVICE" -n 6 --no-pager | sed 's/^/   /'
else
  echo "$SERVICE failed to start. Recent logs:" >&2
  sudo journalctl -u "$SERVICE" -n 20 --no-pager >&2
  exit 1
fi
