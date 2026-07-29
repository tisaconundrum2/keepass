#!/usr/bin/env bash
# Local equivalent of the keepass-merge.yml workflow.
# Run from the repo root: bash scripts/local-merge.sh
set -euo pipefail

REPO_ROOT="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
cd "$REPO_ROOT"

# ── Password ──────────────────────────────────────────────────────────────────
read -s -p "KeePass master password: " KEEPASS_PASSWORD
echo

# ── Build ─────────────────────────────────────────────────────────────────────
echo "==> Building merge tool..."
dotnet build Keepass.Background.Service/Keepass.Background.Service.csproj \
  --configuration Release --no-restore -v q

# ── Commit list ───────────────────────────────────────────────────────────────
echo "==> Building commit list..."
# Use the last 5 commits on the current branch, oldest first
COMMITS="$(git log --reverse --pretty=format:"%H" -5)"
if [[ -z "$COMMITS" ]]; then
  COMMITS="$(git rev-parse HEAD)"
fi
echo "Commits to process (oldest→newest):"
while IFS= read -r C; do
  echo "  $C $(git log -1 --pretty=format:'%s' "$C")"
done <<< "$COMMITS"
echo

# ── Incremental merge ─────────────────────────────────────────────────────────
echo "==> Incremental merge..."
  find . -name "*.kdbx" -not -path "./.git/*" -not -path "./lib/*" | while read -r KDBX_PATH; do
  echo "Processing: $KDBX_PATH"
  REL_PATH="${KDBX_PATH#./}"
  BASENAME=$(basename "$KDBX_PATH" .kdbx)
  RUNNING_TMP="/tmp/${BASENAME}__running.kdbx"
  INCOMING_TMP="/tmp/${BASENAME}__incoming.kdbx"
  MERGED_TMP="/tmp/${BASENAME}__merged.kdbx"

  SEEDED=false

  while IFS= read -r COMMIT; do
    if ! git show "${COMMIT}:${REL_PATH}" > "$INCOMING_TMP" 2>/dev/null; then
      echo "  $COMMIT: file absent — skipping"
      continue
    fi

    if [[ "$SEEDED" == "false" ]]; then
      echo "  $COMMIT: seed — running self-merge to normalise output"
      KeePassMerge__BasePath="$INCOMING_TMP" \
      KeePassMerge__IncomingPath="$INCOMING_TMP" \
      KeePassMerge__OutputPath="$RUNNING_TMP" \
      KeePassMerge__Password="$KEEPASS_PASSWORD" \
      dotnet run \
        --project Keepass.Background.Service/Keepass.Background.Service.csproj \
        --configuration Release --no-build
      SEEDED=true
      continue
    fi

    echo "  $COMMIT: merging into running result"
    KeePassMerge__BasePath="$RUNNING_TMP" \
    KeePassMerge__IncomingPath="$INCOMING_TMP" \
    KeePassMerge__OutputPath="$MERGED_TMP" \
    KeePassMerge__Password="$KEEPASS_PASSWORD" \
    dotnet run \
      --project Keepass.Background.Service/Keepass.Background.Service.csproj \
      --configuration Release --no-build

    cp "$MERGED_TMP" "$RUNNING_TMP"
  done <<< "$COMMITS"

  if [[ "$SEEDED" == "false" ]]; then
    echo "  File not found in any commit — skipping."
    continue
  fi

  echo "  Copying result back to $KDBX_PATH"
  cp "$RUNNING_TMP" "$KDBX_PATH"
done

# ── Git status ────────────────────────────────────────────────────────────────
echo
echo "==> Git status after merge:"
git status
echo
git diff --stat HEAD
