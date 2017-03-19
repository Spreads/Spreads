using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Spreads.Serialization;

namespace Spreads.DataTypes
{
    /// <summary>
    /// A structure to represent "Small" and "Large" best bids and asks.
    /// Often best bids and asks have very small volume, which could create noise.
    /// "Small/Large" price is a WAP price we will pay/receive to buy/sell "Small/Large" quantity.
    /// The values are constructed from a limit order book snapshots to simplify modeling of historical
    /// market depth, reduce memory and increase speed.
    /// The notion of "Small/Large" depends on the context.
    /// Offset is the number of minimum price steps from absolute best bid/ask.
    /// Allows to recover those absolute best bid/ask levels with known price step or estimate market impact.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
    [Serialization(BlittableSize = 64)]
    [DataContract(Name = "MarketDepth2")]
    public struct MarketDepth2 : IDiffable<MarketDepth2>
    {
        internal Quote _largeAsk;
        internal Quote _smallAsk;
        internal Quote _smallBid;
        internal Quote _largeBid;

        public MarketDepth2(Price largeAskPrice, int largeAskSize, int largeAskOffset,
                            Price smallAskPrice, int smallAskSize, int smallAskOffset,
                            Price smallBidPrice, int smallBidSize, int smallBidOffset,
                            Price largeBidPrice, int largeBidSize, int largeBidOffset)
        {
            _largeAsk = new Quote(largeAskPrice, largeAskSize, largeAskOffset);
            _smallAsk = new Quote(smallAskPrice, smallAskSize, smallAskOffset);
            _smallBid = new Quote(smallBidPrice, smallBidSize, smallBidOffset);
            _largeBid = new Quote(largeBidPrice, largeBidSize, largeBidOffset);
        }

        // NB Order of properties matched binary layout, do not change

        [DataMember(Order = 1)]
        public Price LargeAskPrice => _largeAsk.Price;

        [DataMember(Order = 2)]
        public int LargeAskSize => _largeAsk.Volume;

        [DataMember(Order = 3)]
        public int LargeAskOffset => _largeAsk.Reserved;

        [DataMember(Order = 4)]
        public Price SmallAskPrice => _smallAsk.Price;

        [DataMember(Order = 5)]
        public int SmallAskSize => _smallAsk.Volume;

        [DataMember(Order = 6)]
        public int SmallAskOffset => _smallAsk.Reserved;

        [DataMember(Order = 7)]
        public Price SmallBidPrice => _smallBid.Price;

        [DataMember(Order = 8)]
        public int SmallBidSize => _smallBid.Volume;

        [DataMember(Order = 9)]
        public int SmallBidOffset => _smallBid.Reserved;

        [DataMember(Order = 10)]
        public Price LargeBidPrice => _largeBid.Price;

        [DataMember(Order = 11)]
        public int LargeBidSize => _largeBid.Volume;

        [DataMember(Order = 12)]
        public int LargeBidOffset => _largeBid.Reserved;

        public bool IsBestAsk => _smallAsk.Reserved == 0;
        public bool IsBestBid => _smallBid.Reserved == 0;

        public MarketDepth2 GetDelta(MarketDepth2 next)
        {
            var delta = new MarketDepth2(
                next.LargeAskPrice - this.LargeAskPrice,
                next.LargeAskSize,
                next.LargeAskOffset,
                next.SmallAskPrice - this.SmallAskPrice,
                next.SmallAskSize,
                next.SmallAskOffset,
                next.SmallBidPrice - this.SmallBidPrice,
                next.SmallBidSize,
                next.SmallBidOffset,
                next.LargeBidPrice - this.LargeBidPrice,
                next.LargeBidSize,
                next.LargeBidOffset
                );

            return delta;
        }

        public MarketDepth2 AddDelta(MarketDepth2 delta)
        {
            var newValue = new MarketDepth2(
                this.LargeAskPrice + delta.LargeAskPrice,
                delta.LargeAskSize,
                delta.LargeAskOffset,
                this.SmallAskPrice + delta.SmallAskPrice,
                delta.SmallAskSize,
                delta.SmallAskOffset,
                this.SmallBidPrice + delta.SmallBidPrice,
                delta.SmallBidSize,
                delta.SmallBidOffset,
                this.LargeBidPrice + delta.LargeBidPrice,
                delta.LargeBidSize,
                delta.LargeBidOffset
                );

            return newValue;
        }
    }
}