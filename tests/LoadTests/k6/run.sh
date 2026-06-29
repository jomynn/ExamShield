#!/usr/bin/env bash
# Quick runner: k6 run <scenario> [extra k6 flags]
# Usage:
#   ./run.sh capture-upload                    # defaults to localhost:5000
#   ./run.sh ocr-pipeline --env BASE_URL=http://api:5000
set -euo pipefail

SCENARIO=${1:-capture-upload}
shift || true

k6 run \
  --env BASE_URL="${BASE_URL:-http://localhost:5000}" \
  --env ADMIN_EMAIL="${ADMIN_EMAIL:-admin@examshield.local}" \
  --env ADMIN_PASS="${ADMIN_PASS:-Admin@123!}" \
  "scenarios/${SCENARIO}.js" \
  "$@"
