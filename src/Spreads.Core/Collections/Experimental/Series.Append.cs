// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Collections.Experimental
{
    public class AppendSeries<TKey, TValue> : Series<TKey, TValue>, IAppendSeries<TKey, TValue>
    {
        private static readonly int MaxBufferLength = Settings.LARGE_BUFFER_LIMIT / Math.Max(Unsafe.SizeOf<TKey>(), Unsafe.SizeOf<TValue>());

        internal AppendSeries()
        {
            DataBlock = new DataBlock();
        }

        // We could afford simple locking for mutation. We have API to create series from batches, uncontended lock is not so slow.
        // Will add manual locking via Interlocked later. It is 2.5 faster.
        private object _syncRoot;

        protected object SyncRoot
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_syncRoot == null)
                {
                    Interlocked.CompareExchange(ref _syncRoot, new object(), null);
                }
                return _syncRoot;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // if devirt works
        public virtual Task<bool> TryAddLast(TKey key, TValue value)
        {
#pragma warning disable 618
            BeforeWrite();
            var added = false;
            try
            {
                added = TryAddLastDirect(key, value);
            }
            finally
            {
                AfterWrite(added);
            }
            return added ? TaskUtil.TrueTask : TaskUtil.FalseTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryAddLastDirect(TKey key, TValue value)
        {
            DataBlock block;
            if (DataSource == null)
            {
                block = DataBlock;
            }
            else
            {
                block = DataSource.LastOrDefault;
                //if (lastBlockOpt.IsPresent)
                //{
                //    block = lastBlockOpt.Present.Value;
                //}
                //else
                //{
                //    // TODO this should not happen
                //    block = null;
                //}
            }

            Debug.Assert(block != null);

            if (block.RowLength > 0)
            {
                var lastKey = block.RowIndex._vec.DangerousGet<TKey>(block.RowLength - 1);

                // TODO why _comparer is not visible here?
                var c = Comparer.Compare(key, lastKey);
                if (c <= 0)
                {
                    return false;
                }
            }

            if (block.RowLength == block.RowIndex.Length)
            {
                block = GrowCapacity(key, block);
                if (block == null)
                {
                    return false;
                }
            }

            block.InsertSeries(block.RowLength, key, value);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                     | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private DataBlock GrowCapacity(TKey key, DataBlock block)
        {
            try
            {
                // TODO review: do we want buffers in LOH or not? <= vs <
                // next increment will be 64kb, avoid buffer in LOH
                if (block.RowIndex.Length < MaxBufferLength)
                {
                    if (block.IncreaseSeriesCapacity<TKey, TValue>() < 0)
                    {
                        return null;
                    }
                }
                else
                {
                    if (DataSource == null)
                    {
                        DataSource = new DataBlockSource<TKey>();
                        DataSource.AddLast(block.RowIndex.DangerousGetRef<TKey>(0), block);
                        DataBlock = null;
                    }

                    var minCapacity = block.RowIndex.Length;
                    var newBlock = new DataBlock();
                    if (newBlock.IncreaseSeriesCapacity<TKey, TValue>(minCapacity) < 0)
                    {
                        return null;
                    };
                    DataSource.AddLast(key, newBlock);
                    block = newBlock;
                }

                return block;
            }
            catch(Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return null;
            }
        }
    }
}
