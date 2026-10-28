#!/usr/bin/env bash
# Download the pinned MediaMTX build and serve the dev config on this Mac.
set -euo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
VERSION=v1.21.3
ARCH=$(uname -m)
case "$(uname -s)-$ARCH" in
  Darwin-arm64) ASSET=darwin_arm64 ;;
  Darwin-x86_64) ASSET=darwin_amd64 ;;
  *) printf 'Este script es para macOS.\n' >&2; exit 1 ;;
esac
TARBALL="mediamtx_${VERSION}_${ASSET}.tar.gz"
DEST=${SITU_MEDIAMTX_DIR:-/tmp/situ-mediamtx-dev}
mkdir -p "$DEST"
PINNED=$(grep "[* ]${TARBALL}$" "${ROOT}/checksums.sha256")
EXPECTED=${PINNED%% *}
curl -fsSL "https://github.com/bluenviron/mediamtx/releases/download/${VERSION}/${TARBALL}" -o "${DEST}/${TARBALL}"
curl -fsSL "https://github.com/bluenviron/mediamtx/releases/download/${VERSION}/checksums.sha256" -o "${DEST}/upstream.sha256"
UPSTREAM=$(grep "[* ]${TARBALL}$" "${DEST}/upstream.sha256")
if [[ "${UPSTREAM%% *}" != "$EXPECTED" ]]; then
  printf 'El checksum publicado no coincide con el fijado.\n' >&2
  exit 1
fi
(cd "$DEST" && printf '%s  %s\n' "$EXPECTED" "$TARBALL" | shasum -a 256 -c -)
tar -xzf "${DEST}/${TARBALL}" -C "$DEST" mediamtx
printf 'RTMP en el puerto 1935 y HLS en el 8888. Ctrl-C detiene el servidor.\n'
exec "${DEST}/mediamtx" "${ROOT}/mediamtx.dev.yml"
