#!/usr/bin/env bash
# Starts the headless X server the keyboard tests type into, and waits until it answers.
set -euo pipefail

DISPLAY_NUMBER="${DISPLAY_NUMBER:-99}"

if xdpyinfo -display ":${DISPLAY_NUMBER}" >/dev/null 2>&1; then
    echo "Display :${DISPLAY_NUMBER} is already running"
    exit 0
fi

Xvfb ":${DISPLAY_NUMBER}" -screen 0 1280x1024x24 >/dev/null 2>&1 &

for _ in $(seq 1 50); do
    if xdpyinfo -display ":${DISPLAY_NUMBER}" >/dev/null 2>&1; then
        echo "Display :${DISPLAY_NUMBER} ready"
        exit 0
    fi
    sleep 0.2
done

echo "Xvfb failed to start on :${DISPLAY_NUMBER}" >&2
exit 1
