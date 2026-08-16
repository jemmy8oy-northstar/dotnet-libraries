#!/usr/bin/env bash
# Packs Northstar.SourceGenerators and consumes the real .nupkg from a local feed.
#
# Why this exists: `dotnet build` proves the generator works via a ProjectReference,
# which is a completely different code path from a PackageReference. Get
# PackagePath wrong and the package still restores, still builds, and generates
# nothing — a failure that is invisible until a consumer wonders where their
# interface went. This is the only check that exercises the packaged layout.
#
# It asserts BOTH directions: a consumer WITH the package compiles against an
# interface that exists nowhere in source, and the same consumer WITHOUT it fails
# to compile. Without the negative half, a consumer that quietly hand-wrote the
# interface would pass.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

VERSION="0.0.0-verify"
FEED="$WORK/feed"
APP="$WORK/consumer"

echo "==> pack"
dotnet pack "$ROOT/src/Northstar.SourceGenerators/Northstar.SourceGenerators.csproj" \
  -c Release -o "$FEED" -p:Version="$VERSION" --nologo -v q

NUPKG="$FEED/Northstar.SourceGenerators.$VERSION.nupkg"
[ -f "$NUPKG" ] || { echo "NO  — pack produced no $NUPKG"; exit 1; }
echo "YES — packed $(basename "$NUPKG")"

echo "==> consumer project"
mkdir -p "$APP"
cat > "$APP/nuget.config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
XML

cat > "$APP/Consumer.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Northstar.SourceGenerators" Version="$VERSION" PrivateAssets="all" />
  </ItemGroup>
</Project>
XML

# IThing exists nowhere in source. If the packaged analyzer does not run, this
# file does not compile — that is the assertion.
cat > "$APP/Thing.cs" <<'CS'
using Northstar.SourceGenerators;

namespace Consumer;

[GenerateInterface]
public class Thing : IThing
{
    public string Name { get; set; } = "";
    public int Describe(string prefix, int times = 2) => prefix.Length * times;
}
CS

echo "==> build WITH the package (expect success)"
if dotnet build "$APP/Consumer.csproj" --nologo -v q >"$WORK/with.log" 2>&1; then
  echo "YES — a package consumer compiles against the generated IThing"
else
  echo "NO  — consumer failed to build; the packaged analyzer did not run"
  tail -30 "$WORK/with.log"
  exit 1
fi

echo "==> control: build WITHOUT the package (expect failure)"
sed -i '/PackageReference/d' "$APP/Consumer.csproj"
rm -rf "$APP/obj" "$APP/bin"
if dotnet build "$APP/Consumer.csproj" --nologo -v q >"$WORK/without.log" 2>&1; then
  echo "NO  — it compiled with no package reference, so the check above proved nothing"
  exit 1
else
  echo "YES — without the package the same source fails to compile"
fi

echo
echo "package verified: analyzers/dotnet/cs layout is correct and the attribute ships with it"
