using System.Text.Json.Serialization;

namespace RailwayForge;

/// <summary>Optional metadata contract enabling automatic telemetry and HTTP ProblemDetails mapping.</summary>
public interface IForgeError
{
    /// <summary>Stable machine-readable error code (for example, <c>"VALIDATION"</c> or <c>"NOT_FOUND"</c>).</summary>
    string Code { get; }

    /// <summary>Human-readable diagnostic description of the failure.</summary>
    string Message { get; }
}

/// <summary>
/// Optional batteries-included C# 15 <c>closed</c> error hierarchy with .NET 11
/// <c>InferClosedTypePolymorphism</c> (and explicit <see cref="JsonDerivedTypeAttribute"/> fallback on .NET 10).
/// Domain projects may use <see cref="ForgeError"/> directly or define their own closed error hierarchy implementing <see cref="IForgeError"/>.
/// </summary>
#if NET11_0_OR_GREATER
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record class ForgeError(string Code, string Message) : IForgeError
#else
[JsonPolymorphic]
[JsonDerivedType(typeof(Validation), nameof(Validation))]
[JsonDerivedType(typeof(NotFound), nameof(NotFound))]
[JsonDerivedType(typeof(Conflict), nameof(Conflict))]
[JsonDerivedType(typeof(Unauthorized), nameof(Unauthorized))]
[JsonDerivedType(typeof(Forbidden), nameof(Forbidden))]
[JsonDerivedType(typeof(Unavailable), nameof(Unavailable))]
[JsonDerivedType(typeof(Unexpected), nameof(Unexpected))]
public abstract record class ForgeError(string Code, string Message) : IForgeError
#endif
{
    /// <summary>Input or schema validation failure associated with <paramref name="Field"/>.</summary>
    public sealed record class Validation(string Field, string Message)
        : ForgeError("VALIDATION", Message);

    /// <summary>Requested entity or resource was not found.</summary>
    public sealed record class NotFound(string Resource, string Id, string Message)
        : ForgeError("NOT_FOUND", Message);

    /// <summary>State or concurrency conflict on <paramref name="Target"/>.</summary>
    public sealed record class Conflict(string Target, string Message)
        : ForgeError("CONFLICT", Message);

    /// <summary>Authentication is missing or invalid.</summary>
    public sealed record class Unauthorized(string Message)
        : ForgeError("UNAUTHORIZED", Message);

    /// <summary>Authenticated caller is not permitted to perform <paramref name="Operation"/>.</summary>
    public sealed record class Forbidden(string Operation, string Message)
        : ForgeError("FORBIDDEN", Message);

    /// <summary>Required downstream dependency or external tool is unavailable.</summary>
    public sealed record class Unavailable(string Dependency, string Message)
        : ForgeError("UNAVAILABLE", Message);

    /// <summary>Unexpected boundary failure optionally recording the originating <paramref name="ExceptionType"/>.</summary>
    public sealed record class Unexpected(string Message, string? ExceptionType = null)
        : ForgeError("UNEXPECTED", Message);
}
