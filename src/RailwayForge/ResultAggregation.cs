namespace RailwayForge;

public static partial class Result
{
    /// <summary>Combines two results, accumulating all errors if either or both fail.</summary>
    public static Result<(T1, T2), IReadOnlyList<TError>> Combine<T1, T2, TError>(
        Result<T1, TError> r1,
        Result<T2, TError> r2)
    {
        List<TError>? errors = null;
        if (r1.TryGetError(out var e1)) (errors ??= []).Add(e1);
        else if (r1.IsUninitialized) throw new InvalidOperationException("Uninitialized Result in Combine.");
        if (r2.TryGetError(out var e2)) (errors ??= []).Add(e2);
        else if (r2.IsUninitialized) throw new InvalidOperationException("Uninitialized Result in Combine.");

        return errors is not null
            ? new Result<(T1, T2), IReadOnlyList<TError>>(errors)
            : new Result<(T1, T2), IReadOnlyList<TError>>((r1.UnsafeValue, r2.UnsafeValue));
    }

    /// <summary>Combines three results, accumulating all errors if any fail.</summary>
    public static Result<(T1, T2, T3), IReadOnlyList<TError>> Combine<T1, T2, T3, TError>(
        Result<T1, TError> r1,
        Result<T2, TError> r2,
        Result<T3, TError> r3)
    {
        List<TError>? errors = null;
        if (r1.TryGetError(out var e1)) (errors ??= []).Add(e1);
        if (r2.TryGetError(out var e2)) (errors ??= []).Add(e2);
        if (r3.TryGetError(out var e3)) (errors ??= []).Add(e3);

        return errors is not null
            ? new Result<(T1, T2, T3), IReadOnlyList<TError>>(errors)
            : new Result<(T1, T2, T3), IReadOnlyList<TError>>((r1.UnsafeValue, r2.UnsafeValue, r3.UnsafeValue));
    }

    /// <summary>Combines four results, accumulating all errors if any fail.</summary>
    public static Result<(T1, T2, T3, T4), IReadOnlyList<TError>> Combine<T1, T2, T3, T4, TError>(
        Result<T1, TError> r1,
        Result<T2, TError> r2,
        Result<T3, TError> r3,
        Result<T4, TError> r4)
    {
        List<TError>? errors = null;
        if (r1.TryGetError(out var e1)) (errors ??= []).Add(e1);
        if (r2.TryGetError(out var e2)) (errors ??= []).Add(e2);
        if (r3.TryGetError(out var e3)) (errors ??= []).Add(e3);
        if (r4.TryGetError(out var e4)) (errors ??= []).Add(e4);

        return errors is not null
            ? new Result<(T1, T2, T3, T4), IReadOnlyList<TError>>(errors)
            : new Result<(T1, T2, T3, T4), IReadOnlyList<TError>>(
                (r1.UnsafeValue, r2.UnsafeValue, r3.UnsafeValue, r4.UnsafeValue));
    }

    /// <summary>
    /// Collects a sequence of results, returning all values in order if every element succeeded,
    /// or all accumulated errors in order if one or more failed (proved complete in formal/RailwayLaws.dfy).
    /// </summary>
    public static Result<IReadOnlyList<T>, IReadOnlyList<TError>> Collect<T, TError>(
        IEnumerable<Result<T, TError>> results)
    {
        List<T> values = [];
        List<TError>? errors = null;
        foreach (var r in results)
        {
            if (r.IsOk)
            {
                if (errors is null) values.Add(r.UnsafeValue);
            }
            else if (r.IsError)
            {
                (errors ??= []).Add(r.UnsafeError);
            }
            else
            {
                throw new InvalidOperationException("Uninitialized Result in Collect.");
            }
        }

        return errors is not null
            ? new Result<IReadOnlyList<T>, IReadOnlyList<TError>>(errors)
            : new Result<IReadOnlyList<T>, IReadOnlyList<TError>>(values);
    }

    /// <summary>Splits a sequence of results into separate lists of successes and errors.</summary>
    public static (IReadOnlyList<T> Values, IReadOnlyList<TError> Errors) Partition<T, TError>(
        IEnumerable<Result<T, TError>> results)
    {
        List<T> values = [];
        List<TError> errors = [];
        foreach (var r in results)
        {
            if (r.IsOk) values.Add(r.UnsafeValue);
            else if (r.IsError) errors.Add(r.UnsafeError);
            else throw new InvalidOperationException("Uninitialized Result in Partition.");
        }
        return (values, errors);
    }

    /// <summary>
    /// Applies an async result-producing projection sequentially across <paramref name="items"/>,
    /// short-circuiting on the first error.
    /// </summary>
    public static async Task<Result<IReadOnlyList<TOut>, TError>> TraverseAsync<TIn, TOut, TError>(
        IEnumerable<TIn> items,
        Func<TIn, CancellationToken, Task<Result<TOut, TError>>> selector,
        CancellationToken ct = default)
    {
        List<TOut> values = [];
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            var res = await selector(item, ct).ConfigureAwait(false);
            if (res.IsError) return new Result<IReadOnlyList<TOut>, TError>(res.UnsafeError);
            if (res.IsUninitialized) throw new InvalidOperationException("Uninitialized Result in TraverseAsync.");
            values.Add(res.UnsafeValue);
        }
        return new Result<IReadOnlyList<TOut>, TError>(values);
    }
}
