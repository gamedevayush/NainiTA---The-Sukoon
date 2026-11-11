#!/bin/sh
# bridge.sh <id> <command> [argument] : send one command to the running Unity editor and wait for its result.
B="D:/NainitalGame/Bridge"; id="$1"; shift
mkdir -p "$B"; rm -f "$B/result_$id.txt"
printf '%s %s\n' "$id" "$*" > "$B/cmd.tmp" && mv "$B/cmd.tmp" "$B/cmd.txt"
n=0
until [ -f "$B/result_$id.txt" ]; do
  sleep 0.5; n=$((n+1))
  if [ $((n % 30)) -eq 0 ] && ! tasklist 2>/dev/null | grep -q "^Unity.exe"; then echo "error: the editor is not running"; exit 2; fi
  if [ $n -gt 2400 ]; then echo "error: no result after 20 minutes ($(cat "$B/status.txt" 2>/dev/null))"; exit 3; fi
done
cat "$B/result_$id.txt"
