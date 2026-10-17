#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG="${1:-Release_ML}"
if [[ $# -gt 0 ]]; then shift; fi

if [[ "$CONFIG" == *"ML"* ]] && [[ -d "$ROOT/../ModMenu" ]]; then
    dotnet build "$ROOT/../ModMenu/ModMenu/ModMenu.csproj" -c "$CONFIG" "$@"
fi

dotnet build "$ROOT/XPerfect/XPerfect.csproj" -c "$CONFIG" "$@"
