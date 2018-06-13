// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reset()
        {
            _syncCount = 0;
            _asyncCount = 0;
            _awaitCount = 0;
        }
    }

    internal class BaseCursorAsync
    {
        protected static readonly Action<object> SCompletedSentinel = s => throw new InvalidOperationException("Called completed sentinel");
        protected static readonly Action<object> SAvailableSentinel = s => throw new InvalidOperationException("Called available sentinel");
    }

    internal sealed class BaseCursorAsync<TKey, TValue, TCursor> : BaseCursorAsync,
        ISpecializedCursor<TKey, TValue, TCursor>,
        IValueTaskSource<bool>,
        IAsyncStateMachine
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        // TODO Pooling, but see #84

        // NB this is often a struct, should not be made readonly!
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private TCursor _innerCursor;

        private AsyncTaskMethodBuilder _builder = AsyncTaskMethodBuilder.Create();

        private Action<object> _continuation;
        private object _continuationState;
        private object _capturedContext;
        private ExecutionContext _executionContext;
        internal volatile bool _completed;
        private bool _result;
        private ExceptionDispatchInfo _error;
        private short _version;

        public BaseCursorAsync(Func<TCursor> cursorFactory) : this(cursorFactory())
        { }

        public BaseCursorAsync(TCursor cursor)
        {
            _innerCursor = cursor;
            if (_innerCursor.Source == null)
            {
                Console.WriteLine("Source is null");
            }

            _continuation = SAvailableSentinel;
            _continuationState = null;
            _capturedContext = null;
            _executionContext = null;
            _completed = false;
            _error = null;
            _version = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetStateMachine()
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _continuation, null, SAvailableSentinel),
                SAvailableSentinel))
            {
                unchecked
                {
                    _version++;
                }

                _completed = false;
                _result = default;
                _continuationState = null;
                _error = null;
                _executionContext = null;
                _capturedContext = null;
            }
            else
            {
                ThrowHelper.ThrowInvalidOperationException("Cannot reset");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueTask<bool> GetMoveNextAsyncValueTask()
        {
            if (_innerCursor.MoveNext())
            {
                AsyncCursorCounters.LogSync();
                return new ValueTask<bool>(true);
            }

            if (_innerCursor.Source.IsCompleted)
            {
                if (_innerCursor.MoveNext())
                {
                    AsyncCursorCounters.LogSync();
                    return new ValueTask<bool>(true);
                }
                AsyncCursorCounters.LogSync();
                return new ValueTask<bool>(false);
            }

            ResetStateMachine();

            var inst = this;
            _builder.Start(ref inst); // invokes MoveNext, protected by ExecutionContext guards

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
                ThrowHelper.ThrowInvalidOperationException("token != _version");
            }
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IAsyncStateMachine.MoveNext()
        {
            if (_completed)
            {
                return;
            }
            try
            {
                if (_innerCursor.MoveNext())
                {
                    SetResult(true);
                    AsyncCursorCounters.LogAsync();
                    return;
                }

                if (_innerCursor.Source.IsCompleted)
                {
                    if (_innerCursor.MoveNext())
                    {
                        SetResult(true);
                        AsyncCursorCounters.LogAsync();
                        return;
                    }

                    SetResult(false);
                    AsyncCursorCounters.LogAsync();
                    return;
                }

                AsyncCursorCounters.LogAwait();
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
                ThrowHelper.ThrowArgumentNullException(nameof(continuation));
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

            if (_continuationState != null)
            {
                ThrowHelper.ThrowInvalidOperationException("Multiple continuations");
            }
            _continuationState = state;

            Action<object> prevContinuation = Interlocked.CompareExchange(ref _continuation, continuation, null);

            if (prevContinuation != null)
            {
                if (!ReferenceEquals(prevContinuation, SCompletedSentinel))
                {
                    Debug.Assert(prevContinuation != SAvailableSentinel, "Continuation was the available sentinel.");
                    ThrowHelper.ThrowInvalidOperationException("Multiple continuations");
                }

                _executionContext = null;

                object cc = _capturedContext;
                _capturedContext = null;

                switch (cc)
                {
                    case null:
#if NETCOREAPP2_1
                        ThreadPool.QueueUserWorkItem(continuation, state, true);
#else
                        ThreadPool.QueueUserWorkItem(new WaitCallback(continuation), state);
#endif
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SignalCompletion()
        {
            if (_completed)
            {
                ThrowHelper.ThrowInvalidOperationException("Calling SignalCompletion on already completed task");
            }
            _completed = true;

            if (Interlocked.CompareExchange(ref _continuation, SCompletedSentinel, null) != null)
            {
                if (_continuation == SCompletedSentinel || _continuation == SAvailableSentinel)
                {
                    Console.WriteLine("CONT == SENT");
                    // return;
                    // ThrowHelper.ThrowInvalidOperationException("Wrong continuation");
                }

                if (_executionContext != null)
                {
                    ExecutionContext.Run(_executionContext, s => InvokeContinuation(), null);
                }
                else
                {
                    InvokeContinuation();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvokeContinuation()
        {
            object cc = _capturedContext;
            _capturedContext = null;

            switch (cc)
            {
                case null:
                    SetCompletionAndInvokeContinuation();
                    break;

                case SynchronizationContext sc:
                    sc.Post(s => { SetCompletionAndInvokeContinuation(); }, null);
                    break;

                case TaskScheduler ts:
                    Task.Factory.StartNew(s => ((BaseCursorAsync<TKey, TValue, TCursor>)s).SetCompletionAndInvokeContinuation(), this, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                    break;
            }
        }

        private void SetCompletionAndInvokeContinuation()
        {
            Action<object> c = _continuation;
            _continuation = SCompletedSentinel;
            c(_continuationState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> MoveNextAsync()
        {
            return GetMoveNextAsyncValueTask();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            return _innerCursor.MoveNext();
        }

        public void Reset()
        {
            ResetStateMachine();
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

        private void Dispose(bool disposing)
        {
            if (!disposing) return;

            Reset();
            _innerCursor?.Dispose();
            // TODO (docs) a disposed cursor could still be used as a cursor factory and is actually used
            // via Source.GetCursor(). This must be clearly mentioned in cursor specification
            // and be a part of contracts test suite
            // NB don't do this: _innerCursor = default(TCursor);
        }

        public Task DisposeAsync()
        {
            Reset();
            return _innerCursor.DisposeAsync();
        }
    }
}