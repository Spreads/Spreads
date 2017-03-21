// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace Spreads.DataTypes
{
    public class OrderBook<TOrder> where TOrder : IOrder
    {
    }

    // This should be the same structure to store snapshots as well as deltas
    public class SparseOrderBook
    {
        private DateTime _moment;

        private Price _minPrice;
        private Price _maxPrice;
        private Price _priceStep;

        private int[] _volumes;

        private int _bestBid; // NB Best ask could be more than 1 away from best bid, out grid is sparse
        private int _bestAsk;

        // TODO store volumes that cleared between frames as an array

        // NB
        // * index i defines price as (_minPrice + i * _priceStep)
        // * we do not spend memory on Prices range
        // * when _min/_max/_step are the same, we could use SIMD for addition/substraction
        //   e.g. snapshot(t+1) = snapshot(t) + delta(t+1)
        // * deltas will be very very small after Blosc compression
        //
    }
}