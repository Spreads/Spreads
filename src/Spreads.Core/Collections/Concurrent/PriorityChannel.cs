// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Collections.Concurrent
{
    /// <summary>
    /// Single-producer single-consumer queue with a priority channel that allows priority items jump ahead of normal ones.
    /// All items with the same priority are FIFO.
    /// </summary>
    public sealed class PriorityChannel<T>
    {
        private readonly CancellationToken _ct;
        private volatile bool _isWaiting;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0, int.MaxValue);
        private readonly SingleProducerSingleConsumerQueue<T> _items = new SingleProducerSingleConsumerQueue<T>();
        private readonly SingleProducerSingleConsumerQueue<T> _priorityItems = new SingleProducerSingleConsumerQueue<T>();
        private readonly SingleProducerSingleConsumerQueue<T>[] _queues = new SingleProducerSingleConsumerQueue<T>[2];

        public PriorityChannel(CancellationToken ct = default)
        {
            _ct = ct;
            _queues[0] = _items;
            _queues[1] = _priorityItems;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _items.IsEmpty && _priorityItems.IsEmpty; // NB order, priority are less frequent
        }

        public int TotalCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _items.Count + _priorityItems.Count;
        }

        public int PriorityCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _priorityItems.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool TryAdd(T item, bool isPriority = false)
        {
            if (IsCancelled)
            {
                return false;
            }
            // branchless choice of queue
            var idx = *(int*)&isPriority & 1;
            _queues[idx].Enqueue(item);

            if (_isWaiting)
            {
                _semaphore.Release();
            }

            return true;
        }

        public bool IsCancelled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _ct.IsCancellationRequested;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Take(out T item, out bool isPriority)
        {
            var spinner = new SpinWait();
            while (true)
            {
                if (_priorityItems.IsEmpty)
                {
                    if (_items.TryDequeue(out item))
                    {
                        isPriority = false;
                        return true;
                    }
                }
                else
                {
                    // ReSharper disable once RedundantAssignment
                    var taken = _priorityItems.TryDequeue(out item);
                    Debug.Assert(taken);
                    isPriority = true;
                    return true;
                }

                spinner.SpinOnce();
                if (spinner.NextSpinWillYield)
                {
                    if (_ct.IsCancellationRequested)
                    {
                        item = default;
                        isPriority = false;
                        return false;
                    }
                    _isWaiting = true;
                    _semaphore.Wait(_ct);
                    _isWaiting = false;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryTake(out T item, out bool isPriority)
        {
            if (_priorityItems.IsEmpty)
            {
                if (_items.TryDequeue(out item))
                {
                    isPriority = false;
                    return true;
                }
            }
            else
            {
                // ReSharper disable once RedundantAssignment
                var taken = _priorityItems.TryDequeue(out item);
                Debug.Assert(taken);
                isPriority = true;
                return true;
            }

            item = default;
            isPriority = false;
            return false;
        }
    }
}
