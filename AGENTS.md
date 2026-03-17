# Nibbler — Agent Guide

Nibbler is a .NET CLI tool for making simple modifications to OCI/Docker images without needing Docker or root privileges. It reads image metadata from a registry, applies changes (labels, env vars, entrypoint, layers, etc.), and pushes the result back — all via the OCI Distribution API.

## Build

```bash
dotnet restore
dotnet build --no-restore
```

## Running Tests

```bash
dotnet test Nibbler.Test --no-build

# Run a single test by name
dotnet test Nibbler.Test --filter "TestMethodName"
```

### Test Prerequisites

Most unit tests require a local registry at `localhost:5000` and generated test data. Without these, registry-dependent tests will fail.

**Local setup:**

```bash
# 1. Start a local registry
docker run -d -p 5000:5000 --name nibbler-test-registry registry:2

# 2. Seed it with required images
docker pull mcr.microsoft.com/dotnet/aspnet:9.0
docker tag mcr.microsoft.com/dotnet/aspnet:9.0 localhost:5000/dotnet/aspnet:9.0
docker push localhost:5000/dotnet/aspnet:9.0

docker pull hello-world:latest
docker tag hello-world:latest localhost:5000/hello-world:latest
docker push localhost:5000/hello-world:latest

# 3. Generate test data
rm -rf tests/TestData && mkdir tests/TestData
dotnet new webapp -o tests/TestData
dotnet new gitignore -o tests/TestData
dotnet publish tests/TestData -o ./tests/TestData/publish
curl -L https://mcr.microsoft.com/v2/dotnet/core/aspnet/blobs/sha256:9526604e089d8d4aa947f34e52a14ac8793232dd181022932f3c15291c5cd3af -o tests/TestData/test.tar.gz
```

**Expected result:** 158 passed, 45 skipped, 0 failed. The 45 skipped tests are permanently marked `[Ignore]` in three test classes (`AuthenticationTest`, `ImageCompareTest`, `MatchTest`) and are not environment-dependent.

### In GitHub Actions

The workflow at `.github/workflows/test.yaml` handles all prerequisites automatically via a `registry:2` service container. The relevant steps:

```yaml
services:
  registry:
    image: registry:2
    ports:
      - 5000:5000

steps:
  # seeds the registry, generates test data, then:
  - run: dotnet restore
  - run: dotnet build --no-restore
  - run: dotnet test Nibbler.Test --no-build
```

The workflow also runs bash end-to-end tests after the unit tests:

```bash
dotnet tool install -g minver-cli
cd tests && chmod u+x test-e2e.sh && ./test-e2e.sh
```

The e2e tests are Linux-only and require the seeded local registry.

## Architecture

### Core Workflow

```
CLI args → BuildCommand (parse/validate) → BuildRun (orchestrate)
  → IImageSource.LoadImage()           # pull manifest + config from registry or file
  → Image.Clone()                      # non-destructive: clone before modifying
  → UpdateImageConfig() / AddLayer()   # apply changes
  → IImageDestination × N:             # push to one or more destinations
      CheckConfigExists / FindMissingLayers / CopyLayers / PushManifest
```

### Key Abstractions

- **`IImageSource`** — reads an image (registry or tar.gz file)
- **`IImageDestination`** — writes an image (registry or tar.gz file)
- **`Image`** — holds the OCI manifest + config; recalculates digests on every mutation
- **`Archive`** — creates tar.gz layers from folders, with .dockerignore-style filtering and reproducible timestamps (fixed to 2000-01-01 by default)
- **`Registry`** — OCI Distribution API v2 client (manifests, blobs, uploads)
- **`BuildCommand`** — McMaster CLI argument parsing and validation
- **`BuildRun`** — wires together sources, destinations, and config; calls `ExecuteAsync`

### Project Layout

- `Nibbler/` — main CLI: `Command/`, `Models/`, `Utils/`, plus `Image.cs`, `Archive.cs`, `Registry.cs`, source/dest implementations
- `Nibbler.Test/` — MSTest unit tests; uses Moq; targets net10.0
- `Nibbler.LinuxFileUtils/` — P/Invoke bindings for Linux file ownership (uid/gid); targets net8/9/10
- `Nibbler.Aot/` — AOT compilation entry point placeholder
- `tests/` — bash end-to-end scripts run in CI against a real registry

### Multi-target

`Nibbler` and `Nibbler.LinuxFileUtils` target net8.0, net9.0, and net10.0 (`RollForward=major`). `Nibbler.Test` targets net10.0 only.

### Authentication

Reads `~/.docker/config.json` (or `--docker-config` override). Supports base64 `auth`, credential helpers, Bearer tokens, and Basic auth. Docker Hub normalizes to `https://index.docker.io/v1/`. The `--trace` flag logs HTTP headers including auth — flagged with a warning in the README.

### Versioning

Uses MinVer with `v`-prefixed git tags. Version is embedded at build time.
