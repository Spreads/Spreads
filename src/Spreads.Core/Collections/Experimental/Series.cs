using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Spreads.Data;

namespace Spreads.Collections.Experimental
{
    // TODO Interfaces to match hierarchy

    // TODO will conflict with Data.Series when using static.
    // Prefer Data.XXX for discoverability, write good xml docs there
    public static class Series
    {
        public static void Test()
        {
        }
    }

    public class Series<TKey> : BaseContainer<TKey>, ISeries<TKey, object>, ISeriesNew
    {
        public IAsyncEnumerator<KeyValuePair<TKey, object>> GetAsyncEnumerator()
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<TKey, object>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool IsCompleted => throw new NotImplementedException();

        public KeySorting KeySorting => throw new NotImplementedException();

        public bool IsIndexed => throw new NotImplementedException();

        public ICursor<TKey, object> GetCursor()
        {
            throw new NotImplementedException();
        }

        public KeyComparer<TKey> Comparer => throw new NotImplementedException();

        public Opt<KeyValuePair<TKey, object>> First => throw new NotImplementedException();

        public Opt<KeyValuePair<TKey, object>> Last => throw new NotImplementedException();

        public object this[TKey key] => throw new NotImplementedException();

        public bool TryGetValue(TKey key, out object value)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAt(long index, out KeyValuePair<TKey, object> kvp)
        {
            if(TryGetBlockAt(index, out var chunk, out var chunkIndex))
            {
                var k = chunk.RowIndex.DangerousGet<TKey>(chunkIndex);
                var v = chunk.Values.DangerousGet(chunkIndex);
                kvp = new KeyValuePair<TKey, object>(k,v);
                return true;
            }
            kvp = default;
            return false;
        }

        public bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, object> kvp)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TKey> Keys => throw new NotImplementedException();

        public IEnumerable<object> Values => throw new NotImplementedException();

        public Mutability Mutability => throw new NotImplementedException();
    }


    public struct SCursor<TKey> // : ISpecializedCursor<TKey, object, Cursor<TKey, object>>
    {

    }
    ///////////////////////////////////////////////////////////////////////////////

    public class Series<TKey, TValue> : Series<TKey>
    {
    }

    public class AppendSeries<TKey>
    {
    }

    public class AppendSeries<TKey, TValue> : AppendSeries<TKey>
    {
    }

    public class MutableSeries<TKey>
    {
    }

    public class MutableSeries<TKey, TValue> : MutableSeries<TKey>
    {
    }

    //[DebuggerTypeProxy(typeof(IDictionaryDebugView<object, object>))]
    //[DebuggerDisplay("SortedMap: Count = {Count}")]
    //public sealed class Series<TKey, TValue> : ContainerSeries<TKey, TValue, SortedMapCursor<TKey, TValue>>, IMutableSeries<TKey, TValue>
    //{
    //    public override KeyComparer<TKey> Comparer => throw new NotImplementedException();

    //    public override Opt<KeyValuePair<TKey, TValue>> First => throw new NotImplementedException();

    //    public override Opt<KeyValuePair<TKey, TValue>> Last => throw new NotImplementedException();

    //    public override bool TryGetValue(TKey key, out TValue value)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public override bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public override bool IsIndexed => throw new NotImplementedException();

    //    internal override SortedMapCursor<TKey, TValue> GetContainerCursor()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public long Count => throw new NotImplementedException();

    //    public long Version => throw new NotImplementedException();

    //    public bool IsAppendOnly => throw new NotImplementedException();

    //    public Task<bool> Set(TKey key, TValue value)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Task<bool> TryAdd(TKey key, TValue value)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Task<bool> TryAddLast(TKey key, TValue value)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Task<bool> TryAddFirst(TKey key, TValue value)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public ValueTask<Opt<TValue>> TryRemove(TKey key)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveFirst()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveLast()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveMany(TKey key, Lookup direction)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Task<bool> TryRemoveMany(TKey key, TValue updatedAtKey, Lookup direction)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public ValueTask<long> TryAppend(ISeries<TKey, TValue> appendMap, AppendOption option = AppendOption.RejectOnOverlap)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Task Complete()
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    //[StructLayout(LayoutKind.Sequential, Pack = 1)]
    //public struct SortedMapCursor<K, V> : ISpecializedCursor<K, V, SortedMapCursor<K, V>>, IAsyncBatchEnumerator<KeyValuePair<K, V>>

    //{
    //    public ValueTask<bool> MoveNextAsync()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public CursorState State => throw new NotImplementedException();

    //    public KeyComparer<K> Comparer => throw new NotImplementedException();

    //    public bool MoveAt(K key, Lookup direction)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public bool MoveFirst()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public bool MoveLast()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    bool ICursor<K, V>.MoveNext()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public long MoveNext(long stride, bool allowPartial)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public bool MovePrevious()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public long MovePrevious(long stride, bool allowPartial)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public K CurrentKey => throw new NotImplementedException();

    //    public V CurrentValue => throw new NotImplementedException();

    //    Series<K, V, SortedMapCursor<K, V>> ISpecializedCursor<K, V, SortedMapCursor<K, V>>.Source => throw new NotImplementedException();

    //    public IAsyncCompleter AsyncCompleter => throw new NotImplementedException();

    //    ISeries<K, V> ICursor<K, V>.Source => throw new NotImplementedException();

    //    public bool IsContinuous => throw new NotImplementedException();

    //    public SortedMapCursor<K, V> Initialize()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    SortedMapCursor<K, V> ISpecializedCursor<K, V, SortedMapCursor<K, V>>.Clone()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public bool IsIndexed => throw new NotImplementedException();

    //    public bool IsCompleted => throw new NotImplementedException();

    //    ICursor<K, V> ICursor<K, V>.Clone()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public bool TryGetValue(K key, out V value)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    bool IEnumerator.MoveNext()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void Reset()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public KeyValuePair<K, V> Current => throw new NotImplementedException();

    //    object IEnumerator.Current => Current;

    //    public void Dispose()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public ValueTask DisposeAsync()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public ValueTask<bool> MoveNextBatch(bool noAsync)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public IEnumerable<KeyValuePair<K, V>> CurrentBatch => throw new NotImplementedException();
    //}
}
