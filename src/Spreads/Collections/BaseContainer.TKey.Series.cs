// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Algorithms;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads
{
    public partial class BaseContainer<TKey>
    {
        /// <summary>
        /// Read synced
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetSeriesValue<TValue>(TKey key, out TValue value)
        {
            var sw = new SpinWait();
            value = default!;

        SYNC:
            var found = false;
            var version = Version;
            {
                var block = DataBlock;

                if (DataSource != null)
                {
                    TryFindBlock_ValidateOrGetBlockFromSource(ref block, key, Lookup.EQ, Lookup.LE);
                    Data = block;
                }

                if (block != null)
                {
                    var blockIndex = VectorSearch.SortedSearch(ref block.DangerousRowKeyRef<TKey>(0),
                        block.RowCount, key, _comparer);

                    if (blockIndex >= 0)
                    {
                        value = block.DangerousValueRef<TValue>(blockIndex);
                        found = true;
                    }
                }
            }

            if (NextVersion != version)
            {
                sw.SpinOnce();
                goto SYNC;
            }

            return found;
        }
    }
}
