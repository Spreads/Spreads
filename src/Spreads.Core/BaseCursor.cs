// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Utils;

namespace Spreads
{
    internal sealed class BaseCursorAsync<TKey, TValue, TCursor> : ISpecializedCursor<TKey, TValue, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        // TODO Pooling, but see #84

        // NB this is often a struct, should not be made readonly!
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private TCursor _innerCursor;

        private TaskCompletionSource<Task<bool>> _cancelledTcs;
        private CancellationTokenRegistration _registration;
        private CancellationToken _token;

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
            _registration.Dispose();
            _registration = default;

            _token = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextAsync()
        {
            return MoveNextAsync(CancellationToken.None);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            // sync move, hot path
            if (_innerCursor.MoveNext())
            {
                return TaskUtil.TrueTask;
            }

            return MoveNextSlow(cancellationToken);
        }

        private Task<bool> MoveNextSlow(CancellationToken cancellationToken)
        {
            // we took a task, but it could have been created after the previous update, need to try moving next
            var task = _innerCursor.Source.Updated;
            if (_innerCursor.MoveNext())
            {
                return TaskUtil.TrueTask;
            }

            if (_innerCursor.Source.IsCompleted)
            {
                // false almost always
                if (_innerCursor.MoveNext())
                {
                    return TaskUtil.TrueTask;
                }

                return TaskUtil.FalseTask;
            }

            // now task will always be completed by NotifyUpdate

            Task<Task<bool>> returnTask = task.ContinueWith(continuationFunction: MoveNextContinuation,
                continuationOptions: TaskContinuationOptions.DenyChildAttach);

            if (!cancellationToken.CanBeCanceled)
            {
                return returnTask.Unwrap();
            }

            if (_token != cancellationToken)
            {
                _registration.Dispose();
                _token = cancellationToken;
                _cancelledTcs = new TaskCompletionSource<Task<bool>>();
                _registration = _token.Register(() =>
                {
                    _cancelledTcs.SetResult(TaskUtil.FromCanceled<bool>(_token));
                });
            }

            var anyReturn = Task.WhenAny(returnTask, _cancelledTcs.Task);

            return anyReturn.Unwrap().Unwrap();
        }

        // TODO check if caching for this delegate is needed
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Task<bool> MoveNextContinuation(Task<bool> t)
        {
            if (!t.Result) return TaskUtil.FalseTask;
            if (_token.IsCancellationRequested) return TaskUtil.FromCanceled<bool>(_token);
            return _innerCursor.MoveNext() ? TaskUtil.TrueTask : MoveNextAsync(_token);
        }

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
        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            return _innerCursor.MoveNextBatch(cancellationToken);
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