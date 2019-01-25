// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads
{
    /// <summary>
    /// Mutability of underlying data storage.
    /// </summary>
    public enum Mutability : byte
    {
        /// <summary>
        /// Data cannot be modified.
        /// </summary>
        Immutable,

        /// <summary>
        /// Data could be added without changing existing order. Segments of existing data could be treated as <see cref="Immutable"/>.
        /// </summary>
        AppendOnly,

        /// <summary>
        /// Data could be modified at any place and order of existing data could change.
        /// </summary>
        Mutable
    }
}