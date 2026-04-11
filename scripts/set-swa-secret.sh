#!/usr/bin/env bash
set -euo pipefail

# Usage:
#   ./scripts/set-swa-secret.sh [owner/repo] [resource-group] [static-web-app-name]
# Example:
#   ./scripts/set-swa-secret.sh PatAltimore/applesoft-emulator rg-apple-westus3 swa-apple-bvpprvy3safqa

REPO="${1:-PatAltimore/applesoft-emulator}"
RESOURCE_GROUP="${2:-rg-apple-westus3}"
SWA_NAME="${3:-swa-apple-bvpprvy3safqa}"

if ! command -v gh >/dev/null 2>&1; then
  echo "gh CLI is required. Install it first: https://cli.github.com/"
  exit 1
fi

if ! gh auth status >/dev/null 2>&1; then
  echo "GitHub CLI is not authenticated. Run: gh auth login"
  exit 1
fi

TOKEN="$(az staticwebapp secrets list --name "$SWA_NAME" --resource-group "$RESOURCE_GROUP" --query properties.apiKey -o tsv)"

if [[ -z "$TOKEN" ]]; then
  echo "Failed to retrieve Static Web App deployment token."
  exit 1
fi

printf '%s' "$TOKEN" | gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN --repo "$REPO"

echo "Secret AZURE_STATIC_WEB_APPS_API_TOKEN set for $REPO"
