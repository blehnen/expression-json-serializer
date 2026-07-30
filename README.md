# DotNetWorkQueue.Aq.ExpressionJsonSerializer

[![NuGet](https://img.shields.io/nuget/v/DotNetWorkQueue.Aq.ExpressionJsonSerializer.svg)](https://www.nuget.org/packages/DotNetWorkQueue.Aq.ExpressionJsonSerializer)
[![Release](https://img.shields.io/github/v/release/blehnen/expression-json-serializer?sort=semver)](https://github.com/blehnen/expression-json-serializer/releases)
[![License MIT](https://img.shields.io/badge/license-MIT-green.svg)](https://github.com/blehnen/expression-json-serializer/blob/master/LICENSE)
[![CI](https://github.com/blehnen/expression-json-serializer/actions/workflows/ci.yml/badge.svg)](https://github.com/blehnen/expression-json-serializer/actions/workflows/ci.yml)
[![SonarQube](https://github.com/blehnen/expression-json-serializer/actions/workflows/sonarcloud.yml/badge.svg)](https://github.com/blehnen/expression-json-serializer/actions/workflows/sonarcloud.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=blehnen_expression-json-serializer&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=blehnen_expression-json-serializer)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=blehnen_expression-json-serializer&metric=coverage)](https://sonarcloud.io/summary/new_code?id=blehnen_expression-json-serializer)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=blehnen_expression-json-serializer&metric=bugs)](https://sonarcloud.io/summary/new_code?id=blehnen_expression-json-serializer)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=blehnen_expression-json-serializer&metric=code_smells)](https://sonarcloud.io/summary/new_code?id=blehnen_expression-json-serializer)

Expression tree serializer/deserializer for [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/).

Fork of [aquilae/expression-json-serializer](https://github.com/aquilae/expression-json-serializer) with multi-target support and loop/goto expression handling. Published for use by [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue).

## Install

```bash
dotnet add package DotNetWorkQueue.Aq.ExpressionJsonSerializer
```

## Supported targets

- .NET 10.0
- .NET 8.0
- .NET Framework 4.8
- .NET Standard 2.0

## Usage

```csharp
var settings = new JsonSerializerSettings();
settings.Converters.Add(new ExpressionJsonConverter(typeof(MyMessage)));

Expression<Func<MyMessage, bool>> expr = m => m.Value > 10;

string json = JsonConvert.SerializeObject(expr, settings);
var restored = JsonConvert.DeserializeObject<Expression<Func<MyMessage, bool>>>(json, settings);
```

## Changes from upstream

- Added net10.0, net8.0, net48, and netstandard2.0 multi-targeting
- Merged loop and goto expression support
- Thread-safe: all internal dictionaries use `ConcurrentDictionary`
- Added NuGet packaging and GitHub Actions CI/publish pipeline

## Build and CI

| Pipeline | What it does |
|----------|--------------|
| [`.github/workflows/ci.yml`](.github/workflows/ci.yml) | Builds the solution and runs the test suite on net10.0 / net8.0 (ubuntu) and net48 (windows). Also packs and publishes to NuGet on a `v*` tag. |
| [`.github/workflows/sonarcloud.yml`](.github/workflows/sonarcloud.yml) | SonarCloud CI-based analysis. Builds, runs the net10.0 tests with coverlet (OpenCover format), and feeds coverage to SonarCloud. Requires the `SONAR_TOKEN` repository secret. Rule exclusions are documented in [`docs/sonar-exclusions.md`](docs/sonar-exclusions.md). |
| [`Jenkinsfile`](Jenkinsfile) | Jenkins pipeline that fires on every PR (reported as the `continuous-integration/jenkins/pr-merge` check). Builds and runs the net10.0 / net8.0 test stages on a Docker agent. |

To reproduce the Sonar coverage run locally:

```bash
dotnet build Aq.ExpressionJsonSerializer.sln -c Debug
dotnet test Aq.ExpressionJsonSerializer.Tests/Aq.ExpressionJsonSerializer.Tests.csproj \
  -f net10.0 -c Debug --no-build \
  --collect:"XPlat Code Coverage;Format=opencover"
```

## Publishing a release

Publishing uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
over GitHub OIDC — there is no long-lived API key. The publish job exchanges a short-lived
OIDC token for a NuGet key valid for one hour, immediately before pushing.

1. Push a version tag: `git tag v1.0.0 && git push origin v1.0.0`
2. GitHub Actions runs build and tests across all targets, then packs and pushes to nuget.org automatically.

### Trusted publishing setup

A one-time policy on nuget.org (your username → **Trusted Publishing**) backs this:

| Field | Value |
|-------|-------|
| Policy owner | `blehnen` |
| Repository Owner | `blehnen` |
| Repository | `expression-json-serializer` |
| Workflow File | `ci.yml` (filename only, no path) |
| Environment | *(blank — the publish job declares no GitHub environment)* |

To confirm the policy resolves without cutting a release, run the **CI** workflow manually
from the Actions tab. That triggers the `verify-trusted-publishing` job, which performs the
OIDC exchange and stops — it never pushes a package. Do this after any change to the policy
or to the workflow filename, since the policy is bound to `ci.yml` by name.

## License

MIT
