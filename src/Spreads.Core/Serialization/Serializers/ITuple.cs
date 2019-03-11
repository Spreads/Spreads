using System.Runtime.CompilerServices;

namespace Spreads.Serialization.Serializers
{
    internal interface ITuple<T1, T2, TImpl> where TImpl : struct, ITuple<T1, T2, TImpl>
    {
        // with struct constraint we could use default(TImpl).FromTuple((T1, T2) tuple)
        // as an alternative to static interface.
        TImpl FromTuple((T1, T2) tuple);

        (T1, T2) ToTuple();
    }

    internal interface ITuple<T1, T2, T3, TImpl> where TImpl : struct, ITuple<T1, T2, T3, TImpl>
    {
        // with struct constraint we could use default(TImpl).FromTuple((T1, T2) tuple)
        // as an alternative to static interface.
        TImpl FromTuple((T1, T2, T3) tuple);

        (T1, T2, T3) ToTuple();
    }

    // TODO review if this makes sense to have such interface
    // + it could be a convenient shortcut instead of full blown BinarySerializer impl
    // + it could reduce typing of tuple names,
    //      e.g. (SmallDecimal Price, int Volume) vs Quote<SmallDecimal,int>,
    //      or if price is always SD then just Quote<int>
    //      or if both SD then Quote.
    // + With this interface no need to specify attributes and care about fields
    //   order, fields vs props, etc.
    // + Also see OHLCV, it could be just ITuple and still pack deltas.
    // - But it will be slower that blittable struct (TODO test),
    //   and when packed cannot be consumed separately.
    // - Arrays of ITuples will be much much slower than blittables.
    // - Plus a struct could have multiple iface impl and we cannot prevent that
    //   as with attributes. (see below)
    // + JSON could use the interface and we could unify serialization.

    /// <summary>
    /// Sometimes volume is int (lots), other times it is full decimal (Ethereum wei).
    /// TODO At least price could always fir into SmallDecimal.
    /// </summary>
    /// <typeparam name="TPrice"></typeparam>
    /// <typeparam name="TVolume"></typeparam>
    internal struct Quote<TPrice, TVolume> : ITuple<TPrice, TVolume, Quote<TPrice, TVolume>>
    {
        public TPrice Price { get; }
        public TVolume Volume { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quote(TPrice price, TVolume volume)
        {
            Price = price;
            Volume = volume;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quote<TPrice, TVolume> FromTuple((TPrice, TVolume) tuple)
        {
            return new Quote<TPrice, TVolume>(tuple.Item1, tuple.Item2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (TPrice, TVolume) ToTuple()
        {
            return (Price, Volume);
        }
    }
}