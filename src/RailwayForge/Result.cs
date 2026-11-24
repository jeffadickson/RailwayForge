using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RailwayForge;

/// <summary>
/// Zero-boxing Errors-as-Values union type implementing the C# 15 <c>[Union]</c>
/// and <c>IUnion</c> protocol on .NET 11+ (with identical zero-boxing stack layout on .NET 10).
/// <para>
/// Stores typed <typeparamref name="T"/> and <typeparamref name="TError"/> fields directly on the stack
/// (eliminating value-type boxing on hot paths) while supporting implicit conversions from
/// <typeparamref name="T"/> / <typeparamref name="TError"/> and compiler-checked exhaustive
/// <c>switch</c> expressions on .NET 11+.
/// </para>
/// </summary>
#if NET11_0_OR_GREATER
[Union]
public readonly struct Result<T, TError> : IUnion, IEquatable<Result<T, TError>>
#else
public readonly struct Result<T, TError> : IEquatable<Result<T, TError>>
#endif
{
    private const byte UninitializedState = 0;
    private const byte OkState = 1;
    private const byte ErrorState = 2;

    private readonly byte _state;
    private readonly T _value;
    private readonly TError _error;

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result(T value)
    {
        _state = OkState;
        _value = value;
        _error = default!;
    }

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result(TError error)
    {
        _state = ErrorState;
        _value = default!;
        _error = error;
    }

    /// <summary>True when this union was initialized (either Ok or Error); false for <c>default(Result&lt;T, TError&gt;)</c>.</summary>
    public bool HasValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state != UninitializedState;
    }

    /// <summary>True when the result carries a success value of type <typeparamref name="T"/>.</summary>
    public bool IsOk
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state == OkState;
    }

    /// <summary>True when the result carries an error value of type <typeparamref name="TError"/>.</summary>
    public bool IsError
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state == ErrorState;
    }

    /// <summary>True when the struct was constructed via <c>default</c> or parameterless <c>new()</c>.</summary>
    public bool IsUninitialized
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state == UninitializedState;
    }

    /// <summary>Boxed union payload for <c>IUnion</c> protocol compatibility; prefer <see cref="TryGetValue(out T)"/> or pattern matching to avoid boxing.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public object? Value => _state switch
    {
        OkState => _value,
        ErrorState => _error,
        _ => null,
    };

    /// <summary>
    /// Zero-boxing pattern-matching accessor for <typeparamref name="T"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        if (_state == OkState)
        {
            value = _value;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// Zero-boxing pattern-matching accessor for <typeparamref name="TError"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue([MaybeNullWhen(false)] out TError error)
    {
        if (_state == ErrorState)
        {
            error = _error;
            return true;
        }
        error = default!;
        return false;
    }

    /// <summary>Extracts either the success value or the error in a single non-boxing call.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue([MaybeNullWhen(false)] out T value, [MaybeNullWhen(true)] out TError error)
    {
        if (_state == OkState)
        {
            value = _value;
            error = default!;
            return true;
        }
        if (_state == ErrorState)
        {
            value = default!;
            error = _error;
            return false;
        }
        throw new InvalidOperationException(
            $"Uninitialized default({nameof(Result<T, TError>)}). Construct results from a value or error.");
    }

    /// <summary>Extracts the error if failed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetError([MaybeNullWhen(false)] out TError error)
    {
        if (_state == ErrorState)
        {
            error = _error;
            return true;
        }
        error = default!;
        return false;
    }

    internal T UnsafeValue => _value;
    internal TError UnsafeError => _error;

    /// <summary>Implicitly wraps a success value in <see cref="Result{T, TError}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<T, TError>(T value) => new(value);

    /// <summary>Implicitly wraps an error value in <see cref="Result{T, TError}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<T, TError>(TError error) => new(error);

    /// <inheritdoc />
    public bool Equals(Result<T, TError> other) => _state == other._state && _state switch
    {
        OkState => EqualityComparer<T>.Default.Equals(_value, other._value),
        ErrorState => EqualityComparer<TError>.Default.Equals(_error, other._error),
        _ => true,
    };

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T, TError> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _state switch
    {
        OkState => HashCode.Combine(OkState, _value),
        ErrorState => HashCode.Combine(ErrorState, _error),
        _ => 0,
    };

    /// <summary>Returns <see langword="true"/> when two results represent the same state and equal payload.</summary>
    public static bool operator ==(Result<T, TError> left, Result<T, TError> right) => left.Equals(right);

    /// <summary>Returns <see langword="true"/> when two results differ in state or payload.</summary>
    public static bool operator !=(Result<T, TError> left, Result<T, TError> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _state switch
    {
        OkState => $"Ok({_value})",
        ErrorState => $"Error({_error})",
        _ => "Uninitialized",
    };
}

/// <summary>
/// Zero-boxing Errors-as-Values union type for void-equivalent side-effecting operations,
/// implementing C# 15 <c>[Union]</c> and <c>IUnion</c> on .NET 11+.
/// </summary>
#if NET11_0_OR_GREATER
[Union]
public readonly struct Result<TError> : IUnion, IEquatable<Result<TError>>
#else
public readonly struct Result<TError> : IEquatable<Result<TError>>
#endif
{
    private const byte UninitializedState = 0;
    private const byte OkState = 1;
    private const byte ErrorState = 2;

    private readonly byte _state;
    private readonly TError _error;

    /// <summary>Creates a successful unit result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result(Unit _)
    {
        _state = OkState;
        _error = default!;
    }

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result(TError error)
    {
        _state = ErrorState;
        _error = error;
    }

    /// <summary>True when this union was initialized (either Ok or Error); false for <c>default(Result&lt;TError&gt;)</c>.</summary>
    public bool HasValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state != UninitializedState;
    }

    /// <summary>True when the operation succeeded.</summary>
    public bool IsOk
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state == OkState;
    }

    /// <summary>True when the operation failed with an error of type <typeparamref name="TError"/>.</summary>
    public bool IsError
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state == ErrorState;
    }

    /// <summary>True when the struct was constructed via <c>default</c> or parameterless <c>new()</c>.</summary>
    public bool IsUninitialized
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state == UninitializedState;
    }

    /// <summary>Boxed union payload for <c>IUnion</c> protocol compatibility.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public object? Value => _state switch
    {
        OkState => Unit.Value,
        ErrorState => _error,
        _ => null,
    };

    /// <summary>Zero-boxing pattern-matching accessor for <see cref="Unit"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(out Unit value)
    {
        value = Unit.Value;
        return _state == OkState;
    }

    /// <summary>Zero-boxing pattern-matching accessor for <typeparamref name="TError"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue([MaybeNullWhen(false)] out TError error)
    {
        if (_state == ErrorState)
        {
            error = _error;
            return true;
        }
        error = default!;
        return false;
    }

    /// <summary>Extracts the error if failed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetError([MaybeNullWhen(false)] out TError error) => TryGetValue(out error);

    internal TError UnsafeError => _error;

    /// <summary>Implicitly wraps <see cref="Unit"/> as a successful <see cref="Result{TError}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TError>(Unit unit) => new(unit);

    /// <summary>Implicitly wraps an error value in <see cref="Result{TError}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Result<TError>(TError error) => new(error);

    /// <inheritdoc />
    public bool Equals(Result<TError> other) => _state == other._state && _state switch
    {
        ErrorState => EqualityComparer<TError>.Default.Equals(_error, other._error),
        _ => true,
    };

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<TError> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _state switch
    {
        OkState => OkState,
        ErrorState => HashCode.Combine(ErrorState, _error),
        _ => 0,
    };

    /// <summary>Returns <see langword="true"/> when two results represent the same state and equal error.</summary>
    public static bool operator ==(Result<TError> left, Result<TError> right) => left.Equals(right);

    /// <summary>Returns <see langword="true"/> when two results differ in state or error.</summary>
    public static bool operator !=(Result<TError> left, Result<TError> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _state switch
    {
        OkState => "Ok(())",
        ErrorState => $"Error({_error})",
        _ => "Uninitialized",
    };
}

