using System.Text.Json.Serialization;

namespace RailwayForge;

/// <summary>Optional metadata contract enabling automatic telemetry and HTTP ProblemDetails mapping.</summary>
public interface IForgeError
{
    string Code { get; }
    string Message { get; }
}

/// <summary>
/// Optional batteries-included C# 15 <c>closed</c> error hierarchy with .NET 11
/// <see cref="JsonPolymorphicAttribute.InferClosedTypePolymorphism"/> support.
/// Domain projects may use <see cref="ForgeError"/> directly or define their own closed error hierarchy.
/// </summary>
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record class ForgeError(string Code, string Message) : IForgeError
{
    public sealed record class Validation(string Field, string Message)
        : ForgeError("VALIDATION", Message);

    public sealed record class NotFound(string Resource, string Id, string Message)
        : ForgeError("NOT_FOUND", Message);

    public sealed record class Conflict(string Target, string Message)
        : ForgeError("CONFLICT", Message);

    public sealed record class Unauthorized(string Message)
        : ForgeError("UNAUTHORIZED", Message);

    public sealed record class Forbidden(string Operation, string Message)
        : ForgeError("FORBIDDEN", Message);

    public sealed record class Unavailable(string Dependency, string Message)
        : ForgeError("UNAVAILABLE", Message);

    public sealed record class Unexpected(string Message, string? ExceptionType = null)
        : ForgeError("UNEXPECTED", Message);
}
