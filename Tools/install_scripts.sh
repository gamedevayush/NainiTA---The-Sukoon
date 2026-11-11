#!/bin/sh
# back up and install every staged script that differs from the project (no data): install_scripts.sh <label>
set -e
A="D:/NainitalGame/Assets/Nainital"; S="D:/Claude Projects/NainitalGame/Src/stage2"
B="D:/Claude Projects/NainitalGame/Backups/before_$1"; mkdir -p "$B"
cd "$S"
find . -type f \( -name "*.cs" -o -name "*.shader" -o -name "*.hlsl" \) | while read f; do
  r="${f#./}"; t="$A/$r"
  if [ ! -f "$t" ]; then mkdir -p "$(dirname "$t")"; cp "$f" "$t"; echo "new $r";
  elif ! cmp -s "$f" "$t"; then mkdir -p "$B/$(dirname "$r")"; cp "$t" "$B/$r"; cp "$f" "$t"; echo "updated $r"; fi
done
