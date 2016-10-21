// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Threading.Tasks;

namespace Spreads.Storage {
    public interface ISeriesStorage : IDisposable {
        /// <summary>
        /// Get writable series that persist changes. Always returns a reference to the same object for each seriesId.
        /// </summary>
        Task<IPersistentOrderedMap<TKey, TValue>> GetPersistentOrderedMap<TKey, TValue>(string seriesId, bool readOnly = false);
    }
}
