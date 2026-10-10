#!/bin/sh
set -eu
repo_root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
exec docker compose --project-directory "$repo_root/infra" -f "$repo_root/infra/compose.yaml" "$@"
