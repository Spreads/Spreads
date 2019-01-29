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

        public virtual Task<bool> TryAddLast(TKey key, TValue value)
        {
            lock (SyncRoot)
            {
                var last = Last;
                if (last.IsPresent)
                {
                    var c = Comparer.Compare(key, last.Present.Key);
                    if (c <= 0)
                    {
                        return TaskUtil.FalseTask;
                    }
                }

                if (DataSource != null)
                {
                    throw new NotImplementedException();
                }

                // we need to get the last data block and insert to it,
                // double it's capacity until it fits to LOH,
                // then switch to in-memory DataSource

                var block = DataBlock;

                Debug.Assert(block != null);

                if (block.RowLength == block.RowIndex.Length)
                {
                    var byteSize = block.RowIndex.Length * Math.Max(Unsafe.SizeOf<TKey>(), Unsafe.SizeOf<TValue>()) ;
                    // next increment will be 128kb, first pow2 buffer in LOH
                    if (byteSize <= Settings.LARGE_BUFFER_LIMIT)
                    {
                        block.DoubleSeriesCapacity<TKey, TValue>();
                    }
                    else
                    {
                        // TODO switch to in-memory DataSource
                        throw new NotImplementedException();
                    }
                }

                block.InsertSeries(block.RowLength, key, value);
                return TaskUtil.TrueTask;
            }
        }

        internal static void Insert(DataBlock block, int index, TKey key, TValue value)
        {
        }
    }
}