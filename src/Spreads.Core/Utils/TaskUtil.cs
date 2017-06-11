// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
#if NET451
using System.Collections.Generic;
#endif
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Utils
{
    public static class TaskUtil
    {
        public static Task CompletedTask = Task.FromResult<object>(null);
        public static Task<bool> TrueTask = Task.FromResult(true);
        public static Task<bool> FalseTask = Task.FromResult(false);
        public static Task<bool> CancelledBoolTask = FromCanceled<bool>(default(CancellationToken));
#if NET451

        private static class TaskExCache<T>
        {
            private static Task<T> _defaultCompleted;
            private static Task<T> _cancelled;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Task<T> GetCompleted(T result = default(T))
            {
                if (EqualityComparer<T>.Default.Equals(result, default(T)))
                {
                    return _defaultCompleted ?? (_defaultCompleted = Task.FromResult(default(T)));
                }
                return Task.FromResult(result);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Task<T> GetCancelled(CancellationToken cancellationToken)
            {
                if (_cancelled != null) return _cancelled;
                var tcs = AsyncTaskMethodBuilder<T>.Create(); // new TaskCompletionSource<T>();
                var t = tcs.Task;
                tcs.SetException(new OperationCanceledException(cancellationToken));
                _cancelled = t;
                return t;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Task<T> GetFromException(Exception ex)
            {
                var tcs = new TaskCompletionSource<T>();
                tcs.SetException(ex);
                return tcs.Task;
            }
        }

#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<T> FromCanceled<T>(CancellationToken cancellationToken)
        {
#if NET451
            return TaskExCache<T>.GetCancelled(cancellationToken);
#else
            return Task.FromCanceled<T>(cancellationToken);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task FromCanceled(CancellationToken cancellationToken)
        {
            return FromCanceled<object>(cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<T> FromException<T>(Exception exception)
        {
#if NET451
            return TaskExCache<T>.GetFromException(exception);
#else
            return Task.FromException<T>(exception);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task FromException(Exception exception)
        {
            return FromException<object>(exception);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<T> FromResult<T>(T result)
        {
#if NET451
            return TaskExCache<T>.GetCompleted(result);
#else
            return Task.FromResult<T>(result);
#endif
        }
    }
}