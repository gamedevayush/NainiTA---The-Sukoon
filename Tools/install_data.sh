#!/bin/sh
# install the regenerated map data: install_data.sh <label>
set -e
A="D:/NainitalGame/Assets/Nainital"; P="D:/Claude Projects/NainitalGame/Processed/world_big"
B="D:/Claude Projects/NainitalGame/Backups/before_$1/Data"; mkdir -p "$B"
for f in heightmap.bytes world.json splat.png far_ridges.json minimap.png; do
  if ! cmp -s "$P/$f" "$A/Data/$f"; then cp "$A/Data/$f" "$B/$f"; cp "$P/$f" "$A/Data/$f"; echo "updated Data/$f"; fi
done
