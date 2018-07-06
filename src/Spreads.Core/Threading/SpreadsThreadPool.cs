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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
#pragma warning disable 420

namespace Spreads.Threading
{
    [StructLayout(LayoutKind.Sequential)] // enforce layout so that padding reduces false sharing
    internal sealed class ThreadPoolWorkQueue
    {
        [StructLayout(LayoutKind.Explicit, Size = CACHE_LINE_SIZE - sizeof(int))]
        internal struct PaddingFor32
        {
            public const int CACHE_LINE_SIZE = 64;
        }

        private readonly SpreadsThreadPool _pool;
        private readonly UnfairSemaphore _semaphore = new UnfairSemaphore();
        private static readonly int ProcessorCount = Environment.ProcessorCount;
        private const int CompletedState = 1;

        private int _isAddingCompleted;

        public bool IsAddingCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Volatile.Read(ref _isAddingCompleted) == CompletedState; }
        }

        public void CompleteAdding()
        {
            int previousCompleted = Interlocked.Exchange(ref _isAddingCompleted, CompletedState);

            if (previousCompleted == CompletedState)
                return;

            // When CompleteAdding() is called, we fill up the _outstandingRequests and the semaphore
            // This will ensure that all threads will unblock and try to execute the remaining item in
            // the queue. When IsAddingCompleted is set, all threads will exit once the queue is empty.

            while (true)
            {
                int count = numOutstandingThreadRequests;
                int countToRelease = UnfairSemaphore.MaxWorker - count;

                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, UnfairSemaphore.MaxWorker, count);

                if (prev == count)
                {
                    _semaphore.Release((short)countToRelease);
                    break;
                }
            }
        }

        internal static class WorkStealingQueueList
        {
            private static volatile WorkStealingQueue[] _queues = new WorkStealingQueue[0];

            public static WorkStealingQueue[] Queues => _queues;

            public static void Add(WorkStealingQueue queue)
            {
                Debug.Assert(queue != null);
                while (true)
                {
                    WorkStealingQueue[] oldQueues = _queues;
                    Debug.Assert(Array.IndexOf(oldQueues, queue) == -1);

                    var newQueues = new WorkStealingQueue[oldQueues.Length + 1];
                    Array.Copy(oldQueues, 0, newQueues, 0, oldQueues.Length);
                    newQueues[newQueues.Length - 1] = queue;
                    if (Interlocked.CompareExchange(ref _queues, newQueues, oldQueues) == oldQueues)
                    {
                        break;
                    }
                }
            }

            public static void Remove(WorkStealingQueue queue)
            {
                Debug.Assert(queue != null);
                while (true)
                {
                    WorkStealingQueue[] oldQueues = _queues;
                    if (oldQueues.Length == 0)
                    {
                        return;
                    }

                    int pos = Array.IndexOf(oldQueues, queue);
                    if (pos == -1)
                    {
                        Debug.Fail("Should have found the queue");
                        return;
                    }

                    var newQueues = new WorkStealingQueue[oldQueues.Length - 1];
                    if (pos == 0)
                    {
                        Array.Copy(oldQueues, 1, newQueues, 0, newQueues.Length);
                    }
                    else if (pos == oldQueues.Length - 1)
                    {
                        Array.Copy(oldQueues, 0, newQueues, 0, newQueues.Length);
                    }
                    else
                    {
                        Array.Copy(oldQueues, 0, newQueues, 0, pos);
                        Array.Copy(oldQueues, pos + 1, newQueues, pos, newQueues.Length - pos);
                    }

                    if (Interlocked.CompareExchange(ref _queues, newQueues, oldQueues) == oldQueues)
                    {
                        break;
                    }
                }
            }
        }

        internal sealed class WorkStealingQueue
        {
            private const int INITIAL_SIZE = 32;

            internal volatile Action<object>[] m_array = new Action<object>[INITIAL_SIZE];
            private volatile int m_mask = INITIAL_SIZE - 1;

#if DEBUG

            // in debug builds, start at the end so we exercise the index reset logic.
            private const int START_INDEX = int.MaxValue;

#else
            private const int START_INDEX = 0;
#endif

            private volatile int m_headIndex = START_INDEX;
            private volatile int m_tailIndex = START_INDEX;

            private SpinLock m_foreignLock = new SpinLock(enableThreadOwnerTracking: false);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void LocalPush(Action<object> obj)
            {
                int tail = m_tailIndex;

                // We're going to increment the tail; if we'll overflow, then we need to reset our counts
                if (tail == int.MaxValue)
                {
                    bool lockTaken = false;
                    try
                    {
                        m_foreignLock.Enter(ref lockTaken);

                        if (m_tailIndex == int.MaxValue)
                        {
                            //
                            // Rather than resetting to zero, we'll just mask off the bits we don't care about.
                            // This way we don't need to rearrange the items already in the queue; they'll be found
                            // correctly exactly where they are.  One subtlety here is that we need to make sure that
                            // if head is currently < tail, it remains that way.  This happens to just fall out from
                            // the bit-masking, because we only do this if tail == int.MaxValue, meaning that all
                            // bits are set, so all of the bits we're keeping will also be set.  Thus it's impossible
                            // for the head to end up > than the tail, since you can't set any more bits than all of
                            // them.
                            //
                            m_headIndex = m_headIndex & m_mask;
                            m_tailIndex = tail = m_tailIndex & m_mask;
                            Debug.Assert(m_headIndex <= m_tailIndex);
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                            m_foreignLock.Exit(useMemoryBarrier: true);
                    }
                }

                // When there are at least 2 elements' worth of space, we can take the fast path.
                if (tail < m_headIndex + m_mask)
                {
                    Volatile.Write(ref m_array[tail & m_mask], obj);
                    m_tailIndex = tail + 1;
                }
                else
                {
                    // We need to contend with foreign pops, so we lock.
                    bool lockTaken = false;
                    try
                    {
                        m_foreignLock.Enter(ref lockTaken);

                        int head = m_headIndex;
                        int count = m_tailIndex - m_headIndex;

                        // If there is still space (one left), just add the element.
                        if (count >= m_mask)
                        {
                            // We're full; expand the queue by doubling its size.
                            var newArray = new Action<object>[m_array.Length << 1];
                            for (int i = 0; i < m_array.Length; i++)
                                newArray[i] = m_array[(i + head) & m_mask];

                            // Reset the field values, incl. the mask.
                            m_array = newArray;
                            m_headIndex = 0;
                            m_tailIndex = tail = count;
                            m_mask = (m_mask << 1) | 1;
                        }

                        Volatile.Write(ref m_array[tail & m_mask], obj);
                        m_tailIndex = tail + 1;
                    }
                    finally
                    {
                        if (lockTaken)
                            m_foreignLock.Exit(useMemoryBarrier: false);
                    }
                }
            }

            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
            public bool LocalFindAndPop(Action<object> obj)
            {
                // Fast path: check the tail. If equal, we can skip the lock.
                if (m_array[(m_tailIndex - 1) & m_mask] == obj)
                {
                    Action<object> unused = LocalPop();
                    Debug.Assert(unused == null || unused == obj);
                    return unused != null;
                }

                // Else, do an O(N) search for the work item. The theory of work stealing and our
                // inlining logic is that most waits will happen on recently queued work.  And
                // since recently queued work will be close to the tail end (which is where we
                // begin our search), we will likely find it quickly.  In the worst case, we
                // will traverse the whole local queue; this is typically not going to be a
                // problem (although degenerate cases are clearly an issue) because local work
                // queues tend to be somewhat shallow in length, and because if we fail to find
                // the work item, we are about to block anyway (which is very expensive).
                for (int i = m_tailIndex - 2; i >= m_headIndex; i--)
                {
                    if (m_array[i & m_mask] == obj)
                    {
                        // If we found the element, block out steals to avoid interference.
                        bool lockTaken = false;
                        try
                        {
                            m_foreignLock.Enter(ref lockTaken);

                            // If we encountered a race condition, bail.
                            if (m_array[i & m_mask] == null)
                                return false;

                            // Otherwise, null out the element.
                            Volatile.Write(ref m_array[i & m_mask], null);

                            // And then check to see if we can fix up the indexes (if we're at
                            // the edge).  If we can't, we just leave nulls in the array and they'll
                            // get filtered out eventually (but may lead to superfluous resizing).
                            if (i == m_tailIndex)
                                m_tailIndex -= 1;
                            else if (i == m_headIndex)
                                m_headIndex += 1;

                            return true;
                        }
                        finally
                        {
                            if (lockTaken)
                                m_foreignLock.Exit(useMemoryBarrier: false);
                        }
                    }
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Action<object> LocalPop() => m_headIndex < m_tailIndex ? LocalPopCore() : null;

            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Action<object> LocalPopCore()
            {
                while (true)
                {
                    int tail = m_tailIndex;
                    if (m_headIndex >= tail)
                    {
                        return null;
                    }

                    // Decrement the tail using a fence to ensure subsequent read doesn't come before.
                    tail -= 1;
                    Interlocked.Exchange(ref m_tailIndex, tail);

                    // If there is no interaction with a take, we can head down the fast path.
                    if (m_headIndex <= tail)
                    {
                        int idx = tail & m_mask;
                        Action<object> obj = Volatile.Read(ref m_array[idx]);

                        // Check for nulls in the array.
                        if (obj == null) continue;

                        m_array[idx] = null;
                        return obj;
                    }
                    else
                    {
                        // Interaction with takes: 0 or 1 elements left.
                        bool lockTaken = false;
                        try
                        {
                            m_foreignLock.Enter(ref lockTaken);

                            if (m_headIndex <= tail)
                            {
                                // Element still available. Take it.
                                int idx = tail & m_mask;
                                Action<object> obj = Volatile.Read(ref m_array[idx]);

                                // Check for nulls in the array.
                                if (obj == null) continue;

                                m_array[idx] = null;
                                return obj;
                            }
                            else
                            {
                                // If we encountered a race condition and element was stolen, restore the tail.
                                m_tailIndex = tail + 1;
                                return null;
                            }
                        }
                        finally
                        {
                            if (lockTaken)
                                m_foreignLock.Exit(useMemoryBarrier: false);
                        }
                    }
                }
            }

            public bool CanSteal
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return m_headIndex < m_tailIndex; }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Action<object> TrySteal(ref bool missedSteal)
            {
                while (true)
                {
                    if (CanSteal)
                    {
                        bool taken = false;
                        try
                        {
                            m_foreignLock.TryEnter(ref taken);
                            if (taken)
                            {
                                // Increment head, and ensure read of tail doesn't move before it (fence).
                                int head = m_headIndex;
                                Interlocked.Exchange(ref m_headIndex, head + 1);

                                if (head < m_tailIndex)
                                {
                                    int idx = head & m_mask;
                                    Action<object> obj = Volatile.Read(ref m_array[idx]);

                                    // Check for nulls in the array.
                                    if (obj == null) continue;

                                    m_array[idx] = null;
                                    return obj;
                                }
                                else
                                {
                                    // Failed, restore head.
                                    m_headIndex = head;
                                }
                            }
                        }
                        finally
                        {
                            if (taken)
                                m_foreignLock.Exit(useMemoryBarrier: false);
                        }

                        missedSteal = true;
                    }

                    return null;
                }
            }
        }

        internal readonly ConcurrentQueue<(Action<object>, ExecutionContext, object)> workItems = new ConcurrentQueue<(Action<object>, ExecutionContext, object)>();

#pragma warning disable 169
        private readonly PaddingFor32 pad1;
#pragma warning restore 169

        private volatile int numOutstandingThreadRequests;

#pragma warning disable 169
        private readonly PaddingFor32 pad2;
#pragma warning restore 169

        public ThreadPoolWorkQueue(SpreadsThreadPool pool)
        {
            _pool = pool;
        }

        public ThreadPoolWorkQueueThreadLocals EnsureCurrentThreadHasQueue() =>
            ThreadPoolWorkQueueThreadLocals.threadLocals ??
            (ThreadPoolWorkQueueThreadLocals.threadLocals = new ThreadPoolWorkQueueThreadLocals(this));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureThreadRequested()
        {
            // There is a double counter here (_outstandingRequest and _semaphore)
            // Unfair semaphore does not support value bigger than short.MaxValue,
            // tring to Release more than short.MaxValue could fail miserably.

            // The _outstandingRequest counter ensure that we only request a
            // maximum of {ProcessorCount} to the semaphore.

            // It's also more efficient to have two counter, _outstandingRequests is
            // more lightweight than the semaphore.

            // This trick is borrowed from the .Net ThreadPool
            // https://github.com/dotnet/coreclr/blob/bc146608854d1db9cdbcc0b08029a87754e12b49/src/mscorlib/src/System/Threading/ThreadPool.cs#L568

            int count = numOutstandingThreadRequests;
            while (count < ProcessorCount)
            {
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count + 1, count);
                if (prev == count)
                {
                    _semaphore.Release();
                    break;
                }
                count = prev;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MarkThreadRequestSatisfied()
        {
            int count = numOutstandingThreadRequests;
            while (count > 0)
            {
                int prev = Interlocked.CompareExchange(ref numOutstandingThreadRequests, count - 1, count);
                if (prev == count)
                {
                    break;
                }
                count = prev;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(Action<object> callback, ExecutionContext exCtx, object state, bool forceGlobal)
        {
            
            ThreadPoolWorkQueueThreadLocals tl = null;
            if (!forceGlobal)
            {
                tl = ThreadPoolWorkQueueThreadLocals.threadLocals;
            }

            if (null != tl)
            {
                tl.workStealingQueue.LocalPush(callback);
            }
            else
            {
                workItems.Enqueue((callback, exCtx, state));
            }
            EnsureThreadRequested();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool LocalFindAndPop(Action<object> callback)
        {
            ThreadPoolWorkQueueThreadLocals tl = ThreadPoolWorkQueueThreadLocals.threadLocals;
            return tl != null && tl.workStealingQueue.LocalFindAndPop(callback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (Action<object>, ExecutionContext, object) Dequeue(ThreadPoolWorkQueueThreadLocals tl, ref bool missedSteal)
        {
            WorkStealingQueue localWsq = tl.workStealingQueue;
            (Action<object>, ExecutionContext, object) callback;

            if ((callback = (localWsq.LocalPop(), null, null)).Item1 == null && // first try the local queue
                !workItems.TryDequeue(out callback)) // then try the global queue
            {
                // finally try to steal from another thread's local queue
                WorkStealingQueue[] queues = WorkStealingQueueList.Queues;
                int c = queues.Length;
                Debug.Assert(c > 0, "There must at least be a queue for this thread.");
                int maxIndex = c - 1;
                int i = tl.random.Next(c);
                while (c > 0)
                {
                    i = (i < maxIndex) ? i + 1 : 0;
                    WorkStealingQueue otherQueue = queues[i];
                    if (otherQueue != localWsq && otherQueue.CanSteal)
                    {
                        callback = (otherQueue.TrySteal(ref missedSteal), null, null);
                        if (callback.Item1 != null)
                        {
                            break;
                        }
                    }
                    c--;
                }
            }

            return callback;
        }

        internal void Dispatch()
        {
            // Set up our thread-local data
            ThreadPoolWorkQueueThreadLocals tl = EnsureCurrentThreadHasQueue();

            while (true)
            {
                bool missedSteal = false;
                var completableCtx = Dequeue(tl, ref missedSteal);

                if (completableCtx.Item1 == null)
                {
                    if (IsAddingCompleted)
                    {
                        if (!missedSteal)
                        {
                            break;
                        }
                    }
                    else
                    {
                        _semaphore.Wait();
                        MarkThreadRequestSatisfied();
                    }
                    continue;
                }

                // this is called before Enqueue: EnsureThreadRequested();

                try
                {
                    if (completableCtx.Item2 == null)
                    {
                        completableCtx.Item1.Invoke(completableCtx.Item3);
                    }
                    else
                    {
                        ExecutionContext.Run(completableCtx.Item2, s => ((Action)s).Invoke(),
                            completableCtx.Item1);
                    }
                }
                catch (Exception ex)
                {
                    _pool.Settings.ExceptionHandler(ex);
                }
            }
        }

        // Simple random number generator. We don't need great randomness, we just need a little and for it to be fast.
        internal struct FastRandom // xorshift prng
        {
            private uint _w, _x, _y, _z;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public FastRandom(int seed)
            {
                _x = (uint)seed;
                _w = 88675123;
                _y = 362436069;
                _z = 521288629;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Next(int maxValue)
            {
                Debug.Assert(maxValue > 0);

                uint t = _x ^ (_x << 11);
                _x = _y; _y = _z; _z = _w;
                _w = _w ^ (_w >> 19) ^ (t ^ (t >> 8));

                return (int)(_w % (uint)maxValue);
            }
        }

        // Holds a WorkStealingQueue, and removes it from the list when this object is no longer referenced.
        internal sealed class ThreadPoolWorkQueueThreadLocals
        {
            [ThreadStatic]
            public static ThreadPoolWorkQueueThreadLocals threadLocals;

            public readonly ThreadPoolWorkQueue workQueue;
            public readonly WorkStealingQueue workStealingQueue;
            public FastRandom random = new FastRandom(Thread.CurrentThread.ManagedThreadId); // mutable struct, do not copy or make readonly

            public ThreadPoolWorkQueueThreadLocals(ThreadPoolWorkQueue tpq)
            {
                workQueue = tpq;
                workStealingQueue = new WorkStealingQueue();
                WorkStealingQueueList.Add(workStealingQueue);
            }

            private void CleanUp()
            {
                if (null != workStealingQueue)
                {
                    if (null != workQueue)
                    {
                        Action<object> cb;
                        while ((cb = workStealingQueue.LocalPop()) != null)
                        {
                            Debug.Assert(null != cb);
                            workQueue.Enqueue(cb, null, null, forceGlobal: true);
                        }
                    }

                    WorkStealingQueueList.Remove(workStealingQueue);
                }
            }

            ~ThreadPoolWorkQueueThreadLocals()
            {
                // Since the purpose of calling CleanUp is to transfer any pending workitems into the global
                // queue so that they will be executed by another thread, there's no point in doing this cleanup
                // if we're in the process of shutting down or unloading the AD.  In those cases, the work won't
                // execute anyway.  And there are subtle race conditions involved there that would lead us to do the wrong
                // thing anyway.  So we'll only clean up if this is a "normal" finalization.
                if (!(Environment.HasShutdownStarted || AppDomain.CurrentDomain.IsFinalizingForUnload()))
                    CleanUp();
            }
        }
    }

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
                                           int threadMaxStackSize = 0)
            : this(numThreads, DefaultThreadType, name, apartmentState, exceptionHandler, threadMaxStackSize)
        { }

        public ThreadPoolSettings(int numThreads,
                                           ThreadType threadType,
                                           string name = null,
                                           ApartmentState apartmentState = ApartmentState.Unknown,
                                           Action<Exception> exceptionHandler = null,
                                           int threadMaxStackSize = 0)
        {
            Name = name ?? ("DedicatedThreadPool-" + Guid.NewGuid());
            ThreadType = threadType;
            NumThreads = numThreads;
            ApartmentState = apartmentState;
            ExceptionHandler = exceptionHandler ?? (ex => { ThrowHelper.FailFast("Unhandled exception in dedicated thread pool: " + ex.ToString()); });
            ThreadMaxStackSize = threadMaxStackSize;

            if (numThreads <= 0)
                throw new ArgumentOutOfRangeException("numThreads", string.Format("numThreads must be at least 1. Was {0}", numThreads));
        }

        /// <summary>
        /// The total number of threads to run in this thread pool.
        /// </summary>
        public int NumThreads { get; }

        /// <summary>
        /// The type of threads to run in this thread pool.
        /// </summary>
        public ThreadType ThreadType { get; }

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
    public class SpreadsThreadPool
    {
        public static readonly SpreadsThreadPool Default = new SpreadsThreadPool(
            new ThreadPoolSettings(Environment.ProcessorCount * 4, "DefaultSpinningThreadPool"));

        internal readonly ThreadPoolWorkQueue workQueue;
        public ThreadPoolSettings Settings { get; }
        private readonly PoolWorker[] _workers;

        public SpreadsThreadPool(ThreadPoolSettings settings)
        {
            workQueue = new ThreadPoolWorkQueue(this);
            Settings = settings;
            _workers = Enumerable.Range(1, settings.NumThreads).Select(workerId => new PoolWorker(this, workerId)).ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void QueueCompletableItem(Action<object> completable, object state, bool preferLocal)
        {
            if (completable == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(completable));
            }

            ExecutionContext context = null;

            if (!preferLocal)
            {
                context = ExecutionContext.Capture();
            }

            // after ctx logic
            if (state != null)
            {
                preferLocal = false;
            }

            workQueue.Enqueue(completable, context, state, forceGlobal: !preferLocal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeQueueCompletableItem(Action<object> completable, object state, bool preferLocal)
        {
            Debug.Assert(null != completable);
            if (state != null)
            {
                preferLocal = false;
            }
            workQueue.Enqueue(completable, null, state, forceGlobal: !preferLocal);
        }

        // Get all workitems.  Called by TaskScheduler in its debugger hooks.
        internal IEnumerable<(Action<object>, ExecutionContext, object)> GetQueuedWorkItems()
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
                    Action<object>[] items = wsq.m_array;
                    for (int i = 0; i < items.Length; i++)
                    {
                        Action<object> item = items[i];
                        if (item != null)
                        {
                            yield return (item, null, null);
                        }
                    }
                }
            }
        }

        internal IEnumerable<(Action<object>, ExecutionContext, object)> GetLocallyQueuedWorkItems()
        {
            ThreadPoolWorkQueue.WorkStealingQueue wsq = ThreadPoolWorkQueue.ThreadPoolWorkQueueThreadLocals.threadLocals.workStealingQueue;
            if (wsq != null && wsq.m_array != null)
            {
                Action<object>[] items = wsq.m_array;
                for (int i = 0; i < items.Length; i++)
                {
                    Action<object> item = items[i];
                    if (item != null)
                        yield return (item, null, null);
                }
            }
        }

        internal IEnumerable<(Action<object>, ExecutionContext, object)> GetGloballyQueuedWorkItems() => workQueue.workItems;

        private object[] ToObjectArray(IEnumerable<(Action<object>, ExecutionContext, object)> workitems)
        {
            int i = 0;
            foreach ((Action<object>, ExecutionContext, object) item in workitems)
            {
                i++;
            }

            object[] result = new object[i];
            i = 0;
            foreach ((Action<object>, ExecutionContext, object) item in workitems)
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

            public PoolWorker(SpreadsThreadPool pool, int workerId)
            {
                _pool = pool;
                _threadExit = new TaskCompletionSource<object>();

                var thread = new Thread(RunThread, pool.Settings.ThreadMaxStackSize);

                thread.IsBackground = pool.Settings.ThreadType == ThreadType.Background;

                // THEORY, need to validate:
                // For Spreads the most critical path is completion of cursors that are wating
                // on MoveNextAsync. On machines with small (v)CPUs count there could be a lot of
                // activity on IO threads or other threads, but that could wait until we are
                // performing actual calculations. We use Thread.Sleep(0) in UnfairSemaphore
                // that yields only to threads with the same priority - this is good for this 
                // case - threads from this pool will continue to do work until they have it.
                // Note that we do not stick to the pool threads and could often jump to the 
                // normal ThreadPool or start waiting from it. That's OK because if we have 
                // data available then those threads will just execute calculations. If they 
                // have to wait then we should wake consumers from higher-priority threads.
                // Try: thread.Priority = Thread.CurrentThread.Priority + 1;

                if (pool.Settings.Name != null)
                    thread.Name = string.Format("{0}_{1}", pool.Settings.Name, workerId);

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

    #region UnfairSemaphore implementation

    /// <summary>
    ///     This class has been translated from:
    /// https://github.com/dotnet/coreclr/blob/97433b9d153843492008652ff6b7c3bf4d9ff31c/src/vm/win32threadpool.h#L124
    ///
    /// UnfairSemaphore is a more scalable semaphore than Semaphore.It prefers to release threads that have more recently begun waiting,
    /// to preserve locality.Additionally, very recently-waiting threads can be released without an addition kernel transition to unblock
    /// them, which reduces latency.
    ///
    /// UnfairSemaphore is only appropriate in scenarios where the order of unblocking threads is not important, and where threads frequently
    /// need to be woken.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public sealed class UnfairSemaphore
    {
        public const int MaxWorker = 0x7FFF;

        // We track everything we care about in A 64-bit struct to allow us to
        // do CompareExchanges on this for atomic updates.
        [StructLayout(LayoutKind.Explicit)]
        private struct SemaphoreState
        {
            //how many threads are currently spin-waiting for this semaphore?
            [FieldOffset(0)]
            public short Spinners;

            //how much of the semaphore's count is availble to spinners?
            [FieldOffset(2)]
            public short CountForSpinners;

            //how many threads are blocked in the OS waiting for this semaphore?
            [FieldOffset(4)]
            public short Waiters;

            //how much count is available to waiters?
            [FieldOffset(6)]
            public short CountForWaiters;

            [FieldOffset(0)]
            public long RawData;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CacheLinePadding
        { }

        private readonly Semaphore m_semaphore;

        // padding to ensure we get our own cache line
#pragma warning disable 169
        private readonly CacheLinePadding m_padding1;
        private SemaphoreState m_state;
        private readonly CacheLinePadding m_padding2;
#pragma warning restore 169

        public UnfairSemaphore()
        {
            m_semaphore = new Semaphore(0, short.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait()
        {
            return Wait(Timeout.InfiniteTimeSpan);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(TimeSpan timeout)
        {
            while (true)
            {
                SemaphoreState currentCounts = GetCurrentState();
                SemaphoreState newCounts = currentCounts;

                // First, just try to grab some count.
                if (currentCounts.CountForSpinners > 0)
                {
                    --newCounts.CountForSpinners;
                    if (TryUpdateState(newCounts, currentCounts))
                        return true;
                }
                else
                {
                    // No count available, become a spinner
                    ++newCounts.Spinners;
                    if (TryUpdateState(newCounts, currentCounts))
                        break;
                }
            }

            //
            // Now we're a spinner.
            //
            int numSpins = 0;
            const int spinLimitPerProcessor = 50;
            while (true)
            {
                SemaphoreState currentCounts = GetCurrentState();
                SemaphoreState newCounts = currentCounts;

                if (currentCounts.CountForSpinners > 0)
                {
                    --newCounts.CountForSpinners;
                    --newCounts.Spinners;
                    if (TryUpdateState(newCounts, currentCounts))
                        return true;
                }
                else
                {
                    double spinnersPerProcessor = (double)currentCounts.Spinners / Environment.ProcessorCount;
                    int spinLimit = (int)((spinLimitPerProcessor / spinnersPerProcessor) + 0.5);
                    if (numSpins >= spinLimit)
                    {
                        --newCounts.Spinners;
                        ++newCounts.Waiters;
                        if (TryUpdateState(newCounts, currentCounts))
                            break;
                    }
                    else
                    {
                        //
                        // We yield to other threads using Thread.Sleep(0) rather than the more traditional Thread.Yield().
                        // This is because Thread.Yield() does not yield to threads currently scheduled to run on other
                        // processors.  On a 4-core machine, for example, this means that Thread.Yield() is only ~25% likely
                        // to yield to the correct thread in some scenarios.
                        // Thread.Sleep(0) has the disadvantage of not yielding to lower-priority threads.  However, this is ok because
                        // once we've called this a few times we'll become a "waiter" and wait on the Semaphore, and that will
                        // yield to anything that is runnable.
                        //
                        Thread.Sleep(0);
                        numSpins++;
                    }
                }
            }

            //
            // Now we're a waiter
            //
            bool waitSucceeded = m_semaphore.WaitOne(timeout);

            while (true)
            {
                SemaphoreState currentCounts = GetCurrentState();
                SemaphoreState newCounts = currentCounts;

                --newCounts.Waiters;

                if (waitSucceeded)
                    --newCounts.CountForWaiters;

                if (TryUpdateState(newCounts, currentCounts))
                    return waitSucceeded;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release()
        {
            Release(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release(short count)
        {
            while (true)
            {
                SemaphoreState currentState = GetCurrentState();
                SemaphoreState newState = currentState;

                short remainingCount = count;

                // First, prefer to release existing spinners,
                // because a) they're hot, and b) we don't need a kernel
                // transition to release them.
                short spinnersToRelease = Math.Max((short)0, Math.Min(remainingCount, (short)(currentState.Spinners - currentState.CountForSpinners)));
                newState.CountForSpinners += spinnersToRelease;
                remainingCount -= spinnersToRelease;

                // Next, prefer to release existing waiters
                short waitersToRelease = Math.Max((short)0, Math.Min(remainingCount, (short)(currentState.Waiters - currentState.CountForWaiters)));
                newState.CountForWaiters += waitersToRelease;
                remainingCount -= waitersToRelease;

                // Finally, release any future spinners that might come our way
                newState.CountForSpinners += remainingCount;

                // Try to commit the transaction
                if (TryUpdateState(newState, currentState))
                {
                    // Now we need to release the waiters we promised to release
                    if (waitersToRelease > 0)
                        m_semaphore.Release(waitersToRelease);

                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryUpdateState(SemaphoreState newState, SemaphoreState currentState)
        {
            if (Interlocked.CompareExchange(ref m_state.RawData, newState.RawData, currentState.RawData) == currentState.RawData)
            {
                Debug.Assert(newState.CountForSpinners <= MaxWorker, "CountForSpinners is greater than MaxWorker");
                Debug.Assert(newState.CountForSpinners >= 0, "CountForSpinners is lower than zero");
                Debug.Assert(newState.Spinners <= MaxWorker, "Spinners is greater than MaxWorker");
                Debug.Assert(newState.Spinners >= 0, "Spinners is lower than zero");
                Debug.Assert(newState.CountForWaiters <= MaxWorker, "CountForWaiters is greater than MaxWorker");
                Debug.Assert(newState.CountForWaiters >= 0, "CountForWaiters is lower than zero");
                Debug.Assert(newState.Waiters <= MaxWorker, "Waiters is greater than MaxWorker");
                Debug.Assert(newState.Waiters >= 0, "Waiters is lower than zero");
                Debug.Assert(newState.CountForSpinners + newState.CountForWaiters <= MaxWorker, "CountForSpinners + CountForWaiters is greater than MaxWorker");

                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SemaphoreState GetCurrentState()
        {
            // Volatile.Read of a long can get a partial read in x86 but the invalid
            // state will be detected in TryUpdateState with the CompareExchange.

            SemaphoreState state = new SemaphoreState();
            state.RawData = Volatile.Read(ref m_state.RawData);
            return state;
        }
    }

    #endregion UnfairSemaphore implementation
}