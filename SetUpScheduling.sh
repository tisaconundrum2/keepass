#!/bin/bash

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
COMMIT_SCRIPT="$SCRIPT_DIR/auto_commit.sh"

if ! crontab -l 2>/dev/null | grep -q "$COMMIT_SCRIPT"; then
    (crontab -l 2>/dev/null; echo "*/30 * * * * $COMMIT_SCRIPT") | crontab -
    echo "Cron job has been set up to run every 30 minutes"
else
    echo "Cron job already exists"
fi
