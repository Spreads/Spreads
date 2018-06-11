// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Spreads.Collections.Experimental
{
    // The following is a sketch of an implementation to explore the IAsyncEnumerable feature.
    // This should be replaced with the real implementation when available.
    // https://github.com/dotnet/csharplang/issues/43

    public interface IAsyncEnumerable<out T>
    {
        IAsyncEnumerator<T> GetAsyncEnumerator();
    }

    public interface IAsyncEnumerator<out T> : IAsyncDisposable
    {
        // One of two potential shapes for IAsyncEnumerator; another is
        //     ValueTask<bool> WaitForNextAsync();
        //     bool TryGetNext(out T current);
        // which has several advantages, including that while the next
        // result is available synchronously, it incurs only one interface
        // call rather than two, and doesn't incur any boilerplate related
        // to await.

        ValueTask<bool> MoveNextAsync();

        T Current { get; }
    }

    public interface IAsyncDisposable
    {
        ValueTask DisposeAsync();
    }

    // Approximate compiler-generated code for:
    //     internal static AsyncEnumerable<int> CountAsync(int items)
    //     {
    //         for (int i = 0; i < items; i++)
    //         {
    //             await Task.Delay(i).ConfigureAwait(false);
    //             yield return i;
    //         }
    //     }

    public class TestVTS : IValueTaskSource
    {
        private ConcurrentQueue<(Action<object>, object)> _queue = new ConcurrentQueue<(Action<object>, object)>();

        public ValueTask GetValueTask() => new ValueTask(this, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTaskSourceStatus GetStatus(short token)
        {
            // Console.WriteLine("GetStatus");
            return ValueTaskSourceStatus.Pending;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _queue.Enqueue((continuation, state));
            Notify();
            // Console.WriteLine("OnCompleted");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetResult(short token)
        {
            // Console.WriteLine("GetResult");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Notify()
        {
            while (_queue.TryDequeue(out var item))
            {
                item.Item1(item.Item2);
                //var a = ;
                //var wcb = Unsafe.As<Action<object>, WaitCallback>(ref a);
                //ThreadPool.UnsafeQueueUserWorkItem(wcb, item.Item2);
            }
        }
    }

    public sealed class CountAsyncEnumerable :
        IAsyncEnumerable<int>,  // used as the enumerable itself
        IAsyncEnumerator<int>,  // used as the enumerator returned from first call to enumerable's GetAsyncEnumerator
        IValueTaskSource<bool>, // used as the backing store behind the ValueTask<bool> returned from each MoveNextAsync
        IStrongBox<ManualResetValueTaskSourceLogic<bool>>, // exposes its ValueTaskSource logic implementation
        IAsyncStateMachine // uses existing builder's support for ExecutionContext, optimized awaits, etc.
    {
        // This implementation will generally incur only two allocations of overhead
        // for the entire enumeration:
        // - The CountAsyncEnumerable object itself.
        // - A throw-away task object inside of _builder.
        // The task built by the builder isn't necessary, but using the _builder allows
        // this implementation to a) avoid needing to be concerned with ExecutionContext
        // flowing, and b) enables the implementation to take advantage of optimizations
        // such as avoiding Action allocation when all awaited types are known to corelib.

        private const int StateStart = -1;
        private const int StateDisposed = -2;
        private const int StateCtor = -3;

        /// <summary>Current state of the state machine.</summary>
        private int _state = StateCtor;

        /// <summary>All of the logic for managing the IValueTaskSource implementation</summary>
        private ManualResetValueTaskSourceLogic<bool> _vts; // mutable struct; do not make this readonly

        /// <summary>Builder used for efficiently waiting and appropriately managing ExecutionContext.</summary>
        private AsyncTaskMethodBuilder _builder = AsyncTaskMethodBuilder.Create(); // mutable struct; do not make this readonly

        private readonly int _param_items;

        private int _local_items;
        private int _local_i;
        private TaskAwaiter _awaiter0;

        public CountAsyncEnumerable(int items)
        {
            _local_items = _param_items = items;
            _vts = new ManualResetValueTaskSourceLogic<bool>(this);
        }

        ref ManualResetValueTaskSourceLogic<bool> IStrongBox<ManualResetValueTaskSourceLogic<bool>>.Value => ref _vts;

        public IAsyncEnumerator<int> GetAsyncEnumerator() =>
            Interlocked.CompareExchange(ref _state, StateStart, StateCtor) == StateCtor ?
                this :
                new CountAsyncEnumerable(_param_items) { _state = StateStart };

        public ValueTask<bool> MoveNextAsync()
        {
            _vts.Reset();

            CountAsyncEnumerable inst = this;
            _builder.Start(ref inst); // invokes MoveNext, protected by ExecutionContext guards

            switch (_vts.GetStatus(_vts.Version))
            {
                case ValueTaskSourceStatus.Succeeded:
                    return new ValueTask<bool>(_vts.GetResult(_vts.Version));

                default:
                    return new ValueTask<bool>(this, _vts.Version);
            }
        }

        public ValueTask DisposeAsync()
        {
            _vts.Reset();
            _state = StateDisposed;
            return default;
        }

        public int Current { get; private set; }

        public void MoveNext()
        {
            try
            {
                switch (_state)
                {
                    case StateStart:
                        _local_i = 0;
                        goto case 0;

                    case 0:
                        if (_local_i < _local_items)
                        {
                            //Current = _local_i;
                            //_state = 2;
                            //_vts.SetResult(true);
                            //return;

                            _awaiter0 = Task.Delay(1).GetAwaiter();// Task.CompletedTask.GetAwaiter();
                            if (!_awaiter0.IsCompleted)
                            {
                                _state = 1;
                                CountAsyncEnumerable inst = this;
                                _builder.AwaitUnsafeOnCompleted(ref _awaiter0, ref inst);
                                return;
                            }
                            goto case 1;
                        }
                        _state = int.MaxValue;
                        _vts.SetResult(false);
                        return;

                    case 1:
                        _awaiter0.GetResult();
                        _awaiter0 = default;

                        Current = _local_i;
                        _state = 2;
                        _vts.SetResult(true);
                        return;

                    case 2:
                        _local_i++;
                        _state = 0;
                        goto case 0;

                    default:
                        throw new InvalidOperationException();
                }
            }
            catch (Exception e)
            {
                _state = int.MaxValue;
                _vts.SetException(e); // see https://github.com/dotnet/roslyn/issues/26567; we may want to move this out of the catch
                return;
            }
        }

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
        {
        }

        bool IValueTaskSource<bool>.GetResult(short token) => _vts.GetResult(token);

        ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => _vts.GetStatus(token);

        void IValueTaskSource<bool>.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _vts.OnCompleted(continuation, state, token, flags);
    }

    public sealed class ManualResetValueTaskSource<T> : IStrongBox<ManualResetValueTaskSourceLogic<T>>, IValueTaskSource<T>, IValueTaskSource
    {
        private ManualResetValueTaskSourceLogic<T> _logic; // mutable struct; do not make this readonly

        public ManualResetValueTaskSource() => _logic = new ManualResetValueTaskSourceLogic<T>(this);

        public short Version => _logic.Version;

        public void Reset() => _logic.Reset();

        public void SetResult(T result) => _logic.SetResult(result);

        public void SetException(Exception error) => _logic.SetException(error);

        public T GetResult(short token) => _logic.GetResult(token);

        void IValueTaskSource.GetResult(short token) => _logic.GetResult(token);

        public ValueTaskSourceStatus GetStatus(short token) => _logic.GetStatus(token);

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags) => _logic.OnCompleted(continuation, state, token, flags);

        ref ManualResetValueTaskSourceLogic<T> IStrongBox<ManualResetValueTaskSourceLogic<T>>.Value => ref _logic;
    }

    public interface IStrongBox<T>
    {
        ref T Value { get; }
    }

    public struct ManualResetValueTaskSourceLogic<TResult>
    {
        private static readonly Action<object> s_sentinel = new Action<object>(s => throw new InvalidOperationException());

        private readonly IStrongBox<ManualResetValueTaskSourceLogic<TResult>> _parent;
        private Action<object> _continuation;
        private object _continuationState;
        private object _capturedContext;
        private ExecutionContext _executionContext;
        private bool _completed;
        private TResult _result;
        private ExceptionDispatchInfo _error;
        private short _version;

        public ManualResetValueTaskSourceLogic(IStrongBox<ManualResetValueTaskSourceLogic<TResult>> parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _continuation = null;
            _continuationState = null;
            _capturedContext = null;
            _executionContext = null;
            _completed = false;
            _result = default;
            _error = null;
            _version = 0;
        }

        public short Version => _version;

        private void ValidateToken(short token)
        {
            if (token != _version)
            {
                throw new InvalidOperationException();
            }
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            ValidateToken(token);

            return
                !_completed ? ValueTaskSourceStatus.Pending :
                _error == null ? ValueTaskSourceStatus.Succeeded :
                _error.SourceException is OperationCanceledException ? ValueTaskSourceStatus.Canceled :
                ValueTaskSourceStatus.Faulted;
        }

        public TResult GetResult(short token)
        {
            ValidateToken(token);

            if (!_completed)
            {
                throw new InvalidOperationException();
            }

            _error?.Throw();
            return _result;
        }

        public void Reset()
        {
            unchecked
            {
                _version++;
            }

            _completed = false;
            _continuation = null;
            _continuationState = null;
            _result = default;
            _error = null;
            _executionContext = null;
            _capturedContext = null;
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
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

            _continuationState = state;
            if (Interlocked.CompareExchange(ref _continuation, continuation, null) != null)
            {
                _executionContext = null;

                object cc = _capturedContext;
                _capturedContext = null;

                switch (cc)
                {
                    case null:
                        Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
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

        public void SetResult(TResult result)
        {
            _result = result;
            SignalCompletion();
        }

        public void SetException(Exception error)
        {
            _error = ExceptionDispatchInfo.Capture(error);
            SignalCompletion();
        }

        private void SignalCompletion()
        {
            if (_completed)
            {
                throw new InvalidOperationException();
            }
            _completed = true;

            if (Interlocked.CompareExchange(ref _continuation, s_sentinel, null) != null)
            {
                if (_executionContext != null)
                {
                    ExecutionContext.Run(
                        _executionContext,
                        s => ((IStrongBox<ManualResetValueTaskSourceLogic<TResult>>)s).Value.InvokeContinuation(),
                        _parent ?? throw new InvalidOperationException());
                }
                else
                {
                    InvokeContinuation();
                }
            }
        }

        private void InvokeContinuation()
        {
            object cc = _capturedContext;
            _capturedContext = null;

            switch (cc)
            {
                case null:
                    _continuation(_continuationState);
                    break;

                case SynchronizationContext sc:
                    sc.Post(s =>
                    {
                        ref ManualResetValueTaskSourceLogic<TResult> logicRef = ref ((IStrongBox<ManualResetValueTaskSourceLogic<TResult>>)s).Value;
                        logicRef._continuation(logicRef._continuationState);
                    }, _parent ?? throw new InvalidOperationException());
                    break;

                case TaskScheduler ts:
                    Task.Factory.StartNew(_continuation, _continuationState, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                    break;
            }
        }
    }
}