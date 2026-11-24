using System.Runtime.CompilerServices;

namespace RailwayForge;

/// <summary>
/// C# 15 extension blocks providing zero-boxing synchronous, state-passing, <see cref="Task{TResult}"/>,
/// and <see cref="ValueTask{TResult}"/> railway combinators verified against <c>formal/RailwayLaws.dfy</c>.
/// </summary>
public static class ResultCombinators
{
    private static InvalidOperationException UninitializedException(string typeName) =>
        new($"Uninitialized default({typeName}). Construct results from an explicit value or error.");

    extension<T, TError>(Result<T, TError> result)
    {
        /// <summary>Projects either the success value or the error into <typeparamref name="TOut"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TOut Match<TOut>(Func<T, TOut> onOk, Func<TError, TOut> onError)
        {
            if (result.IsOk) return onOk(result.UnsafeValue);
            if (result.IsError) return onError(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Closure-free state-passing overload of <c>Match</c> for zero-allocation hot loops.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TOut Match<TState, TOut>(TState state, Func<T, TState, TOut> onOk, Func<TError, TState, TOut> onError)
        {
            if (result.IsOk) return onOk(result.UnsafeValue, state);
            if (result.IsError) return onError(result.UnsafeError, state);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Invokes either <paramref name="onOk"/> or <paramref name="onError"/> for side effects.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Switch(Action<T> onOk, Action<TError> onError)
        {
            if (result.IsOk) { onOk(result.UnsafeValue); return; }
            if (result.IsError) { onError(result.UnsafeError); return; }
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Transforms the success value via <paramref name="map"/>, short-circuiting on error.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TOut, TError> Map<TOut>(Func<T, TOut> map)
        {
            if (result.IsOk) return new Result<TOut, TError>(map(result.UnsafeValue));
            if (result.IsError) return new Result<TOut, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Closure-free state-passing overload of <c>Map</c> for zero-allocation hot loops.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TOut, TError> Map<TState, TOut>(TState state, Func<T, TState, TOut> map)
        {
            if (result.IsOk) return new Result<TOut, TError>(map(result.UnsafeValue, state));
            if (result.IsError) return new Result<TOut, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Transforms the error value via <paramref name="mapError"/> while preserving any success value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> mapError)
        {
            if (result.IsOk) return new Result<T, TNewError>(result.UnsafeValue);
            if (result.IsError) return new Result<T, TNewError>(mapError(result.UnsafeError));
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Monadic bind chaining a result-producing operation on the success value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TOut, TError> Bind<TOut>(Func<T, Result<TOut, TError>> bind)
        {
            if (result.IsOk) return bind(result.UnsafeValue);
            if (result.IsError) return new Result<TOut, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Closure-free state-passing overload of <c>Bind</c> for zero-allocation hot loops.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TOut, TError> Bind<TState, TOut>(TState state, Func<T, TState, Result<TOut, TError>> bind)
        {
            if (result.IsOk) return bind(result.UnsafeValue, state);
            if (result.IsError) return new Result<TOut, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Monadic bind chaining a void-equivalent <see cref="Result{TError}"/> operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TError> Bind(Func<T, Result<TError>> bind)
        {
            if (result.IsOk) return bind(result.UnsafeValue);
            if (result.IsError) return new Result<TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Validates <paramref name="predicate"/> on a success value, converting failure via <paramref name="onFail"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T, TError> Ensure(Func<T, bool> predicate, Func<T, TError> onFail)
        {
            if (result.IsOk)
                return predicate(result.UnsafeValue) ? result : new Result<T, TError>(onFail(result.UnsafeValue));
            if (result.IsError) return result;
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Closure-free state-passing overload of <c>Ensure</c> for zero-allocation hot loops.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T, TError> Ensure<TState>(TState state, Func<T, TState, bool> predicate, Func<T, TState, TError> onFail)
        {
            if (result.IsOk)
                return predicate(result.UnsafeValue, state) ? result : new Result<T, TError>(onFail(result.UnsafeValue, state));
            if (result.IsError) return result;
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Validates <paramref name="predicate"/> on a success value, returning <paramref name="error"/> on failure.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T, TError> Ensure(Func<T, bool> predicate, TError error)
        {
            if (result.IsOk)
                return predicate(result.UnsafeValue) ? result : new Result<T, TError>(error);
            if (result.IsError) return result;
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Executes <paramref name="action"/> when successful and returns the original result unchanged.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T, TError> Tap(Action<T> action)
        {
            if (result.IsOk) action(result.UnsafeValue);
            else if (result.IsUninitialized) throw UninitializedException(nameof(Result<T, TError>));
            return result;
        }

        /// <summary>Executes <paramref name="action"/> when failed and returns the original result unchanged.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T, TError> TapError(Action<TError> action)
        {
            if (result.IsError) action(result.UnsafeError);
            else if (result.IsUninitialized) throw UninitializedException(nameof(Result<T, TError>));
            return result;
        }

        /// <summary>Returns the success value or computes a fallback value from the error.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Recover(Func<TError, T> fallback)
        {
            if (result.IsOk) return result.UnsafeValue;
            if (result.IsError) return fallback(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Returns the success value or <paramref name="fallbackValue"/> when failed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Recover(T fallbackValue)
        {
            if (result.IsOk) return result.UnsafeValue;
            if (result.IsError) return fallbackValue;
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Attempts recovery from an error by producing a replacement <see cref="Result{T, TNewError}"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T, TNewError> OrElse<TNewError>(Func<TError, Result<T, TNewError>> recover)
        {
            if (result.IsOk) return new Result<T, TNewError>(result.UnsafeValue);
            if (result.IsError) return recover(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Returns the success value or throws the exception produced by <paramref name="toException"/> at throwing API boundaries.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T OrThrow(Func<TError, Exception> toException)
        {
            if (result.IsOk) return result.UnsafeValue;
            if (result.IsError) throw toException(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Discards the success payload, converting <see cref="Result{T, TError}"/> to <see cref="Result{TError}"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TError> DiscardValue()
        {
            if (result.IsOk) return new Result<TError>(Unit.Value);
            if (result.IsError) return new Result<TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Asynchronously maps a success value via <paramref name="mapAsync"/>.</summary>
        public async Task<Result<TOut, TError>> MapAsync<TOut>(Func<T, Task<TOut>> mapAsync)
        {
            if (result.IsOk) return new Result<TOut, TError>(await mapAsync(result.UnsafeValue).ConfigureAwait(false));
            if (result.IsError) return new Result<TOut, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Asynchronously binds a success value via <paramref name="bindAsync"/>.</summary>
        public async Task<Result<TOut, TError>> BindAsync<TOut>(Func<T, Task<Result<TOut, TError>>> bindAsync)
        {
            if (result.IsOk) return await bindAsync(result.UnsafeValue).ConfigureAwait(false);
            if (result.IsError) return new Result<TOut, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Asynchronously invokes <paramref name="actionAsync"/> on success and returns the original result.</summary>
        public async Task<Result<T, TError>> TapAsync(Func<T, Task> actionAsync)
        {
            if (result.IsOk) await actionAsync(result.UnsafeValue).ConfigureAwait(false);
            else if (result.IsUninitialized) throw UninitializedException(nameof(Result<T, TError>));
            return result;
        }
    }

    extension<TError>(Result<TError> result)
    {
        /// <summary>Projects either success or error into <typeparamref name="TOut"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TOut Match<TOut>(Func<TOut> onOk, Func<TError, TOut> onError)
        {
            if (result.IsOk) return onOk();
            if (result.IsError) return onError(result.UnsafeError);
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Invokes either <paramref name="onOk"/> or <paramref name="onError"/> for side effects.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Switch(Action onOk, Action<TError> onError)
        {
            if (result.IsOk) { onOk(); return; }
            if (result.IsError) { onError(result.UnsafeError); return; }
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Produces a value result by invoking <paramref name="map"/> on success.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T, TError> Map<T>(Func<T> map)
        {
            if (result.IsOk) return new Result<T, TError>(map());
            if (result.IsError) return new Result<T, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Transforms the error via <paramref name="mapError"/> while preserving success.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TNewError> MapError<TNewError>(Func<TError, TNewError> mapError)
        {
            if (result.IsOk) return new Result<TNewError>(Unit.Value);
            if (result.IsError) return new Result<TNewError>(mapError(result.UnsafeError));
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Chains a value-producing result operation on success.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<T, TError> Bind<T>(Func<Result<T, TError>> bind)
        {
            if (result.IsOk) return bind();
            if (result.IsError) return new Result<T, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Chains another void-equivalent result operation on success.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TError> Bind(Func<Result<TError>> bind)
        {
            if (result.IsOk) return bind();
            if (result.IsError) return result;
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Validates <paramref name="predicate"/> on success, returning <paramref name="error"/> if false.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TError> Ensure(Func<bool> predicate, TError error)
        {
            if (result.IsOk) return predicate() ? result : new Result<TError>(error);
            if (result.IsError) return result;
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Validates <paramref name="predicate"/> on success, lazily creating an error if false.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TError> Ensure(Func<bool> predicate, Func<TError> onFail)
        {
            if (result.IsOk) return predicate() ? result : new Result<TError>(onFail());
            if (result.IsError) return result;
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Executes <paramref name="action"/> when successful and returns the original result unchanged.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TError> Tap(Action action)
        {
            if (result.IsOk) action();
            else if (result.IsUninitialized) throw UninitializedException(nameof(Result<TError>));
            return result;
        }

        /// <summary>Executes <paramref name="action"/> when failed and returns the original result unchanged.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TError> TapError(Action<TError> action)
        {
            if (result.IsError) action(result.UnsafeError);
            else if (result.IsUninitialized) throw UninitializedException(nameof(Result<TError>));
            return result;
        }

        /// <summary>Attempts recovery from an error by producing a replacement <see cref="Result{TNewError}"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Result<TNewError> OrElse<TNewError>(Func<TError, Result<TNewError>> recover)
        {
            if (result.IsOk) return new Result<TNewError>(Unit.Value);
            if (result.IsError) return recover(result.UnsafeError);
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Throws the exception produced by <paramref name="toException"/> when failed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OrThrow(Func<TError, Exception> toException)
        {
            if (result.IsOk) return;
            if (result.IsError) throw toException(result.UnsafeError);
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Asynchronously maps a void-equivalent success into a value result.</summary>
        public async Task<Result<T, TError>> MapAsync<T>(Func<Task<T>> mapAsync)
        {
            if (result.IsOk) return new Result<T, TError>(await mapAsync().ConfigureAwait(false));
            if (result.IsError) return new Result<T, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Asynchronously binds a void-equivalent success into a value result.</summary>
        public async Task<Result<T, TError>> BindAsync<T>(Func<Task<Result<T, TError>>> bindAsync)
        {
            if (result.IsOk) return await bindAsync().ConfigureAwait(false);
            if (result.IsError) return new Result<T, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Asynchronously binds a void-equivalent success into another void-equivalent result.</summary>
        public async Task<Result<TError>> BindAsync(Func<Task<Result<TError>>> bindAsync)
        {
            if (result.IsOk) return await bindAsync().ConfigureAwait(false);
            if (result.IsError) return result;
            throw UninitializedException(nameof(Result<TError>));
        }
    }

    extension<T, TError>(Task<Result<T, TError>> task)
    {
        /// <summary>Maps the awaited result's success value synchronously.</summary>
        public async Task<Result<TOut, TError>> MapAsync<TOut>(Func<T, TOut> map) =>
            (await task.ConfigureAwait(false)).Map(map);

        /// <summary>Maps the awaited result's success value asynchronously.</summary>
        public async Task<Result<TOut, TError>> MapAsync<TOut>(Func<T, Task<TOut>> mapAsync) =>
            await (await task.ConfigureAwait(false)).MapAsync(mapAsync).ConfigureAwait(false);

        /// <summary>Maps the awaited result's error value.</summary>
        public async Task<Result<T, TNewError>> MapErrorAsync<TNewError>(Func<TError, TNewError> mapError) =>
            (await task.ConfigureAwait(false)).MapError(mapError);

        /// <summary>Binds the awaited result's success value synchronously.</summary>
        public async Task<Result<TOut, TError>> BindAsync<TOut>(Func<T, Result<TOut, TError>> bind) =>
            (await task.ConfigureAwait(false)).Bind(bind);

        /// <summary>Binds the awaited result's success value asynchronously.</summary>
        public async Task<Result<TOut, TError>> BindAsync<TOut>(Func<T, Task<Result<TOut, TError>>> bindAsync) =>
            await (await task.ConfigureAwait(false)).BindAsync(bindAsync).ConfigureAwait(false);

        /// <summary>Ensures <paramref name="predicate"/> holds on the awaited result.</summary>
        public async Task<Result<T, TError>> EnsureAsync(Func<T, bool> predicate, Func<T, TError> onFail) =>
            (await task.ConfigureAwait(false)).Ensure(predicate, onFail);

        /// <summary>Executes <paramref name="action"/> on the awaited success value.</summary>
        public async Task<Result<T, TError>> TapAsync(Action<T> action) =>
            (await task.ConfigureAwait(false)).Tap(action);

        /// <summary>Executes <paramref name="action"/> on the awaited error value.</summary>
        public async Task<Result<T, TError>> TapErrorAsync(Action<TError> action) =>
            (await task.ConfigureAwait(false)).TapError(action);

        /// <summary>Matches the awaited result into <typeparamref name="TOut"/>.</summary>
        public async Task<TOut> MatchAsync<TOut>(Func<T, TOut> onOk, Func<TError, TOut> onError) =>
            (await task.ConfigureAwait(false)).Match(onOk, onError);

        /// <summary>Recovers a fallback value from an awaited failed result.</summary>
        public async Task<T> RecoverAsync(Func<TError, T> fallback) =>
            (await task.ConfigureAwait(false)).Recover(fallback);

        /// <summary>Unwraps the awaited result or throws via <paramref name="toException"/>.</summary>
        public async Task<T> OrThrowAsync(Func<TError, Exception> toException) =>
            (await task.ConfigureAwait(false)).OrThrow(toException);
    }

    extension<TError>(Task<Result<TError>> task)
    {
        /// <summary>Maps an awaited void-equivalent success into a value result.</summary>
        public async Task<Result<T, TError>> MapAsync<T>(Func<T> map) =>
            (await task.ConfigureAwait(false)).Map(map);

        /// <summary>Maps an awaited void-equivalent error value.</summary>
        public async Task<Result<TNewError>> MapErrorAsync<TNewError>(Func<TError, TNewError> mapError) =>
            (await task.ConfigureAwait(false)).MapError(mapError);

        /// <summary>Binds an awaited void-equivalent success into a value result.</summary>
        public async Task<Result<T, TError>> BindAsync<T>(Func<Result<T, TError>> bind) =>
            (await task.ConfigureAwait(false)).Bind(bind);

        /// <summary>Binds an awaited void-equivalent success asynchronously into another void-equivalent result.</summary>
        public async Task<Result<TError>> BindAsync(Func<Task<Result<TError>>> bindAsync) =>
            await (await task.ConfigureAwait(false)).BindAsync(bindAsync).ConfigureAwait(false);

        /// <summary>Executes <paramref name="action"/> when the awaited result succeeded.</summary>
        public async Task<Result<TError>> TapAsync(Action action) =>
            (await task.ConfigureAwait(false)).Tap(action);

        /// <summary>Executes <paramref name="action"/> when the awaited result failed.</summary>
        public async Task<Result<TError>> TapErrorAsync(Action<TError> action) =>
            (await task.ConfigureAwait(false)).TapError(action);

        /// <summary>Matches the awaited void-equivalent result into <typeparamref name="TOut"/>.</summary>
        public async Task<TOut> MatchAsync<TOut>(Func<TOut> onOk, Func<TError, TOut> onError) =>
            (await task.ConfigureAwait(false)).Match(onOk, onError);

        /// <summary>Throws via <paramref name="toException"/> when the awaited void-equivalent result failed.</summary>
        public async Task OrThrowAsync(Func<TError, Exception> toException) =>
            (await task.ConfigureAwait(false)).OrThrow(toException);
    }

    extension<T, TError>(ValueTask<Result<T, TError>> task)
    {
        /// <summary>Maps the awaited <see cref="ValueTask{TResult}"/> success value synchronously.</summary>
        public async ValueTask<Result<TOut, TError>> MapAsync<TOut>(Func<T, TOut> map) =>
            (await task.ConfigureAwait(false)).Map(map);

        /// <summary>Maps the awaited <see cref="ValueTask{TResult}"/> error value.</summary>
        public async ValueTask<Result<T, TNewError>> MapErrorAsync<TNewError>(Func<TError, TNewError> mapError) =>
            (await task.ConfigureAwait(false)).MapError(mapError);

        /// <summary>Binds the awaited <see cref="ValueTask{TResult}"/> success value synchronously.</summary>
        public async ValueTask<Result<TOut, TError>> BindAsync<TOut>(Func<T, Result<TOut, TError>> bind) =>
            (await task.ConfigureAwait(false)).Bind(bind);

        /// <summary>Binds the awaited <see cref="ValueTask{TResult}"/> success value asynchronously.</summary>
        public async ValueTask<Result<TOut, TError>> BindAsync<TOut>(Func<T, ValueTask<Result<TOut, TError>>> bindAsync)
        {
            var r = await task.ConfigureAwait(false);
            if (r.IsOk) return await bindAsync(r.UnsafeValue).ConfigureAwait(false);
            if (r.IsError) return new Result<TOut, TError>(r.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        /// <summary>Ensures <paramref name="predicate"/> holds on the awaited <see cref="ValueTask{TResult}"/> result.</summary>
        public async ValueTask<Result<T, TError>> EnsureAsync(Func<T, bool> predicate, Func<T, TError> onFail) =>
            (await task.ConfigureAwait(false)).Ensure(predicate, onFail);

        /// <summary>Executes <paramref name="action"/> on the awaited <see cref="ValueTask{TResult}"/> success value.</summary>
        public async ValueTask<Result<T, TError>> TapAsync(Action<T> action) =>
            (await task.ConfigureAwait(false)).Tap(action);

        /// <summary>Executes <paramref name="action"/> on the awaited <see cref="ValueTask{TResult}"/> error value.</summary>
        public async ValueTask<Result<T, TError>> TapErrorAsync(Action<TError> action) =>
            (await task.ConfigureAwait(false)).TapError(action);

        /// <summary>Matches the awaited <see cref="ValueTask{TResult}"/> result into <typeparamref name="TOut"/>.</summary>
        public async ValueTask<TOut> MatchAsync<TOut>(Func<T, TOut> onOk, Func<TError, TOut> onError) =>
            (await task.ConfigureAwait(false)).Match(onOk, onError);

        /// <summary>Recovers a fallback value from a failed <see cref="ValueTask{TResult}"/> result.</summary>
        public async ValueTask<T> RecoverAsync(Func<TError, T> fallback) =>
            (await task.ConfigureAwait(false)).Recover(fallback);

        /// <summary>Unwraps the awaited <see cref="ValueTask{TResult}"/> result or throws via <paramref name="toException"/>.</summary>
        public async ValueTask<T> OrThrowAsync(Func<TError, Exception> toException) =>
            (await task.ConfigureAwait(false)).OrThrow(toException);
    }

    extension<TError>(ValueTask<Result<TError>> task)
    {
        /// <summary>Maps an awaited void-equivalent <see cref="ValueTask{TResult}"/> error value.</summary>
        public async ValueTask<Result<TNewError>> MapErrorAsync<TNewError>(Func<TError, TNewError> mapError) =>
            (await task.ConfigureAwait(false)).MapError(mapError);

        /// <summary>Binds an awaited void-equivalent <see cref="ValueTask{TResult}"/> asynchronously.</summary>
        public async ValueTask<Result<TError>> BindAsync(Func<ValueTask<Result<TError>>> bindAsync)
        {
            var r = await task.ConfigureAwait(false);
            if (r.IsOk) return await bindAsync().ConfigureAwait(false);
            if (r.IsError) return r;
            throw UninitializedException(nameof(Result<TError>));
        }

        /// <summary>Matches an awaited void-equivalent <see cref="ValueTask{TResult}"/> result into <typeparamref name="TOut"/>.</summary>
        public async ValueTask<TOut> MatchAsync<TOut>(Func<TOut> onOk, Func<TError, TOut> onError) =>
            (await task.ConfigureAwait(false)).Match(onOk, onError);

        /// <summary>Throws via <paramref name="toException"/> when the awaited void-equivalent <see cref="ValueTask{TResult}"/> failed.</summary>
        public async ValueTask OrThrowAsync(Func<TError, Exception> toException) =>
            (await task.ConfigureAwait(false)).OrThrow(toException);
    }
}
