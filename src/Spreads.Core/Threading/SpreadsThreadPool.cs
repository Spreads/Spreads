// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// This implementation is a mix of works-stealing queue from .NET
// and Helios.DedicatedThreadPool.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Copyright 2015-2016 Roger Alsing, Aaron Stannard, Jeff Cyr
// Helios.DedicatedThreadPool - https://github.com/helios-io/DedicatedThreadPool

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
#pragma warning disable 420

namespace Spreads.Threading
{
    /// <summary>
    /// The type of threads to use - either foreground or background threads.
    /// </summary>
    public enum ThreadType
    {
        Foreground,
        Background
    }

    /// <summary>
    /// Provides settings for a dedicated thread pool
    /// </summary>
    public class ThreadPoolSettings
    {
        /// <summary>
        /// Background threads are the default thread type
        /// </summary>
        public const ThreadType DefaultThreadType = ThreadType.Background;

        public ThreadPoolSettings(int numThreads,
                                           string name = null,
                                           ApartmentState apartmentState = ApartmentState.Unknown,
                                           Action<Exception> exceptionHandler = null,
                                           int threadMaxStackSize = 0,
                                            ThreadPriority threadPriority = ThreadPriority.Normal)
            : this(numThreads, DefaultThreadType, name, apartmentState, exceptionHandler, threadMaxStackSize, threadPriority)
        { }

        public ThreadPoolSettings(int numThreads,
                                   ThreadType threadType,
                                   string? name = null,
                                   ApartmentState apartmentState = ApartmentState.Unknown,
                                   Action<Exception>? exceptionHandler = null,
                                   int threadMaxStackSize = 0,
                                   ThreadPriority threadPriority = ThreadPriority.Normal)
        {
            Name = name ?? ("DedicatedThreadPool-" + Guid.NewGuid());
            ThreadType = threadType;
            ThreadPriority = threadPriority;
            NumThreads = numThreads;
            ApartmentState = apartmentState;
            ExceptionHandler = exceptionHandler ?? (ex =>
            {
                Environment.FailFast("Unhandled exception in the dedicated thread pool: " + ex);
            });
            ThreadMaxStackSize = threadMaxStackSize;

#if !HAS_TPWORKITEM
            if (numThreads <= 0)
                throw new ArgumentOutOfRangeException(nameof(numThreads), $"numThreads must be at least 1. Was {numThreads}");
#endif
        }

        /// <summary>
        /// The total number of threads to run in this thread pool.
        /// </summary>
        public int NumThreads { get; }

        /// <summary>
        /// The type of threads to run in this thread pool.
        /// </summary>
        public ThreadType ThreadType { get; }

        public ThreadPriority ThreadPriority { get; }

        /// <summary>
        /// Apartment state for threads to run in this thread pool
        /// </summary>
        public ApartmentState ApartmentState { get; }

        public string Name { get; }

        public Action<Exception> ExceptionHandler { get; }

        /// <summary>
        /// Gets the thread stack size, 0 represents the default stack size.
        /// </summary>
        public int ThreadMaxStackSize { get; }
    }

    /// <summary>
    /// Non-allocating thread pool.
    /// </summary>
    /// <remarks>
    /// When using <see cref="Default"/> pool, it polyfills <see cref="ThreadPool.UnsafeQueueUserWorkItem(IThreadPoolWorkItem, bool)"/>
    /// method for .NET Standard using <see cref="ISpreadsThreadPoolWorkItem"/> and uses
    /// <see cref="ThreadPool"/> for .NET Core 3 and above.
    /// <para/>
    /// When using non-<see cref="Default"/> pools, a custom implementation
    /// with <see cref="ThreadPoolSettings"/> is always used.
    /// </remarks>
    public class SpreadsThreadPool
    {
        // It's shared and there is a comment from MSFT that the number should
        // be larger than intuition tells. By default ThreadPool has number
        // of workers equals to processor count.
        public static readonly int DefaultDedicatedWorkerThreads =
            1 * 4 + 1 * 2 + Environment.ProcessorCount * 2;

        // Without accessing this namespace and class it is not created
        private static SpreadsThreadPool? _default;
        private static SpreadsThreadPool? _background;

        private TaskScheduler _scheduler;

