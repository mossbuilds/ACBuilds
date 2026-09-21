#!/bin/bash
# usage: mods-proposed/check-mod.sh <ModName>   (run from anywhere; needs Docker and %TEMP%/ace-vr-bin with the ACE dlls)
# Compiles mods-proposed/<ModName> against the ACE binaries in the .NET 10 SDK image, exactly like the image build does. Output goes to a scratch folder only. Deploys NOTHING.
set -u
name="$1"; here="$(cd "$(dirname "$0")" && pwd)"
bin="$(cygpath -m "${TEMP:-/tmp}")/ace-vr-bin"; out="$(cygpath -m "${TEMP:-/tmp}")/modcheck-$name"
rm -rf "$out"; mkdir -p "$out"
MSYS_NO_PATHCONV=1 docker run --rm -v "$(cygpath -m "$here/$name"):/src" -v "$bin:/ace:ro" -v "$(cygpath -m "$here/../server/build-mod.sh"):/build-mod.sh:ro" -v "$out:/mods" \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -c "cp -r /src /work && bash /build-mod.sh $name /work" 2>&1 | grep -E "error|MOD" | sort -u
