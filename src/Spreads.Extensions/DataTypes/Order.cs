/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

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
        Symbol Symbol { get; }
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
        private readonly Symbol _symbol; // 16

        private readonly Tick _tick; // 24
        private readonly TradeSide _tradeSide; // 1
        private readonly OrderType _orderType; // 1
        private byte _reserved1; // 1
        private byte _reserved2; // 1

        private int _reservedInt; // 4


        public Order(long orderId,
            OrderType orderType,
            Symbol symbol,
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
        public Symbol Symbol => _symbol;
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
