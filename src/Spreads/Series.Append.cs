// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using Spreads.Internal;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Spreads
{
    // ReSharper disable once RedundantExtendsListEntry
    public partial class Series<TKey, TValue> : IAppendSeries<TKey, TValue>
    {
        private static readonly int MaxBufferLength = Math.Max(Settings.MIN_POOLED_BUFFER_LEN, Settings.LARGE_BUFFER_LIMIT / Math.Max(Unsafe.SizeOf<TKey>(), Unsafe.SizeOf<TValue>()));

        // TODO review, we still have _locker and versions at BaseContainer

        // We could afford simple locking for mutation. We have API to create series from batches, uncontended lock is not so slow.
        // Will add manual locking via Interlocked later. It is 2.5 faster.
        //private object _syncRoot;

        //protected object SyncRoot
        //{
        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    get
        //    {
        //        if (_syncRoot == null)
        //        {
        //            Interlocked.CompareExchange(ref _syncRoot, new object(), null);
        //        }
        //        return _syncRoot;
        //    }
        //}

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

        // TODO MarkComplete
        public Task Complete()
        {
            throw new NotImplementedException();
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
                block = DataSource.LastValueOrDefault;
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
                var lastKey = block.RowKeys.DangerousGet<TKey>(block.RowLength - 1);

                var c = _comparer.Compare(key, lastKey);
                if (c <= 0)
                {
                    return false;
                }
            }

            if (block.RowLength == block.RowKeys.Length)
            {
                block = GrowCapacity(key, block);
                if (block == null)
                {
                    return false;
                }
            }

            block.SeriesInsert(block.RowLength, key, value);
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
                if (block.RowKeys.Length < MaxBufferLength)
                {
                    if (block.SeriesIncreaseCapacity<TKey, TValue>() < 0)
                    {
                        return null;
                    }
                }
                else
                {
                    // refactor switching to source logic to reuse in MutableSeries
                    if (DataSource == null)
                    {
                        var ds = new DataBlockSource<TKey>();
                        ds.AddLast(block.RowKeys.DangerousGetRef<TKey>(0), block);
                        Data = ds;
                    }

                    var minCapacity = block.RowKeys.Length;
                    var newBlock = DataBlock.Create();
                    if (newBlock.SeriesIncreaseCapacity<TKey, TValue>(minCapacity) < 0)
                    {
                        return null;
                    }
                    DataSource.AddLast(key, newBlock);
                    block = newBlock;
                }

                return block;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return null;
            }
        }
    }
}
