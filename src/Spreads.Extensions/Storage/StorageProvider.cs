// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Storage {

    // TODO Transactions accross diferent methods? Or add should accept an array of panel chunks
    // 

    public interface ITransaction
    {
        void Commit();
        void Rollback();
    }

    /// <summary>
    /// Base class to abstract storage implementation
    /// </summary>
    public abstract class StorageProvider {
        // NB this method allows to implement
        // MoveFirst - 0L + GE, MoveLast - long.MaxValue + LE
        // MoveNext - CurrentKey + GT, etc.
        public abstract int TryGetChunksAt(int panelId, long key, Lookup direction, ref RawPanelChunk[] rawPanelChunks, int[] columnIds = null);

        public abstract ValueTuple<long, long> GetRange(int panelId);

        public abstract bool Add(RawPanelChunk rawPanelChunk, bool replace = false);

        public abstract bool Remove(int panelId, long key, Lookup direction);

        // TODO key range
        // Columns by panel id
        // Create series
        // Create panels
    }
}
