// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Internal
{
    internal class DataBlockSource<TKey> : IRowCount, IDisposable //  ISeries<TKey, DataBlock>, // TODO review: we do not need ISeries, we expose only needed methods and could inject inner implementation
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
        internal MutableSeries<TKey, DataBlock> _blockSeries;

        public DataBlockSource()
        {
            _blockSeries = new MutableSeries<TKey, DataBlock>();
        }

        internal DataBlockSource(MutableSeries<TKey, DataBlock> blockSeries)
        {
            _blockSeries = blockSeries;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public bool AddLast(TKey key, DataBlock value)
        {
            // var wr = new WeakReference<DataBlock>(value);
            DataBlock? lastBlock = null;
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

            var added = _blockSeries.TryAppend(key, value);
            if (!added)
            {
                // ThrowHelper.ThrowInvalidOperationException("This should always succeed");
                return false;
            }

            if (lastBlock != null)
            {
                lastBlock.NextBlock = value;
                value.PreviousBlock = lastBlock;
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
                // very important optimization for Series.Append
                if (_last != null)
                {
                    return _last;
                }

                return _blockSeries.LastValueOrDefault;
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

        public bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, DataBlock> kvp)
        {
            return _blockSeries.DoTryFindAt(key, direction, out kvp);
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

        public ulong? RowCount => null; // TODO here it won't be O(1) but O(N/average block size), maybe we should implement it as sum.

        public bool IsEmpty => LastValueOrDefault == null || LastValueOrDefault.RowCount == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetNextBlock(DataBlock currentBlock, [NotNullWhen(true)] out DataBlock? nextBlock)
        {
            nextBlock = currentBlock.NextBlock;

            if (nextBlock != null)
            {
                if (AdditionalCorrectnessChecks.Enabled)
                    ThrowHelper.Assert(nextBlock.RowCount > 0, "nextBlock.RowCount > 0");
                return nextBlock.RowCount > 0;
            }

            if (currentBlock == LastValueOrDefault)
            {
                nextBlock = null;
                return false;
            }

            return TryGetNextBlockSlower(currentBlock, out nextBlock);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetNextBlockSlower(DataBlock currentBlock, out DataBlock? nextBlock)
        {
            if (currentBlock == DataBlock.Empty && First.IsPresent)
            {
                nextBlock = First.Present.Value;
            } else if (!_blockSeries.TryFindBlockAtFromSource(out nextBlock, this,
                currentBlock.DangerousRowKey<TKey>(index: 0), Lookup.GT))
            {
                if (AdditionalCorrectnessChecks.Enabled)
                    ThrowHelper.Assert(nextBlock == null, "nextBlock == null");
                nextBlock = null;
            }

            if (nextBlock != null)
            {
                if (AdditionalCorrectnessChecks.Enabled)
                    ThrowHelper.DebugAssert(nextBlock.RowCount > 0, "nextBlock.RowCount > 0 2");
                return nextBlock.RowCount > 0;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetPreviousBlock(DataBlock currentBlock, [NotNullWhen(true)] out DataBlock? previousBlock)
        {
            previousBlock = currentBlock.PreviousBlock;

            if (previousBlock != null)
            {
                if (AdditionalCorrectnessChecks.Enabled)
                    ThrowHelper.Assert(previousBlock.RowCount > 0);
                return previousBlock.RowCount > 0;
            }

            if (currentBlock == First.Present.Value)
            {
                previousBlock = null;
                return false;
            }

            return TryGetPreviousBlockSlower(currentBlock, out previousBlock);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryGetPreviousBlockSlower(DataBlock currentBlock, out DataBlock? previousBlock)
        {
            if (currentBlock == DataBlock.Empty && LastValueOrDefault != null)
            {
                previousBlock = LastValueOrDefault;
            }
            else if (!_blockSeries.TryFindBlockAtFromSource(out previousBlock, this,
                currentBlock.DangerousRowKey<TKey>(index: 0), Lookup.LT))
            {
                if (AdditionalCorrectnessChecks.Enabled)
                    ThrowHelper.Assert(previousBlock == null);
                previousBlock = null;
            }

            if (previousBlock != null)
            {
                if (AdditionalCorrectnessChecks.Enabled)
                    ThrowHelper.DebugAssert(previousBlock.RowCount > 0);
                return previousBlock.RowCount > 0;
            }

            return false;
        }
    }
}
