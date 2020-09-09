// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.DataTypes
{
    /// <summary>
    /// Trade side enum: Buy, Sell or None (default)
    /// </summary>
    public enum TradeSide : sbyte
    {
        /// <summary>
        /// By default, this should be always zero, so if there is an error and we forget to specify the Side, we must fail fast.
        /// </summary>
        None = 0,

        /// <summary>
        /// Buy.
        /// </summary>
        Buy = 1,

        /// <summary>
        /// Sell.
        /// </summary>
        Sell = -1,
    }
}