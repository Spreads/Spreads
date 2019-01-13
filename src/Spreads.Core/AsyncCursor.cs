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
    }

    // AsyncCursor is a state machine that is usually moved by SpreadsThreadPool when notified by the source and until cancelled.
    // Continuation could be sqeduled to the default ThreadPool when context is required (TODO review, there is nothing in Spread pipiline, we only need this to return back e.g. to UI thread)

    public sealed class AsyncCursor<TKey, TValue, TCursor> : AsyncCursor,
         ISpecializedCursor<TKey, TValue, TCursor>,
         IValueTaskSource<bool>, IAsyncCompletable, IThreadPoolWorkItem
         where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        // TODO (?,low) Pooling, but see #84

        // NB this is often a struct, should not be made readonly
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private TCursor _innerCursor;

        /// <summary>
        /// The callback to invoke when the operation completes if <see cref="OnCompleted"/> was called before the operation completed,
        /// or <see cref="AsyncCursor.SCompletedSentinel"/> if the operation completed before a callback was supplied,
        /// or null if a callback hasn't yet been provided and the operation hasn't yet completed.
        /// </summary>
        private Action<object> _continuation;

        /// <summary>State to pass to <see cref="_continuation"/>.</summary>
        private object _continuationState;

        /// <summary><see cref="ExecutionContext"/> to flow to the callback, or null if no flowing is required.</summary>
        private ExecutionContext _executionContext;

        /// <summary>
        /// A "captured" <see cref="SynchronizationContext"/> or <see cref="TaskScheduler"/> with which to invoke the callback,
        /// or null if no special context is required.
        /// </summary>
        private object _capturedContext;

        /// <summary>Whether the current operation has completed.</summary>
        internal volatile bool _completed;

        /// <summary>The result with which the operation succeeded, or the default value if it hasn't yet completed or failed.</summary>
        private bool _result;

        /// <summary>The exception with which the operation failed, or null if it hasn't yet completed or completed successfully.</summary>
        private ExceptionDispatchInfo _error;

        /// <summary>The current version of this value, used to help prevent misuse.</summary>
        private short _version;

        private long _isLocked = 1L;
        private bool _hasSkippedUpdate;

        // TODO try to use a single field
        private IDisposable _subscription;

        private IAsyncSubscription _subscriptionEx;

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

            _continuation = null;
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
            get => _innerCursor;
        }

        #region Async synchronization

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryOwnAndReset()
        {
            unchecked
            {
                _version++;
            }
            _completed = false;
            _result = default;
            _error = null;
            _executionContext = null;
            _capturedContext = null;
            _continuation = null;
            _continuationState = null;

            _isLocked = 1L;
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
                _subscriptionEx = _subscription as IAsyncSubscription;
            }

            if (ReferenceEquals(_subscription, _nullSubscriptionSentinel))
            {
                // NB last chance, no async support
                return new ValueTask<bool>(_innerCursor.MoveNext());
            }

            TryOwnAndReset();

            if (GetStatus(_version) == ValueTaskSourceStatus.Succeeded)
            {
                return new ValueTask<bool>(GetResult(_version));
            }

            return new ValueTask<bool>(this, _version);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTaskSourceStatus GetStatus(short token)
        {
            ValidateToken(token);

            // Do not try cursor move here, it's done already in MNA, then we check if value is available after OnCompleted call

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
                ThrowGetResultUncompleted();
            }

            // Volatile.Write(ref _continuation, SAvailableSentinel);

            _error?.Throw();
            return _result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateToken(short token)
        {
            if (token != _version)
            {
                ThrowBadToken();
            }
        }

        // This method is used to chain/combine Spreads's logic, actual execution on a completion/consumer thread happens in Execute method.
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryComplete(bool cancel)
        {
            if (cancel)
            {
                _error = _error ?? ExceptionDispatchInfo.Capture(new OperationCanceledException());
            }

            // NB: OnCompleted opens the lock (set to 0). If there is no awaiter then
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

            SpreadsThreadPool.Default.UnsafeQueueCompletableItem(this, false); // TODO (review) currently hangs with true.
        }

        /// <summary>
        /// This method is normally called by a thread pool worker. If there is no context (execution or scheduling) then execute continuation synchronuously
        /// since we are on a threadpool worker thread or a caller of this method knows what is going on.
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
                    Volatile.Write(ref _hasSkippedUpdate, false);

                    if (_innerCursor.MoveNext())
                    {
                        _subscriptionEx?.RequestNotification(-1);

                        SetResult(true);
                        LogAsync();
                        return;
                    }

                    if (_innerCursor.IsCompleted)
                    {
                        // not needed: if completed then there will be no updates
                        _subscriptionEx?.RequestNotification(-1);

                        var moved = _innerCursor.MoveNext();
                        SetResult(moved);
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
                    // logically recursive call but via thread pool
                    TryComplete(false);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                SetException(e); // TODO (low) review https://github.com/dotnet/roslyn/issues/26567; we may want to move this out of the catch
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

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (continuation == null) // TODO (review) #ifdebug ?
            {
                FailContinuationIsNull();
            }
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
            Debug.Assert(_capturedContext == null);
            SynchronizationContext sc = null;
            TaskScheduler ts = null;
            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                sc = SynchronizationContext.Current;
                if (sc != null && sc.GetType() != typeof(SynchronizationContext))
                {
                    _capturedContext = sc;
                }
                else
                {
                    sc = null;
                    ts = TaskScheduler.Current;
                    if (ts != TaskScheduler.Default)
                    {
                        _capturedContext = ts;
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
                Debug.Assert(IsCompleted, $"Expected IsCompleted");
                if (!ReferenceEquals(prevContinuation, SCompletedSentinel))
                {
                    // Debug.Assert(prevContinuation != SAvailableSentinel, "Continuation was the available sentinel.");
                    ThrowMultipleContinuations();
                }

                // From S.Th.Channels:
                // Queue the continuation.  We always queue here, even if !RunContinuationsAsynchronously, in order
                // to avoid stack diving; this path happens in the rare race when we're setting up to await and the
                // object is completed after the awaiter.IsCompleted but before the awaiter.OnCompleted.
                if (_capturedContext == null)
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
                    // ReSharper disable AssignNullToNotNullAttribute
                    Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                    // ReSharper restore AssignNullToNotNullAttribute
                }
            }
            else
            {
                // TODO (!) get rid of this
                // we will lose the root after this method method returns without
                // completion, so we keep the reference to this cursor alive
                // https://github.com/dotnet/coreclr/issues/19161
                // _keepAliveHandle = GCHandle.Alloc(this);
                // Console.WriteLine("HANDLE");

                // if we request notification before releasing the lock then
                // we will have _hasSkippedUpdate flag set. Then we retry ourselves anyway

                _subscriptionEx?.RequestNotification(1);
                Volatile.Write(ref _isLocked, 0L);
                // Retry self, _continuations is now set, last chance to get result
                // without external notification. If cannot move from here
                // lock will remain open
                TryComplete(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void QueueUserWorkItem(Action<object> action, object state)
        {
#if NETCOREAPP3_0
            ThreadPool.QueueUserWorkItem(action, state, preferLocal: false);
#else
            Task.Factory.StartNew(action, state,
                CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCompletionAndInvokeContinuation()
        {
            if (_executionContext == null)
            {
                Action<object> c = _continuation;
                _continuation = SCompletedSentinel;
                c(_continuationState);
            }
            else
            {
                SetCompletionAndInvokeContinuationCtx();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SetCompletionAndInvokeContinuationCtx()
        {
            ExecutionContext.Run(_executionContext, s =>
            {
                var thisRef = (AsyncCursor<TKey, TValue, TCursor>)s;
                Action<object> c = thisRef._continuation;
                thisRef._continuation = SCompletedSentinel;
                c(thisRef._continuationState);
            }, this);
        }

        /// <summary>Signals to a registered continuation that the operation has now completed.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SignalCompletion()
        {
            if (_completed)
            {
                ThrowSignallingAlreadyCompleted();
            }
            _completed = true;

            if (_continuation != null || Interlocked.CompareExchange(ref _continuation, SCompletedSentinel, null) != null)
            {
                Debug.Assert(_continuation != SCompletedSentinel, $"The continuation was the completion sentinel.");
                // Debug.Assert(_continuation != SAvailableSentinel, $"The continuation was the available sentinel.");

                if (_capturedContext == null)
                {
                    // There's no captured scheduling context.
                    // Fall through to invoke it synchronously.
                }
                else if (_capturedContext is SynchronizationContext sc)
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
                    // There's a captured TaskScheduler.  If we're forced to run continuations asynchronously,
                    // or if there's a current scheduler that's not the one we're targeting, queue it.
                    // Otherwise fall through to invoke it synchronously.
                    TaskScheduler ts = (TaskScheduler)_capturedContext;
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowGetResultUncompleted()
        {
            ThrowHelper.ThrowInvalidOperationException("_completed = false in GetResult");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBadToken()
        {
            ThrowHelper.FailFast("token != _version");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowMultipleContinuations()
        {
            ThrowHelper.ThrowInvalidOperationException("Multiple continuations");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FailContinuationIsNull()
        {
            ThrowHelper.FailFast("continuation");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowSignallingAlreadyCompleted()
        {
            ThrowHelper.ThrowInvalidOperationException("Calling SignalCompletion on already completed task");
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
            Console.WriteLine("dispose");
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
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        ~AsyncCursor()
        {
            //Console.WriteLine("Async cursor finalized: " + _st);
            Console.WriteLine("-----------------------------------");
            Dispose(false);
        }

        public Task DisposeAsync()
        {
            Reset();
            return _innerCursor.DisposeAsync();
        }
    }
}
