using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable 618

namespace Spreads.Collections
{
    // TODO Clear() extension method

    public class SortedChunkedMap<TKey, TValue> : SortedChunkedMapBase<TKey, TValue>, IPersistentSeries<TKey, TValue>
    {
        private readonly int _chunkUpperLimit;
        private TKey _prevWHash;
        private SortedMap<TKey, TValue> _prevWBucket;
        private Opt<TKey> _lastKey;

        internal SortedChunkedMap(Func<KeyComparer<TKey>, IMutableSeries<TKey, SortedMap<TKey, TValue>>> outerFactory,
            Func<int, KeyComparer<TKey>, SortedMap<TKey, TValue>> innerFactory,
            KeyComparer<TKey> comparer,
            Opt<int> chunkMaxSize) : base(outerFactory, innerFactory, comparer, chunkMaxSize)
        {
            _chunkUpperLimit = chunkMaxSize.IsPresent && chunkMaxSize.Present > 0 ? chunkMaxSize.Present : Settings.SCMDefaultChunkLength;
            var lastKvp = LastUnchecked;
            _lastKey = lastKvp.IsPresent ? Opt.Present(lastKvp.Present.Key) : Opt<TKey>.Missing;
        }

        private static readonly Func<int, KeyComparer<TKey>, SortedMap<TKey, TValue>> SmInnerFactory = (capacity, keyComparer) =>
        {
            var sm = new SortedMap<TKey, TValue>(capacity, keyComparer)
            {
                _isSynchronized = false
            };
            return sm;
        };

        public SortedChunkedMap() : this(c =>
            new SortedMap<TKey, SortedMap<TKey, TValue>>(c)
            {
                keepOrderVersionDelegate = (prev, next) => prev.orderVersion == next.orderVersion
            },
            SmInnerFactory, KeyComparer<TKey>.Default, Opt<int>.Missing)
        { }

