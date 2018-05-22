using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Spreads.Collections
{
    // TODO Clear extension method

    public class SortedChunkedMap<K, V> : SortedChunkedMapBase<K, V>, IPersistentSeries<K, V>
    {
        internal SortedChunkedMap(Func<KeyComparer<K>, IMutableSeries<K, SortedMap<K, V>>> outerFactory,
            Func<int, KeyComparer<K>, SortedMap<K, V>> innerFactory,
            KeyComparer<K> comparer,
            Opt<IKeyHasher<K>> hasher,
            Opt<int> chunkMaxSize) : base(outerFactory, innerFactory, comparer, hasher, chunkMaxSize)
        { }

        private static Func<int, KeyComparer<K>, SortedMap<K, V>> smInnerFactory = (capacity, keyComparer) =>
        {
            var sm = new SortedMap<K, V>(capacity, keyComparer)
            {
                _isSynchronized = false
            };
            return sm;
        };

        public SortedChunkedMap() : base((KeyComparer<K> c) =>
            new SortedMap<K, SortedMap<K, V>>(c), smInnerFactory, KeyComparer<K>.Default, Opt<IKeyHasher<K>>.Missing, Opt<int>.Missing)
        {
            Func<KeyComparer<K>, IMutableSeries<K, SortedMap<K, V>>> factory = (KeyComparer<K> c) =>
                new SortedMap<K, SortedMap<K, V>>(c);
            //  new SortedChunkedMapBase<_,_>(factory, (fun (capacity, comparer) -> let sm = new SortedMap<'K,'V>(capacity, comparer) in sm._isSynchronized <- false;sm), comparer, None, None)
        }

        //[<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
        //member internal this.SetWithHasher(key: 'K, value: 'V, overwrite: bool) : unit =
        // TODO
        //let hash = hasher.Hash(key)
        //let c = comparer.Compare(hash, prevHash)
        //let mutable prevBucket' = Unchecked.defaultof<_>
        //let bucketIsSet = this.PrevBucketIsSet(&prevBucket')
        //if c = 0 && bucketIsSet then
        //  Debug.Assert(prevBucket'._version = this._version)
        //  prevBucket'.Set(key, value)
        //  this.NotifyUpdate(true)
        //else
        //  // bucket switch
        //  if bucketIsSet then this.FlushUnchecked()
        //  let isNew, bucket =
        //    let mutable bucketKvp = Unchecked.defaultof<_>
        //    let ok = outerMap.TryFindAt(hash, Lookup.EQ, &bucketKvp)
        //    if ok then
        //      false, bucketKvp.Value
        //    else
        //      let newSm = innerFactory(0, comparer)
        //      true, newSm
        //  bucket._version <- this._version // NB old bucket could have stale version, update for both cases
        //  bucket._nextVersion <- this._version
        //  bucket.Set(key, value)
        //  if isNew then
        //    outerMap.Set(hash, bucket)
        //    Debug.Assert(bucket._version = outerMap.Version, "Outer setter must update its version")
        //  this.NotifyUpdate(true)
        //  prevHash <- hash
        //  prevBucket.SetTarget(bucket)
        // ()

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal async Task<bool> SetOrAddUnchecked(K key, V value, bool overwrite)
        {
            if (chunkUpperLimit == 0)
            {
                ThrowHelper.ThrowNotImplementedException();
                return await TaskUtil.FalseTask;
            }
            else
            {
                if (!(prevWBucket is null) 
                    && prevWBucket.CompareToLast(key) <= 0  
                    && comparer.Compare(key, prevWHash) >= 0 )
                {
                    // we are inside previous bucket, setter has no choice but to set to this
                    // bucket regardless of its size
                    var res = prevWBucket.SetOrAdd(key, value, overwrite).Result;
                    NotifyUpdate(true);
                    return res;
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
                            await FlushUnchecked();
                            // if add fails later, it is ok to update the stale version to this._version (TODO WTF this comment says?)
                            kvp.Value._version = _version;
                            kvp.Value._nextVersion = _version;
                            prevWHash = kvp.Key;
                            prevWBucket = kvp.Value;
                        }

                        Debug.Assert(kvp.Value._version == _version);
                        var res = kvp.Value.SetOrAdd(key, value, overwrite).Result;
                        NotifyUpdate(true);
                        return res;
                    }
                    else
                    {
                        if (!(prevWBucket is null))
                        {
                            await FlushUnchecked();
                        }
                        // create a new bucket at key
                        var newSm = innerFactory.Invoke(0, comparer);
                        newSm._version = _version;
                        newSm._nextVersion = _version;
                        // Set and Add are the same here, we use new SM
                        newSm.SetOrAdd(key, value, overwrite); // we know that SM is syncronous

                        var outerSet =
                            await outerMap.Set(key,
                                newSm); // outerMap.Version is incremented here, set non-empty bucket only

                        prevWHash = key;
                        prevWBucket = newSm;
                        NotifyUpdate(true);
                        return outerSet;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete]
        internal async Task<bool> SetOrAdd(K key, V value, bool overwrite)
        {
            BeforeWrite();
            var result = await SetOrAddUnchecked(key, value, overwrite);
            AfterWrite(result);
            return result;
        }

        public bool IsAppendOnly => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<bool> Set(K key, V value)
        {
            BeforeWrite();
            var result = await SetOrAddUnchecked(key, value, true);
            AfterWrite(result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<bool> TryAdd(K key, V value)
        {
            BeforeWrite();
            var result = await SetOrAddUnchecked(key, value, false);
            AfterWrite(result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("This method is mainly for ctor pattern with IEnumerable and tests")]
        public void Add(K key, V value)
        {
            BeforeWrite();
            var result = SetOrAddUnchecked(key, value, false).Result;
            AfterWrite(result);
            if (!result)
            {
                ThrowHelper.ThrowArgumentException("Key already exists");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<bool> TryAddFirst(K key, V value)
        {
            BeforeWrite();
            var o = FirstUnchecked;
            var c = o.IsMissing
                ? -1
                : comparer.Compare(key, o.Present.Key);
            var added = c < 0
                ? await SetOrAddUnchecked(key, value, false)
                : false;

            AfterWrite(added);
            return added;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<bool> TryAddLast(K key, V value)
        {
            BeforeWrite();
            var o = LastUnchecked;
            var c = o.IsMissing
                    ? 1
                    : comparer.Compare(key, o.Present.Key);
            var added = c > 0
                ? await SetOrAddUnchecked(key, value, false)
                : false;

            AfterWrite(added);
            return added;
        }

        public ValueTask<Opt<V>> TryRemove(K key)
        {
            ThrowHelper.ThrowNotImplementedException();
            return default;
        }

        public ValueTask<Opt<KeyValuePair<K, V>>> TryRemoveFirst()
        {
            ThrowHelper.ThrowNotImplementedException();
            return default;
        }

        public ValueTask<Opt<KeyValuePair<K, V>>> TryRemoveLast()
        {
            ThrowHelper.ThrowNotImplementedException();
            return default;
        }

        public ValueTask<Opt<KeyValuePair<K, V>>> TryRemoveMany(K key, Lookup direction)
        {
            ThrowHelper.ThrowNotImplementedException();
            return default;
        }

        public Task<bool> TryRemoveMany(K key, V updatedAtKey, Lookup direction)
        {
            ThrowHelper.ThrowNotImplementedException();
            return default;
        }

        public ValueTask<long> TryAppend(ISeries<K, V> appendMap, AppendOption option = AppendOption.RejectOnOverlap)
        {
            ThrowHelper.ThrowNotImplementedException();
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task FlushUnchecked()
        {
            prevRBucket = null;
            prevRHash = default;
            if (!(prevWBucket is null) && prevWBucket._version != outerMap.Version)
            {
                // ensure the version of current bucket is saved in outer
                Debug.Assert(prevWBucket._version == _version,
                    "TODO review/test, this must be true? RemoveMany doesn't use prev bucket, review logic there");

                prevWBucket._version = _version;
                prevWBucket._nextVersion = this._version;
                var outerSet = await outerMap.Set(prevWHash, prevWBucket);
                if (outerMap is IPersistentObject x)
                {
                    await x.Flush();
                }
            }
            else
            {
                // nothing to flush
                Debug.Assert(outerMap.Version == this._version);
            }
            prevWBucket = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task Flush()
        {
            BeforeWrite();
            await FlushUnchecked();
            AfterWrite(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task DisposeAsync(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
            return Flush();
        }

        public void Dispose()
        {
            DisposeAsync(true);
        }

        ~SortedChunkedMap()
        {
            DisposeAsync(false);
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
