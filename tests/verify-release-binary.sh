#!/bin/sh
# Smoke test for a release binary. Runs in a clean container (ubuntu or alpine) with no
# .NET installed, so it catches binaries linked against the wrong libc or missing
# native dependencies.
#
# usage: verify-release-binary.sh <path-to-nibbler> [expected-version]
#
# It builds an image from a public base image to a local folder (--to-file, no registry
# needed), then checks the reported version, the manifest and that the added file is in
# one of the layers.
set -eu

NIBBLER="$1"
VERSION="${2:-}"
FROM_IMAGE="${FROM_IMAGE:-mcr.microsoft.com/dotnet/aspnet:11.0}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

fail() { echo "VERIFY FAILED: $*" >&2; exit 1; }

# a clean ubuntu image has no CA bundle, which the registry pull needs
if [ ! -e /etc/ssl/certs/ca-certificates.crt ] && command -v apt-get >/dev/null 2>&1; then
    apt-get update -qq >/dev/null && apt-get install -y -qq ca-certificates >/dev/null
fi

echo "--- binary: $NIBBLER"
[ -x "$NIBBLER" ] || fail "$NIBBLER is missing or not executable"

mkdir -p "$WORK/app"
echo "nibbler release check" > "$WORK/app/hello.txt"

echo "--- build $FROM_IMAGE -> $WORK/out"
if ! "$NIBBLER" --from-image "$FROM_IMAGE" --to-file "$WORK/out" \
        --add "$WORK/app:/home/app" --temp-folder "$WORK/tmp" -v > "$WORK/log" 2>&1; then
    cat "$WORK/log"
    fail "nibbler exited with an error"
fi
cat "$WORK/log"

grep -q '^Nibbler v' "$WORK/log" || fail "no version line in output"
if [ -n "$VERSION" ]; then
    grep -q "^Nibbler v$VERSION+" "$WORK/log" || fail "expected version $VERSION, got: $(grep '^Nibbler v' "$WORK/log")"
fi
grep -q 'Image digest: sha256:' "$WORK/log" || fail "no image digest in output"
[ -s "$WORK/out/manifest.json" ] || fail "no manifest.json in output"

found=""
for blob in "$WORK"/out/sha256_*; do
    if tar -tzf "$blob" 2>/dev/null | grep -q 'home/app/hello.txt'; then
        found="$blob"
        break
    fi
done
[ -n "$found" ] || fail "added file home/app/hello.txt not found in any layer"

echo "--- OK: $(grep '^Nibbler v' "$WORK/log"), added layer $(basename "$found")"
