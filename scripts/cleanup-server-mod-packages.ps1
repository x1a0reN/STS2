param(
  [string]$SshTarget = "root@47.98.165.140",
  [int]$Keep = 2,
  [switch]$InstallTimer,
  [switch]$RunNow,
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ($Keep -lt 1 -or $Keep -gt 2) {
  throw "Keep must be 1 or 2. MOD server cleanup must keep at most two packages."
}

if (-not $InstallTimer -and -not $RunNow) {
  $RunNow = $true
}

$remoteScriptPath = "/usr/local/sbin/gongdou-cleanup-mod-packages.sh"
$servicePath = "/etc/systemd/system/gongdou-mod-package-cleanup.service"
$timerPath = "/etc/systemd/system/gongdou-mod-package-cleanup.timer"

$bashScript = @'
#!/usr/bin/env bash
set -euo pipefail

KEEP_COUNT="${1:-2}"
DRY_RUN="${3:-0}"
APP_DIR="/var/www/fightcommunity-api"
STORAGE_ROOT="/var/www/fightcommunity-api/storage"
LOCK_FILE="/var/lock/gongdou-mod-package-cleanup.lock"
PACKAGE_NAME_LIKE="Gongdou_STS2_Mods-%"

if [[ "$KEEP_COUNT" -lt 1 || "$KEEP_COUNT" -gt 2 ]]; then
  echo "Keep count must be 1 or 2." >&2
  exit 2
fi

exec 9>"$LOCK_FILE"
if ! flock -n 9; then
  echo "Another MOD cleanup is running; skip."
  exit 0
fi

if [[ ! -d "$APP_DIR" ]]; then
  echo "App directory not found: $APP_DIR" >&2
  exit 2
fi

if [[ ! -d "$STORAGE_ROOT" ]]; then
  echo "Storage root not found: $STORAGE_ROOT" >&2
  exit 2
fi

conn_parts="$(python3 - <<'PY'
import pathlib
import re
import sys

settings = pathlib.Path("/var/www/fightcommunity-api/appsettings.Production.json")
text = settings.read_text(encoding="utf-8")
match = re.search(r'"DefaultConnection"\s*:\s*"([^"]+)"', text)
if not match:
    sys.exit("DefaultConnection not found")

parts = {}
for segment in match.group(1).split(";"):
    if not segment.strip() or "=" not in segment:
        continue
    key, value = segment.split("=", 1)
    parts[key.strip().lower()] = value.strip()

required = ["host", "port", "database", "username", "password"]
missing = [key for key in required if not parts.get(key)]
if missing:
    sys.exit("Missing connection fields: " + ",".join(missing))

print("|".join(parts[key] for key in required))
PY
)"

