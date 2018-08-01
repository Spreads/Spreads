// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Spreads
{
    internal class AsyncCursor
    {
        protected static IDisposable _nullSubscriptionSentinel = new DummyDisposable();

        // TODO Event tracing or conditional
        private static long _syncCount;

        private static long _asyncCount;
        private static long _awaitCount;
        private static long _skippedCount;
        private static long _missedCount;

        private static long _finishedCount;

        internal static long SyncCount => Interlocked.Add(ref _syncCount, 0);

        internal static long AsyncCount => _asyncCount;
        internal static long AwaitCount => _awaitCount;
        internal static long SkippedCount => _skippedCount;
        internal static long MissedCount => _missedCount;
        internal static long FinishedCount => _finishedCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogSync()
        {
            Interlocked.Increment(ref _syncCount);
            // _syncCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogAsync()
        {
            _asyncCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogAwait()
        {
            _awaitCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogSkipped()
        {
            _skippedCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogMissed()
        {
            _missedCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LogFinished()
        {
            _finishedCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ResetCounters()
        {
            Interlocked.Exchange(ref _syncCount, 0);
            // _syncCount = 0;
            _asyncCount = 0;
            _awaitCount = 0;
            _skippedCount = 0;
            _missedCount = 0;
            _finishedCount = 0;
        }

        protected static readonly Action<object> SCompletedSentinel = s => throw new InvalidOperationException("Called completed sentinel");
        protected static readonly Action<object> SAvailableSentinel = s => throw new InvalidOperationException("Called available sentinel");
    }

    internal sealed class AsyncCursor<TKey, TValue, TCursor> : AsyncCursor,
         ISpecializedCursor<TKey, TValue, TCursor>,
         IValueTaskSource<bool>, IAsyncCompletable
         where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        // TODO Pooling, but see #84

        // NB this is often a struct, should not be made readonly!
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private TCursor _innerCursor;

        private Action<object> _continuation;
        private object _continuationState;
        private object _capturedContext;
        private ExecutionContext _executionContext;
        internal volatile bool _completed;
        private bool _result;
        private ExceptionDispatchInfo _error;
        private short _version;

        private long _isLocked = 1L;
        private bool _hasSkippedUpdate;
        private IDisposable _subscription;
        private IAsyncSubscription _subscriptionEx;
        private GCHandle _keepAliveHandle;

        private bool _preferBatchMode;
        private bool _isInBatch;
        private readonly IAsyncBatchEnumerator<KeyValuePair<TKey, TValue>> _outerBatchEnumerator;
        private IEnumerator<KeyValuePair<TKey, TValue>> _innerBatchEnumerator;
        private IEnumerable<KeyValuePair<TKey, TValue>> _nextBatch;

        public AsyncCursor(TCursor cursor, bool preferBatchMode = false)
        {
            _innerCursor = cursor;
            if (_innerCursor.Source == null)
            {
                Environment.FailFast("Source is null");
            }

            _preferBatchMode = preferBatchMode;

            if (_preferBatchMode)
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                if (cursor is IAsyncBatchEnumerator<KeyValuePair<TKey, TValue>> batchEnumerator)
                {
                    _outerBatchEnumerator = batchEnumerator;
                }
                else
                {
                    _preferBatchMode = false;
                }
            }

            _continuation = SAvailableSentinel;
            _continuationState = null;
            _capturedContext = null;
            _executionContext = null;
            _completed = false;
            _error = null;
            _version = 0;
        }

        public TCursor InnerCursor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor; }
        }

        #region Async synchronization

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryOwnAndReset()
        {
            var previous = Interlocked.CompareExchange(ref _continuation, null, SAvailableSentinel);
            if (ReferenceEquals(previous, SAvailableSentinel) || previous is null)
            {
                unchecked
                {
                    _version++;
                }

                _isLocked = 1L;

                _completed = false;
                _result = default;
                _continuationState = null;
                _error = null;
                _executionContext = null;
                _capturedContext = null;
            }
            else
            {
                ThrowHelper.FailFast("Cannot reset");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask<bool> GetMoveNextAsyncValueTask()
        {
            if (_innerCursor.MoveNext())
            {
                LogSync();
                return new ValueTask<bool>(true);
            }
            if (_innerCursor.IsCompleted)
            {
                if (_innerCursor.MoveNext())
                {
                    LogSync();
                    return new ValueTask<bool>(true);
                }
                LogSync();
                return new ValueTask<bool>(false);
            }
            // Delay subscribing as much as possible
            if (_subscription == null)
            {
                _subscription = _innerCursor.AsyncCompleter?.Subscribe(this) ?? _nullSubscriptionSentinel;
                _subscriptionEx = _subscription as IAsyncSubscription;
            }
            if (ReferenceEquals(_subscription, _nullSubscriptionSentinel))
            {
                // NB last chance, no async support
                return new ValueTask<bool>(_innerCursor.MoveNext());
            }

            TryOwnAndReset();

            switch (GetStatus(_version))
            {
                case ValueTaskSourceStatus.Succeeded:
                    return new ValueTask<bool>(GetResult(_version));

                default:
                    return new ValueTask<bool>(this, _version);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTaskSourceStatus GetStatus(short token)
        {
            ValidateToken(token);

            if (_innerCursor.MoveNext())
            {
                LogSync();
                _completed = true;
                _result = true;
            }
            else if (_innerCursor.IsCompleted)
            {
                LogSync();
                if (_innerCursor.MoveNext())
                {
                    _completed = true;
                    _result = true;
                }
                _completed = true;
                _result = false;
            }

            return
                !_completed ? ValueTaskSourceStatus.Pending :
                _error == null ? ValueTaskSourceStatus.Succeeded :
                _error.SourceException is OperationCanceledException ? ValueTaskSourceStatus.Canceled :
                ValueTaskSourceStatus.Faulted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetResult(short token)
        {
            ValidateToken(token);

            if (!_completed)
            {
                ThrowHelper.ThrowInvalidOperationException("_completed = false in GetResult");
            }

            var result = _result;

            Volatile.Write(ref _continuation, SAvailableSentinel);

            _error?.Throw();
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateToken(short token)
        {
            if (token != _version)
            {
                ThrowHelper.FailFast("token != _version");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryComplete(bool runAsync, bool cancel)
        {
            if (cancel)
            {
                _error = _error ?? ExceptionDispatchInfo.Capture(new OperationCanceledException());
            }

            // NB: OnCompleted opens the lock. If there is no awaiter then
            // we register a missed update and return. This methods does
            // not move _innerCursor if noone is awating on MNA. Cursors are
            // single-reader (one thread at time) so if someone awaits on one
            // thread and moves a cursor on another then this is incorrect
            // unsupporate usage.
            if (Interlocked.CompareExchange(ref _isLocked, 1L, 0L) != 0L)
            {
                Volatile.Write(ref _hasSkippedUpdate, true);
                LogMissed();
                return;
            }

            if (runAsync)
            {
                SpreadsThreadPool.Default.UnsafeQueueCompletableItem(TryCompleteImpl, null, true);
            }
            else
            {
                TryCompleteImpl(null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryCompleteImpl(object dummy)
        {
            var runAsync = false;
            // separate because there could be no awaiter when we cancel and lock is taken
            if (_error != null)
            {
                SignalCompletion(runAsync);
                return;
            }

            try
            {
                do
                {
                    // if during checks someones notifies us about updates but we are trying to move,
                    // then we could miss update (happenned in tests once in 355M-5.5B ops)
                    Volatile.Write(ref _hasSkippedUpdate, false);
                    if (_innerCursor.MoveNext())
                    {
                        _subscriptionEx?.RequestNotification(-1);
                        if (runAsync)
                        {
                            SetResultAsync(true);
                        }
                        else
                        {
                            SetResult(true);
                        }

                        LogAsync();
                        return;
                    }

                    if (_innerCursor.IsCompleted)
                    {
                        // not needed: if completed then there will be no updates
                        _subscriptionEx?.RequestNotification(-1);
                        if (_innerCursor.MoveNext())
                        {
                            if (runAsync)
                            {
                                SetResultAsync(true);
                            }
                            else
                            {
                                SetResult(true);
                            }

                            LogAsync();
                            return;
                        }

                        if (runAsync) { SetResultAsync(false); }
                        else { SetResult(false); }

                        LogAsync();
                        return;
                    }

                    // if (Volatile.Read(ref _hasSkippedUpdate)) { LogSkipped(); }
                } while (Volatile.Read(ref _hasSkippedUpdate));

                LogAwait();

                _subscriptionEx?.RequestNotification(1);
                Volatile.Write(ref _isLocked, 0L);
                if (Volatile.Read(ref _hasSkippedUpdate))//  && locked == 0 && !_completed)
                {
                    LogSkipped();
                    TryComplete(true, false);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                SetException(e); // see https://github.com/dotnet/roslyn/issues/26567; we may want to move this out of the catch
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (continuation == null)
            {
                ThrowHelper.FailFast(nameof(continuation));
                return;
            }
            ValidateToken(token);

            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            {
                _executionContext = ExecutionContext.Capture();
            }

            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                SynchronizationContext sc = SynchronizationContext.Current;
                if (sc != null && sc.GetType() != typeof(SynchronizationContext))
                {
                    _capturedContext = sc;
                }
                else
                {
                    TaskScheduler ts = TaskScheduler.Current;
                    if (ts != TaskScheduler.Default)
                    {
                        _capturedContext = ts;
                    }
                }
            }

            // From S.Th.Channels:
            // We need to store the state before the CompareExchange, so that if it completes immediately
            // after the CompareExchange, it'll find the state already stored.  If someone misuses this
            // and schedules multiple continuations erroneously, we could end up using the wrong state.
            // Make a best-effort attempt to catch such misuse.
            if (_continuationState != null)
            {
                ThrowHelper.FailFast("Multiple continuations");
            }
            _continuationState = state;

            // From S.Th.Channels:
            // Try to set the provided continuation into _continuation.  If this succeeds, that means the operation
            // has not yet completed, and the completer will be responsible for invoking the callback.  If this fails,
            // that means the operation has already completed, and we must invoke the callback, but because we're still
            // inside the awaiter's OnCompleted method and we want to avoid possible stack dives, we must invoke
            // the continuation asynchronously rather than synchronously.
            Action<object> prevContinuation = Interlocked.CompareExchange(ref _continuation, continuation, null);

            if (prevContinuation != null)
            {
                if (!ReferenceEquals(prevContinuation, SCompletedSentinel))
                {
                    Debug.Assert(prevContinuation != SAvailableSentinel, "Continuation was the available sentinel.");
                    ThrowHelper.FailFast("Multiple continuations");
                }

                _executionContext = null;

                object cc = _capturedContext;
                _capturedContext = null;

                switch (cc)
                {
                    case null:
                        SpreadsThreadPool.Default.UnsafeQueueCompletableItem(continuation, state, true);
                        break;

                    case SynchronizationContext sc:
                        sc.Post(s =>
                        {
                            var tuple = (Tuple<Action<object>, object>)s;
                            tuple.Item1(tuple.Item2);
                        }, Tuple.Create(continuation, state));
                        break;

                    case TaskScheduler ts:
                        Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                        break;
                }
            }
            else
            {
                // we will lose the root after this method method returns without 
                // completion, so we keep the reference to this cursor alive
                // https://github.com/dotnet/coreclr/issues/19161
                _keepAliveHandle = GCHandle.Alloc(this);

                // if we request notification before releasing the lock then
                // we will have _hasSkippedUpdate flag set. Then we retry ourselves anyway

                _subscriptionEx?.RequestNotification(1);
                Volatile.Write(ref _isLocked, 0L);
                // Retry self, _continuations is now set, last chance to get result
                // without external notification. If cannot move from here
                // lock will remain open
                TryComplete(true, false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult(bool result)
        {
            _result = result;
            SignalCompletion();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResultAsync(bool result)
        {
            _result = result;
            SignalCompletion(true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetException(Exception error)
        {
            _error = ExceptionDispatchInfo.Capture(error);
            SignalCompletion();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetExceptionAsync(Exception error)
        {
            _error = ExceptionDispatchInfo.Capture(error);
            SignalCompletion(true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SignalCompletion(bool runAsync = false)
        {
            if (_keepAliveHandle.IsAllocated)
            {
                _keepAliveHandle.Free();
            }

            if (_completed)
            {
                ThrowHelper.ThrowInvalidOperationException("Calling SignalCompletion on already completed task");
            }
            _completed = true;

            if (Interlocked.CompareExchange(ref _continuation, SCompletedSentinel, null) != null)
            {
                if (_continuation == SCompletedSentinel || _continuation == SAvailableSentinel)
                {
                    ThrowHelper.FailFast("Wrong continuation");
                }

                if (_executionContext != null)
                {
                    ExecutionContext.Run(_executionContext, runAsync ? _contextCallbackAsync : _contextCallback, this);
                }
                else
                {
                    InvokeContinuation(runAsync);
                }
            }
        }

        // TODO refactor callbacks sync vs async path

        private readonly ContextCallback _contextCallback = (th) =>
        {
            ((AsyncCursor<TKey, TValue, TCursor>)th).InvokeContinuation(false);
        };

        private readonly ContextCallback _contextCallbackAsync = (th) =>
        {
            ((AsyncCursor<TKey, TValue, TCursor>)th).InvokeContinuation(true);
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvokeContinuation(bool runAsync)
        {
            object cc = _capturedContext;
            _capturedContext = null;

            switch (cc)
            {
                case null:
                    if (runAsync)
                    {
                        SpreadsThreadPool.Default.UnsafeQueueCompletableItem(_cbForScheduler, this, true);
                    }
                    else
                    {
                        SetCompletionAndInvokeContinuation();
                    }

                    break;

                case SynchronizationContext sc:
                    sc.Post(_sendOrPostCallback, this);
                    break;

                case TaskScheduler ts:
                    Task.Factory.StartNew(_cbForScheduler, this, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                    break;
            }
        }

        private readonly SendOrPostCallback _sendOrPostCallback = (th) =>
        {
            ((AsyncCursor<TKey, TValue, TCursor>)th).SetCompletionAndInvokeContinuation();
        };

        private readonly Action<object> _cbForScheduler = (th) =>
        {
            ((AsyncCursor<TKey, TValue, TCursor>)th).SetCompletionAndInvokeContinuation();
        };

        private void SetCompletionAndInvokeContinuation()
        {
            Action<object> c = _continuation;
            _continuation = SCompletedSentinel;
            c(_continuationState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> MoveNextAsync()
        {
            if (!_preferBatchMode) { return GetMoveNextAsyncValueTask(); }

            ////////// BATCH MODE //////////

            if (_isInBatch && _innerBatchEnumerator.MoveNext())
            {
                return new ValueTask<bool>(true);
            }
            return MoveNextAsyncBatchMode();

            async ValueTask<bool> MoveNextAsyncBatchMode()
            {
                var wasInBatch = _isInBatch;

                // _nextBatch = _outerBatchEnumerator.CurrentBatch;
                // previous happy-path move was false, try to get next batch
                // but we do not use read locking here, probably new values were added to the current batch
                // cache the new batch and retry current
                _isInBatch = _nextBatch != null || await _outerBatchEnumerator.MoveNextBatch(false);
                if (_isInBatch)
                {
                    if (_nextBatch == null)
                    {
                        _nextBatch = _outerBatchEnumerator.CurrentBatch;
                    }

                    if (wasInBatch && _innerBatchEnumerator.MoveNext())
                    {
                        // try move over potentially missed values
                        // regardless of movedNextBatch, if previous moved then next is either null or unused cached
                        return true;
                    }

                    if (_nextBatch != null)
                    {
                        _innerBatchEnumerator?.Dispose();
                        _innerBatchEnumerator = _nextBatch.GetEnumerator();
                        _nextBatch = null;
                        if (_innerBatchEnumerator.MoveNext())
                        {
                            return true;
                        }
                        else
                        {
                            ThrowHelper.ThrowInvalidOperationException("Batches should not be empty");
                        }
                    }
                    else
                    {
                        ThrowHelper.ThrowInvalidOperationException("_nextBatch == null");
                    }

                    //     _isInBatch = await MoveNextBatch();
                }
                else
                {
                    // when MNB returns false there will be no more batches
                    _preferBatchMode = false;
                    if (wasInBatch)
                    {
                        // NB: in the current implementation moveat must work because the batch
                        // was available and we have not yet disposed _innerBatchEnumerator
                        // This depends on the fact that batching is an optional internal feature of ICursor
                        // and not a standalone implementation
                        if (!_innerCursor.MoveAt(_innerBatchEnumerator.Current.Key, Lookup.EQ))
                        {
                            ThrowHelper.ThrowInvalidOperationException("Cannot move to the current batch key after no more batches are available.");
                        }
                    }
                }
                return await MoveNextAsync();
            }
        }

        #endregion Async synchronization

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_isInBatch)
            {
                return _innerBatchEnumerator.MoveNext();
            }
            return _innerCursor.MoveNext();
        }

        public void Reset()
        {
            TryOwnAndReset();
            _innerCursor?.Reset();
            _innerBatchEnumerator?.Reset();
        }

        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _isInBatch ? _innerBatchEnumerator.Current : _innerCursor.Current; }
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
            if (_isInBatch)
            {
                ThrowHelper.ThrowNotSupportedException();
            }
            return _innerCursor.MoveAt(key, direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveFirst()
        {
            if (_isInBatch)
            {
                return _innerBatchEnumerator.MoveNext();
            }
            return _innerCursor.MoveFirst();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveLast()
        {
            if (_isInBatch)
            {
                ThrowHelper.ThrowNotSupportedException();
            }
            return _innerCursor.MoveLast();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MoveNext(long stride, bool allowPartial)
        {
            if (_isInBatch)
            {
                ThrowHelper.ThrowNotSupportedException();
            }
            return _innerCursor.MoveNext(stride, allowPartial);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if (_isInBatch)
            {
                ThrowHelper.ThrowNotSupportedException();
            }
            return _innerCursor.MovePrevious();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MovePrevious(long stride, bool allowPartial)
        {
            if (_isInBatch)
            {
                ThrowHelper.ThrowNotSupportedException();
            }
            return _innerCursor.MovePrevious(stride, allowPartial);
        }

        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _isInBatch ? _innerBatchEnumerator.Current.Key : _innerCursor.CurrentKey; }
        }

        public TValue CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _isInBatch ? _innerBatchEnumerator.Current.Value : _innerCursor.CurrentValue; }
        }

        public ISeries<TKey, TValue> Source
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.Source; }
        }

        public bool IsContinuous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.IsContinuous; }
        }

        public bool IsIndexed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.IsIndexed; }
        }

        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.IsCompleted; }
        }

        public IAsyncCompleter AsyncCompleter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _innerCursor.AsyncCompleter; }
        }

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
            return new AsyncCursor<TKey, TValue, TCursor>(_innerCursor.Clone());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            return _innerCursor.TryGetValue(key, out value);
        }

        private void Dispose(bool disposing)
        {
            _subscription?.Dispose();
            _innerBatchEnumerator?.Dispose();

            if (_keepAliveHandle.IsAllocated)
            {
                _keepAliveHandle.Free();
            }

            if (!disposing) return;

            Reset();
            _innerCursor?.Dispose();
            // TODO (docs) a disposed cursor could still be used as a cursor factory and is actually used
            // via Source.GetCursor(). This must be clearly mentioned in cursor specification
            // and be a part of contracts test suite
            // NB don't do this: _innerCursor = default(TCursor);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        ~AsyncCursor()
        {
            Console.WriteLine("Async cursor finalized");
            Dispose(false);
        }

        public Task DisposeAsync()
        {
            Reset();
            return _innerCursor.DisposeAsync();
        }
    }
}
