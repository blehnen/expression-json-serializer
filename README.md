# DotNetWorkQueue.Aq.ExpressionJsonSerializer

Expression tree serializer/deserializer for [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/).

Fork of [aquilae/expression-json-serializer](https://github.com/aquilae/expression-json-serializer) with multi-target support and loop/goto expression handling. Published for use by [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue).

## Install

```
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
- Added NuGet packaging and GitHub Actions CI/publish pipeline

## Publishing a release

1. Ensure the `NUGET_API_KEY` secret is set in the GitHub repository settings.
2. Push a version tag: `git tag v1.0.0 && git push origin v1.0.0`
3. GitHub Actions runs build and tests across all targets, then packs and pushes to nuget.org automatically.

## License

MIT
