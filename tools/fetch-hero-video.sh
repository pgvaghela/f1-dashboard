#!/usr/bin/env bash
# Downloads royalty-free F1 track footage from Pexels for the home hero.
# Source: https://www.pexels.com/video/exciting-formula-1-track-race-with-crowds-36062879/
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
HERO_DIR="$ROOT/src/F1Dashboard.Web/public/hero"
VIDEO_ID="${1:-36062879}"
UA="Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36"

mkdir -p "$HERO_DIR"

SRC_URL=$(curl -sI -A "$UA" -H "Referer: https://www.pexels.com/" \
  "https://www.pexels.com/download/video/${VIDEO_ID}/" \
  | rg -i '^location:' | awk '{print $2}' | tr -d '\r')

echo "Downloading $SRC_URL"
curl -sL -A "$UA" -H "Referer: https://www.pexels.com/" "$SRC_URL" -o "$HERO_DIR/hero-source.mp4"

ffmpeg -y -i "$HERO_DIR/hero-source.mp4" \
  -c:v libx264 -crf 22 -preset slow \
  -g 12 -keyint_min 12 -sc_threshold 0 \
  -pix_fmt yuv420p -movflags +faststart -an \
  "$HERO_DIR/hero.mp4"

ffmpeg -y -ss 0.4 -i "$HERO_DIR/hero.mp4" -update 1 -vframes 1 -q:v 2 "$HERO_DIR/poster.jpg"
rm -f "$HERO_DIR/hero-source.mp4"

echo "Hero assets ready in $HERO_DIR"
