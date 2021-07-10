﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Internal
{
    internal class DataBlockSource<TKey> : IDisposable //  ISeries<TKey, DataBlock>, // TODO review: we do not need ISeries, we expose only needed methods and could inject inner implementation
    {
        /// <summary>
        /// For append-only containers blocks will have the same size. Last block could be only partially filled.
        /// </summary>
        internal uint ConstantBlockLength = 0;

        /// <summary>
        /// DataBlock has Next property to form a linked list. If _root is set then all blocks after it are GC-rooted and won't be collected.
        /// </summary>
        // ReSharper disable once NotAccessedField.Local
        private DataBlock? _root;

        /// <summary>
        /// Last block is always rooted and speeds up most frequent operations.
        /// </summary>
        private DataBlock? _last;

        // This is used from locked context, do not use locked methods
        internal IMutableSeries<TKey, DataBlock> _blockSeries;

        public DataBlockSource()
        {
            _blockSeries = new Series<TKey, DataBlock>();
        }

        internal DataBlockSource(IMutableSeries<TKey, DataBlock> blockSeries)
        {
            _blockSeries = blockSeries;
        }

        public IAsyncEnumerator<KeyValuePair<TKey, DataBlock>> GetAsyncEnumerator()
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public bool AddLast(TKey key, DataBlock value)
        {
            // var wr = new WeakReference<DataBlock>(value);
            DataBlock lastBlock = null;
            var last = _blockSeries.Last;
            if (last.IsPresent)
            {
                lastBlock = last.Present.Value;
                // Console.WriteLine($"LAST: {last.Present.Key} adding: {key}");
                //if (!last.Present.Value.TryGetTarget(out lastBlock))
                //{
                //    // Console.WriteLine("CANNOT GET LAST");
                //    // TODO cleanup
                //    // Assert that all do not have target
                //}
            }
            else
            {
                // Console.WriteLine("Cannot get last: " + key);
                // last = _weakSeries.Last;
            }

            var added = _blockSeries.TryAddLast(key, value).Result;
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

        //IEnumerator IEnumerable.GetEnumerator()
        //{
        //    return GetEnumerator();
        //}

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
                var wOpt = _blockSeries.First;
                if (wOpt.IsPresent) // && wOpt.Present.Value.TryGetTarget(out var block))
                {
                    return Opt.Present(wOpt.Present); //new KeyValuePair<TKey, DataBlock>(wOpt.Present.Key, block));
                }
                return Opt<KeyValuePair<TKey, DataBlock>>.Missing;
            }
        }

        public DataBlock LastValueOrDefault // TODO review if should move to iface
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_last != null)
                {
                    return _last;
                }

                var wOpt = _blockSeries.LastValueOrDefault;
                //if (wOpt.TryGetTarget(out var block))
                //{
                //    _last = block;
                //    return block;
                //}
                return wOpt;
            }
        }

        public Opt<KeyValuePair<TKey, DataBlock>> Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var wOpt = _blockSeries.Last;
                if (wOpt.IsPresent) // && wOpt.Present.Value.TryGetTarget(out var block))
                {
                    // TODO use _last
                    // Debug.Assert(_last == null || block == _last);
                    return new Opt<KeyValuePair<TKey, DataBlock>>(wOpt.Present); // new KeyValuePair<TKey, DataBlock>(wOpt.Present.Key, block));
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
            if (_blockSeries.TryFindAt(key, direction, out var kvpBlock)) // && kvpBlock.Value.TryGetTarget(out var block))
            {
                kvp = kvpBlock; // new KeyValuePair<TKey, DataBlock>(kvpBlock.Key, block);
                return true;
            }
            // TODO handle weak reference collected case
            kvp = default;
            return false;
        }

        public IEnumerable<TKey> Keys => _blockSeries.Keys;

        public IEnumerable<DataBlock> Values => _blockSeries.Values; //.Select(x => x.TryGetTarget(out var tgt) ? tgt : null);

        public virtual void Dispose()
        {
            foreach (var block in _blockSeries.Values)
            {
                block.Dispose();
            }

            _root = null;
            _blockSeries.Dispose();
        }
    }
}