/// <summary>
/// Explicit constructors, condition/nullable/<c>Try*</c> adapters, and exception-boundary adapters
/// for <see cref="Result{T, TError}"/> and <see cref="Result{TError}"/>.
/// </summary>
public static partial class Result
{
    /// <summary>Creates a successful <see cref="Result{T, TError}"/> carrying <paramref name="value"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Ok<T, TError>(T value) => new(value);

    /// <summary>Creates a failed <see cref="Result{T, TError}"/> carrying <paramref name="error"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> Fail<T, TError>(TError error) => new(error);

    /// <summary>Creates a successful void-equivalent <see cref="Result{TError}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Ok<TError>() => new(Unit.Value);

    /// <summary>Creates a failed void-equivalent <see cref="Result{TError}"/> carrying <paramref name="error"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> Fail<TError>(TError error) => new(error);

    /// <summary>Returns <see cref="Ok{TError}()"/> when <paramref name="condition"/> is true, otherwise <paramref name="error"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> FromCondition<TError>(bool condition, TError error) =>
        condition ? new Result<TError>(Unit.Value) : new Result<TError>(error);

    /// <summary>Returns <see cref="Ok{TError}()"/> when <paramref name="condition"/> is true, otherwise evaluates <paramref name="errorFactory"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<TError> FromCondition<TError>(bool condition, Func<TError> errorFactory) =>
        condition ? new Result<TError>(Unit.Value) : new Result<TError>(errorFactory());

