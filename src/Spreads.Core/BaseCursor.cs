// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Spreads
{
    // TODO Event tracing or conditional
    internal static class AsyncCursorCounters
    {
        private static long _syncCount;
        private static long _asyncCount;
        private static long _awaitCount;

        public static long SyncCount => _syncCount;

        public static long AsyncCount => _asyncCount;
        public static long AwaitCount => _awaitCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogSync()
        {
            _syncCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogAsync()
        {
            _asyncCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogAwait()
        {
            _awaitCount++;
        }
    }

    internal sealed class BaseCursorAsync<TKey, TValue, TCursor> : ISpecializedCursor<TKey, TValue, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        // TODO Pooling, but see #84

        // NB this is often a struct, should not be made readonly!
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private TCursor _innerCursor;

        private TaskCompletionSource<Task<bool>> _cancelledTcs;

        public BaseCursorAsync(Func<TCursor> cursorFactory)
        {
            _innerCursor = cursorFactory();
            if (_innerCursor.Source == null)
            {
                Console.WriteLine("Source is null");
            }
        }

        public BaseCursorAsync(TCursor cursor)
        {
            _innerCursor = cursor;
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;

            _innerCursor?.Dispose();
            // TODO (docs) a disposed cursor could still be used as a cursor factory and is actually used
            // via Source.GetCursor(). This must be clearly mentioned in cursor specification
            // and be a part of contracts test suite
            // NB don't do this: _innerCursor = default(TCursor);

            _cancelledTcs = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextAsync()
        {
            if (_innerCursor.MoveNext())
            {
                AsyncCursorCounters.LogSync();
                return TaskUtil.TrueTask;
            }

            if (_innerCursor.Source.IsCompleted)
            {
                // false almost always
                if (_innerCursor.MoveNext())
                {
                    AsyncCursorCounters.LogSync();
                    return TaskUtil.TrueTask;
                }

                AsyncCursorCounters.LogSync();
                return TaskUtil.FalseTask;
            }

            return AwaitNotify();
        }

        private async Task<bool> AwaitNotify()
        {
            // account for (unlikely) false positive return from await,
            // check MN/IsCompleted before returing
            while (true)
            {
                var task = _innerCursor.Source.Updated;
                await task;
                AsyncCursorCounters.LogAsync();
                if (_innerCursor.MoveNext())
                {
                    return true;
                }

                if (_innerCursor.Source.IsCompleted)
                {
                    if (_innerCursor.MoveNext())
                    {
                        return true;
                    }

                    return false;
                }
            }

            ThrowHelper.ThrowInvalidOperationException();
            return false;
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public Task<bool> MoveNextAsync()
        //{
        //    // sync move, hot path
        //    if (_innerCursor.MoveNext())
        //    {
        //        return TaskUtil.TrueTask;
        //    }

        //    return MoveNextSlow();
        //}

        //private Task<bool> MoveNextSlow()
        //{
        //    // we took a task, but it could have been created after the previous update, need to try moving next
        //    var task = (_innerCursor.Source as ContainerSeries<TKey, TValue, TCursor>).Updated2;
        //    if (_innerCursor.MoveNext())
        //    {
        //        return TaskUtil.TrueTask;
        //    }

        //    if (_innerCursor.Source.IsCompleted)
        //    {
        //        // false almost always
        //        if (_innerCursor.MoveNext())
        //        {
        //            return TaskUtil.TrueTask;
        //        }

        //        return TaskUtil.FalseTask;
        //    }

        //    // now task will always be completed by NotifyUpdate

        //    Task<Task<bool>> returnTask = task.ContinueWith(continuationFunction: MoveNextContinuation,
        //        continuationOptions: TaskContinuationOptions.DenyChildAttach);

        //        return returnTask.Unwrap();
           
        //}

        //// TODO check if caching for this delegate is needed
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private Task<bool> MoveNextContinuation(Task t)
        //{
        //    return _innerCursor.MoveNext() ? TaskUtil.TrueTask : MoveNextAsync();
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            return _innerCursor.MoveNext();
        }

        public void Reset()
        {
            _innerCursor?.Reset();
        }

        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.Current; }
        }

        object IEnumerator.Current => ((IEnumerator)_innerCursor).Current;

        public CursorState State
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.State; }
        }

        public KeyComparer<TKey> Comparer => _innerCursor.Comparer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            return _innerCursor.MoveAt(key, direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveFirst()
        {
            return _innerCursor.MoveFirst();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveLast()
        {
            return _innerCursor.MoveLast();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MoveNext(long stride, bool allowPartial)
        {
            return _innerCursor.MoveNext(stride, allowPartial);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            return _innerCursor.MovePrevious();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MovePrevious(long stride, bool allowPartial)
        {
            return _innerCursor.MovePrevious(stride, allowPartial);
        }

        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.CurrentKey; }
        }

        public TValue CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.CurrentValue; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextBatch()
        {
            return _innerCursor.MoveNextBatch();
        }

        public ISeries<TKey, TValue> CurrentBatch
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.CurrentBatch; }
        }

        public ISeries<TKey, TValue> Source => _innerCursor.Source;

        public bool IsContinuous => _innerCursor.IsContinuous;

        public TCursor Initialize()
        {
            return _innerCursor.Initialize();
        }

        public TCursor Clone()
        {
            return _innerCursor.Clone();
        }

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            return new BaseCursorAsync<TKey, TValue, TCursor>(_innerCursor.Clone());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            return _innerCursor.TryGetValue(key, out value);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public Task DisposeAsync()
        {
            return _innerCursor.DisposeAsync();
        }
    }
}