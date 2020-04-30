// ReSharper disable once CheckNamespace

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Spreads;
using Spreads.Collections;
using Spreads.Collections.Internal;
using Index = Spreads.Index;

// ReSharper disable InconsistentNaming

namespace SpreadsX.Experimental
{
    public struct Any
    {
        private long Idx;

        public Any(long idx)
        {
            Idx = idx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Any(int value)
        {
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Any(string value)
        {
            return default;
        }
    }

    public interface ICursor<K, out V> // : IEnumerator<KeyValuePair<K, V>>
    {
        new bool MoveNext();
        K CurrentKey { get; }
        V CurrentValue { get; }
    }

    public interface ISeries<K, V> // : IEnumerable<KeyValuePair<K, V>>
    {
        ICursor<K, V> GetCursor();
    }

    public interface ISeries<K, V, out TCursor> : ISeries<K, V> where TCursor : ICursor<K, V>
    {
        TCursor GetCursor();
    }

    public interface ISeries<K> : ISeries<K, Any>
    {
    }

    public interface IVector<V> : ISeries<Index, V>
    {
    }

    public interface IVector : IVector<Any>
    {
    }

    public interface IPanel<R, C, V> : ISeries<R, ISeries<C, V>>
    {
    }

    public interface IPanel<R, V> : IPanel<R, Index, V>, ISeries<R, IVector<V>>
    {
    }

    public interface IPanel<V> : IPanel<Index, Index, V>, IVector<IVector<V>>
    {
    }

    public interface IMatrix<V> : IPanel<V>
    {
    }

    public interface IFrame<R, C> : IPanel<R, C, Any>, ISeries<R, ISeries<C>>
    {
    }

    //////////////////////////////////////////////////////////////////////////////////////

    internal class SeriesImpl
    {
    }

    internal class SeriesImpl<K> : SeriesImpl
    {
    }

    internal class SeriesImpl<K, V> : SeriesImpl<K>, ISeries<K, V, Cursor<K, V>>
    {
        public Cursor<K, V> GetCursor() => default;

        ICursor<K, V> ISeries<K, V>.GetCursor() => GetCursor();
    }

    public struct Cursor<K, V> : ICursor<K, V>
    {
        // Move _inner cursor
        private BlockIndexCursor<K, V, KVFactory> _inner;

        // with _source range constraints and fill value
        private Series<K, V> _source;

        public bool MoveNext() => _inner.MoveNext();
        public K CurrentKey => _inner._currentKey;
        public V CurrentValue => _inner._currentValue;

        private struct KVFactory : IBlockIndexCursorKeyValueFactory<K, V>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetCurrentKeyValue(DataBlock dataBlock, int currentBlockIndex, ref K key, ref V value)
            {
                if (typeof(K) == typeof(Index))
                {
                    value = dataBlock.UnsafeGetValue<V>(currentBlockIndex);
                    key = (K) (object) new Index(currentBlockIndex); // TODO this is wrong, we need global index, not current block index
                    return;
                }

                dataBlock.UnsafeGetRowKeyValue(currentBlockIndex, out key, out value);
            }

            public void ReleaseValue(V value)
            {
                // noop
            }
        }
    }

    public struct Cursor<R, C, V> : ICursor<R, ISeries<C, V>>, ICursor<R, Series<C, V>>
    {
        // Move _inner cursor
        private BlockIndexCursor<R, Series<C, V>, KVFactory> _inner;

        // with _source range constraints and fill value
        private Panel<R, C, V> _source;

        public bool MoveNext() => _inner.MoveNext();
        public R CurrentKey => _inner._currentKey;
        public Series<C, V> CurrentValue => _inner._currentValue;
        ISeries<C, V> ICursor<R, ISeries<C, V>>.CurrentValue => CurrentValue;

        private struct KVFactory : IBlockIndexCursorKeyValueFactory<R, Series<C, V>>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetCurrentKeyValue(DataBlock dataBlock, int currentBlockIndex, ref R key, ref Series<C, V> value)
            {
                // Maybe smth like this:
                // if

                throw new NotImplementedException();
                // if (typeof(R) == typeof(Index))
                // {
                //     value = dataBlock.DangerousValue<V>(currentBlockIndex);
                //     key = (R) (object) new Index(currentBlockIndex); // TODO this is wrong, we need global index, not current block index
                //     return;
                // }
                //
                // dataBlock.DangerousGetRowKeyValue(currentBlockIndex, out key, out value);
            }

            public void ReleaseValue(Series<C, V> value)
            {
                // TODO
                // value.Dispose();
            }
        }
    }

