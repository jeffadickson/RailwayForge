using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RailwayForge;

/// <summary>
/// Zero-boxing Errors-as-Values union type implementing the C# 15 <see cref="UnionAttribute"/>
/// and <see cref="IUnion"/> protocol.
/// <para>
/// Stores typed <typeparamref name="T"/> and <typeparamref name="TError"/> fields directly on the stack
/// (eliminating value-type boxing on hot paths) while supporting C# 15 implicit conversions from
/// <typeparamref name="T"/> / <typeparamref name="TError"/> and compiler-checked exhaustive
/// <c>switch</c> expressions.
/// </para>
/// </summary>
[Union]
public readonly struct Result<T, TError> : IUnion, IEquatable<Result<T, TError>>
{
    private const byte UninitializedState = 0;
    private const byte OkState = 1;
    private const byte ErrorState = 2;

    private readonly byte _state;
    private readonly T _value;
    private readonly TError _error;

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    public Result(T value)
    {
        _state = OkState;
        _value = value;
        _error = default!;
    }

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    public Result(TError error)
    {
        _state = ErrorState;
        _value = default!;
        _error = error;
    }

    /// <summary>True when this union was initialized (either Ok or Error); false for default(Result).</summary>
    public bool HasValue => _state != UninitializedState;

    /// <summary>True when the result carries a success value of type <typeparamref name="T"/>.</summary>
    public bool IsOk => _state == OkState;

    /// <summary>True when the result carries an error value of type <typeparamref name="TError"/>.</summary>
    public bool IsError => _state == ErrorState;

    /// <summary>True when the struct was constructed via <c>default</c> or parameterless <c>new()</c>.</summary>
    public bool IsUninitialized => _state == UninitializedState;

    /// <inheritdoc />
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public object? Value => _state switch
    {
        OkState => _value,
        ErrorState => _error,
        _ => null,
    };

    /// <summary>
    /// C# 15 <see cref="UnionAttribute"/> zero-boxing pattern-matching accessor for <typeparamref name="T"/>.
    /// </summary>
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
    /// C# 15 <see cref="UnionAttribute"/> zero-boxing pattern-matching accessor for <typeparamref name="TError"/>.
    /// </summary>
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
    public static implicit operator Result<T, TError>(T value) => new(value);

    /// <summary>Implicitly wraps an error value in <see cref="Result{T, TError}"/>.</summary>
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

    public static bool operator ==(Result<T, TError> left, Result<T, TError> right) => left.Equals(right);
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
/// implementing C# 15 <see cref="UnionAttribute"/> and <see cref="IUnion"/>.
/// </summary>
[Union]
public readonly struct Result<TError> : IUnion, IEquatable<Result<TError>>
{
    private const byte UninitializedState = 0;
    private const byte OkState = 1;
    private const byte ErrorState = 2;

    private readonly byte _state;
    private readonly TError _error;

    /// <summary>Creates a successful unit result.</summary>
    public Result(Unit _)
    {
        _state = OkState;
        _error = default!;
    }

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    public Result(TError error)
    {
        _state = ErrorState;
        _error = error;
    }

    public bool HasValue => _state != UninitializedState;
    public bool IsOk => _state == OkState;
    public bool IsError => _state == ErrorState;
    public bool IsUninitialized => _state == UninitializedState;

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public object? Value => _state switch
    {
        OkState => Unit.Value,
        ErrorState => _error,
        _ => null,
    };

    public bool TryGetValue(out Unit value)
    {
        value = Unit.Value;
        return _state == OkState;
    }

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

    public bool TryGetError([MaybeNullWhen(false)] out TError error) => TryGetValue(out error);

    internal TError UnsafeError => _error;

    public static implicit operator Result<TError>(Unit unit) => new(unit);
    public static implicit operator Result<TError>(TError error) => new(error);

    public bool Equals(Result<TError> other) => _state == other._state && _state switch
    {
        ErrorState => EqualityComparer<TError>.Default.Equals(_error, other._error),
        _ => true,
    };

    public override bool Equals(object? obj) => obj is Result<TError> other && Equals(other);

    public override int GetHashCode() => _state switch
    {
        OkState => OkState,
        ErrorState => HashCode.Combine(ErrorState, _error),
        _ => 0,
    };

    public static bool operator ==(Result<TError> left, Result<TError> right) => left.Equals(right);
    public static bool operator !=(Result<TError> left, Result<TError> right) => !left.Equals(right);

    public override string ToString() => _state switch
    {
        OkState => "Ok(())",
        ErrorState => $"Error({_error})",
        _ => "Uninitialized",
    };
}

/// <summary>
/// Explicit constructors and exception-boundary adapters for <see cref="Result{T, TError}"/>
/// and <see cref="Result{TError}"/>.
/// </summary>
public static partial class Result
{
    public static Result<T, TError> Ok<T, TError>(T value) => new(value);
    public static Result<T, TError> Fail<T, TError>(TError error) => new(error);
    public static Result<TError> Ok<TError>() => new(Unit.Value);
    public static Result<TError> Fail<TError>(TError error) => new(error);

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
}
