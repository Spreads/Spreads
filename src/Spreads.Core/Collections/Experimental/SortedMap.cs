using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Spreads.Collections.Experimental
{
    // Rewrite of initial (and battle-tested) F# version using
    // Memory<T> instead of arrays as the backing

    [DebuggerTypeProxy(typeof(IDictionaryDebugView<object, object>))]
    [DebuggerDisplay("SortedMap: Count = {Count}")]
    public sealed class SortedMap<K, V> : ContainerSeries<K, V, SortedMapCursor<K, V>>, IMutableSeries<K, V>
    {
        public override KeyComparer<K> Comparer => throw new NotImplementedException();

        public override Opt<KeyValuePair<K, V>> First => throw new NotImplementedException();

        public override Opt<KeyValuePair<K, V>> Last => throw new NotImplementedException();

        public override bool TryGetValue(K key, out V value)
        {
            throw new NotImplementedException();
        }

        public override bool TryFindAt(K key, Lookup direction, out KeyValuePair<K, V> kvp)
        {
            throw new NotImplementedException();
        }

        public override bool IsIndexed => throw new NotImplementedException();

        internal override SortedMapCursor<K, V> GetContainerCursor()
        {
            throw new NotImplementedException();
        }

        public long Count => throw new NotImplementedException();

        public long Version => throw new NotImplementedException();

        public bool IsAppendOnly => throw new NotImplementedException();

        public Task<bool> Set(K key, V value)
        {
            throw new NotImplementedException();
        }

        public Task<bool> TryAdd(K key, V value)
        {
            throw new NotImplementedException();
        }

        public Task<bool> TryAddLast(K key, V value)
        {
            throw new NotImplementedException();
        }

        public Task<bool> TryAddFirst(K key, V value)
        {
            throw new NotImplementedException();
        }

        public ValueTask<Opt<V>> TryRemove(K key)
        {
            throw new NotImplementedException();
        }

        public ValueTask<Opt<KeyValuePair<K, V>>> TryRemoveFirst()
        {
            throw new NotImplementedException();
        }

        public ValueTask<Opt<KeyValuePair<K, V>>> TryRemoveLast()
        {
            throw new NotImplementedException();
        }

        public ValueTask<Opt<KeyValuePair<K, V>>> TryRemoveMany(K key, Lookup direction)
        {
            throw new NotImplementedException();
        }

        public Task<bool> TryRemoveMany(K key, V updatedAtKey, Lookup direction)
        {
            throw new NotImplementedException();
        }

        public ValueTask<long> TryAppend(ISeries<K, V> appendMap, AppendOption option = AppendOption.RejectOnOverlap)
        {
            throw new NotImplementedException();
        }

        public Task Complete()
        {
            throw new NotImplementedException();
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SortedMapCursor<K, V> : ISpecializedCursor<K, V, SortedMapCursor<K, V>>, IAsyncBatchEnumerator<KeyValuePair<K, V>>

    {
        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotImplementedException();
        }

        public CursorState State => throw new NotImplementedException();

        public KeyComparer<K> Comparer => throw new NotImplementedException();

        public bool MoveAt(K key, Lookup direction)
        {
            throw new NotImplementedException();
        }

        public bool MoveFirst()
        {
            throw new NotImplementedException();
        }

        public bool MoveLast()
        {
            throw new NotImplementedException();
        }

        bool ICursor<K, V>.MoveNext()
        {
            throw new NotImplementedException();
        }

        public long MoveNext(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        public bool MovePrevious()
        {
            throw new NotImplementedException();
        }

        public long MovePrevious(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        public K CurrentKey => throw new NotImplementedException();

        public V CurrentValue => throw new NotImplementedException();

        Series<K, V, SortedMapCursor<K, V>> ISpecializedCursor<K, V, SortedMapCursor<K, V>>.Source => throw new NotImplementedException();

        public IAsyncCompleter AsyncCompleter => throw new NotImplementedException();

        ISeries<K, V> ICursor<K, V>.Source => throw new NotImplementedException();

        public bool IsContinuous => throw new NotImplementedException();

        public SortedMapCursor<K, V> Initialize()
        {
            throw new NotImplementedException();
        }

        SortedMapCursor<K, V> ISpecializedCursor<K, V, SortedMapCursor<K, V>>.Clone()
        {
            throw new NotImplementedException();
        }

        public bool IsIndexed => throw new NotImplementedException();

        public bool IsCompleted => throw new NotImplementedException();

        ICursor<K, V> ICursor<K, V>.Clone()
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(K key, out V value)
        {
            throw new NotImplementedException();
        }

        bool IEnumerator.MoveNext()
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

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public ValueTask<bool> MoveNextBatch(bool noAsync)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<KeyValuePair<K, V>> CurrentBatch => throw new NotImplementedException();
    }
}