    public readonly struct Series<K, V> : ISeries<K, V, Cursor<K, V>>
    {
        internal readonly DataContainer _container;
        private readonly SeriesImpl<K, V> _impl;

        ICursor<K, V> ISeries<K, V>.GetCursor() => GetCursor();
        public Cursor<K, V> GetCursor() => _impl.GetCursor();

        // implicitly convertible to Vector<V>, Series<K>, Vector 

        // TODO split tuples into fields

        // for range/slice it could be a part of basic behavior
        private readonly bool _hasRange;
        private readonly (Lookup, K) _from;

        // _to could be greater than the last existing key
        private readonly (Lookup, K) _to;

        // instead of returning true/false for TGV and similar we could always return this field 
        private readonly (bool isFill, V fillValue) _fill;
        private bool IsContinuous => _fill.isFill;

        // Fill(x).Fill(y) == Fill(x) because Fill is defined everywhere
        // it's interesting that all possible filled series form a vector space if we could define zero and one for V. 

        public bool Test()
        {
            if (typeof(V) == typeof(Series<V, double>))
            {
                return true;
            }

            return false;
        }
    }

    public static class SeriesExtensions
    {
        // TODO this is internal, publicly expose isInclusive. E.g. from (LT,X) makes no sense, or if it does it's more complex to implement vs simple lookup. For "from" only GE or GT, for "to" only LT or LE.
        public static Series<K, V> Slice<K, V>(this Series<K, V> series, Opt<(Lookup, K)> from, Opt<(Lookup, K)> to)
        {
            // borrow reference to container
            // return new Series<K,V>(...)
            return default;
        }

        // TODO After, Before
    }

    public readonly struct Series<K> : ISeries<K, Any, Cursor<K, Any>>
    {
        private readonly ISeries<K, Any, Cursor<K, Any>> _impl;

        ICursor<K, Any> ISeries<K, Any>.GetCursor() => GetCursor();
        public Cursor<K, Any> GetCursor() => _impl.GetCursor();
    }

    public readonly struct Vector<V> : ISeries<Index, V, Cursor<Index, V>>
    {
        private readonly ISeries<Index, V, Cursor<Index, V>> _impl;

        ICursor<Index, V> ISeries<Index, V>.GetCursor() => GetCursor();
        public Cursor<Index, V> GetCursor() => _impl.GetCursor();
    }

    public readonly struct Vector : ISeries<Index, Any, Cursor<Index, Any>>
    {
        private readonly ISeries<Index, Any, Cursor<Index, Any>> _impl;

        ICursor<Index, Any> ISeries<Index, Any>.GetCursor() => GetCursor();
        public Cursor<Index, Any> GetCursor() => _impl.GetCursor();
    }

    public readonly struct Series : ISeries<Any, Any, Cursor<Any, Any>>
    {
        private readonly ISeries<Any, Any, Cursor<Any, Any>> _impl;

        ICursor<Any, Any> ISeries<Any, Any>.GetCursor() => GetCursor();
        public Cursor<Any, Any> GetCursor() => _impl.GetCursor();
    }

    public readonly struct Panel<R, C, V> : ISeries<R, Series<C, V>, Cursor<R, C, V>>, IPanel<R, C, V>
    {
        private readonly ISeries<R, Series<C, V>, Cursor<R, Series<C, V>>> _impl;

        ICursor<R, Series<C, V>> ISeries<R, Series<C, V>>.GetCursor() => GetCursor();

        public Cursor<R, C, V> GetCursor() => default; // _impl.GetCursor();

        ICursor<R, ISeries<C, V>> ISeries<R, ISeries<C, V>>.GetCursor()
        {
            throw new System.NotImplementedException();
        }

        public Series<R, V> GetColumn(C column) => throw new NotImplementedException();
        public Series<R, V> GetColumn(Index column) => throw new NotImplementedException();
    }

    public readonly struct Panel<R, V> : ISeries<R, Series<Index, V>, Cursor<R, Index, V>>, IPanel<R, Index, V>
    {
        private readonly ISeries<R, Series<Index, V>, Cursor<R, Series<Index, V>>> _impl;

        ICursor<R, Series<Index, V>> ISeries<R, Series<Index, V>>.GetCursor() => GetCursor();

        public Cursor<R, Index, V> GetCursor() => default; // _impl.GetCursor();

        ICursor<R, ISeries<Index, V>> ISeries<R, ISeries<Index, V>>.GetCursor()
        {
            throw new System.NotImplementedException();
        }
    }

