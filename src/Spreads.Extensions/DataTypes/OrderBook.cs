using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.DataTypes {

    // Some common ground for
    // http://www.nyxdata.com/doc/244339
    // ftp://ftp.moex.com/pub/FORTS/Plaza2/docs/p2gate_ru.pdf
    // Numbers correspond to NYSE description

    public enum OrderType : byte
    {
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
        // ITick interface
        //DateTime DateTimeUtc { get; }
        // incl. IQuote interface
        //Price Price { get; }
        //int Volume { get; }
        Side Side { get; }
        long TradeId { get; }
    }

    public class OrderBook<TOrder> where TOrder : IOrder {
    }

}
