#!/bin/bash
# usage: build-mod.sh <ModName> <dir with the .csproj>. Never fails the image build: a broken mod is skipped.
name="$1"; dir="$2"; cd "$dir"
csproj=$(ls *.csproj | head -n1)
sed -i -E 's|<HintPath>[^<]*[\/]([^\/<]+\.dll)</HintPath>|<HintPath>/ace/\1</HintPath>|g; s|net8\.0|net10.0|g; s|<OutputPath>[^<]*</OutputPath>|<OutputPath>/mods/'"$name"'/</OutputPath>|; /<BaseOutputPath>/d' "$csproj"
if dotnet build "$csproj" -c Release -o "/mods/$name" 2>&1 | tail -n 15 && [ -f "/mods/$name/Meta.json" ]; then echo "MOD OK: $name"; else echo "MOD SKIPPED (build failed): $name"; rm -rf "/mods/$name"; fi
exit 0
