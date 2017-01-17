// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads {

    public class BaseCursorAsync<TK, TV, TCursor> : ICursor<TK, TV>
        where TCursor : ICursor<TK, TV> {
        private readonly BaseSeries<TK, TV> _source;

        // NB this is often a struct, should not be made readonly!
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private TCursor _state;

        private TaskCompletionSource<long> _unusedTcs;
        private TaskCompletionSource<Task<bool>> _cancelledTcs;
        private CancellationTokenRegistration _registration;

        private CancellationToken _token;

        // NB factory could be more specific than GetCursor method of the source, which returns an interface
        // At the same time, we need access to BaseSeries members and cannot use Source property of the cursor
        public BaseCursorAsync(BaseSeries<TK, TV> source, Func<TCursor> cursorFactory) {
            this._source = source;
            _state = cursorFactory();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNext(CancellationToken cancellationToken) {
            // sync move, hot path
            if (_state.MoveNext()) {
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
                if (_state.MoveNext()) {
                    return TaskEx.TrueTask;
                }
            }

            if (_source.IsReadOnly) { // false almost always
                return _state.MoveNext() ? TaskEx.TrueTask : TaskEx.FalseTask;
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
            return _state.MoveNext() ? TaskEx.TrueTask : MoveNext(_token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() {
            return _state.MoveNext();
        }

        public void Reset() {
            _state.Reset();
        }

        public KeyValuePair<TK, TV> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _state.Current; }
        }

        object IEnumerator.Current => ((IEnumerator)_state).Current;

        public IComparer<TK> Comparer => _state.Comparer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TK key, Lookup direction) {
            return _state.MoveAt(key, direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveFirst() {
            return _state.MoveFirst();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveLast() {
            return _state.MoveLast();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious() {
            return _state.MovePrevious();
        }

        public TK CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _state.CurrentKey; }
        }

        public TV CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _state.CurrentValue; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextBatch(CancellationToken cancellationToken) {
            return _state.MoveNextBatch(cancellationToken);
        }

        public ISeries<TK, TV> CurrentBatch
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _state.CurrentBatch; }
        }

        public ISeries<TK, TV> Source => _state.Source;

        public bool IsContinuous => _state.IsContinuous;

        public ICursor<TK, TV> Clone() {
            return _state.Clone();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TK key, out TV value) {
            return _state.TryGetValue(key, out value);
        }

        public void Dispose() {
            Dispose(true);
        }

        ~BaseCursorAsync() {
            Dispose(false);
        }

        private void Dispose(bool disposing) {
            if (disposing) {
                GC.SuppressFinalize(this);
            }
            _state.Dispose();
            _registration.Dispose();
        }
    }
}