    public readonly struct Matrix<V> : ISeries<Index, Vector<V>, Cursor<Index, Vector<V>>>, IMatrix<V>
    {
        private readonly ISeries<Index, Vector<V>, Cursor<Index, Vector<V>>> _impl;

        ICursor<Index, Vector<V>> ISeries<Index, Vector<V>>.GetCursor() => GetCursor();

        public Cursor<Index, Vector<V>> GetCursor() => _impl.GetCursor();

        ICursor<Index, ISeries<Index, V>> ISeries<Index, ISeries<Index, V>>.GetCursor()
        {
            throw new System.NotImplementedException();
        }

        ICursor<Index, IVector<V>> ISeries<Index, IVector<V>>.GetCursor()
        {
            throw new System.NotImplementedException();
        }
    }

    public struct BlockIndexCursor<K>
    {
        internal DataBlock Block;
        internal int Index;
        internal K CurrentKey;

        public int Move(int steps)
        {
            return steps;
        }
    }

    // public interface ICursorX<K, V>
    // {
    //     
    // }
    // public interface ICursorX<K, V, TImpl> : ICursorX<K,V>
    //     where TImpl : ICursorX<K, V, TImpl>
    // {
    //     TImpl GetImpl();
    //
    //     TCNew Combine<U, TImpl, TCNew>() where TCNew : ICursorX<K, U, TCNew>;
    // }
    //
    // public struct CursorX<K, V> : ICursorX<K, V, CursorX<K, V>>
    // {
    //     internal BlockIndexCursor<K> InnerCursor;
    //
    //     private Func<BlockIndexCursor<K>, V>? ValueGetter;
    //
    //     public V GetV()
    //     {
    //         if (ValueGetter == null)
    //         {
    //             return InnerCursor.Block.UnsafeGetValue<V>(InnerCursor.Index);
    //         }
    //
    //         return ValueGetter(InnerCursor);
    //     }
    //
    //     public CursorX<K, V> GetImpl()
    //     {
    //         return this;
    //     }
    //
    //     public TCNew Combine<U, TImpl, TCNew>() where TCNew : ICursorX<K, U, TCNew>
    //     {
    //         throw new NotImplementedException();
    //     }
    //
    //     public  TCNew Combine<U, TCNew>() where TCNew : ICursorX<K, U, TCNew>
    //     {
    //         throw new NotImplementedException();
    //     }
    // }
    //
    // internal struct CursorY<K, V, C> : ICursorX<K,V> where C : ICursorX<K, V, C>
    // {
    //     private readonly C _inner;
    //
    //     public CursorY(C inner)
    //     {
    //         _inner = inner;
    //     }
    // }
    //
    // static class TestDynamic
    // {
    //     public static CursorY<K, V, C> CreateY<K, V, C>(ICursorX<K, V, C> inner) where C : ICursorX<K, V, C>
    //     {
    //         return new CursorY<K, V, C>(inner.GetImpl());
    //     }
    //
    //     public static void Test()
    //     {
    //         var x = new CursorX<int, int>();
    //         var y = CreateY(x);
    //         ICursorX<int, int> xbox = x;
    //         var xd = (dynamic) x;
    //         ICursorX<int,int> yd = CreateY(xd);
    //         
    //         // var y = CreateY((dynamic)x);
    //     }
    // }

    public struct CusrorA<K, V> : ICursorX<K, V, CusrorA<K, V>>
    {
        public TCursor2 Combine<TValue2, TCursor2>(Func<CusrorA<K, V>, TCursor2> factory) where TCursor2 : ICursor<K, TValue2, TCursor2>
        {
            throw new NotImplementedException();
        }

        public bool MoveNext()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public KeyValuePair<K, V> Current => throw new NotImplementedException();

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public CursorState State => throw new NotImplementedException();

        public KeyComparer<K> Comparer => throw new NotImplementedException();

        public bool MoveFirst()
        {
            throw new NotImplementedException();
        }

        public bool MoveLast()
        {
            throw new NotImplementedException();
        }

