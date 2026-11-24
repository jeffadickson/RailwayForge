namespace RailwayForge;

/// <summary>
/// Accumulates multiple validation errors before producing a typed <see cref="Result{T, TError}"/>
/// without throwing or short-circuiting on the first invalid field.
/// </summary>
public sealed class ValidationBag<TError>
{
    private readonly List<TError> _errors = [];

    /// <summary>True when at least one error has been recorded.</summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>Number of errors currently recorded in the bag.</summary>
    public int Count => _errors.Count;

    /// <summary>Read-only view of all recorded errors in insertion order.</summary>
    public IReadOnlyList<TError> Errors => _errors;

    /// <summary>Records <paramref name="error"/> when <paramref name="condition"/> is false.</summary>
    public ValidationBag<TError> Check(bool condition, TError error)
    {
        if (!condition) _errors.Add(error);
        return this;
    }

    /// <summary>Lazily constructs and records an error via <paramref name="errorFactory"/> when <paramref name="condition"/> is false.</summary>
    public ValidationBag<TError> Check(bool condition, Func<TError> errorFactory)
    {
        if (!condition) _errors.Add(errorFactory());
        return this;
    }

    /// <summary>Unconditionally records <paramref name="error"/>.</summary>
    public ValidationBag<TError> Add(TError error)
    {
        _errors.Add(error);
        return this;
    }

    /// <summary>Appends all errors from <paramref name="errors"/>.</summary>
    public ValidationBag<TError> AddRange(IEnumerable<TError> errors)
    {
        _errors.AddRange(errors);
        return this;
    }

    /// <summary>Records the error of <paramref name="result"/> if it failed; throws if <paramref name="result"/> is uninitialized.</summary>
    public ValidationBag<TError> Merge(Result<TError> result)
    {
        if (result.TryGetError(out var error)) _errors.Add(error);
        else if (result.IsUninitialized) throw new InvalidOperationException("Uninitialized Result in ValidationBag.Merge.");
        return this;
    }

    /// <summary>Clears all recorded errors so the bag can be reused.</summary>
    public void Clear() => _errors.Clear();

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
