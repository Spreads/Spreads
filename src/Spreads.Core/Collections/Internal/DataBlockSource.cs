// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Experimental;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Internal
{
    internal class DataBlockSource<TKey> : ISeries<TKey, DataBlock>, IDisposable
    {
        /// <summary>
        /// For append-only containers blocks will have the same size. Last block could be only partially filled.
        /// </summary>
        internal uint ConstantBlockLength = 0;

        /// <summary>
        /// DataBlock has Next property to form a linked list. If _root is set then all blocks after it are GC-rooted and won't be collected.
        /// </summary>
        // ReSharper disable once NotAccessedField.Local
        private DataBlock _root;

        /// <summary>
        /// Last block is always rooted and speeds up most frequent operations.
        /// </summary>
        private DataBlock _last;

        // This is used from locked context, do not use locked methods
        private readonly MutableSeries<TKey, WeakReference<DataBlock>> _weakSeries;

        public DataBlockSource()
        {
            _weakSeries = new MutableSeries<TKey, WeakReference<DataBlock>>();
        }

        public IAsyncEnumerator<KeyValuePair<TKey, DataBlock>> GetAsyncEnumerator()
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public bool AddLast(TKey key, DataBlock value)
        {
            var wr = new WeakReference<DataBlock>(value);
            DataBlock lastBlock = null;
            var last = _weakSeries.Last;
            if (last.IsPresent)
            {
                // Console.WriteLine($"LAST: {last.Present.Key} adding: {key}");
                if (!last.Present.Value.TryGetTarget(out lastBlock))
                {
                    // Console.WriteLine("CANNOT GET LAST");
                    // TODO cleanup
                    // Assert that all do not have target
                }
            }
            else
            {
                // Console.WriteLine("Cannot get last: " + key);
                // last = _weakSeries.Last;
            }

            var added = _weakSeries.TryAddLastDirect(key, wr);
            if (!added)
            {
                // ThrowHelper.ThrowInvalidOperationException("This should always succeed");
                return false;
            }

            if (lastBlock != null)
            {
                lastBlock.NextBlock = value;
            }
            else
            {
                // Console.WriteLine("SETTING ROOT");
                _root = value;
            }
            // GC.KeepAlive(value);
            _last = value;
            return true;
        }

        public IEnumerator<KeyValuePair<TKey, DataBlock>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool IsCompleted => throw new NotImplementedException();

        public bool IsIndexed => throw new NotImplementedException();

        public ICursor<TKey, DataBlock> GetCursor()
        {
            throw new NotImplementedException();
        }

        public IAsyncCursor<TKey, DataBlock> GetAsyncCursor()
        {
            throw new NotImplementedException();
        }

        public KeyComparer<TKey> Comparer => throw new NotImplementedException();

        public Opt<KeyValuePair<TKey, DataBlock>> First
        {
            get
            {
                var wOpt = _weakSeries.First;
                if (wOpt.IsPresent && wOpt.Present.Value.TryGetTarget(out var block))
                {
                    return Opt.Present(new KeyValuePair<TKey, DataBlock>(wOpt.Present.Key, block));
                }
                return Opt<KeyValuePair<TKey, DataBlock>>.Missing;
            }
        }

        public DataBlock LastOrDefault
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_last != null)
                {
                    return _last;
                }
                var wOpt = _weakSeries.LastOrDefault;
                if (wOpt.TryGetTarget(out var block))
                {
                    return block;
                }
                return default;
            }
        }

        public Opt<KeyValuePair<TKey, DataBlock>> Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var wOpt = _weakSeries.Last;
                if (wOpt.IsPresent && wOpt.Present.Value.TryGetTarget(out var block))
                {
                    return new Opt<KeyValuePair<TKey, DataBlock>>(new KeyValuePair<TKey, DataBlock>(wOpt.Present.Key, block));
                }
                return Opt<KeyValuePair<TKey, DataBlock>>.Missing;
            }
        }

        public DataBlock this[TKey key] => throw new NotImplementedException();

        public bool TryGetValue(TKey key, out DataBlock value)
        {
            throw new NotImplementedException();
        }

        public bool TryGetAt(long index, out KeyValuePair<TKey, DataBlock> kvp)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, DataBlock> kvp)
        {
            if (_weakSeries.TryFindAt(key, direction, out var kvpBlock) && kvpBlock.Value.TryGetTarget(out var block))
            {
                kvp = new KeyValuePair<TKey, DataBlock>(kvpBlock.Key, block);
                return true;
            }
            // TODO handle weak reference collected case
            kvp = default;
            return false;
        }

        public IEnumerable<TKey> Keys => _weakSeries.Keys;

        public IEnumerable<DataBlock> Values => _weakSeries.Values.Select(x => x.TryGetTarget(out var tgt) ? tgt : null);

        public void Dispose()
        {
            _root = null;
            _weakSeries.Dispose();
        }
    }
}