        public long Move(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        public bool MovePrevious()
        {
            throw new NotImplementedException();
        }

        public bool MoveTo(K key, Lookup direction)
        {
            throw new NotImplementedException();
        }

        public K CurrentKey => throw new NotImplementedException();

        public V CurrentValue => throw new NotImplementedException();

        Series<K, V, CusrorA<K, V>> ICursor<K, V, CusrorA<K, V>>.Source => throw new NotImplementedException();

        public bool TryMoveNextBatch(out Series<K, V, CusrorA<K, V>> batch)
        {
            throw new NotImplementedException();
        }

        Spreads.ISeries<K, V> Spreads.ICursor<K, V>.Source => throw new NotImplementedException();

        public CusrorA<K, V> Initialize()
        {
            throw new NotImplementedException();
        }

        CusrorA<K, V> ICursor<K, V, CusrorA<K, V>>.Clone()
        {
            throw new NotImplementedException();
        }

        public IAsyncCompleter AsyncCompleter => throw new NotImplementedException();

        Spreads.ICursor<K, V> Spreads.ICursor<K, V>.Clone()
        {
            throw new NotImplementedException();
        }

        public bool IsContinuous => throw new NotImplementedException();

        public bool TryGet(K key, out V value)
        {
            throw new NotImplementedException();
        }

        public bool TryMoveNextBatch(out Spreads.ISeries<K, V>? batch)
        {
            throw new NotImplementedException();
        }
    }

    public static class MapFactory
    {
        public static Map<TKey, TInput, TResult, TCursor> Factory<TKey, TInput, TResult, TCursor>(TCursor x) where TCursor : ICursor<TKey, TInput, TCursor> => new Map<TKey, TInput, TResult, TCursor>();
    }
    
    public struct Map<TKey, TInput, TResult, TCursor> :
        ICursorX<TKey, TResult, Map<TKey, TInput, TResult, TCursor>>
        where TCursor : ICursor<TKey, TInput, TCursor>
    {
        private readonly TCursor _inner;

        public Map(TCursor inner)
        {
            _inner = inner;
        }
        public TCursor2 Combine<TValue2, TCursor2>(Func<Map<TKey, TInput, TResult, TCursor>, TCursor2> factory) where TCursor2 : ICursor<TKey, TValue2, TCursor2>
        {
            throw new NotImplementedException();
        }

        public bool MoveNext()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public KeyValuePair<TKey, TResult> Current => throw new NotImplementedException();

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public CursorState State => throw new NotImplementedException();

        public KeyComparer<TKey> Comparer => throw new NotImplementedException();

        public bool MoveFirst()
        {
            throw new NotImplementedException();
        }

        public bool MoveLast()
        {
            throw new NotImplementedException();
        }

        public long Move(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        public bool MovePrevious()
        {
            throw new NotImplementedException();
        }

        public bool MoveTo(TKey key, Lookup direction)
        {
            throw new NotImplementedException();
        }

        public TKey CurrentKey => throw new NotImplementedException();

        public TResult CurrentValue => throw new NotImplementedException();

        Series<TKey, TResult, Map<TKey, TInput, TResult, TCursor>> ICursor<TKey, TResult, Map<TKey, TInput, TResult, TCursor>>.Source => throw new NotImplementedException();

        public bool TryMoveNextBatch(out Series<TKey, TResult, Map<TKey, TInput, TResult, TCursor>> batch)
        {
            throw new NotImplementedException();
        }

        Spreads.ISeries<TKey, TResult> Spreads.ICursor<TKey, TResult>.Source => throw new NotImplementedException();

        public Map<TKey, TInput, TResult, TCursor> Initialize()
        {
            throw new NotImplementedException();
        }

        Map<TKey, TInput, TResult, TCursor> ICursor<TKey, TResult, Map<TKey, TInput, TResult, TCursor>>.Clone()
        {
            throw new NotImplementedException();
        }

        public IAsyncCompleter AsyncCompleter => throw new NotImplementedException();

        Spreads.ICursor<TKey, TResult> Spreads.ICursor<TKey, TResult>.Clone()
        {
            throw new NotImplementedException();
        }

        public bool IsContinuous => throw new NotImplementedException();

        public bool TryGet(TKey key, out TResult value)
        {
            throw new NotImplementedException();
        }

        public bool TryMoveNextBatch(out Spreads.ISeries<TKey, TResult>? batch)
        {
            throw new NotImplementedException();
        }
    }

    public static class Mapper
    {
        public static Spreads.ICursor<K,U> Map<K,V, U>(CusrorA<K,V> cursor, Func<V,U> selector)
        {
            var x = cursor.Combine<U, Map<K, V, U, CusrorA<K, V>>>(inner => new Map<K, V, U, CusrorA<K, V>>(inner));
            return (Spreads.ICursor<K,U>)x;
        }
    }

    

    // Principles:
    // Never evaluate same value twice when we know in advance that this will happen and we could avoid it by saving data to memory, which is cheap

    internal delegate Span<U> MapBatch<K, V, U>(Span<K> keys, Span<V> values);

}