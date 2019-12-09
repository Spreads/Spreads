// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Collections
{
    public partial class BaseContainer<TKey>
    {
        /// <summary>
        /// Read synced
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal bool TryGetSeriesValue<TValue>(TKey key, out TValue value)
        {
            var sw = new SpinWait();
            value = default!;

        SYNC:
            var found = false;
            var version = Version;
            {
                if (!IsDataBlock(out var block, out var ds))
                {
                    TryFindBlock_ValidateOrGetBlockFromSource(ref block, ds, key, Lookup.EQ, Lookup.LE);
                }

                if (block != null)
                {
                    var blockIndex = block.SearchKey(key, _comparer);
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
