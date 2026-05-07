#!/bin/bash
set -euo pipefail

mkdir -p /var/run/sshd

if ! pgrep dockerd >/dev/null 2>&1; then
  dockerd >/var/log/dockerd.log 2>&1 &
fi

for attempt in $(seq 1 30); do
  if docker info >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

exec /usr/sbin/sshd -D -e