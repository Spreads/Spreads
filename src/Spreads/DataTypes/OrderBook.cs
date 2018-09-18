// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Runtime.InteropServices;
using Spreads.Serialization.Utf8Json;

namespace Spreads.DataTypes
{
    
    // TODO WIP

    // NB OB could be a persistent tree for "fast" lookup and versioning, but in reality several arrays will be faster and smaller.

    // OB is an example of non-atomic data type that benefits from byte shuffling. It needs a custom IBinaryConverter to leverage this.

    /// <summary>
    /// Data transfer object to store simple order book info. It has no additional functionality other than data storage.
    /// Sell orders has negative volume. Quotes are sorted in ascending order.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    [JsonFormatter(typeof(Formatter))]
    public readonly struct OrderBookQuotes // : IEquatable<OrderBookQuotes>, IDelta<OrderBookQuotes>
    {
        private readonly int _bidsCount;
        private readonly Memory<Quote> _quotes;

        internal class Formatter : IJsonFormatter<OrderBookQuotes>
        {
            public void Serialize(ref JsonWriter writer, OrderBookQuotes value, IJsonFormatterResolver formatterResolver)
            {
                throw new NotImplementedException();
            }

            public OrderBookQuotes Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
            {
                throw new NotImplementedException();
            }
        }
    }
}