        public static SpreadsThreadPool Default
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_default == null)
                {
                    InitDefault();
                }
                return _default;
            }
        }

        /// <summary>
        /// A thread pool for lower priority tasks. By default
        /// it is the same pool as <see cref="Default"/>.
        /// </summary>
        /// <remarks>
        /// Could be set to a custom thread pool with lower thread priority,
        /// which might improve performance of the main thread pool.
        /// But keep in mind that tasks such as returning unused
        /// objects/buffers to an object/buffer pool could affect
        /// main threads performance (empty buffers could cause allocations).
        /// Instead of using lower thread priority a small number of threads
        /// in the background pool could be a better option.
        /// </remarks>
        public static SpreadsThreadPool Background
        {
            get => _background ?? Default;
            set
            {
                if(value != Default)
                    _background = value;
            }
        }

        public TaskScheduler Scheduler
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_scheduler == null)
                {
                    CreateScheduler();
                }
                return _scheduler;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CreateScheduler()
        {
            lock (this)
            {
                if (_scheduler == null)
                {
                    _scheduler = new SpreadsThreadPoolTaskScheduler(this);
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitDefault()
        {
            lock (typeof(SpreadsThreadPool))
            {
                if (_default == null)
                {
                    const string tpName = "DefaultSpreadsThreadPool";
                    var settings = new ThreadPoolSettings(DefaultDedicatedWorkerThreads,
                        tpName);
#if HAS_TPWORKITEM
                    var settings0 = new ThreadPoolSettings(0,
                        tpName);
                    _default = new SpreadsThreadPool(settings0);
#else

                    _default = new SpreadsThreadPool(settings);
#endif
                    ThreadPool.SetMinThreads(settings.NumThreads, settings.NumThreads);
                }
            }
        }

        internal readonly ThreadPoolWorkQueue workQueue;
        public ThreadPoolSettings Settings { get; }
        private readonly PoolWorker[] _workers;

        public SpreadsThreadPool(ThreadPoolSettings settings)
        {
            workQueue = new ThreadPoolWorkQueue(this);
            Settings = settings;
            _workers = new PoolWorker[settings.NumThreads];

            for (int i = 0; i < settings.NumThreads; i++)
            {
                _workers[i] = new PoolWorker(this);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeQueueCompletableItem(ISpreadsThreadPoolWorkItem workItem, bool preferLocal)
        {
#if HAS_TPWORKITEM
            if (this == Default)
            {
                ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal);
                return;
            }
#endif
            if (workItem == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(workItem));
            }
            workQueue.Enqueue(workItem, forceGlobal: !preferLocal);
        }

        // Get all workitems.  Called by TaskScheduler in its debugger hooks.
        internal IEnumerable<ISpreadsThreadPoolWorkItem> GetQueuedWorkItems()
        {
            // Enumerate global queue
            foreach (var workItem in workQueue.workItems)
            {
                yield return workItem;
            }

            // Enumerate each local queue
            foreach (ThreadPoolWorkQueue.WorkStealingQueue wsq in ThreadPoolWorkQueue.WorkStealingQueueList.Queues)
            {
                if (wsq != null && wsq.m_array != null)
                {
                    ISpreadsThreadPoolWorkItem[] items = wsq.m_array;
                    for (int i = 0; i < items.Length; i++)
                    {
                        ISpreadsThreadPoolWorkItem item = items[i];
                        if (item != null)
                        {
                            yield return item;
                        }
                    }
                }
            }
        }

        internal IEnumerable<ISpreadsThreadPoolWorkItem> GetLocallyQueuedWorkItems()
        {
            ThreadPoolWorkQueue.WorkStealingQueue wsq = ThreadPoolWorkQueue.ThreadPoolWorkQueueThreadLocals.threadLocals.workStealingQueue;
            if (wsq != null && wsq.m_array != null)
            {
                ISpreadsThreadPoolWorkItem[] items = wsq.m_array;
                for (int i = 0; i < items.Length; i++)
                {
                    ISpreadsThreadPoolWorkItem item = items[i];
                    if (item != null)
                        yield return item;
                }
            }
        }

        internal IEnumerable<ISpreadsThreadPoolWorkItem> GetGloballyQueuedWorkItems() => workQueue.workItems;

        private object[] ToObjectArray(IEnumerable<ISpreadsThreadPoolWorkItem> workitems)
        {
            int i = 0;
            // ReSharper disable once PossibleMultipleEnumeration
            foreach (var _ in workitems)
            {
                i++;
            }

            object[] result = new object[i];
            i = 0;
            // ReSharper disable once PossibleMultipleEnumeration
            foreach (var item in workitems)
            {
                if (i < result.Length) //just in case someone calls us while the queues are in motion
                    result[i] = item;
                i++;
            }

            return result;
        }

        // This is the method the debugger will actually call, if it ends up calling
        // into ThreadPool directly.  Tests can use this to simulate a debugger, as well.
        internal object[] GetQueuedWorkItemsForDebugger() =>
            ToObjectArray(GetQueuedWorkItems());

        internal object[] GetGloballyQueuedWorkItemsForDebugger() =>
            ToObjectArray(GetGloballyQueuedWorkItems());

        internal object[] GetLocallyQueuedWorkItemsForDebugger() =>
            ToObjectArray(GetLocallyQueuedWorkItems());

        public void Dispose()
        {
            workQueue.CompleteAdding();
        }

        public void WaitForThreadsExit()
        {
            WaitForThreadsExit(Timeout.InfiniteTimeSpan);
        }

        public void WaitForThreadsExit(TimeSpan timeout)
        {
            if (this == Default)
            {
                return;
            }
            Task.WaitAll(_workers.Select(worker => worker.ThreadExit).ToArray(), timeout);
        }

        #region Pool worker implementation

        private class PoolWorker
        {
            private readonly SpreadsThreadPool _pool;

            private readonly TaskCompletionSource<object> _threadExit;

            public Task ThreadExit
            {
                get { return _threadExit.Task; }
            }

            public PoolWorker(SpreadsThreadPool pool)
            {
                _pool = pool;
                _threadExit = new TaskCompletionSource<object>();

                var thread = new Thread(RunThread, pool.Settings.ThreadMaxStackSize);

                thread.IsBackground = pool.Settings.ThreadType == ThreadType.Background;

                thread.Priority = pool.Settings.ThreadPriority;

                if (pool.Settings.Name != null)
                {
                    thread.Name = pool.Settings.Name + "_worker";
                }

                if (pool.Settings.ApartmentState != ApartmentState.Unknown)
                    thread.SetApartmentState(pool.Settings.ApartmentState);

                thread.Start();
            }

            private void RunThread()
            {
                try
                {
                    _pool.workQueue.Dispatch();
                }
                finally
                {
                    _threadExit.TrySetResult(null);
                }
            }
        }

        #endregion Pool worker implementation
    }

    /// <summary>
    /// TaskScheduler for working with a <see cref="SpreadsThreadPool"/> instance
    /// </summary>
    internal class SpreadsThreadPoolTaskScheduler : TaskScheduler, ISpreadsThreadPoolWorkItem
    {
        // Indicates whether the current thread is processing work items.
        [ThreadStatic]
        private static bool _currentThreadIsRunningTasks;

        /// <summary>
        /// Number of tasks currently running
        /// </summary>
        private volatile int _parallelWorkers;

        private readonly LinkedList<Task> _tasks = new LinkedList<Task>();

        private readonly SpreadsThreadPool _pool;

        public SpreadsThreadPoolTaskScheduler(SpreadsThreadPool pool)
        {
            _pool = pool;
        }

        protected override void QueueTask(Task task)
        {
            lock (_tasks)
            {
                _tasks.AddLast(task);
            }

            EnsureWorkerRequested();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            //current thread isn't running any tasks, can't execute inline
            if (!_currentThreadIsRunningTasks) return false;

            //remove the task from the queue if it was previously added
            if (taskWasPreviouslyQueued)
                if (TryDequeue(task))
                    return TryExecuteTask(task);
                else
                    return false;
            return TryExecuteTask(task);
        }

        protected override bool TryDequeue(Task task)
        {
            lock (_tasks) return _tasks.Remove(task);
        }

        /// <summary>
        /// Level of concurrency is directly equal to the number of threads
        /// in the <see cref="SpreadsThreadPool"/>.
        /// </summary>
        public override int MaximumConcurrencyLevel => _pool.Settings.NumThreads;

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            var lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);

                //should this be immutable?
                if (lockTaken) return _tasks;
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_tasks);
            }
        }

        private void EnsureWorkerRequested()
        {
            var count = _parallelWorkers;
            while (count < _pool.Settings.NumThreads)
            {
                var prev = Interlocked.CompareExchange(ref _parallelWorkers, count + 1, count);
                if (prev == count)
                {
                    RequestWorker();
                    break;
                }
                count = prev;
            }
        }

        private void ReleaseWorker()
        {
            var count = _parallelWorkers;
            while (count > 0)
            {
                var prev = Interlocked.CompareExchange(ref _parallelWorkers, count - 1, count);
                if (prev == count)
                {
                    break;
                }
                count = prev;
            }
        }

        private void RequestWorker()
        {
            _pool.UnsafeQueueCompletableItem(this, true);
        }

        public void Execute()
        {
            // this thread is now available for inlining
            _currentThreadIsRunningTasks = true;
            try
            {
                // Process all available items in the queue.
                while (true)
                {
                    Task item;
                    lock (_tasks)
                    {
                        // done processing
                        if (_tasks.Count == 0)
                        {
                            ReleaseWorker();
                            break;
                        }

                        // Get the next item from the queue
                        item = _tasks.First.Value;
                        _tasks.RemoveFirst();
                    }

                    // Execute the task we pulled out of the queue
                    TryExecuteTask(item);
                }
            }
            // We're done processing items on the current thread
            finally { _currentThreadIsRunningTasks = false; }
        }
    }
}
