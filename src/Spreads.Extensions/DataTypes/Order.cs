// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.DataTypes {

    // Some common ground for
    // http://www.nyxdata.com/doc/244339
    // ftp://ftp.moex.com/pub/FORTS/Plaza2/docs/p2gate_ru.pdf
    // Numbers correspond to NYSE description

    public enum OrderType : byte {
        None = 0,
        Add = 100,
        Modify = 101,
        Delete = 102,
        Fill = 103,
        Replace = 104,
    }


    public interface IOrder : ITick, IQuote {
        long OrderId { get; }
        OrderType OrderType { get; }
        Symbol16 Symbol { get; }
        // ITick interface
        //DateTime DateTimeUtc { get; }
        // incl. IQuote interface
        //Price Price { get; }
        //int Volume { get; }
        TradeSide TradeSide { get; }
        long TradeId { get; }
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct Order : IOrder {
        private readonly long _orderId; // 8
        private readonly long _tradeId; // 8
        private readonly Symbol16 _symbol; // 16

        private readonly Tick _tick; // 24
        private readonly TradeSide _tradeSide; // 1
        private readonly OrderType _orderType; // 1
        private byte _reserved1; // 1
        private byte _reserved2; // 1

        private int _reservedInt; // 4


        public Order(long orderId,
            OrderType orderType,
            Symbol16 symbol,
            DateTime dateTimeUtc,
            Price price,
            int volume,
            TradeSide tradeSide,
            long tradeId) {
            _orderId = orderId;
            _tradeId = tradeId;
            _symbol = symbol;
            _tick = new Tick(dateTimeUtc, price, volume);
            _tradeSide = tradeSide;
            _orderType = orderType;
            _reserved1 = 0;
            _reserved2 = 0;
            _reservedInt = 0;
        }

        public long OrderId => _orderId;
        public OrderType OrderType => _orderType;
        public Symbol16 Symbol => _symbol;
        public DateTime DateTimeUtc => _tick.DateTimeUtc;
        public Price Price => _tick.Price;
        public int Volume => _tick.Volume;

        public TradeSide TradeSide => _tradeSide;
        public long TradeId => _tradeId;
        public int Reserved
        {
            get { return _reservedInt; }
            set { _reservedInt = value; }
        }
    }

}
