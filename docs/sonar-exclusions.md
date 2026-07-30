# SonarCloud rule exclusions

Analysis runs from [`.github/workflows/sonarcloud.yml`](../.github/workflows/sonarcloud.yml).
The `sonar.issue.ignore.multicriteria` entries there are recorded decisions that a rule
is wrong for this codebase, not a backlog. Findings that were genuinely fixable were
fixed rather than excluded.

Kept in the workflow rather than marked "Accepted" through the SonarCloud API so the
reasoning lives in version control and survives the project being recreated.

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

## e3–e8 — `csharpsquid:S2325` / `external_roslyn:CA1822` "can be marked static" (35 issues)

Scope, per rule (`resourceKey` accepts one pattern, so each path needs its own criterion):

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

## e9 — `external_roslyn:CA1861` "prefer static readonly over constant array" (1 issue)

Scope: `**/ExpressionJsonSerializerTest.cs`

The flagged literal is inside the expression under test:

```csharp
TestExpression((Expression<Func<Context, int[]>>) (c => new[] { 0 }));
```

The point of `InitArray` is to serialize a `NewArrayInit` node. Hoisting the array to a
field replaces that node with a field access and the test stops testing anything.

## e10 — `external_roslyn:CA1859` "narrow return type for performance" (1 issue)

Scope: `**/ExpressionJsonSerializerTest.cs`

`Context.Method3()` returns `object` deliberately so `MethodResultCast` can exercise an
unbox/convert node:

```csharp
public object Method3() { return this.A; }
// ...
TestExpression((Expression<Func<Context, int>>) (c => (int) c.Method3()));
```

Narrowing the return type to `int` removes the cast, and with it the node under test.
