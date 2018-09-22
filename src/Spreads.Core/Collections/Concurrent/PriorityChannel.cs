// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

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
        private volatile bool _isWaiting;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0, int.MaxValue);
        private readonly SingleProducerSingleConsumerQueue<T> _items = new SingleProducerSingleConsumerQueue<T>();
        private readonly SingleProducerSingleConsumerQueue<T> _priorityItems = new SingleProducerSingleConsumerQueue<T>();
        private readonly SingleProducerSingleConsumerQueue<T>[] _queues = new SingleProducerSingleConsumerQueue<T>[2];

        public PriorityChannel()
        {
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
        public unsafe void Add(T item, bool isPriority = false)
        {
            // branchless choice of queue
            var idx = *(int*)&isPriority & 1;
            _queues[idx].Enqueue(item);

            if (_isWaiting)
            {
                _semaphore.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Take(out bool isPriority, CancellationToken ct = default)
        {
            var spinner = new SpinWait();
            while (true)
            {
                if (!_priorityItems.IsEmpty && _priorityItems.TryDequeue(out var item))
                {
                    isPriority = true;
                    return item;
                }

                if (_items.TryDequeue(out item))
                {
                    isPriority = false;
                    return item;
                }

                spinner.SpinOnce();
                if (spinner.NextSpinWillYield)
                {
                    _isWaiting = true;
                    _semaphore.Wait(ct);
                    _isWaiting = false;
                }
            }
        }
    }
}
