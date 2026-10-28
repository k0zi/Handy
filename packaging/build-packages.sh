#!/usr/bin/env bash
# Builds the Handy .deb and .rpm packages into artifacts/packages/.
#
# Versioning: Nerdbank.GitVersioning, version.json holds "<year>.<month>" (e.g. 26.10) and the git
# height becomes the patch number (26.10.0, 26.10.1, ...). When the month changes, version.json is
# updated to the current year/month first (commit it so the next builds count from there).
#
# Usage: packaging/build-packages.sh [--no-bump] [--skip-tests] [deb|rpm ...]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/packages"
STAGE="$ROOT/artifacts/stage"
TOOLS="$ROOT/artifacts/tools"
NFPM_VERSION=2.48.0
NFPM_SHA256=6d7f9a28b2f397934403950fcbba83f598b08d02edbe79a0815b3b4897341750

bump=true
tests=true
formats=()
for arg in "$@"; do
    case "$arg" in
        --no-bump) bump=false ;;
        --skip-tests) tests=false ;;
        deb|rpm) formats+=("$arg") ;;
        *) echo "Usage: $0 [--no-bump] [--skip-tests] [deb|rpm ...]" >&2; exit 2 ;;
    esac
done
[[ ${#formats[@]} -gt 0 ]] || formats=(deb rpm)

cd "$ROOT"
dotnet tool restore >/dev/null

# ── version ──
expected="$(date +%y).$(date +%-m)"
current="$(sed -n 's/^ *"version": *"\([^"]*\)".*/\1/p' version.json)"
if [[ "$current" != "$expected" ]]; then
    if $bump; then
        # Not "nbgv set-version": that rewrites version.json and drops the other settings.
        sed -i "s/^\( *\"version\": *\"\)[^\"]*\"/\1$expected\"/" version.json
        echo "version.json: $current -> $expected (commit this change)"
    else
        echo "warning: version.json is $current, current month would be $expected" >&2
    fi
fi

version="$(dotnet nbgv get-version -v SimpleVersion)"
prerelease=""
if [[ "$(dotnet nbgv get-version -v PublicRelease)" != "True" ]]; then
    # Builds outside the main branch / release tags sort before the release of the same version.
    prerelease="g$(dotnet nbgv get-version -v GitCommitIdShort)"
fi
echo "Handy version: $version${prerelease:+~$prerelease}"

# ── build ──
if $tests; then
    dotnet test "$ROOT/Handy.slnx" -c Release
fi

rm -rf "$STAGE"
mkdir -p "$STAGE" "$OUT"
dotnet publish "$ROOT/src/Handy/Handy.csproj" \
    -c Release -r linux-x64 --self-contained true -o "$STAGE/app"
find "$STAGE/app" -name '*.pdb' -delete
# The publish copies the license files next to the binaries; the packages install them under /usr/share instead.
mkdir -p "$STAGE/doc"
mv "$STAGE/app/LICENSE" "$STAGE/app/THIRD_PARTY_NOTICES.md" "$STAGE/app/licenses" "$STAGE/doc/"
find "$STAGE/doc" -type f -exec chmod 644 {} +
cp "$ROOT/packaging/icons/handy-256.png" "$STAGE/handy-256.png"
sed "s|@BIN@|/usr/bin/handy|" "$ROOT/packaging/handy.desktop" > "$STAGE/handy.desktop"
# A umask of 002 would otherwise ship group-writable files.
chmod -R go-w "$STAGE"

# ── nfpm (single static binary, builds both formats without dpkg/rpmbuild) ──
nfpm="$(command -v nfpm || true)"
if [[ -z "$nfpm" ]]; then
    nfpm="$TOOLS/nfpm-$NFPM_VERSION/nfpm"
    if [[ ! -x "$nfpm" ]]; then
        archive="$TOOLS/nfpm_${NFPM_VERSION}_Linux_x86_64.tar.gz"
        mkdir -p "$(dirname "$nfpm")"
        curl -fsSL -o "$archive" \
            "https://github.com/goreleaser/nfpm/releases/download/v$NFPM_VERSION/nfpm_${NFPM_VERSION}_Linux_x86_64.tar.gz"
        echo "$NFPM_SHA256  $archive" | sha256sum -c --quiet -
        tar -xzf "$archive" -C "$(dirname "$nfpm")" nfpm
        rm "$archive"
    fi
fi

export HANDY_VERSION="$version" HANDY_PRERELEASE="$prerelease"
for format in "${formats[@]}"; do
    rm -f "$OUT"/handy*."$format"
    "$nfpm" package --config "$ROOT/packaging/nfpm.yaml" --packager "$format" --target "$OUT/"
done
ls -1 "$OUT"
