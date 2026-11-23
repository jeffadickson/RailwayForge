namespace RailwayForge;

/// <summary>
/// Accumulates multiple validation errors before producing a typed <see cref="Result{T, TError}"/>
/// without throwing or short-circuiting on the first invalid field.
/// </summary>
public sealed class ValidationBag<TError>
{
    private readonly List<TError> _errors = [];

    public bool HasErrors => _errors.Count > 0;
    public IReadOnlyList<TError> Errors => _errors;

    public ValidationBag<TError> Check(bool condition, TError error)
    {
        if (!condition) _errors.Add(error);
        return this;
    }

    public ValidationBag<TError> Check(bool condition, Func<TError> errorFactory)
    {
        if (!condition) _errors.Add(errorFactory());
        return this;
    }

    public ValidationBag<TError> Add(TError error)
    {
        _errors.Add(error);
        return this;
    }

    public ValidationBag<TError> AddRange(IEnumerable<TError> errors)
    {
        _errors.AddRange(errors);
        return this;
    }

    /// <summary>Builds a value if no errors were recorded, or returns all accumulated errors.</summary>
    public Result<T, IReadOnlyList<TError>> Build<T>(Func<T> factory) =>
        _errors.Count == 0
            ? new Result<T, IReadOnlyList<TError>>(factory())
            : new Result<T, IReadOnlyList<TError>>([.. _errors]);

    /// <summary>Returns <see cref="Unit.Value"/> if valid, or all accumulated errors.</summary>
    public Result<IReadOnlyList<TError>> Build() =>
        _errors.Count == 0
            ? new Result<IReadOnlyList<TError>>(Unit.Value)
            : new Result<IReadOnlyList<TError>>([.. _errors]);
}
