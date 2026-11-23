# RailwayForge

**Zero-boxing `.NET 11 / C# 15` Errors-as-Values & Railway-Oriented Programming suite**, backed by native C# 15 `[Union]` structs, Roslyn compile-time `#[must_use]` analyzers, RFC 9457 ASP.NET Core `ProblemDetails`, zero-dependency OpenTelemetry instrumentation, and **Dafny-verified monad & functor laws**.

## Why RailwayForge?

C# 15 introduces native `union` types and `closed` hierarchies—but raw syntactic `union Result<T, E>(T, E)` declarations still leave critical production gaps:
1. **Value-type boxing:** Syntactic `union` types store values in `object?`, allocating on every hot-path `Result<int, TError>`. RailwayForge hand-implements C# 15's `[Union]` / `IUnion` protocol with typed struct fields and `TryGetValue` accessors: **0 bytes allocated** across 1M+ iterations while retaining native C# 15 implicit conversions and exhaustive `switch` checking.
2. **Silent discards (`RF0001`) & default unions (`RF0002`):** `RailwayForge.Analyzers` catches ignored `Result` return values, uninitialized `default`/`new()` unions, and ambiguous `Result<T, T>` (`RF0003`) at compile time.
3. **Sync, `Task`, and `ValueTask` railway combinators:** C# 15 extension blocks for `Map`, `Bind`, `MapError`, `Ensure`, `Tap`, `TapError`, `Recover`, `OrElse`, `Match`, `Combine`, `Collect`, `Partition`, `TraverseAsync`, `ValidationBag<TError>`, and `Result.Try`/`TryAsync`.
4. **Formally verified algebra:** [`formal/RailwayLaws.dfy`](formal/RailwayLaws.dfy) proves Functor, Monad, Short-Circuit, Guard, Recovery, and Multi-Error Accumulation soundness in Dafny 4.11 (**27 verified, 0 errors**).

## Packages

| Package | Dependencies | Purpose |
|---|---|---|
| `RailwayForge` | **Zero** | Zero-boxing `[Union]` `Result<T, TError>`, `Result<TError>`, `Unit`, combinators, `Combine`/`Collect`/`ValidationBag`, optional `closed ForgeError` |
| `RailwayForge.Analyzers` | Roslyn (dev only) | Compile-time diagnostics `RF0001` (must-use), `RF0002` (no default), `RF0003` (no `Result<T, T>`) |
| `RailwayForge.AspNetCore` | `Microsoft.AspNetCore.App` | Exhaustive `ForgeError` & `IHttpError` → RFC 9457 `ProblemDetails` / Minimal API `IResult` |
| `RailwayForge.OpenTelemetry` | **Zero** (BCL `System.Diagnostics`) | `ActivitySource.Trace`/`TraceAsync` span error tagging + `Meter("RailwayForge")` counters & latency histograms |

## Quick Start

```csharp
using RailwayForge;

public closed record class OrderError(string Code, string Message) : IForgeError
{
    public sealed record class NotFound(string Id) : OrderError("NOT_FOUND", $"Order '{Id}' not found.");
    public sealed record class InvalidAmount(decimal Amount) : OrderError("INVALID_AMOUNT", $"Amount {Amount} must be > 0.");
}

static Result<decimal, OrderError> ValidateOrder(string id, decimal amount)
{
    if (string.IsNullOrWhiteSpace(id)) return new OrderError.NotFound(id);
    if (amount <= 0) return new OrderError.InvalidAmount(amount);
    return amount;
}

// Native C# 15 exhaustive switch — zero boxing, zero wildcard required!
string message = ValidateOrder("ord-1", 49.95m)
    .Map(a => a * 1.08m)
    .Ensure(total => total < 10_000m, total => new OrderError.InvalidAmount(total))
    switch
    {
        decimal total => $"Charged {total:C}",
        OrderError.NotFound e => $"404: {e.Message}",
        OrderError.InvalidAmount e => $"400: {e.Message}",
    };
```

### Multi-Error Validation (`ValidationBag<TError>`)

```csharp
var bag = new ValidationBag<ForgeError>();
bag.Check(!string.IsNullOrWhiteSpace(email), new ForgeError.Validation("email", "Email is required."))
   .Check(age >= 18, new ForgeError.Validation("age", "Must be 18 or older."));

Result<User, IReadOnlyList<ForgeError>> userResult = bag.Build(() => new User(email, age));
```

### ASP.NET Core Minimal APIs (`RailwayForge.AspNetCore`)

```csharp
app.MapGet("/orders/{id}", (string id) =>
    GetOrder(id).ToHttpResult()); // 200 OK or exhaustive RFC 9457 ProblemDetails
```

### OpenTelemetry (`RailwayForge.OpenTelemetry`)

```csharp
var result = await activitySource.TraceAsync("orders.charge", () => ChargeOrderAsync(id));
```

## Analyzer Rules (`RailwayForge.Analyzers`)

| Rule ID | Severity | Description |
|---|---|---|
| `RF0001` | Warning | Result value is discarded without inspection (use `_ = ...` to discard intentionally) |
| `RF0002` | Warning | Uninitialized `default` or parameterless `new()` construction of a Result union |
| `RF0003` | Warning | `Result<T, TError>` uses identical types for `T` and `TError` (ambiguous union matching) |

## Build, Test, Benchmark & Formal Verification

```bash
dotnet build RailwayForge.sln -c Release /warnaserror
dotnet test RailwayForge.sln -c Release --no-build
dotnet run -c Release --project benchmarks/RailwayForge.Bench
dafny verify formal/RailwayLaws.dfy
```
