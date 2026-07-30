# SonarCloud rule exclusions

These are recorded decisions that a rule is wrong for this codebase, not a backlog.
Findings that were genuinely fixable were fixed rather than excluded.

Kept in version control rather than marked "Accepted" through the SonarCloud API, so the
reasoning survives the Sonar project being recreated.

## Two mechanisms, and why

Suppressions live in **two** places depending on which engine raises the rule:

| Engine | Example | Suppressed in |
|--------|---------|---------------|
| Sonar's own C# analyser | `csharpsquid:S1172` | [`.github/workflows/sonarcloud.yml`](../.github/workflows/sonarcloud.yml) via `sonar.issue.ignore.multicriteria` |
| Roslyn / .NET SDK analysers | `external_roslyn:CA1822` | [`.editorconfig`](../.editorconfig) via `dotnet_diagnostic.<id>.severity = none` |

This split is not cosmetic. **`sonar.issue.ignore.multicriteria` does not filter imported
external analyser issues** — SonarCloud ingests those from the build output *after* its own
exclusion filters have run, so `multicriteria` entries naming an `external_roslyn:*` rule
are silently ignored. They look correct in the workflow and do nothing.

That mistake was live from the first exclusions commit until it was noticed on master:
`csharpsquid:S1172`, `S3011` and `S2325` were being excluded correctly while
`external_roslyn:CA1822`, `CA1861` and `CA1859` were not, leaving 16 findings open.

Suppressing an external rule in `.editorconfig` means the analyser never emits it, so
there is nothing for Sonar to import — and the compiler stops reporting it too.

## e1 — `csharpsquid:S1172` "Remove this unused method parameter" (15 issues)

Scope: `**/Deserializer.*.cs`

`Deserializer.Expression` dispatches through a single switch table where every arm has
the same shape:

```csharp
case "binary":  return BinaryExpression(nodeType, type, obj);
case "block":   return BlockExpression(nodeType, type, obj);
case "loop":    return LoopExpression(nodeType, type, obj);
// ...25 arms
```

All 25 handlers therefore take `(ExpressionType nodeType, Type type, JObject obj)`. Some
need `type`, some reconstruct it from the payload and don't. Trimming the parameters the
individual handlers happen not to read would make the dispatch table heterogeneous — each
arm would need a different argument list — for no gain. The uniform signature is the
design.

## e2 — `csharpsquid:S3011` "Make sure that this accessibility bypass is safe here" (4 issues)

Scope: `**/Deserializer.Reflection.cs`

All four sites are `BindingFlags.NonPublic` lookups used to resolve constructors, methods,
properties, and members named in the serialized payload. Rehydrating an arbitrary
expression tree requires binding to the exact member the tree referenced, including
non-public ones. Without the bypass the library cannot do its job.

Note the trust boundary this implies: deserializing an expression tree is equivalent to
deserializing code, so payloads must come from a trusted source. That is inherent to the
library, not to these four call sites.

## e3–e5 (`sonarcloud.yml`) + `CA1822` (`.editorconfig`) — "can be marked static"

S2325 is excluded per path in the workflow (`resourceKey` accepts one pattern, so each
path needs its own criterion). CA1822 says the same thing and is suppressed project-wide
in `.editorconfig`, since it only fires in these files anyway:

| Path | `S2325` | `CA1822` |
|------|--------:|---------:|
| `**/Aq.ExpressionJsonSerializer/Deserializer.cs` | 5 | 5 |
| `**/Aq.ExpressionJsonSerializer/Deserializer/*.cs` | 9 | 2 |
| `**/Aq.ExpressionJsonSerializer/Serializer/*.cs` | 7 | 7 |

The `Serializer` and `Deserializer` partials are families of small handlers with uniform
signatures, spread one-per-file across ~20 files. Some touch `_writer` / `_serializer`,
some don't. Making the subset that doesn't `static` would split a deliberately uniform
family into two kinds of method for no functional benefit, and would widen the diff
against upstream `aquilae/expression-json-serializer` across most files in the project.

Deliberately **not** excluded: `ExpressionJsonConverter.cs` (the public entry point) and
the root `Serializer.cs`. Neither holds any of these findings today, and neither is part
of a handler family, so both keep full signal — a future "can be marked static" there is
worth seeing rather than masking. An earlier revision of this file used a single
`**/Aq.ExpressionJsonSerializer/**` pattern per rule, which covered them for no reason.

## `CA1861` (`.editorconfig`) — "prefer static readonly over constant array"

Scope: the test project (`Aq.ExpressionJsonSerializer.Tests/**.cs`).

The flagged literal is inside the expression under test:

```csharp
TestExpression((Expression<Func<Context, int[]>>) (c => new[] { 0 }));
```

The point of `InitArray` is to serialize a `NewArrayInit` node. Hoisting the array to a
field replaces that node with a field access and the test stops testing anything.

## `CA1859` (`.editorconfig`) — "narrow return type for performance"

Scope: the test project (`Aq.ExpressionJsonSerializer.Tests/**.cs`).

`Context.Method3()` returns `object` deliberately so `MethodResultCast` can exercise an
unbox/convert node:

```csharp
public object Method3() { return this.A; }
// ...
TestExpression((Expression<Func<Context, int>>) (c => (int) c.Method3()));
```

Narrowing the return type to `int` removes the cast, and with it the node under test.
