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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
// ReSharper disable InconsistentNaming

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
            internal volatile ISpreadsThreadPoolWorkItem[] m_array = new ISpreadsThreadPoolWorkItem[INITIAL_SIZE]; // SOS's ThreadPool command depends on this name
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

            public void LocalPush(ISpreadsThreadPoolWorkItem obj)
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
                            var newArray = new ISpreadsThreadPoolWorkItem[m_array.Length << 1];
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
            public bool LocalFindAndPop(object obj)
            {
                // Fast path: check the tail. If equal, we can skip the lock.
                if (m_array[(m_tailIndex - 1) & m_mask] == obj)
                {
                    object unused = LocalPop();
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

            public ISpreadsThreadPoolWorkItem? LocalPop() => m_headIndex < m_tailIndex ? LocalPopCore() : null;

            [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
            private ISpreadsThreadPoolWorkItem LocalPopCore()
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
                        ISpreadsThreadPoolWorkItem obj = Volatile.Read(ref m_array[idx]);

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
                                ISpreadsThreadPoolWorkItem obj = Volatile.Read(ref m_array[idx]);

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

            public bool CanSteal => m_headIndex < m_tailIndex;

            public ISpreadsThreadPoolWorkItem TrySteal(ref bool missedSteal)
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
                                    ISpreadsThreadPoolWorkItem obj = Volatile.Read(ref m_array[idx]);

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

        internal readonly ConcurrentQueue<ISpreadsThreadPoolWorkItem> workItems = new ConcurrentQueue<ISpreadsThreadPoolWorkItem>();

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
        public void Enqueue(ISpreadsThreadPoolWorkItem callback, bool forceGlobal)
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
                workItems.Enqueue(callback);
            }
            EnsureThreadRequested();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool LocalFindAndPop(ISpreadsThreadPoolWorkItem callback)
        {
            ThreadPoolWorkQueueThreadLocals tl = ThreadPoolWorkQueueThreadLocals.threadLocals;
            return tl != null && tl.workStealingQueue.LocalFindAndPop(callback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISpreadsThreadPoolWorkItem? Dequeue(ThreadPoolWorkQueueThreadLocals tl, ref bool missedSteal)
        {
            WorkStealingQueue localWsq = tl.workStealingQueue;
            ISpreadsThreadPoolWorkItem callback;

            if ((callback = localWsq.LocalPop()) == null && // first try the local queue
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
                        callback = otherQueue.TrySteal(ref missedSteal);
                        if (callback != null)
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

                if (completableCtx == null)
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
                    completableCtx.Execute();
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
            public static ThreadPoolWorkQueueThreadLocals? threadLocals;

            public readonly ThreadPoolWorkQueue workQueue;
            public readonly WorkStealingQueue workStealingQueue;
            public readonly Thread currentThread;
            public FastRandom random = new FastRandom(Thread.CurrentThread.ManagedThreadId); // mutable struct, do not copy or make readonly

            public ThreadPoolWorkQueueThreadLocals(ThreadPoolWorkQueue tpq)
            {
                workQueue = tpq;
                workStealingQueue = new WorkStealingQueue();
                WorkStealingQueueList.Add(workStealingQueue);
                currentThread = Thread.CurrentThread;
            }

            private void CleanUp()
            {
                if (null != workStealingQueue)
                {
                    if (null != workQueue)
                    {
                        ISpreadsThreadPoolWorkItem cb;
                        while ((cb = workStealingQueue.LocalPop()) != null)
                        {
                            Debug.Assert(null != cb);
                            workQueue.Enqueue(cb, forceGlobal: true);
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
}
