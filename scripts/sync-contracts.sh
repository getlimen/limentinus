#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
LIMEN_CONTRACTS="$(cd "$REPO_DIR/../limen/contracts/Limen.Contracts" && pwd)"

DEST="$REPO_DIR/src/Limen.Contracts"
rm -rf "$DEST"
mkdir -p "$DEST"
cp -r "$LIMEN_CONTRACTS/." "$DEST/"
echo "Copied Limen.Contracts source into $DEST"
