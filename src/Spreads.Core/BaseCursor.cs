// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads {

    public class BaseCursorAsync<TK, TV, TCursor> : ICursor<TK, TV>
        where TCursor : ICursor<TK, TV> {
        private static readonly BoundedConcurrentBag<BaseCursorAsync<TK, TV, TCursor>> Pool = new BoundedConcurrentBag<BaseCursorAsync<TK, TV, TCursor>>(Environment.ProcessorCount * 16);

        private BaseSeries<TK, TV> _source;

        // NB this is often a struct, should not be made readonly!
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private TCursor _innerCursor;

        private TaskCompletionSource<long> _unusedTcs;
        private TaskCompletionSource<Task<bool>> _cancelledTcs;
        private CancellationTokenRegistration _registration;

        private CancellationToken _token;

        // NB factory could be more specific than GetCursor method of the source, which returns an interface
        // At the same time, we need access to BaseSeries members and cannot use Source property of the cursor
        public BaseCursorAsync(BaseSeries<TK, TV> source, Func<TCursor> cursorFactory) {
            this._source = source;
            _innerCursor = cursorFactory();
        }

        public static BaseCursorAsync<TK, TV, TCursor> Create(BaseSeries<TK, TV> source, Func<TCursor> cursorFactory) {
            BaseCursorAsync<TK, TV, TCursor> inst;
            if (!Pool.TryTake(out inst)) {
                inst = new BaseCursorAsync<TK, TV, TCursor>(source, cursorFactory);
            }
            inst._source = source;
            inst._innerCursor = cursorFactory();
            return inst;
        }

        private void Dispose(bool disposing) {
            var disposable = _source as IDisposable;
            disposable?.Dispose();

            _innerCursor.Dispose();
            _innerCursor = default(TCursor);

            _unusedTcs = null;
            _cancelledTcs = null;
            _registration.Dispose();
            _registration = default(CancellationTokenRegistration);

            _token = default(CancellationToken);

            var pooled = Pool.TryAdd(this);
            // TODO review
            if (disposing && !pooled) {
                GC.SuppressFinalize(this);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNext(CancellationToken cancellationToken) {
            // sync move, hot path
            if (_innerCursor.MoveNext()) {
                return TaskEx.TrueTask;
            }

            var tcs = Volatile.Read(ref _source.UpdateTcs);
            if (tcs == null) {
                TaskCompletionSource<long> newTcs;
                if (_unusedTcs != null) {
                    newTcs = _unusedTcs;
                    _unusedTcs = null;
                } else {
                    newTcs = new TaskCompletionSource<long>();
                }
                var original = Interlocked.CompareExchange(ref _source.UpdateTcs, newTcs, null);
                if (original == null) {
                    // newTcs was put to the SM
                    // if unusedTcs was not null, newTcs = unusedTcs
                    // and unusedTcs went to SM
                    this._unusedTcs = null;
                    tcs = newTcs;
                } else {
                    // Tcs was already set, we set unusedTcs to itself if
                    // it was not null or to a new Tcs that we have allocated
                    this._unusedTcs = newTcs;
                    tcs = original;
                }
                if (_innerCursor.MoveNext()) {
                    return TaskEx.TrueTask;
                }
            }

            if (_source.IsReadOnly) { // false almost always
                return _innerCursor.MoveNext() ? TaskEx.TrueTask : TaskEx.FalseTask;
            }

            // NB activeTcs is already allocated and we cannot avoid this allocation,
            // however it could be shared among many cursors. When its Task completes,
            // we create a continuation Task that does synchronous and very fast
            // work inside its body, so it is a very small and short-lived object
            // TODO Check if we need to use RunContinuationsAsynchronously from 4.6
            // https://blogs.msdn.microsoft.com/pfxteam/2015/02/02/new-task-apis-in-net-4-6/

            Task<Task<bool>> returnTask = tcs.Task.ContinueWith(continuationFunction: MoveNextContinuation, continuationOptions: TaskContinuationOptions.DenyChildAttach);

            if (!cancellationToken.CanBeCanceled) {
                return returnTask.Unwrap();
            }

            if (_token != cancellationToken) {
                _registration.Dispose();
                _token = cancellationToken;
                _cancelledTcs = new TaskCompletionSource<Task<bool>>();
                _registration = _token.Register(() => _cancelledTcs.SetResult(TaskEx.CancelledBoolTask));
            }

            var anyReturn = Task.WhenAny(returnTask, _cancelledTcs.Task);

            return anyReturn.Unwrap().Unwrap();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Task<bool> MoveNextContinuation(Task<long> t) {
            // while this is not null, noone would be able to set a new one
            var original = Volatile.Read(ref _source.UpdateTcs);
            if (original != null) {
                // one of many cursors will succeed
                Interlocked.CompareExchange(ref _source.UpdateTcs, null, original);
            }
            return _innerCursor.MoveNext() ? TaskEx.TrueTask : MoveNext(_token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() {
            return _innerCursor.MoveNext();
        }

        public void Reset() {
            _innerCursor.Reset();
        }

        public KeyValuePair<TK, TV> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.Current; }
        }

        object IEnumerator.Current => ((IEnumerator)_innerCursor).Current;

        public IComparer<TK> Comparer => _innerCursor.Comparer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TK key, Lookup direction) {
            return _innerCursor.MoveAt(key, direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveFirst() {
            return _innerCursor.MoveFirst();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveLast() {
            return _innerCursor.MoveLast();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious() {
            return _innerCursor.MovePrevious();
        }

        public TK CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.CurrentKey; }
        }

        public TV CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.CurrentValue; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextBatch(CancellationToken cancellationToken) {
            return _innerCursor.MoveNextBatch(cancellationToken);
        }

        public IReadOnlySeries<TK, TV> CurrentBatch
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.CurrentBatch; }
        }

        public IReadOnlySeries<TK, TV> Source => _innerCursor.Source;

        public bool IsContinuous => _innerCursor.IsContinuous;

        public ICursor<TK, TV> Clone() {
            return _innerCursor.Clone();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TK key, out TV value) {
            return _innerCursor.TryGetValue(key, out value);
        }

        public void Dispose() {
            Dispose(true);
        }

        ~BaseCursorAsync() {
            Dispose(false);
        }
    }
}