IFS='|' read -r PGHOST PGPORT PGDATABASE PGUSER PGPASSWORD_VALUE <<< "$conn_parts"
export PGPASSWORD="$PGPASSWORD_VALUE"
psql_base=(psql -X -q -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" -v ON_ERROR_STOP=1)

mapfile -t live_ids < <("${psql_base[@]}" -At -c "WITH live AS (SELECT DISTINCT ((regexp_match(\"ModDownloadUrl\", '/api/Files/media/([0-9]+)'))[1])::bigint AS id FROM \"Games\" WHERE \"ModDownloadUrl\" ~ '/api/Files/media/[0-9]+') SELECT mf.\"Id\" FROM \"MediaFiles\" mf JOIN live ON live.id = mf.\"Id\" WHERE mf.\"Purpose\" = 'ModFile' AND mf.\"Status\" = 'Ready' AND mf.\"OriginalFileName\" LIKE '$PACKAGE_NAME_LIKE' ORDER BY mf.\"Id\" DESC;")

if [[ "${#live_ids[@]}" -gt "$KEEP_COUNT" ]]; then
  echo "More live MOD URLs than keep limit; refusing cleanup. live=${live_ids[*]} keep=$KEEP_COUNT" >&2
  exit 3
fi

mapfile -t latest_ids < <("${psql_base[@]}" -At -c "SELECT \"Id\" FROM \"MediaFiles\" WHERE \"Purpose\" = 'ModFile' AND \"Status\" = 'Ready' AND \"OriginalFileName\" LIKE '$PACKAGE_NAME_LIKE' ORDER BY COALESCE(\"ReadyAt\", \"CreatedAt\") DESC, \"Id\" DESC LIMIT 20;")

keep_ids=()
add_keep_id() {
  local candidate="$1"
  [[ -n "$candidate" ]] || return 0
  for existing in "${keep_ids[@]}"; do
    [[ "$existing" == "$candidate" ]] && return 0
  done
  keep_ids+=("$candidate")
}

for id in "${live_ids[@]}"; do
  add_keep_id "$id"
done

for id in "${latest_ids[@]}"; do
  [[ "${#keep_ids[@]}" -ge "$KEEP_COUNT" ]] && break
  add_keep_id "$id"
done

if [[ "${#keep_ids[@]}" -eq 0 ]]; then
  echo "No ready MOD packages found; nothing to cleanup."
  exit 0
fi

keep_csv="$(IFS=,; echo "${keep_ids[*]}")"
echo "Keeping STS2 MOD media ids: $keep_csv"

delete_query="SELECT \"Id\", \"ObjectKey\" FROM \"MediaFiles\" WHERE \"Purpose\" = 'ModFile' AND \"Status\" = 'Ready' AND \"OriginalFileName\" LIKE '$PACKAGE_NAME_LIKE' AND NOT (\"Id\" = ANY(ARRAY[$keep_csv]::bigint[])) ORDER BY COALESCE(\"ReadyAt\", \"CreatedAt\") DESC, \"Id\" DESC;"
mapfile -t delete_rows < <("${psql_base[@]}" -F $'\t' -At -c "$delete_query")

deleted_count=0
for row in "${delete_rows[@]}"; do
  [[ -n "$row" ]] || continue
  IFS=$'\t' read -r media_id object_key <<< "$row"
  if [[ ! "$media_id" =~ ^[0-9]+$ ]]; then
    echo "Skip invalid media id: $media_id" >&2
    continue
  fi
  if [[ "$object_key" == *".."* ]] || [[ "$object_key" != modfile/* && "$object_key" != media/mods/* ]]; then
    echo "Skip unexpected object key for media $media_id: $object_key" >&2
    continue
  fi

  full_path="$(readlink -m "$STORAGE_ROOT/$object_key")"
  case "$full_path" in
    "$STORAGE_ROOT"/*) ;;
    *)
      echo "Refusing path outside storage root for media $media_id: $full_path" >&2
      continue
      ;;
  esac

  case "$full_path" in
    *.zip|*.rar|*.7z) ;;
    *)
      echo "Skip unexpected MOD extension for media $media_id: $full_path" >&2
      continue
      ;;
  esac

  if [[ "$DRY_RUN" == "1" ]]; then
    echo "[dry-run] delete media=$media_id file=$full_path"
    continue
  fi

  if [[ -f "$full_path" ]]; then
    rm -f -- "$full_path"
  fi

  "${psql_base[@]}" -c "UPDATE \"MediaFiles\" SET \"Status\" = 'Deleted', \"DeletedAt\" = NOW(), \"UpdatedAt\" = NOW() WHERE \"Id\" = $media_id AND \"Purpose\" = 'ModFile' AND \"Status\" = 'Ready';" >/dev/null
  deleted_count=$((deleted_count + 1))
  echo "Deleted old MOD media=$media_id file=$full_path"
done

ready_count="$("${psql_base[@]}" -At -c "SELECT COUNT(*) FROM \"MediaFiles\" WHERE \"Purpose\" = 'ModFile' AND \"Status\" = 'Ready' AND \"OriginalFileName\" LIKE '$PACKAGE_NAME_LIKE';")"
if [[ "$ready_count" -gt "$KEEP_COUNT" ]]; then
  echo "Ready MOD package count still exceeds keep limit: ready=$ready_count keep=$KEEP_COUNT" >&2
  exit 4
fi

echo "MOD cleanup complete. deleted=$deleted_count ready=$ready_count keep=$KEEP_COUNT"
'@

$serviceUnit = @"
[Unit]
Description=GongDou MOD package cleanup
After=network.target postgresql.service

[Service]
Type=oneshot
ExecStart=$remoteScriptPath $Keep run 0
"@

$timerUnit = @"
[Unit]
Description=Run GongDou MOD package cleanup hourly

[Timer]
OnBootSec=10min
OnUnitActiveSec=1h
Persistent=true

[Install]
WantedBy=timers.target
"@

function Send-RemoteFile {
  param(
    [string]$Content,
    [string]$RemotePath,
    [string]$Mode = "0644"
  )

  $Content | & ssh $SshTarget "cat > '$RemotePath' && chmod $Mode '$RemotePath'"
}

if ($InstallTimer) {
  Send-RemoteFile -Content $bashScript -RemotePath $remoteScriptPath -Mode "0755"
  Send-RemoteFile -Content $serviceUnit -RemotePath $servicePath -Mode "0644"
  Send-RemoteFile -Content $timerUnit -RemotePath $timerPath -Mode "0644"
  & ssh $SshTarget "systemctl daemon-reload && systemctl enable --now gongdou-mod-package-cleanup.timer && systemctl list-timers --all gongdou-mod-package-cleanup.timer --no-pager"
}

if ($RunNow) {
  $dryRunFlag = if ($DryRun) { "1" } else { "0" }
  & ssh $SshTarget "$remoteScriptPath $Keep run $dryRunFlag"
}
