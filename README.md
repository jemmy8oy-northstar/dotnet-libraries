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

Packages publish to **GitHub Packages** (org-private), not nuget.org — decision
recorded on [dotnet-libraries#1](https://github.com/jemmy8oy-northstar/dotnet-libraries/issues/1).
Publishing (CI → registry) uses the built-in `GITHUB_TOKEN` and needs nothing from
you. **Restoring on your own machine is the part that needs one-time setup**,
because GitHub Packages requires authentication even to *read* a public-to-the-org
feed.

### One-time setup (per developer, per machine)

1. **Create a classic PAT** with the `read:packages` scope:
   [github.com/settings/tokens/new](https://github.com/settings/tokens/new?scopes=read:packages&description=nuget-github-packages).
   Fine-grained tokens do not currently support the packages API — it must be a
   classic token.

2. **Add the package source**, authenticated, to your **global** NuGet config
   (`~/.nuget/NuGet/NuGet.Config` on Linux/macOS, `%AppData%\NuGet\NuGet.Config`
   on Windows) — not a project-local `nuget.config`, so the token never ends up
   in a repo:

   ```bash
   dotnet nuget add source https://nuget.pkg.github.com/jemmy8oy-northstar/index.json \
     --name github-northstar \
     --username <your-github-username> \
     --password <your-PAT> \
     --store-password-in-clear-text
   ```

   (`--store-password-in-clear-text` is what `dotnet nuget` itself asks for on
   Linux/macOS, where there is no OS credential store for it to use instead.)

3. **Reference the package** as usual:

   ```xml
   <PackageReference Include="Northstar.SourceGenerators" Version="x.y.z" PrivateAssets="all" />
   ```

   `dotnet restore` will pull from `github-northstar` using the stored credentials.

**The PAT is per-developer and must never be committed** — it lives only in your
global NuGet config, outside this repo. CI does not need this step: it publishes
with the workflow's `secrets.GITHUB_TOKEN`, which already has `packages: write`.

Source: see `.github/workflows/publish.yml` for the exact push step and source URL
(`https://nuget.pkg.github.com/jemmy8oy-northstar/index.json`) once it's merged —
tracked on [#1](https://github.com/jemmy8oy-northstar/dotnet-libraries/issues/1).