        // TODO we must know if cached bucket is the last one, not this
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private int CompareToLast(TKey key)
        //{
        //    if (_lastKey.IsMissing)
        //    {
        //        var lastKvp = LastUnchecked;
        //        if (lastKvp.IsPresent)
        //        {
        //            _lastKey = Opt.Present(lastKvp.Present.Key);
        //        }
        //    }
        //    if (_lastKey.IsPresent)
        //    {
        //        return comparer.Compare(key, _lastKey.Present);
        //    }
        //    // any key is last when map is empty
        //    return 1;
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal async Task<bool> SetOrAddUnchecked(TKey key, TValue value, bool overwrite)
        {
            // TODO we should keep track of last value and || on the last condition
            if (!(_prevWBucket is null)
                && comparer.Compare(key, _prevWHash) >= 0
                && (_prevWBucket.CompareToLast(key) <= 0)
                )
            {
                // We are inside previous bucket, setter has no choice but to set to this
                // bucket regardless of its size

                // We know that SM is synchronous
                var res = _prevWBucket.SetOrAdd(key, value, overwrite).Result;
                NotifyUpdate(false);
                return res;
            }

            if (outerMap.TryFindAt(key, Lookup.LE, out var kvp)
                // the second condition here is for the case when we add inside existing bucket, overflow above chunkUpperLimit is inevitable without a separate split logic
                && (kvp.Value.size < _chunkUpperLimit || kvp.Value.CompareToLast(key) <= 0)
            )
            {
                if (comparer.Compare(_prevWHash, kvp.Key) != 0)
                {
                    // switched active bucket
                    await FlushUnchecked();
                    // if add fails later, it is ok to update the stale version to this._version
                    kvp.Value._version = _version;
                    kvp.Value._nextVersion = _version;
                    _prevWHash = kvp.Key;
                    _prevWBucket = kvp.Value;
                }

                Debug.Assert(kvp.Value._version == _version);
                var res = await kvp.Value.SetOrAdd(key, value, overwrite);
                NotifyUpdate(false);
                return res;
            }
            else
            {
                if (!(_prevWBucket is null))
                {
                    await FlushUnchecked();
                }
                // create a new bucket at key
                var newSm = innerFactory.Invoke(0, comparer);
                newSm._version = _version;
                newSm._nextVersion = _version;
                // Set and Add are the same here, we use new SM
                var _ = await newSm.SetOrAdd(key, value, overwrite); // we know that SM is syncronous

                var outerSet =
                    await outerMap.Set(key,
                        newSm); // outerMap.Version is incremented here, set non-empty bucket only

                _prevWHash = key;
                _prevWBucket = newSm;
                NotifyUpdate(false);
                return outerSet;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete]
        internal async Task<bool> SetOrAdd(TKey key, TValue value, bool overwrite)
        {
            BeforeWrite();
            var result = await SetOrAddUnchecked(key, value, overwrite);
            AfterWrite(result);
            return result;
        }

        public bool IsAppendOnly => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<bool> Set(TKey key, TValue value)
        {
            BeforeWrite();
            var result = await SetOrAddUnchecked(key, value, true);
            AfterWrite(result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<bool> TryAdd(TKey key, TValue value)
        {
            BeforeWrite();
            var result = await SetOrAddUnchecked(key, value, false);
            AfterWrite(result);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("This method is mainly for ctor pattern with IEnumerable and tests")]
        public void Add(TKey key, TValue value)
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
        public async Task<bool> TryAddFirst(TKey key, TValue value)
        {
            BeforeWrite();
            var o = FirstUnchecked;
            var c = o.IsMissing
                ? -1
                : comparer.Compare(key, o.Present.Key);
            var added = c < 0 && await SetOrAddUnchecked(key, value, false);

            AfterWrite(added);
            return added;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<bool> TryAddLast(TKey key, TValue value)
        {
            BeforeWrite();
            var o = LastUnchecked;
            var c = o.IsMissing
                    ? 1
                    : comparer.Compare(key, o.Present.Key);
            var added = c > 0 && await SetOrAddUnchecked(key, value, false);

            AfterWrite(added);
            return added;
        }

        public Task<bool> TryRemoveMany(TKey key, TValue updatedAtKey, Lookup direction)
        {
            throw new NotSupportedException();
        }

        public ValueTask<long> TryAppend(ISeries<TKey, TValue> appendMap, AppendOption option = AppendOption.RejectOnOverlap)
        {
            ThrowHelper.ThrowNotImplementedException();
            return default;
        }

        public async Task Complete()
        {
            await Flush();
            await outerMap.Complete();
            await DoComplete();
        }

        // NB first/last optimization is possible, but removes are rare in the primary use case
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        private async ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveUnchecked(TKey key)
        {
            outerMap.TryFindAt(key, Lookup.LE, out var hashBucket);

            var hash = hashBucket.Key;
            var c = comparer.Compare(hash, _prevWHash);

            if (c == 0 && !(_prevWBucket is null))
            {
                var res = await _prevWBucket.TryRemove(key); // SM is sync
                if (res.IsPresent)
                {
                    Debug.Assert(_prevWBucket._version == _version + 1L,
                        "Verion of the active bucket must much SCM version");
                    _prevWBucket._version = _version + 1L;
                    _prevWBucket._nextVersion = _version + 1L;
                    // NB no outer set here, it must happen on prev bucket switch
                    if (_prevWBucket.Count == 0)
                    {
                        // but here we must notify outer that version has changed
                        // setting empty bucket will remove it in the outer map
                        // (implementation detail, but this is internal)
                        var _ = await outerMap.Set(_prevWHash, _prevWBucket);
                        _prevWBucket = null;
                        return Opt.Present(new KeyValuePair<TKey, TValue>(key, res.Present));
                    }
                    else
                    {
                        return Opt.Present(new KeyValuePair<TKey, TValue>(key, res.Present));
                    }
                }
                else
                {
                    return Opt<KeyValuePair<TKey, TValue>>.Missing;
                }
            }
            else
            {
                if (!(_prevWBucket is null))
                {
                    // store potentially modified active bucket in the outer, including version
                    await FlushUnchecked();
                }

                KeyValuePair<TKey, SortedMap<TKey, TValue>> innerMapKvp;
                bool ok;
                if (!(hashBucket.Value is null))
                {
                    innerMapKvp = hashBucket;
                    ok = true;
                }
                else
                {
                    ok = outerMap.TryFindAt(hash, Lookup.EQ, out innerMapKvp);
                }

                if (ok)
                {
                    var bucket = innerMapKvp.Value;
                    _prevWHash = hash;
                    _prevWBucket = bucket;
                    var res = await bucket.TryRemove(key);
                    if (res.IsPresent)
                    {
                        bucket._version = _version + 1L;
                        bucket._nextVersion = _version + 1L;
                        // NB empty will be removed, see comment above
                        var _ = await outerMap.Set(_prevWHash, bucket);
                        if (bucket.Count == 0)
                        {
                            _prevWBucket = null;
                        }
                    }

                    return Opt.Present(new KeyValuePair<TKey, TValue>(key, res.Present));
                }
                else
                {
                    return Opt<KeyValuePair<TKey, TValue>>.Missing;
                }
            }
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<Opt<TValue>> TryRemove(TKey key)
        {
            BeforeWrite();
            var result = await TryRemoveUnchecked(key);
            var removed = result.IsPresent;
            AfterWrite(removed);
            if (removed)
            {
                Interlocked.Increment(ref orderVersion);
            }
            return removed ? Opt.Present(result.Present.Value) : Opt<TValue>.Missing;
        }

        public async ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveFirst()
        {
            BeforeWrite();
            var opt = FirstUnchecked;
            var result = Opt<KeyValuePair<TKey, TValue>>.Missing;
            if (opt.IsPresent)
            {
                result = await TryRemoveUnchecked(opt.Present.Key);
            }
            AfterWrite(result.IsPresent);
            return result;
        }

        public async ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveLast()
        {
            BeforeWrite();
            var opt = LastUnchecked;
            var result = Opt<KeyValuePair<TKey, TValue>>.Missing;
            if (opt.IsPresent)
            {
                result = await TryRemoveUnchecked(opt.Present.Key);
            }
            AfterWrite(result.IsPresent);
            return result;
        }

        private async ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveManyUnchecked(TKey key, Lookup direction)
        {
            // TODO review why this access was needed
            var _ = outerMap.Version;
            if (outerMap.Last.IsMissing)
            {
                Debug.Assert(_prevWBucket is null);
                _prevWBucket = null;
                return Opt<KeyValuePair<TKey, TValue>>.Missing;
            }

            switch (direction)
            {
                case Lookup.EQ:
                    return await TryRemoveUnchecked(key);

                case Lookup.LT:
                case Lookup.LE:
                    {
                        await FlushUnchecked(); // ensure current version is set in the outer map from the active chunk
                        if (outerMap.TryFindAt(key, Lookup.LE, out var tmp))
                        {
                            var r1 = await tmp.Value.TryRemoveMany(key, direction);
                            tmp.Value._version = _version + 1L;
                            tmp.Value._nextVersion = _version + 1L;
                            var r2 = await outerMap.TryRemoveMany(key, tmp.Value, direction); // NB order matters
                            if (r1.IsPresent || r2)
                            {
                                Interlocked.Increment(ref orderVersion);
                            }

                            return r1.IsPresent
                                ? r1
                                : r2
                                    ? tmp.Value.Last
                                    : Opt<KeyValuePair<TKey, TValue>>.Missing;
                        }
                        else
                        {
                            return Opt<KeyValuePair<TKey, TValue>>.Missing; // no key below the requested key, notjing to delete
                        }
                    }

                case Lookup.GE:
                case Lookup.GT:
                    {
                        await FlushUnchecked(); // ensure current version is set in the outer map from the active chunk

                        if (outerMap.TryFindAt(key, Lookup.LE, out var tmp)) // NB for deterministic hash LE still works
                        {
                            var chunk = tmp.Value;
                            var r1 = await chunk.TryRemoveMany(key, direction);
                            chunk._version = _version + 1L;
                            chunk._nextVersion = _version + 1L;
                            var r2 = await outerMap.TryRemoveMany(key, chunk, direction);
                            if (r1.IsPresent || r2)
                            {
                                Interlocked.Increment(ref orderVersion);
                            }

                            return r1.IsPresent
                                ? r1
                                : r2
                                    ? chunk.Last
                                    : Opt<KeyValuePair<TKey, TValue>>.Missing;
                        }
                        else
                        {
                            // TODO the next line is not needed, we remove all items in SCM here and use the chunk to set the version
                            var firstChunk = outerMap.First.Present.Value;
                            var r1 = await firstChunk.TryRemoveMany(key,
                                direction); // this will remove all items in the chunk
                            Debug.Assert(firstChunk.Count == 0, "The first chunk must have been completely cleared");
                            firstChunk._version = _version + 1L;
                            firstChunk._nextVersion = _version + 1L;
                            var r2 = await outerMap.TryRemoveMany(key, firstChunk, direction);
                            if (r1.IsPresent || r2)
                            {
                                Interlocked.Increment(ref orderVersion);
                            }

                            return r1.IsPresent
                                ? r1
                                : r2
                                    ? firstChunk.Last
                                    : Opt<KeyValuePair<TKey, TValue>>.Missing;
                        }
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public async ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveMany(TKey key, Lookup direction)
        {
            // TODO(low) for non-EQ this is not atomic now, could add a special method for removed in IMutableChunksSeries
            // then in its impl it could be done in a transaction. However, this nethod is not used frequently
            BeforeWrite();
            var result = await TryRemoveManyUnchecked(key, direction);
            AfterWrite(result.IsPresent);
            return result;
        }

        // TODO after checks, should form changed new chunks and use outer append method with rewrite
        // TODO atomic append with single version increase, now it is a sequence of remove/add mutations
        //[<MethodImplAttribute(MethodImplOptions.AggressiveInlining);RewriteAIL>]
        //member this.Append(appendMap:ISeries<'K,'V>, option:AppendOption) : int =
        //  raise (NotImplementedException())
        //let hasEqOverlap (old:SortedChunkedMapBaseGeneric<_,_>) (append:ISeries<'K,'V>) : bool =
        //  if comparer.Compare(append.First.Key, old.LastUnchecked.Key) > 0 then false
        //  else
        //    let oldC = new SortedChunkedMapBaseGenericCursor<_,_>(old, false) :> ICursor<'K,'V>
        //    let appC = append.GetCursor();
        //    let mutable cont = true
        //    let mutable overlapOk =
        //      oldC.MoveAt(append.First.Key, Lookup.EQ)
        //        && appC.MoveFirst()
        //        && comparer.Compare(oldC.CurrentKey, appC.CurrentKey) = 0
        //        && Unchecked.equals oldC.CurrentValue appC.CurrentValue
        //    while overlapOk && cont do
        //      if oldC.MoveNext() then
        //        overlapOk <-
        //          appC.MoveNext()
        //          && comparer.Compare(oldC.CurrentKey, appC.CurrentKey) = 0
        //          && Unchecked.equals oldC.CurrentValue appC.CurrentValue
        //      else cont <- false
        //    overlapOk
        //if appendMap.IsEmpty then
        //  0
        //else
        //  let mutable entered = false
        //  let mutable finished = false
        //  try
        //    try ()
        //    finally
        //      entered <- this.EnterWriteLock()
        //      if entered then Interlocked.Increment(&this._nextVersion) |> ignore
        //    let result =
        //      match option with
        //      | AppendOption.ThrowOnOverlap ->
        //        if outerMap.IsEmpty || comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
        //          let mutable c = 0
        //          for i in appendMap do
        //            c <- c + 1
        //            this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
        //          c
        //        else
        //          let exn = SpreadsException("values overlap with existing")
        //          raise exn
        //      | AppendOption.DropOldOverlap ->
        //        if outerMap.IsEmpty || comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
        //          let mutable c = 0
        //          for i in appendMap do
        //            c <- c + 1
        //            this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
        //          c
        //        else
        //          let removed = this.RemoveMany(appendMap.First.Key, Lookup.GE)
        //          Trace.Assert(removed)
        //          let mutable c = 0
        //          for i in appendMap do
        //            c <- c + 1
        //            this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
        //          c
        //      | AppendOption.IgnoreEqualOverlap ->
        //        if outerMap.IsEmpty || comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
        //          let mutable c = 0
        //          for i in appendMap do
        //            c <- c + 1
        //            this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
        //          c
        //        else
        //          let isEqOverlap = hasEqOverlap this appendMap
        //          if isEqOverlap then
        //            let appC = appendMap.GetCursor();
        //            if appC.MoveAt(this.LastUnsafe.Key, Lookup.GT) then
        //              this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
        //              let mutable c = 1
        //              while appC.MoveNext() do
        //                this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
        //                c <- c + 1
        //              c
        //            else 0
        //          else
        //            let exn = SpreadsException("overlapping values are not equal")
        //            raise exn
        //      | AppendOption.RequireEqualOverlap ->
        //        if outerMap.IsEmpty then
        //          let mutable c = 0
        //          for i in appendMap do
        //            c <- c + 1
        //            this.AddUnchecked(i.Key, i.Value) // TODO Add last when fixed flushing
        //          c
        //        elif comparer.Compare(appendMap.First.Key, this.LastUnsafe.Key) > 0 then
        //          let exn = SpreadsException("values do not overlap with existing")
        //          raise exn
        //        else
        //          let isEqOverlap = hasEqOverlap this appendMap
        //          if isEqOverlap then
        //            let appC = appendMap.GetCursor();
        //            if appC.MoveAt(this.LastUnsafe.Key, Lookup.GT) then
        //              this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
        //              let mutable c = 1
        //              while appC.MoveNext() do
        //                this.AddUnchecked(appC.CurrentKey, appC.CurrentValue) // TODO Add last when fixed flushing
        //                c <- c + 1
        //              c
        //            else 0
        //          else
        //            let exn = SpreadsException("overlapping values are not equal")
        //            raise exn
        //      | _ -> failwith "Unknown AppendOption"
        //    finished <- true
        //    result
        //  finally
        //    if not finished then Environment.FailFast("SCM.Append must always succeed")
        //    Interlocked.Increment(&this._version) |> ignore
        //    this.FlushUnchecked()
        //    exitWriteLockIf &this.Locker entered
        //    #if DEBUG
        //    Debug.Assert(outerMap.Version = this._version)
        //    if entered && this._version <> this._nextVersion then raise (ApplicationException("this._version <> this._nextVersion"))
        //    #else
        //    if entered && this._version <> this._nextVersion then Environment.FailFast("this._version <> this._nextVersion")
        //    #endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task FlushUnchecked()
        {
            prevRBucket = null;
            prevRHash = default;
            if (!(_prevWBucket is null) && _prevWBucket._version != outerMap.Version)
            {
                // ensure the version of current bucket is saved in outer
                Debug.Assert(_prevWBucket._version == _version,
                    "TODO review/test, this must be true? RemoveMany doesn't use prev bucket, review logic there");

                _prevWBucket._version = _version;
                _prevWBucket._nextVersion = _version;
                var _ = await outerMap.Set(_prevWHash, _prevWBucket);
                if (outerMap is IPersistentObject x)
                {
                    await x.Flush();
                }
            }
            else
            {
                // nothing to flush
                Debug.Assert(outerMap.Version == _version);
            }
            _prevWBucket = null;
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
}

#pragma warning restore 618
