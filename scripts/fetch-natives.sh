#!/usr/bin/env bash
# Stage Relate's prebuilt C ABI libraries (librelate_ffi) for every supported RID.
#
# Source: the per-platform npm packages Neo4j publishes for the official JS client
# (@relate/relate-<platform>), which carry the same librelate_ffi built by the
# upstream release workflow. `npm pack` verifies each tarball against the registry's
# integrity hash; nothing is installed and no package scripts run.
#
# Usage: scripts/fetch-natives.sh [engine-version] [out-dir]
set -euo pipefail

VERSION="${1:-1.0.2}"
OUT="${2:-$(cd "$(dirname "$0")/.." && pwd)/natives}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# npm platform  -> .NET RID   : library file inside the package's lib/
PLATFORMS=(
  "linux-x64:linux-x64:librelate_ffi.so"
  "linux-arm64:linux-arm64:librelate_ffi.so"
  "darwin-x64:osx-x64:librelate_ffi.dylib"
  "darwin-arm64:osx-arm64:librelate_ffi.dylib"
  "win32-x64:win-x64:relate_ffi.dll"
)

mkdir -p "$OUT"
for entry in "${PLATFORMS[@]}"; do
  IFS=: read -r npm_platform rid lib <<<"$entry"
  pkg="@relate/relate-${npm_platform}@${VERSION}"
  echo "==> ${pkg} -> ${rid}"
  (cd "$WORK" && npm pack --silent "$pkg" >/dev/null)
  tgz="$WORK/relate-relate-${npm_platform}-${VERSION}.tgz"
  mkdir -p "$WORK/$rid"
  tar -xzf "$tgz" -C "$WORK/$rid"
  mkdir -p "$OUT/$rid"
  cp "$WORK/$rid/package/lib/$lib" "$OUT/$rid/$lib"
  (cd "$OUT/$rid" && sha256sum "$lib")
done

echo "$VERSION" > "$OUT/VERSION"
echo "Staged Relate ${VERSION} natives in ${OUT}"
