#!/bin/sh
set -e

# Fix Data Protection keys directory permissions.
# Docker volumes are created as root; the app runs as $APP_UID (1654).
# Run as root to chown, then exec as app user.
APP_UID="${APP_UID:-1654}"
APP_GID="${APP_GID:-1654}"
DP_DIR="/home/app/.aspnet/DataProtection-Keys"
mkdir -p "$DP_DIR"
chown -R "$APP_UID:$APP_GID" "$DP_DIR"

exec gosu "$APP_UID" dotnet decorativeplant-be.API.dll
