# dotnet-libraries

Shared .NET libraries for the Northstar org, published as NuGet packages.

| Package | What it does |
|---|---|
| [`Northstar.SourceGenerators`](src/Northstar.SourceGenerators) | `[GenerateInterface]` — mirrors a concrete class's public surface into an interface |

## Why this repo exists

Shared code used to propagate by being **copied out of `web-template`**. That only
ever fixes repos generated *after* the fix — repos generated before it keep the old
copy, and nothing tells you which is which. In one week that failure mode cost four
separate bugs (a Dockerfile that couldn't restore its own solution, unqualified
Service selectors, a dead API path prefix, screenshots nobody wanted).

A package fixes the repos generated before it. That is the whole argument.

The trade is real and worth stating: a package needs a publish pipeline, a version
bump per change, and credentials wherever it is consumed. Copying needs none of
those. It is worth it for code that is *identical everywhere and changes rarely* —
which is what belongs here.

## Layout

```
src/<Package>/        one directory per package
tests/<Package>.Tests/ its tests
scripts/              verification that CI and a human both run
```

## Working on it

```bash
dotnet build Northstar.Libraries.slnx
dotnet test  Northstar.Libraries.slnx
bash scripts/verify-package.sh          # packs, then consumes the real .nupkg
```

`verify-package.sh` is the one that matters before publishing. `dotnet test` proves
the generator works through a **ProjectReference**, which is a different code path
from a **PackageReference** — get `PackagePath` wrong and the package restores,
builds, and silently generates nothing. The script builds a throwaway consumer
against the packed `.nupkg` and asserts both directions: with the package it
compiles against an interface that exists nowhere in source, without it the same
file fails to compile.

## Branches

`main` is the published state; `dev` is where work lands. Feature branch → PR into
`dev` → promotion PR `dev` → `main`, same as the other Northstar repos.

## Consuming a package

```xml
<PackageReference Include="Northstar.SourceGenerators" Version="x.y.z" PrivateAssets="all" />
```

Source: see `.github/workflows/publish.yml` for where packages are pushed, and the
repo's `nuget.config` guidance in that workflow's comments.
