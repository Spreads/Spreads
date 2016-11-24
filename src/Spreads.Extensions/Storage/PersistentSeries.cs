// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Storage {

    // Goals:
    // [x] All IROOM methods are implemented via parent Series<> base class,
    //     which means that we need to implement a cursor as the main read facility
    // [ ] Do not pre-load all keys, store current key in Cursor
    // [ ] Prefetch forward moves, but only using MoveNextBatch method
    // [ ] Use Slices Memory<byte> instead of byte[]
    // [ ] Use some heuristic for batch size & prefetch - they are kept in memory
    //     for each opened series. MAX(4kb/item_size, 3) is a starting point
    // [ ] All storage-specific operations must be abstract
    // [ ] All read/write methods must not have any state other than series id

    // WITHOUT MESSAGING: If we read a series, e.g. x1..x20 and calculate MA(10) with prefetching 10,
    // and then someone puts an out-of-order update at x15, while e.g. we are at x14,
    // but have already prefetched x11..20, we should assume that all moves from 
    // x14 until x20 (or until next prefetch if it occurs sooner) are `happened before`
    // the OOO update at x15.

    public abstract class PersistentSeries : Series<long, Memory<byte>>, IPersistentOrderedMap<long, Memory<byte>> {
        public abstract void Dispose();
        public abstract void Flush();
        public abstract string Id { get; }
        public abstract void Add(long k, Memory<byte> v);
        public abstract void AddLast(long k, Memory<byte> v);
        public abstract void AddFirst(long k, Memory<byte> v);
        public abstract bool Remove(long k);
        public abstract bool RemoveLast(out KeyValuePair<long, Memory<byte>> value);
        public abstract bool RemoveFirst(out KeyValuePair<long, Memory<byte>> value);
        public abstract bool RemoveMany(long k, Lookup direction);
        public abstract int Append(IReadOnlyOrderedMap<long, Memory<byte>> appendMap, AppendOption option);
        public abstract void Complete();
        public abstract long Count { get; }
        public abstract long Version { get; set; }
        public abstract Memory<byte> this[long obj0] { get; set; }
        public override ICursor<long, Memory<byte>> GetCursor() {
            return base.GetCursor();
        }

        /// <summary>
        /// Returns a minimum specified number of items
        /// </summary>
        /// <param name="currentKey"></param>
        /// <param name="minCount"></param>
        /// <returns></returns>
        public abstract ISeries<long, Memory<byte>> MoveNextFrom(long currentKey, int minCount);


    }
}
