# Benchmarks

`Aq.ExpressionJsonSerializer.Benchmarks` measures per-call serialize/deserialize cost and
allocation. It is deliberately **not** in `Aq.ExpressionJsonSerializer.sln`: BenchmarkDotNet
pulls a large dependency graph and CI has no NuGet cache, so including it would add restore
and build time to every run of a gate that never executes benchmarks.

```bash
dotnet run -c Release --project Aq.ExpressionJsonSerializer.Benchmarks
```

Two shapes are measured:

- **predicate** — `m => m.Value > 10 && m.Name != null`, the shape a DotNetWorkQueue LINQ
  message actually takes.
- **statements** — a block with two locals, a loop, compound assignment and a break label,
  exercising both the parameter map and the label-target map.

## ConcurrentDictionary → Dictionary for per-call state

`Serializer` and `Deserializer` are constructed fresh on every call and never shared across
threads, yet each held its parameter and label-target maps in a `ConcurrentDictionary`.
`ConcurrentDictionary`'s default constructor allocates a lock array sized from
`Environment.ProcessorCount`, so that cost was paid per operation for no benefit.

Measured on an AMD Ryzen 9 9950X3D (32 logical cores, .NET SDK 10.0.110) — a favourable
case for the change, since the lock array scales with core count:

| Benchmark | Mean before | Mean after | Δ | Alloc before | Alloc after | Δ |
|-----------|------------:|-----------:|--:|-------------:|------------:|--:|
| Serialize predicate    |  6.03 µs |  5.91 µs | −1.9% |  32.79 KB |  31.65 KB | −3.5% |
| Deserialize predicate  | 22.21 µs | 21.88 µs | −1.5% |  73.24 KB |  70.38 KB | −3.9% |
| Serialize statements   | 14.60 µs | 14.00 µs | −4.1% |  69.61 KB |  68.32 KB | −1.9% |
| Deserialize statements | 53.55 µs | 52.25 µs | −2.4% | 163.29 KB | 160.56 KB | −1.7% |

Real and consistent, with no regression, but modest. The timing deltas are only around 2σ
individually; the allocation deltas are deterministic and consistent, which is the stronger
signal. Roughly 2.9 KB per deserialize — about 2.9 MB/s of GC pressure at 1000 messages
per second.

**The three static reflection caches remain `ConcurrentDictionary`.** Those are genuinely
shared and read-mostly, where lock-free reads are the right tool; a lock-guarded
`Dictionary` there would be worse, not better.

This change depends on `Serializer`/`Deserializer` instances never being shared between
threads. That holds because `Serialize` and `Deserialize` each construct one which never
escapes, and the `Action` closures they hand out are invoked synchronously within the same
call. `LambdaMultiThreaded` guards the boundary: 100 concurrent round trips, each with its
own instance maps, all hitting the shared static caches. If a future change starts caching
a `Serializer` instance, these three maps must go back to being concurrent.

## Where the cost actually is

The headline number is not the dictionaries. Serializing
`m => m.Value > 10 && m.Name != null` allocates **32 KB**, and deserializing it **73 KB**.
The maps account for only ~1–3 KB of that.

Two candidates dominate, neither yet investigated:

1. **Type identity is written in full, repeatedly.** Every `Type` in the payload carries a
   complete assembly-qualified name — `System.Private.CoreLib, Version=10.0.0.0,
   Culture=neutral, PublicKeyToken=...` recurs throughout a document. A per-document type
   table with integer references would shrink both the payload and the allocation.
2. **`JToken.ReadFrom` materialises the whole document** before deserialization begins, so
   the entire tree exists as `JObject`s in addition to the `Expression` tree being built.

Either is a larger win than the dictionary change, and both are design work rather than a
swap.
