namespace RailwayForge;

/// <summary>
/// C# 15 extension blocks providing zero-boxing synchronous, <see cref="Task{TResult}"/>,
/// and <see cref="ValueTask{TResult}"/> railway combinators verified against <c>formal/RailwayLaws.dfy</c>.
/// </summary>
public static class ResultCombinators
{
    private static InvalidOperationException UninitializedException(string typeName) =>
        new($"Uninitialized default({typeName}). Construct results from an explicit value or error.");

    extension<T, TError>(Result<T, TError> result)
    {
        public TOut Match<TOut>(Func<T, TOut> onOk, Func<TError, TOut> onError)
        {
            if (result.IsOk) return onOk(result.UnsafeValue);
            if (result.IsError) return onError(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public void Switch(Action<T> onOk, Action<TError> onError)
        {
            if (result.IsOk) { onOk(result.UnsafeValue); return; }
            if (result.IsError) { onError(result.UnsafeError); return; }
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public Result<TOut, TError> Map<TOut>(Func<T, TOut> map)
        {
            if (result.IsOk) return new Result<TOut, TError>(map(result.UnsafeValue));
            if (result.IsError) return new Result<TOut, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> mapError)
        {
            if (result.IsOk) return new Result<T, TNewError>(result.UnsafeValue);
            if (result.IsError) return new Result<T, TNewError>(mapError(result.UnsafeError));
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public Result<TOut, TError> Bind<TOut>(Func<T, Result<TOut, TError>> bind)
        {
            if (result.IsOk) return bind(result.UnsafeValue);
            if (result.IsError) return new Result<TOut, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public Result<TError> Bind(Func<T, Result<TError>> bind)
        {
            if (result.IsOk) return bind(result.UnsafeValue);
            if (result.IsError) return new Result<TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public Result<T, TError> Ensure(Func<T, bool> predicate, Func<T, TError> onFail)
        {
            if (result.IsOk)
                return predicate(result.UnsafeValue) ? result : new Result<T, TError>(onFail(result.UnsafeValue));
            if (result.IsError) return result;
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public Result<T, TError> Ensure(Func<T, bool> predicate, TError error)
        {
            if (result.IsOk)
                return predicate(result.UnsafeValue) ? result : new Result<T, TError>(error);
            if (result.IsError) return result;
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public Result<T, TError> Tap(Action<T> action)
        {
            if (result.IsOk) action(result.UnsafeValue);
            else if (result.IsUninitialized) throw UninitializedException(nameof(Result<T, TError>));
            return result;
        }

        public Result<T, TError> TapError(Action<TError> action)
        {
            if (result.IsError) action(result.UnsafeError);
            else if (result.IsUninitialized) throw UninitializedException(nameof(Result<T, TError>));
            return result;
        }

        public T Recover(Func<TError, T> fallback)
        {
            if (result.IsOk) return result.UnsafeValue;
            if (result.IsError) return fallback(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public T Recover(T fallbackValue)
        {
            if (result.IsOk) return result.UnsafeValue;
            if (result.IsError) return fallbackValue;
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public Result<T, TNewError> OrElse<TNewError>(Func<TError, Result<T, TNewError>> recover)
        {
            if (result.IsOk) return new Result<T, TNewError>(result.UnsafeValue);
            if (result.IsError) return recover(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public Result<TError> DiscardValue()
        {
            if (result.IsOk) return new Result<TError>(Unit.Value);
            if (result.IsError) return new Result<TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public async Task<Result<TOut, TError>> MapAsync<TOut>(Func<T, Task<TOut>> mapAsync)
        {
            if (result.IsOk) return new Result<TOut, TError>(await mapAsync(result.UnsafeValue).ConfigureAwait(false));
            if (result.IsError) return new Result<TOut, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public async Task<Result<TOut, TError>> BindAsync<TOut>(Func<T, Task<Result<TOut, TError>>> bindAsync)
        {
            if (result.IsOk) return await bindAsync(result.UnsafeValue).ConfigureAwait(false);
            if (result.IsError) return new Result<TOut, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public async Task<Result<T, TError>> TapAsync(Func<T, Task> actionAsync)
        {
            if (result.IsOk) await actionAsync(result.UnsafeValue).ConfigureAwait(false);
            else if (result.IsUninitialized) throw UninitializedException(nameof(Result<T, TError>));
            return result;
        }
    }

    extension<TError>(Result<TError> result)
    {
        public TOut Match<TOut>(Func<TOut> onOk, Func<TError, TOut> onError)
        {
            if (result.IsOk) return onOk();
            if (result.IsError) return onError(result.UnsafeError);
            throw UninitializedException(nameof(Result<TError>));
        }

        public Result<T, TError> Map<T>(Func<T> map)
        {
            if (result.IsOk) return new Result<T, TError>(map());
            if (result.IsError) return new Result<T, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<TError>));
        }

        public Result<TNewError> MapError<TNewError>(Func<TError, TNewError> mapError)
        {
            if (result.IsOk) return new Result<TNewError>(Unit.Value);
            if (result.IsError) return new Result<TNewError>(mapError(result.UnsafeError));
            throw UninitializedException(nameof(Result<TError>));
        }

        public Result<T, TError> Bind<T>(Func<Result<T, TError>> bind)
        {
            if (result.IsOk) return bind();
            if (result.IsError) return new Result<T, TError>(result.UnsafeError);
            throw UninitializedException(nameof(Result<TError>));
        }

        public Result<TError> Bind(Func<Result<TError>> bind)
        {
            if (result.IsOk) return bind();
            if (result.IsError) return result;
            throw UninitializedException(nameof(Result<TError>));
        }

        public Result<TError> TapError(Action<TError> action)
        {
            if (result.IsError) action(result.UnsafeError);
            else if (result.IsUninitialized) throw UninitializedException(nameof(Result<TError>));
            return result;
        }
    }

    extension<T, TError>(Task<Result<T, TError>> task)
    {
        public async Task<Result<TOut, TError>> MapAsync<TOut>(Func<T, TOut> map) =>
            (await task.ConfigureAwait(false)).Map(map);

        public async Task<Result<TOut, TError>> MapAsync<TOut>(Func<T, Task<TOut>> mapAsync) =>
            await (await task.ConfigureAwait(false)).MapAsync(mapAsync).ConfigureAwait(false);

        public async Task<Result<T, TNewError>> MapErrorAsync<TNewError>(Func<TError, TNewError> mapError) =>
            (await task.ConfigureAwait(false)).MapError(mapError);

        public async Task<Result<TOut, TError>> BindAsync<TOut>(Func<T, Result<TOut, TError>> bind) =>
            (await task.ConfigureAwait(false)).Bind(bind);

        public async Task<Result<TOut, TError>> BindAsync<TOut>(Func<T, Task<Result<TOut, TError>>> bindAsync) =>
            await (await task.ConfigureAwait(false)).BindAsync(bindAsync).ConfigureAwait(false);

        public async Task<Result<T, TError>> EnsureAsync(Func<T, bool> predicate, Func<T, TError> onFail) =>
            (await task.ConfigureAwait(false)).Ensure(predicate, onFail);

        public async Task<Result<T, TError>> TapAsync(Action<T> action) =>
            (await task.ConfigureAwait(false)).Tap(action);

        public async Task<Result<T, TError>> TapErrorAsync(Action<TError> action) =>
            (await task.ConfigureAwait(false)).TapError(action);

        public async Task<TOut> MatchAsync<TOut>(Func<T, TOut> onOk, Func<TError, TOut> onError) =>
            (await task.ConfigureAwait(false)).Match(onOk, onError);

        public async Task<T> RecoverAsync(Func<TError, T> fallback) =>
            (await task.ConfigureAwait(false)).Recover(fallback);
    }

    extension<T, TError>(ValueTask<Result<T, TError>> task)
    {
        public async ValueTask<Result<TOut, TError>> MapAsync<TOut>(Func<T, TOut> map) =>
            (await task.ConfigureAwait(false)).Map(map);

        public async ValueTask<Result<TOut, TError>> BindAsync<TOut>(Func<T, Result<TOut, TError>> bind) =>
            (await task.ConfigureAwait(false)).Bind(bind);

        public async ValueTask<Result<TOut, TError>> BindAsync<TOut>(Func<T, ValueTask<Result<TOut, TError>>> bindAsync)
        {
            var r = await task.ConfigureAwait(false);
            if (r.IsOk) return await bindAsync(r.UnsafeValue).ConfigureAwait(false);
            if (r.IsError) return new Result<TOut, TError>(r.UnsafeError);
            throw UninitializedException(nameof(Result<T, TError>));
        }

        public async ValueTask<TOut> MatchAsync<TOut>(Func<T, TOut> onOk, Func<TError, TOut> onError) =>
            (await task.ConfigureAwait(false)).Match(onOk, onError);
    }
}
