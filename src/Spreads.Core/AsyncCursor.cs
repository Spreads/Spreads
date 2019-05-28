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
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Spreads
{
    /// <summary>
    /// Base class with mostly internal static fields used by <see cref="AsyncCursor{TKey,TValue,TCursor}"/>.
    /// </summary>
    public class AsyncCursor
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
            //Interlocked.Increment(ref _syncCount);
            _syncCount++;
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

        protected static readonly Action<object> CompletedSentinel = s => throw new InvalidOperationException($"{nameof(CompletedSentinel)} invoked with {s}");
        protected static readonly Action<object> AvailableSentinel = s => throw new InvalidOperationException($"{nameof(AvailableSentinel)} invoked with {s}");

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected static void ThrowIncompleteOperationException()
        {
            ThrowHelper.ThrowInvalidOperationException("GetResult is called for incomplete task");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected static void ThrowIncorrectCurrentIdException()
        {
            ThrowHelper.ThrowInvalidOperationException("token != _currentId ");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected static void ThrowMultipleContinuations()
        {
            ThrowHelper.ThrowInvalidOperationException("Multiple continuations");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected static void FailContinuationIsNull()
        {
            // TODO do not FF
            ThrowHelper.FailFast("continuation");
        }
    }

    /// <summary>
    /// The default implementation of <see cref="ICursor{TKey,TValue}"/> with supported <see cref="IAsyncEnumerator{T}.MoveNextAsync"/>.
    /// </summary>
    public sealed class AsyncCursor<TKey, TValue, TCursor> : AsyncCursor,
        IAsyncCursor<TKey, TValue>,
        IAsyncCursor<TKey, TValue, TCursor>,
         IValueTaskSource<bool>, IAsyncCompletable, ISpreadsThreadPoolWorkItem
         where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        // Modeled after corefx Channels.AsyncOperation: https://github.com/dotnet/corefx/blob/master/src/System.Threading.Channels/src/System/Threading/Channels/AsyncOperation.cs
        // Pooling is not needed, because there would be too
        // many pools for each generic types combination
        // and using AsyncCursor is supposed to be long running.

        // ReSharper disable once FieldCanBeMadeReadOnly.Local Mutable struct
        private TCursor _innerCursor;

        /// <summary>
        /// The callback to invoke when the operation completes if <see cref="OnCompleted"/> was called before the operation completed,
        /// or <see cref="AsyncCursor.CompletedSentinel"/> if the operation completed before a callback was supplied,
        /// or null if a callback hasn't yet been provided and the operation hasn't yet completed.
        /// </summary>
        private Action<object> _continuation;

        private Action<object> _continuationEx;

        /// <summary>State to pass to <see cref="_continuation"/>.</summary>
        private object _continuationState;

        /// <summary><see cref="ExecutionContext"/> to flow to the callback, or null if no flowing is required.</summary>
        private ExecutionContext _executionContext;

        /// <summary>
        /// A "captured" <see cref="SynchronizationContext"/> or <see cref="TaskScheduler"/> with which to invoke the callback,
        /// or null if no special context is required.
        /// </summary>
        private object _schedulingContext;

        /// <summary>The result with which the operation succeeded, or the default value if it hasn't yet completed or failed.</summary>
        private bool _result;

        /// <summary>The exception with which the operation failed, or null if it hasn't yet completed or completed successfully.</summary>
        private ExceptionDispatchInfo _error;

        /// <summary>The current version of this value, used to help prevent misuse.</summary>
        private short _currentId;

        /// <summary>
        /// Set to true in TryComplete unconditionally and cleared before cursor MN attempts.
        /// If MN is unsuccessful, we check _hasUpdate after failed move and retry if hasUpdate is true.
        /// We also check this field after OnCompleted and call TryComplete if it's true.
        /// </summary>
        private bool _hasUpdate;

        private IDisposable _subscription;

        // TODO(?) remove all batch related logic. But the feature is really useful, only impl is bad
        // E.g. we could replace SQLite by any RDBMS/Cassandra/etc. to retrieve blocks
        // But that feature should be only in AsyncEnumerator. Not sure if we need AsyncCursor at all.
        private bool _preferBatchMode;

        private bool _isInBatch;
        private readonly IAsyncBatchEnumerator<KeyValuePair<TKey, TValue>> _outerBatchEnumerator;
        private IEnumerator<KeyValuePair<TKey, TValue>> _innerBatchEnumerator;
        private IEnumerable<KeyValuePair<TKey, TValue>> _nextBatch;

        // TODO review batch mode and all ctor usages
        internal AsyncCursor(TCursor cursor, bool preferBatchMode = false)
        {
            _innerCursor = cursor;

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

            _continuation = AvailableSentinel;
        }

        public TCursor InnerCursor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _innerCursor;
        }

        #region Async synchronization

        private bool TryOwnAndReset()
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _continuation, null, AvailableSentinel),
                 AvailableSentinel))
            {
                _continuationState = null;
                _result = default;
                _error = null;
                _schedulingContext = null;
                _executionContext = null;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask<bool> GetMoveNextAsyncValueTask()
        {
            // Clear before moves, we will detect any new data during moves
            Volatile.Write(ref _hasUpdate, false);

            if (_innerCursor.MoveNext())
            {
                LogSync();
                return new ValueTask<bool>(true);
            }

            if (_innerCursor.IsCompleted)
            {
                LogSync();
                if (_innerCursor.MoveNext())
                {
                    return new ValueTask<bool>(true);
                }
                return new ValueTask<bool>(false);
            }

            // Delay subscribing as much as possible
            if (_subscription == null)
            {
                _subscription = _innerCursor.AsyncCompleter?.Subscribe(this) ?? _nullSubscriptionSentinel;
            }

            if (ReferenceEquals(_subscription, _nullSubscriptionSentinel))
            {
                // NB last chance, no async support
                return new ValueTask<bool>(_innerCursor.MoveNext());
            }

            if (!TryOwnAndReset())
            {
                ThrowMultipleContinuations();
            }

            return new ValueTask<bool>(this, _currentId);
        }

        /// <summary>Gets the current status of the operation.</summary>
        /// <param name="token">The token that must match <see cref="_currentId"/>.</param>
        public ValueTaskSourceStatus GetStatus(short token)
        {
            ValidateToken(token);

            return
                !IsTaskCompleted ? ValueTaskSourceStatus.Pending :
                _error == null ? ValueTaskSourceStatus.Succeeded :
                _error.SourceException is OperationCanceledException ? ValueTaskSourceStatus.Canceled :
                ValueTaskSourceStatus.Faulted;
        }

        /// <summary>Gets whether the current task operation has completed.</summary>
        internal bool IsTaskCompleted => ReferenceEquals(_continuation, CompletedSentinel);

        internal bool IsTaskAwating
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var c = Volatile.Read(ref _continuation);

                return c != null
                       && !ReferenceEquals(c, CompletedSentinel)
                       && !ReferenceEquals(c, AvailableSentinel);
            }
        }

        public bool GetResult(short token)
        {
            ValidateToken(token);

            if (!IsTaskCompleted)
            {
                ThrowIncompleteOperationException();
            }

            ExceptionDispatchInfo error = _error;
            bool result = _result;
            unchecked
            {
                _currentId++;
            }

            // only after fetching all needed data
            Volatile.Write(ref _continuation, AvailableSentinel);
            Volatile.Write(ref _continuationEx, null);

            error?.Throw();
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateToken(short token)
        {
            if (token != _currentId)
            {
                ThrowIncorrectCurrentIdException();
            }
        }

        /// <summary>
        /// This method is used to chain/combine Spreads' logic, actual execution on a completion/consumer thread happens in Execute method.
        /// </summary>
        /// <remarks>
        /// This method should try to detect that there is no outstanding async awaiter.
        /// Usually <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> completes synchronously if
        /// data is available. If data is produced faster than a cursor consumes it then
        /// there will be too many work items on the ThreadPool, most of them just doing nothing,
        /// and this significantly reduces performance.
        /// </remarks>
        /// <param name="cancel">True to cancel a cursor async enumeration.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryComplete(bool cancel)
        {
            if (cancel || _error != null)
            {
                // Do not call SetException here, it could call completion synchronously
                // but we must never do so from writer threads, only from the thread pool.
                _error ??= ExceptionDispatchInfo.Capture(new OperationCanceledException());
            }
            else // always call Execute on cancel or error
            {
                Volatile.Write(ref _hasUpdate, true);

                // if _continuation is set in OnComplete after the read above
                // then at the end of the OnComplete we check _hasUpdate and
                // call this method to try complete without notification.

                if (!(IsTaskAwating
                    &&
                    _continuationEx == null
                    &&
                    Interlocked.CompareExchange(ref _continuationEx, _continuation, null) == null
                    ))
                {
                    LogMissed();
                    return;
                }
                // no continuation is currently being executed, should call Execute on ThreadPool
            }

#if NETCOREAPP3_0
            ThreadPool.UnsafeQueueUserWorkItem(this, true);
#else
            ThreadPool.UnsafeQueueUserWorkItem(state => ((AsyncCursor<TKey, TValue, TCursor>)state).Execute(), this);
#endif
        }

        /// <summary>
        /// This method is called by the ThreadPool. If there is no context (execution or scheduling) then execute continuation synchronuously
        /// since we are on the ThreadPool worker thread or a caller of this method knows what is going on.
        /// </summary>
        public void Execute()
        {
            // separate because there could be no awaiter when we cancel and lock is taken
            if (_error != null)
            {
                SignalCompletion();
                return;
            }

            try
            {
                do
                {
                    // if during checks someones notifies us about updates but we are trying to move,
                    // then we could miss update (happenned in tests once in 355M-5.5B ops)
                    Volatile.Write(ref _hasUpdate, false);

                    if (_innerCursor.MoveNext())
                    {
                        SetResult(true);
                        LogAsync();
                        return;
                    }

                    if (_innerCursor.IsCompleted)
                    {
                        var moved = _innerCursor.MoveNext();
                        SetResult(moved);
                        LogAsync();
                        return;
                    }

                    // if (Volatile.Read(ref _hasSkippedUpdate)) { LogSkipped(); }
                } while (Volatile.Read(ref _hasUpdate));
            }
            catch (Exception e)
            {
                SetException(e);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult(bool result)
        {
            _result = result;
            SignalCompletion();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetException(Exception error)
        {
            _error = ExceptionDispatchInfo.Capture(error);
            SignalCompletion();
        }

        /// <summary>Hooks up a continuation callback for when the operation has completed.</summary>
        /// <param name="continuation">The callback.</param>
        /// <param name="state">The state to pass to the callback.</param>
        /// <param name="token">The current token that must match <see cref="_currentId"/>.</param>
        /// <param name="flags">Flags that influence the behavior of the callback.</param>
        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            ValidateToken(token);

            // From S.Th.Channels:
            // We need to store the state before the CompareExchange, so that if it completes immediately
            // after the CompareExchange, it'll find the state already stored.  If someone misuses this
            // and schedules multiple continuations erroneously, we could end up using the wrong state.
            // Make a best-effort attempt to catch such misuse.
            if (_continuationState != null)
            {
                ThrowMultipleContinuations();
            }
            _continuationState = state;

            // Capture the execution context if necessary.
            Debug.Assert(_executionContext == null);
            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            {
                _executionContext = ExecutionContext.Capture();
            }

            // Capture the scheduling context if necessary.
            Debug.Assert(_schedulingContext == null);
            SynchronizationContext sc = null;
            TaskScheduler ts = null;
            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                sc = SynchronizationContext.Current;
                if (sc != null && sc.GetType() != typeof(SynchronizationContext))
                {
                    _schedulingContext = sc;
                }
                else
                {
                    sc = null;
                    ts = TaskScheduler.Current;
                    if (ts != TaskScheduler.Default)
                    {
                        _schedulingContext = ts;
                    }
                }
            }

            // From S.Th.Channels:
            // Try to set the provided continuation into _continuation.  If this succeeds, that means the operation
            // has not yet completed, and the completer will be responsible for invoking the callback.  If this fails,
            // that means the operation has already completed, and we must invoke the callback, but because we're still
            // inside the awaiter's OnCompleted method and we want to avoid possible stack dives, we must invoke
            // the continuation asynchronously rather than synchronously.
            Action<object> prevContinuation = Interlocked.CompareExchange(ref _continuation, continuation, null);

            if (prevContinuation != null)
            {
                // TODO review/test
                // In Spreads we try to complete synchronously before touching async machinery
                // but after that nothing could call SetResult. But a cursor could be cancelled
                // and we could call SetException at any point without new data. So this path
                // is possible in Spreads.

                // If the set failed because there's already a delegate in _continuation, but that delegate is
                // something other than CompletedSentinel, something went wrong, which should only happen if
                // the instance was erroneously used, likely to hook up multiple continuations.
                Debug.Assert(IsTaskCompleted, $"Expected IsCompleted");
                if (!ReferenceEquals(prevContinuation, CompletedSentinel))
                {
                    Debug.Assert(prevContinuation != AvailableSentinel, "Continuation was the available sentinel.");
                    ThrowMultipleContinuations();
                }

                // From S.Th.Channels:
                // Queue the continuation.  We always queue here, even if !RunContinuationsAsynchronously, in order
                // to avoid stack diving; this path happens in the rare race when we're setting up to await and the
                // object is completed after the awaiter.IsCompleted but before the awaiter.OnCompleted.
                if (_schedulingContext == null)
                {
                    QueueUserWorkItem(continuation, state);
                }
                else if (sc != null)
                {
                    sc.Post(s =>
                    {
                        var t = (Tuple<Action<object>, object>)s;
                        t.Item1(t.Item2);
                    }, Tuple.Create(continuation, state));
                }
                else
                {
                    Debug.Assert(ts != null);

                    // ReSharper disable once AssignNullToNotNullAttribute
                    Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                }
            }
            else
            {
                // We keep a strong reference from base container for not to lose GC root
                // while awaiting, see https://github.com/dotnet/coreclr/issues/19161
                // If strong reference ever becomes an issue then here we must root `this` somewhere
                // and clear that root in GetResult.
                // But `await foreach` does call Dispose. Manual usage of async cursor is an advanced
                // scenario and users must dispose an async cursor themselves.

                LogAwait();

                // We have set _continuation and are awaiting. Before that no Execute
                // could have been scheduled, but we could have missed updates.

                if (Volatile.Read(ref _hasUpdate))
                {
                    // Retry self, _continuations is now set, last chance to get result
                    // without external notification.
                    TryComplete(false);
                }
            }
        }

        private static void QueueUserWorkItem(Action<object> action, object state)
        {
#if NETCOREAPP3_0
            ThreadPool.QueueUserWorkItem(action, state, preferLocal: false);
#else
            Task.Factory.StartNew(action, state,
                CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
#endif
        }

        /// <summary>Signals to a registered continuation that the operation has now completed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SignalCompletion()
        {
            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, CompletedSentinel, null) != null)
            {
                Debug.Assert(_continuation != CompletedSentinel, "The continuation was the completion sentinel.");
                Debug.Assert(_continuation != AvailableSentinel, "The continuation was the available sentinel.");

                if (_schedulingContext == null)
                {
                    // There's no captured scheduling context.
                    // Fall through to invoke it synchronously, we are on the ThreadPool already from TryComplete -> Execute.
                }
                else if (_schedulingContext is SynchronizationContext sc)
                {
                    // There's a captured synchronization context.  If we're forced to run continuations asynchronously,
                    // or if there's a current synchronization context that's not the one we're targeting, queue it.
                    // Otherwise fall through to invoke it synchronously.
                    if (sc != SynchronizationContext.Current)
                    {
                        sc.Post(s => ((AsyncCursor<TKey, TValue, TCursor>)s).SetCompletionAndInvokeContinuation(), this);
                        return;
                    }
                }
                else
                {
                    // There's a captured TaskScheduler.
                    // If there's a current scheduler that's not the one we're targeting, queue it.
                    // Otherwise fall through to invoke it synchronously.
                    TaskScheduler ts = (TaskScheduler)_schedulingContext;
                    Debug.Assert(ts != null, "Expected a TaskScheduler");
                    if (ts != TaskScheduler.Current)
                    {
                        Task.Factory.StartNew(s => ((AsyncCursor<TKey, TValue, TCursor>)s).SetCompletionAndInvokeContinuation(), this,
                            CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                        return;
                    }
                }

                // Invoke the continuation synchronously.
                SetCompletionAndInvokeContinuation();
            }
        }

        private void SetCompletionAndInvokeContinuation()
        {
            if (_executionContext == null)
            {
                Action<object> c = _continuation;
                _continuation = CompletedSentinel;
                c(_continuationState);
            }
            else
            {
                ExecutionContext.Run(_executionContext, s =>
                {
                    var thisRef = (AsyncCursor<TKey, TValue, TCursor>)s;
                    Action<object> c = thisRef._continuation;
                    thisRef._continuation = CompletedSentinel;
                    c(thisRef._continuationState);
                }, this);
            }
        }

        #endregion Async synchronization

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
            // Outstanding async op could move reset cursor since TryComplete goes via ThreadPool
            // But all cursor operations are single-threaded, or single-caller for async case
            // Reset could be called only by consumer after await is completed.

            if (IsTaskAwating)
            {
                TryComplete(true);
                ThrowHelper.ThrowInvalidOperationException("Cannot reset with active awaiter.");
            }

            _innerCursor?.Reset();
            _innerBatchEnumerator?.Reset();
        }

        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _isInBatch ? _innerBatchEnumerator.Current : _innerCursor.Current;
        }

        object IEnumerator.Current => ((IEnumerator)_innerCursor).Current;

        public CursorState State
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _innerCursor.State;
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
            get => _isInBatch ? _innerBatchEnumerator.Current.Key : _innerCursor.CurrentKey;
        }

        public TValue CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _isInBatch ? _innerBatchEnumerator.Current.Value : _innerCursor.CurrentValue;
        }

        public Series<TKey, TValue, TCursor> Source => _innerCursor.Source;

        ISeries<TKey, TValue> ICursor<TKey, TValue>.Source
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _innerCursor.Source;
        }

        public bool IsContinuous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _innerCursor.IsContinuous;
        }

        public bool IsIndexed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _innerCursor.IsIndexed;
        }

        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _innerCursor.IsCompleted;
        }

        public IAsyncCompleter AsyncCompleter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _innerCursor.AsyncCompleter;
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
            Debug.WriteLine("AsyncCursor dispose");
            //if (_keepAliveHandle.IsAllocated)
            //{
            //    _keepAliveHandle.Free();
            //}

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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AsyncCursor()
        {
            //Console.WriteLine("Async cursor finalized: " + _st);
            Console.WriteLine("-----------------------------------");
            Dispose(false);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return new ValueTask();
        }
    }
}
