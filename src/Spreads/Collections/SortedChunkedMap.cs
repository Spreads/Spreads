using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Spreads.Collections
{
    public class SortedChunkedMap<K, V> : SortedChunkedMapBase<K, V>, IMutableSeries<K, V>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new async Task<bool> SetOrAddUnchecked(K key, V value, bool overwrite)
        {
            if (chunkUpperLimit == 0)
            {
                ThrowHelper.ThrowNotImplementedException();
                return await TaskUtil.FalseTask;
            }
            else
            {
                if (!(prevWBucket is null) && comparer.Compare(key, prevWHash) >= 0
                                           && prevWBucket.CompareToLast(key) <= 0)
                {
                    // we are inside previous bucket, setter has no choice but to set to this
                    // bucket regardless of its size
                    var res = prevWBucket.SetOrAdd(key, value, overwrite);
                    this.NotifyUpdate(true);
                    return await res;
                }
                else
                {
                    if (outerMap.TryFindAt(key, Lookup.LE, out var kvp)
                        // the second condition here is for the case when we add inside existing bucket, overflow above chunkUpperLimit is inevitable without a separate split logic (TODO?)
                        && (kvp.Value.size < chunkUpperLimit || kvp.Value.CompareToLast(key) <= 0)
                    )
                    {
                        if (comparer.Compare(prevWHash, kvp.Key) != 0)
                        {
                            // switched active bucket
                            await this.FlushUnchecked();
                            // if add fails later, it is ok to update the stale version to this._version (TODO WTF this comment says?)
                            kvp.Value._version = this._version;
                            kvp.Value._nextVersion = this._version;
                            prevWHash = kvp.Key;
                            prevWBucket = kvp.Value;
                        }

                        Debug.Assert(kvp.Value._version == _version);
                        var res = kvp.Value.SetOrAdd(key, value, overwrite);
                        this.NotifyUpdate(true);
                        return await res;
                    }
                    else
                    {
                        if (!(prevWBucket is null))
                        {
                            await this.FlushUnchecked();
                        }
                        // create a new bucket at key
                        var newSm = innerFactory.Invoke(Tuple.Create(0, comparer));
                        newSm._version = this._version;
                        newSm._nextVersion = this._version;
                        // Set and Add are the same here, we use new SM
                        newSm.SetOrAdd(key, value, overwrite); // we know that SM is syncronous

                        var outerSet =
                            await outerMap.Set(key,
                                newSm); // outerMap.Version is incremented here, set non-empty bucket only

                        prevWHash = key;
                        prevWBucket = newSm;
                        this.NotifyUpdate(true);
                        return outerSet;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new async Task<bool> SetOrAdd(K key, V value, bool overwrite)
        {
            BeforeWrite();
            var result = await this.SetOrAddUnchecked(key, value, overwrite);
            AfterWrite(result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new void Add(K key, V value)
        {
            var result = this.SetOrAdd(key, value, false).Result;
            if (!result)
            {
                ThrowHelper.ThrowArgumentException("Key already exists");
            }
        }
    }

    //public class SortedChunkedMap<K, V> : ContainerSeries<K, V, SortedChunkedMapCursor<K, V>>, IMutableSeries<K, V>, IPersistentObject
    //{
    //    private readonly Func<int, KeyComparer<K>, SortedMap<K, V>> _innerFactory;
    //    private readonly KeyComparer<K> _comparer;
    //    private readonly IKeyHasher<K> _hasher;
    //    private readonly int _chunkUpperLimit;
    //    private IMutableSeries<K, SortedMap<K, V>> _outerMap;

    //    internal bool _isReadOnly;
    //    internal long _orderVersion;
    //    internal string _id;

    //    internal SortedChunkedMap(Func<KeyComparer<K>, IMutableSeries<K, SortedMap<K, V>>> outerFactory,
    //    Func<int, KeyComparer<K>, SortedMap<K, V>> innerFactory,
    //    KeyComparer<K> comparer,
    //    Opt<IKeyHasher<K>> hasher,
    //    Opt<int> chunkMaxSize)
    //    {
    //        _outerMap = outerFactory(comparer);
    //        _innerFactory = innerFactory;
    //        _comparer = comparer;
    //        _hasher = hasher.IsPresent ? hasher.Present : default;
    //        _chunkUpperLimit = hasher.IsPresent
    //            ? 0
    //            : chunkMaxSize.IsPresent & chunkMaxSize.Present > 0
    //                ? chunkMaxSize.Present
    //                : 0;
    //        _isSynchronized = true;
    //        _version = _outerMap.Version;
    //        _nextVersion = _version;
    //    }

    //    #region Private members

    //    internal KeyValuePair<K, SortedMap<K, V>> ExistingHashBucket(K key)
    //    {
    //        if (_chunkUpperLimit == 0)
    //        {
    //            return new KeyValuePair<K, SortedMap<K, V>>(_hasher.Hash(key), null);
    //        }
    //        _outerMap.TryFindAt(key, Lookup.LE, out var h);
    //        return h;
    //    }

    //    #endregion

    //    public override KeyComparer<K> Comparer
    //    {
    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //        get { return _comparer; }
    //    }

    //    public override Opt<KeyValuePair<K, V>> First => TODO_IMPLEMENT_ME;

    //    public override Opt<KeyValuePair<K, V>> Last => TODO_IMPLEMENT_ME;

    //    public override bool TryGetValue(K key, out V value)
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public override bool TryFindAt(K key, Lookup direction, out KeyValuePair<K, V> kvp)
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public override IEnumerable<K> Keys => TODO_IMPLEMENT_ME;

    //    public override IEnumerable<V> Values => TODO_IMPLEMENT_ME;

    //    public override bool IsIndexed => TODO_IMPLEMENT_ME;

    //    public override bool IsCompleted => TODO_IMPLEMENT_ME;

    //    internal override Collections.SortedChunkedMapCursor<K, V> GetContainerCursor()
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public virtual long Count
    //    {
    //        get
    //        {
    //            BeforeWrite();
    //            var result = _outerMap.Select(kvp => kvp.Value.Count).Sum();
    //            AfterWrite(false);
    //            return result;
    //        }
    //    }

    //    public long Version => TODO_IMPLEMENT_ME;

    //    public bool IsAppendOnly => TODO_IMPLEMENT_ME;

    //    public Task<bool> Set(K key, V value)
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public Task<bool> TryAdd(K key, V value)
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public Task<bool> TryAddLast(K key, V value)
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public Task<bool> TryAddFirst(K key, V value)
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public ValueTask<Opt<V>> TryRemove(K key)
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public ValueTask<Opt<KeyValuePair<K, V>>> TryRemoveFirst()
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public ValueTask<Opt<KeyValuePair<K, V>>> TryRemoveLast()
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public ValueTask<Opt<KeyValuePair<K, V>>> TryRemoveMany(K key, Lookup direction)
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public ValueTask<Opt<KeyValuePair<K, V>>> TryRemoveMany(K key, V value, Lookup direction)
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public ValueTask<long> TryAppend(ISeries<K, V> appendMap, AppendOption option = AppendOption.RejectOnOverlap)
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public Task Complete()
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public void Dispose()
    //    {
    //        TODO_IMPLEMENT_ME();
    //    }

    //    public Task Flush()
    //    {
    //        return TODO_IMPLEMENT_ME;
    //    }

    //    public string Id => _id;

    //    public struct SortedChunkedMapCursor<TKey, TValue> : ISpecializedCursor<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>>
    //    {
    //        public bool MoveNext()
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public void Reset()
    //        {
    //            TODO_IMPLEMENT_ME();
    //        }

    //        public KeyValuePair<TKey, TValue> Current => TODO_IMPLEMENT_ME;

    //        object IEnumerator.Current => Current;

    //        public void Dispose()
    //        {
    //            TODO_IMPLEMENT_ME();
    //        }

    //        public Task DisposeAsync()
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public Task<bool> MoveNextAsync(CancellationToken cancellationToken)
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public Task<bool> MoveNextAsync()
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public CursorState State => TODO_IMPLEMENT_ME;

    //        public KeyComparer<TKey> Comparer => TODO_IMPLEMENT_ME;

    //        public bool MoveAt(TKey key, Lookup direction)
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public bool MoveFirst()
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public bool MoveLast()
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public long MoveNext(long stride, bool allowPartial)
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public bool MovePrevious()
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public long MovePrevious(long stride, bool allowPartial)
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public TKey CurrentKey => TODO_IMPLEMENT_ME;

    //        public TValue CurrentValue => TODO_IMPLEMENT_ME;

    //        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public ISeries<TKey, TValue> CurrentBatch => TODO_IMPLEMENT_ME;

    //        public ISeries<TKey, TValue> Source => TODO_IMPLEMENT_ME;

    //        public bool IsContinuous => TODO_IMPLEMENT_ME;

    //        public SortedChunkedMapCursor<TKey, TValue> Initialize()
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        SortedChunkedMapCursor<TKey, TValue> ISpecializedCursor<TKey, TValue, SortedChunkedMapCursor<TKey, TValue>>.Clone()
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }

    //        public bool TryGetValue(TKey key, out TValue value)
    //        {
    //            return TODO_IMPLEMENT_ME;
    //        }
    //    }
    //}
}
