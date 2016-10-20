using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads {

    public static class TaskEx {
        public static Task CompletedTask = Task.FromResult<object>(null);
#if NET451
        private static class TaskExCache<T> {
            private static Task<T> _defaultCompleted;
            public static Task<T> Cancelled;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Task<T> GetCompleted(T result = default(T)) {
                if (EqualityComparer<T>.Default.Equals(result, default(T))) {
                    return _defaultCompleted ?? (_defaultCompleted = Task.FromResult(default(T)));
                }
                return Task.FromResult(result);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Task<T> GetCancelled() {
                if (Cancelled != null) return Cancelled;
                var tcs = new TaskCompletionSource<T>();
                tcs.SetCanceled();
                Cancelled = tcs.Task;
                return Cancelled;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Task<T> GetFromException(Exception ex) {
                var tcs = new TaskCompletionSource<T>();
                tcs.SetException(ex);
                return tcs.Task;
            }
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<T> FromCanceled<T>(CancellationToken cancellationToken) {
#if NET451
            return TaskExCache<T>.GetCancelled();
#else
            return Task.FromCanceled<T>(cancellationToken);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task FromCanceled(CancellationToken cancellationToken) {
            return FromCanceled<object>(cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<T> FromException<T>(Exception exception) {
#if NET451
            return TaskExCache<T>.GetFromException(exception);
#else
            return Task.FromException<T>(exception);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task FromException(Exception exception) {
            return FromException<object>(exception);
        }
    }
}