    /// <summary>Returns <paramref name="value"/> when <paramref name="condition"/> is true, otherwise <paramref name="error"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> FromCondition<T, TError>(bool condition, T value, TError error) =>
        condition ? new Result<T, TError>(value) : new Result<T, TError>(error);

    /// <summary>Evaluates <paramref name="valueFactory"/> when <paramref name="condition"/> is true, otherwise evaluates <paramref name="errorFactory"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> FromCondition<T, TError>(bool condition, Func<T> valueFactory, Func<TError> errorFactory) =>
        condition ? new Result<T, TError>(valueFactory()) : new Result<T, TError>(errorFactory());

    /// <summary>Converts a nullable reference into a <see cref="Result{T, TError}"/>, failing with <paramref name="error"/> when null.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> FromNullable<T, TError>(T? value, TError error)
        where T : class =>
        value is not null ? new Result<T, TError>(value) : new Result<T, TError>(error);

    /// <summary>Converts a nullable reference into a <see cref="Result{T, TError}"/>, lazily constructing an error when null.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> FromNullable<T, TError>(T? value, Func<TError> errorFactory)
        where T : class =>
        value is not null ? new Result<T, TError>(value) : new Result<T, TError>(errorFactory());

    /// <summary>Converts a <see cref="Nullable{T}"/> value into a <see cref="Result{T, TError}"/>, failing with <paramref name="error"/> when null.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> FromNullable<T, TError>(T? value, TError error)
        where T : struct =>
        value.HasValue ? new Result<T, TError>(value.Value) : new Result<T, TError>(error);

    /// <summary>Converts a <see cref="Nullable{T}"/> value into a <see cref="Result{T, TError}"/>, lazily constructing an error when null.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> FromNullable<T, TError>(T? value, Func<TError> errorFactory)
        where T : struct =>
        value.HasValue ? new Result<T, TError>(value.Value) : new Result<T, TError>(errorFactory());

    /// <summary>Adapts an existing <c>bool Try*(..., out T value)</c> outcome into a <see cref="Result{T, TError}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> FromTry<T, TError>(bool succeeded, T? value, TError error) =>
        succeeded ? new Result<T, TError>(value!) : new Result<T, TError>(error);

    /// <summary>Adapts an existing <c>bool Try*(..., out T value)</c> outcome into a <see cref="Result{T, TError}"/> with lazy error creation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Result<T, TError> FromTry<T, TError>(bool succeeded, T? value, Func<TError> errorFactory) =>
        succeeded ? new Result<T, TError>(value!) : new Result<T, TError>(errorFactory());

    /// <summary>Executes <paramref name="func"/> at an exception-throwing boundary, mapping exceptions to <typeparamref name="TError"/>.</summary>
    public static Result<T, TError> Try<T, TError>(Func<T> func, Func<Exception, TError> mapException)
    {
        try
        {
            return new Result<T, TError>(func());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Result<T, TError>(mapException(ex));
        }
    }

    /// <summary>Executes <paramref name="action"/> at an exception-throwing boundary, mapping exceptions to <typeparamref name="TError"/>.</summary>
    public static Result<TError> Try<TError>(Action action, Func<Exception, TError> mapException)
    {
        try
        {
            action();
            return new Result<TError>(Unit.Value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Result<TError>(mapException(ex));
        }
    }

    /// <summary>Executes an async operation at an exception-throwing boundary, preserving cancellation.</summary>
    public static async Task<Result<T, TError>> TryAsync<T, TError>(
        Func<Task<T>> func, Func<Exception, TError> mapException)
    {
        try
        {
            return new Result<T, TError>(await func().ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Result<T, TError>(mapException(ex));
        }
    }

    /// <summary>Executes an async void-equivalent operation at an exception-throwing boundary, preserving cancellation.</summary>
    public static async Task<Result<TError>> TryAsync<TError>(
        Func<Task> action, Func<Exception, TError> mapException)
    {
        try
        {
            await action().ConfigureAwait(false);
            return new Result<TError>(Unit.Value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Result<TError>(mapException(ex));
        }
    }

    /// <summary>Executes a <see cref="ValueTask{TResult}"/> operation at an exception-throwing boundary, preserving cancellation.</summary>
    public static async ValueTask<Result<T, TError>> TryValueTaskAsync<T, TError>(
        Func<ValueTask<T>> func, Func<Exception, TError> mapException)
    {
        try
        {
            return new Result<T, TError>(await func().ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Result<T, TError>(mapException(ex));
        }
    }

    /// <summary>Executes a void-equivalent <see cref="ValueTask"/> operation at an exception-throwing boundary, preserving cancellation.</summary>
    public static async ValueTask<Result<TError>> TryValueTaskAsync<TError>(
        Func<ValueTask> action, Func<Exception, TError> mapException)
    {
        try
        {
            await action().ConfigureAwait(false);
            return new Result<TError>(Unit.Value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Result<TError>(mapException(ex));
        }
    }
}
