using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Spreads.Serialization;

namespace Spreads.DataTypes
{
    /// <summary>
    /// A structure to represent "Small" best bids and asks.
    /// Often best bids and asks have very small volume, which could create noise.
    /// "Small" price is a WAP price we will pay/receive to buy/sell "Small" quantity.
    /// The values are constructed from a limit order book snapshots to simplify modeling of historical
    /// market depth, reduce memory and increase speed.
    /// The notion of "Small" depends on the context.
    /// Offset is the number of minimum price steps from absolute best bid/ask.
    /// Allows to recover those absolute best bid/ask levels with known price step or estimate market impact.
    /// Offset equalt to zero indicates that this is the best bid/ask.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
    [Serialization(BlittableSize = 32)]
    [DataContract(Name = "MarketDepth")]
    public struct MarketDepth : IDiffable<MarketDepth>
    {
        internal Quote _smallAsk;
        internal Quote _smallBid;

        public MarketDepth(Price smallAskPrice, int smallAskSize, int smallAskOffset,
                            Price smallBidPrice, int smallBidSize, int smallBidOffset)
        {
            _smallAsk = new Quote(smallAskPrice, smallAskSize, smallAskOffset);
            _smallBid = new Quote(smallBidPrice, smallBidSize, smallBidOffset);
        }

        // NB Order of properties matched binary layout, do not change

        [DataMember(Order = 1)]
        public Price SmallAskPrice => _smallAsk.Price;

        [DataMember(Order = 2)]
        public int SmallAskSize => _smallAsk.Volume;

        [DataMember(Order = 3)]
        public int SmallAskOffset => _smallAsk.Reserved;

        [DataMember(Order = 4)]
        public Price SmallBidPrice => _smallBid.Price;

        [DataMember(Order = 5)]
        public int SmallBidSize => _smallBid.Volume;

        [DataMember(Order = 6)]
        public int SmallBidOffset => _smallBid.Reserved;

        public bool IsBestAsk => _smallAsk.Reserved == 0;
        public bool IsBestBid => _smallBid.Reserved == 0;

        public MarketDepth GetDelta(MarketDepth next)
        {
            var delta = new MarketDepth(

                next.SmallAskPrice - this.SmallAskPrice,
                next.SmallAskSize,
                next.SmallAskOffset,
                next.SmallBidPrice - this.SmallBidPrice,
                next.SmallBidSize,
                next.SmallBidOffset

                );

            return delta;
        }

        public MarketDepth AddDelta(MarketDepth delta)
        {
            var newValue = new MarketDepth(
                this.SmallAskPrice + delta.SmallAskPrice,
                delta.SmallAskSize,
                delta.SmallAskOffset,
                this.SmallBidPrice + delta.SmallBidPrice,
                delta.SmallBidSize,
                delta.SmallBidOffset
                );

            return newValue;
        }
    }
}