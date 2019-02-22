// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Algorithms;
using Spreads.Buffers;
using Spreads.Collections.Internal;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Spreads.Threading;

namespace Spreads.Collections
{
    /// <summary>
    /// Base class for data containers implementations.
    /// </summary>
    [CannotApplyEqualityOperator]
    public class BaseContainer : IAsyncCompleter, IDisposable
    {
        internal BaseContainer()
        { }

        // immutable & not sorted
        internal Flags _flags;

        // We need 20-32 bytes of fixed memory. Could use PinnedArrayMemory for 32-bytes slices
        // 

        // TODO these sync fields need rework: move to in-mem only containers or use TLocker object so that persistent series could have their own locking logic without cost
        // But remember that all series must inherit from Series'2

        internal AtomicCounter _orderVersion;
        internal int _locker;
        internal long _version;
        internal long _nextVersion;

        internal long Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _version);
        }

        internal long NextVersion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _nextVersion);
        }

        #region Synchronization

        /// <summary>
        /// Takes a write lock, increments _nextVersion field and returns the current value of the _version field.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use this ONLY for IMutableSeries operations")]
        internal void BeforeWrite()
        {
            // TODO review (recall) why SCM needed access to this
            // TODO Use AdditionalCorrectnessChecks instead of #if DEBUG
            // Failing to unlock in-memory is FailFast condition
            // But before that move DS unlock logic to Utils
            // If high-priority thread is writing in a hot loop on a machine/container
            // with small number of cores it hypothetically could make other threads
            // wait for a long time, and scheduler could switch to this thread
            // when the higher-priority thread is holding the lock.
            // Need to investigate more if thread scheduler if affected by CERs.
#if DEBUG
            var sw = new Stopwatch();
            sw.Start();
#endif
            var spinwait = new SpinWait();
            // NB try{} finally{ .. code here .. } prevents method inlining, therefore should be used at the caller place, not here
            var doSpin = !_flags.IsImmutable;
            // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
            while (doSpin)
            {
                if (Interlocked.CompareExchange(ref _locker, 1, 0) == 0L)
                {
                    if (IntPtr.Size == 8)
                    {
                        Volatile.Write(ref _nextVersion, _nextVersion + 1L);
                        // see the aeron.net 49 & related coreclr issues
                        _nextVersion = Volatile.Read(ref _nextVersion);
                    }
                    else
                    {
                        Interlocked.Increment(ref _nextVersion);
                    }

                    // do not return from a loop, see CoreClr #9692
                    break;
                }
#if DEBUG
                sw.Stop();
                var elapsed = sw.ElapsedMilliseconds;
                if (elapsed > 1000)
                {
                    TryUnlock();
                }
#endif
                spinwait.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG")]
        internal virtual void TryUnlock()
        {
            ThrowHelper.FailFast("This should never happen. Locks are only in memory and should not take longer than a millisecond.");
        }

        /// <summary>
        /// Release write lock and increment _version field or decrement _nextVersion field if no updates were made.
        /// Call NotifyUpdate if doVersionIncrement is true
        /// </summary>
        /// <param name="doVersionIncrement"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use this ONLY for IMutableSeries operations")]
        internal void AfterWrite(bool doVersionIncrement)
        {
            if (_flags.IsImmutable)
            {
                if (doVersionIncrement)
                {
                    _version++;
                    _nextVersion = _version;
                }
            }
            else if (doVersionIncrement)
            {
                if (IntPtr.Size == 8)
                {
                    Volatile.Write(ref _version, _version + 1L);
                }
                else
                {
                    Interlocked.Increment(ref _version);
                }
                // TODO
                NotifyUpdate(false);
            }
            else
            {
                // set nextVersion back to original version, no changes were made
                if (IntPtr.Size == 8)
                {
                    Volatile.Write(ref _nextVersion, _version);
                }
                else
                {
                    Interlocked.Exchange(ref _nextVersion, _version);
                }
            }

            // release write lock
            if (IntPtr.Size == 8)
            {
                Volatile.Write(ref _locker, 0);
            }
            else
            {
                Interlocked.Exchange(ref _locker, 0);
            }
        }

        #endregion Synchronization

        // Union of ContainerSubscription | ConcurrentHashSet<ContainerSubscription>
        private object _cursors;

        private class ContainerSubscription : IAsyncSubscription
        {
            private readonly BaseContainer _container;

            // TODO instead of weak reference (which is now replaced with strong one because of issues)
            // we could break string reference in another place?
            public readonly StrongReference<IAsyncCompletable> Wr;

            // Public interface exposes only IDisposable, only if subscription is IAsyncSubscription cursor knows what to do
            // Otherwise this number will stay at 1 and NotifyUpdate will send all updates
            private long _requests;

            [Obsolete("Temp solution to keep strong ref while the async issue is not sorted out")]
            // ReSharper disable once NotAccessedField.Local
            private IAsyncCompletable _sr;

            public long Requests
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => Volatile.Read(ref _requests);
            }

            public ContainerSubscription(BaseContainer container, StrongReference<IAsyncCompletable> wr)
            {
                _container = container;
                Wr = wr;
                if (wr.TryGetTarget(out var target))
                {
#pragma warning disable 618
                    _sr = target;
#pragma warning restore 618
                }
            }

            // ReSharper disable once UnusedParameter.Local
            private void Dispose(bool disposing)
            {
                try
                {
                    Volatile.Write(ref _requests, 0);
                    var existing = Interlocked.CompareExchange(ref _container._cursors, null, this);
                    if (existing == this)
                    {
                        return;
                    }
                    if (existing is HashSet<ContainerSubscription> hashSet)
                    {
                        lock (hashSet)
                        {
                            hashSet.Remove(this);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Subscription was GCed");
                        //var message = "Wrong cursors type";
                        //Trace.TraceError(message);
                        //ThrowHelper.FailFast(message);
                    }
                }
                catch (Exception ex)
                {
                    var message = "Error in unsubscribe: " + ex;
                    Trace.TraceError(message);
                    ThrowHelper.FailFast(message);
                    throw;
                }
            }

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                Dispose(true);
            }

            public void RequestNotification(int count)
            {
                Interlocked.Add(ref _requests, count);
            }

            ~ContainerSubscription()
            {
                Console.WriteLine("Container subscription is finalized");
                Dispose(false);
            }
        }

        public IDisposable Subscribe(IAsyncCompletable subscriber)
        {
            var wr = new StrongReference<IAsyncCompletable>(subscriber);
            var subscription = new ContainerSubscription(this, wr);
            try
            {
                while (true)
                {
                    var existing1 = Interlocked.CompareExchange<object>(ref _cursors, subscription, null);
                    if (existing1 == null)
                    {
                        break;
                    }

                    if (existing1 is HashSet<ContainerSubscription> hashSet)
                    {
                        lock (hashSet)
                        {
                            if (hashSet.Contains(subscription))
                            {
                                // NB not failfast, existing are not affected
                                ThrowHelper.ThrowInvalidOperationException("Already subscribed");
                            }
                            hashSet.Add(subscription);
                        }

                        break;
                    }

                    if (!(existing1 is ContainerSubscription existing2))
                    {
                        ThrowHelper.FailFast("Wrong _cursors type.");
                        return default;
                    }
                    var newHashSet = new HashSet<ContainerSubscription>();
                    if (existing2.Wr.TryGetTarget(out _))
                    {
                        newHashSet.Add(existing2);
                    }
                    // ReSharper disable once RedundantIfElseBlock
                    else
                    {
                        // No need for existing2.Dispose(), it will be GCed, save one recursive call -
                        // otherwise Dispose will set _cursors to null vs existing2:
                    }

                    if (newHashSet.Contains(subscription))
                    {
                        // NB not failfast, existing are not affected
                        ThrowHelper.ThrowInvalidOperationException("Already subscribed");
                    }
                    newHashSet.Add(subscription);
                    var existing3 = Interlocked.CompareExchange<object>(ref _cursors, newHashSet, existing2);
                    if (existing3 == existing2)
                    {
                        break;
                    }
                }
                return subscription;
            }
            catch (Exception ex)
            {
                var message = "Error in ContainerSeries.Subscribe: " + ex;
                Trace.TraceError(message);
                ThrowHelper.FailFast(message);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void NotifyUpdate(bool force)
        {
            var cursors = _cursors;
            if (cursors != null)
            {
                DoNotifyUpdate(force);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DoNotifyUpdate(bool force)
        {
            var cursors = _cursors;

            if (cursors is ContainerSubscription sub)
            {
                if ((sub.Requests > 0 || force) && sub.Wr.TryGetTarget(out var tg))
                {
                    // ReSharper disable once InconsistentlySynchronizedField
                    DoNotifyUpdateSingleSync(tg);
                    // SpreadsThreadPool.Default.UnsafeQueueCompletableItem(_doNotifyUpdateSingleSyncCallback, tg, true);
                }
            }
            else if (cursors is HashSet<ContainerSubscription> hashSet)
            {
                lock (hashSet)
                {
                    foreach (var kvp in hashSet)
                    {
                        var sub1 = kvp;
                        if ((sub1.Requests > 0 || force) && sub1.Wr.TryGetTarget(out var tg))
                        {
                            DoNotifyUpdateSingleSync(tg);
                            // SpreadsThreadPool.Default.UnsafeQueueCompletableItem(_doNotifyUpdateSingleSyncCallback, tg, true);
                        }
                    }
                }
            }
            else if (!(cursors is null))
            {
                ThrowHelper.FailFast("Wrong cursors subscriptions type");
            }
            else
            {
                Console.WriteLine("Cursors field is null");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DoNotifyUpdateSingleSync(object obj)
        {
            var cursor = (IAsyncCompletable)obj;
            cursor.TryComplete(false);
        }

        #region Attributes

        private static readonly ConditionalWeakTable<BaseContainer, Dictionary<string, object>> Attributes =
            new ConditionalWeakTable<BaseContainer, Dictionary<string, object>>();

        /// <summary>
        /// Get an attribute that was set using SetAttribute() method.
        /// </summary>
        /// <param name="attributeName">Name of an attribute.</param>
        /// <returns>Return an attribute value or null is the attribute is not found.</returns>
        public object GetAttribute(string attributeName)
        {
            if (Attributes.TryGetValue(this, out Dictionary<string, object> dic) &&
                dic.TryGetValue(attributeName, out object res))
            {
                return res;
            }
            return null;
        }

        /// <summary>
        /// Set any custom attribute to a series. An attribute is available during lifetime of a series and is available via GetAttribute() method.
        /// </summary>
        public void SetAttribute(string attributeName, object attributeValue)
        {
            var dic = Attributes.GetOrCreateValue(this);
            dic[attributeName] = attributeValue;
        }

        #endregion Attributes

        protected virtual void Dispose(bool disposing)
        {
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~BaseContainer()
        {
            // Containers are root objects that own data and usually are relatively long-lived.
            // Most containers use memory from some kind of a memory pool, including native
            // memory, and properly releasing that memory is important to avoid GC and high 
            // peaks of memory usage.

            Trace.TraceWarning("Finalizing BaseContainer. This should not normally happen.");
            try
            {
                Dispose(false);
            }
            catch(Exception ex)
            {
                Trace.TraceError("Exception in BaseContainer finalizer: " + ex.ToString());
#if DEBUG
                // Kill it in debug. Should not finalize but in the end we just ask GC for disposing 
                // it and we do often have native resources. But classes where this is important should
                // have their own finalizers.
                throw;
#endif
            }
        }
    }

    /// <summary>
    /// Base container with row keys of type <typeparamref name="TKey"/>.
    /// </summary>
    public class BaseContainer<TKey> : BaseContainer, IDisposable
    {
        // for tests only, it should have been abstract otherwise
        internal BaseContainer()
        {
        }

        protected internal KeyComparer<TKey> _comparer = default;

        internal DataBlock DataBlock;
        internal DataBlockSource<TKey> DataSource;

        // TODO we are forking existing series implementation from here
        // All containers inherit this.
        // Matrix has Int64 key.
        // Matrix could be sparse, it is no more than a series of rows.
        // All series functionality should be moved to new Series

        internal bool IsSingleBlock
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => DataSource == null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetBlock(TKey key, out DataBlock block, out int blockIndex,
            bool updateDataBlock = false)
        {
            // follows TryFindBlockAt implementation, do not edit this directly
            // * always search source with LE, do not retry, no special case since we are always searching EQ key
            // * replace SortedLookup with SortedSearch

            block = DataBlock;

            if (DataSource != null)
            {
                TryFindBlock_ValidateOrGetBlockFromSource(ref block, key, Lookup.EQ, Lookup.LE);
                if (updateDataBlock)
                {
                    DataBlock = block;
                }
            }

            if (block != null)
            {
                Debug.Assert(block.RowIndex._stride == 1);

                blockIndex = VectorSearch.SortedSearch(ref block.RowIndex.DangerousGetRef<TKey>(0),
                    block.RowLength, key, _comparer);

                if (blockIndex >= 0)
                {
                    return true;
                }
            }
            else
            {
                blockIndex = -1;
            }

            return false;
        }

        /// <summary>
        /// Read synced
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetSeriesValue<TValue>(TKey key, out TValue value)
        {
            var sw = new SpinWait();
            value = default;
        SYNC:
            var found = false;
            var version = Volatile.Read(ref _version);
            {
                var block = DataBlock;

                if (DataSource != null)
                {
                    TryFindBlock_ValidateOrGetBlockFromSource(ref block, key, Lookup.EQ, Lookup.LE);

                    // this is huge when key lookup locality > 0
                    DataBlock = block;
                }

                if (block != null)
                {
                    Debug.Assert(block.RowIndex._stride == 1);

                    var blockIndex = VectorSearch.SortedSearch(ref block.RowIndex._vec.DangerousGetRef<TKey>(0),
                        block.RowLength, key, _comparer);

                    if (blockIndex >= 0)
                    {
                        value = block.Values._vec.DangerousGetRef<TValue>(blockIndex);
                        found = true;
                    }
                }
            }

            if (Volatile.Read(ref _nextVersion) != version)
            {
                sw.SpinOnce();
                goto SYNC;
            }

            return found;
        }

        /// <summary>
        /// Returns <see cref="DataBlock"/> that contains <paramref name="index"></paramref> and local index within the block as <paramref name="blockIndex"></paramref>.
        /// </summary>
        /// <param name="index">Index to get element at.</param>
        /// <param name="block"><see cref="DataBlock"/> that contains <paramref name="index"></paramref> or null if not found.</param>
        /// <param name="blockIndex">Local index within the block. -1 if requested index is not range.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetBlockAt(long index, out DataBlock block, out int blockIndex)
        {
            // Take reference, do not work directly. Reference assignment is atomic in .NET
            block = null;
            blockIndex = -1;
            var result = false;

            if (IsSingleBlock)
            {
                Debug.Assert(DataBlock != null, "Single-block series must always have non-null DataBlock");

                if (index < DataBlock.RowLength)
                {
                    block = DataBlock;
                    blockIndex = (int)index;
                    result = true;
                }
            }
            else
            {
                // TODO check DataBlock range, probably we are looking in the same block
                // But for this search it is possible only for immutable or append-only
                // because we need to track first global index. For such cases maybe
                // we should just guarantee that DataSource.ConstantBlockLength > 0 and is pow2.

                var constantBlockLength = DataSource.ConstantBlockLength;
                if (constantBlockLength > 0)
                {
                    // TODO review long division. constantBlockLength should be a poser of 2
                    var sourceIndex = index / constantBlockLength;
                    if (DataSource.TryGetAt(sourceIndex, out var kvp))
                    {
                        block = kvp.Value;
                        blockIndex = (int)(index - sourceIndex * constantBlockLength);
                        result = true;
                    }
                }
                else
                {
                    result = TryGetBlockAtSlow(index, out block, out blockIndex);
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        // ReSharper disable once UnusedParameter.Local
        private static bool TryGetBlockAtSlow(long index, out DataBlock block, out int blockIndex)
        {
            // TODO slow path as non-inlined method
            throw new NotImplementedException();
            //foreach (var kvp in DataSource)
            //{
            //    // if (kvp.Value.RowLength)
            //}
        }

        /// <summary>
        /// When found, updates key by the found key if it is different, returns block and index whithin the block where the data resides.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="lookup"></param>
        /// <param name="block"></param>
        /// <param name="blockIndex"></param>
        /// <param name="updateDataBlock"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryFindBlockAt(ref TKey key, Lookup lookup, out DataBlock block, out int blockIndex,
            bool updateDataBlock = false)
        {
            // This is non-obvious part:
            // For most searches we could find the right block
            // by searching with LE on the source:
            // o - EQ match, x - non-existing target key
            // | - start of a block, there is no space between start of block and it's first element (TODO review, had issues with that)
            // * for LE
            //      - [...] |[o...] |[<..]
            //      - [...] |[.o..] |[<..]
            //      - [...] |[ox..] |[<..]
            //      - [...] |[...o]x|[<..]
            // * for EQ
            //      - [...] |[x...] |[<..] - not possible, because with LE this would be [..o]x|[....] | [<..] since the first key must be LE, if it is not !EQ we find previous block
            //      - [...] |[o...] |[<..]
            //      - [...] |[..o.] |[<..]
            //      - [...] |[..x.] |[<..]
            //      - [...] |[....]x|[<..]
            // * for GE
            //      - [...] |[o...] |[<..]
            //      - [...] |[xo..] |[<..]
            //      - [...] |[..o.] |[<..]
            //      - [...] |[..xo] |[<..]
            //      - [...] |[...x] |[o..] SPECIAL CASE, SLE+SS do not find key but it is in the next block if it exist
            //      - [...] |[....]x|[o..] SPECIAL CASE
            // * for GT
            //      - [...] |[xo..] |[<..]
            //      - [...] |[.xo.] |[<..]
            //      - [...] |[...x] |[o..] SPECIAL CASE
            //      - [...] |[..xo] |[<..]
            //      - [...] |[...x] |[o..] SPECIAL CASE
            //      - [...] |[....]x|[o..] SPECIAL CASE

            // for LT we need to search by LT

            // * for LT
            //      - [..o] |[x...] |[<..]
            //      - [...] |[ox..] |[<..]
            //      - [...] |[...o]x|[<..]

            // So the algorithm is:
            // Search source by LE or by LE if lookup is LT
            // Do SortedSearch on the block
            // If not found check if complement is after the end and

            // Notes: always optimize for LE search, it should have least branches and could try speculatively
            // even if we could detect special cases in advance. Only when we cannot find check if there was a
            // special case and process it in a slow path as non-inlined method.

            block = DataBlock;
            bool retryOnGt = default;

            if (DataSource != null)
            {
                retryOnGt = true;
                TryFindBlock_ValidateOrGetBlockFromSource(ref block, key, lookup, lookup == Lookup.LT ? Lookup.LT : Lookup.LE);

                // TODO (review) updating cache is not responsibility of this method
                // There could be a situation when we know that a search is irregular
                // Also we return the block from this method so a caller could update itself.
                // Cursors should not update

                // Even if we do not find the key update cache anyway here unconditionally to search result below,
                // do not penalize single-block case with this op (significant)
                // and likely the next search will be around current value anyway
                if (updateDataBlock)
                {
                    DataBlock = block;
                }
            }

        RETRY:

            if (block != null)
            {
                Debug.Assert(block != null);

                // Here we use internal knowledge that for series RowIndex in contiguous vec
                // TODO(?) do check if VS is pure, allow strides > 1 or just create Nth cursor?

                Debug.Assert(block.RowIndex._stride == 1);

                // TODO if _stride > 1 is valid at some point, optimize this search via non-generic IVector with generic getter
                // ReSharper disable once PossibleNullReferenceException
                // var x = (block?.RowIndex).DangerousGetRef<TKey>(0);
                blockIndex = VectorSearch.SortedLookup(ref block.RowIndex.Vec.DangerousGetRef<TKey>(0),
                    block.RowLength, ref key, lookup, _comparer);

                if (blockIndex >= 0)
                {
                    // TODO this is not needed? left from initial?
                    if (updateDataBlock)
                    {
                        DataBlock = block;
                    }

                    return true;
                }

                // Check for SPECIAL CASE from the comment above
                if (retryOnGt &&
                    (~blockIndex) == block.RowLength
                    && ((int)lookup & (int)Lookup.GT) != 0)
                {
                    retryOnGt = false;
                    var nextBlock = block.TryGetNextBlock();
                    if (nextBlock == null)
                    {
                        TryFindBlock_ValidateOrGetBlockFromSource(ref nextBlock,
                            block.RowIndex.Vec.DangerousGetRef<TKey>(0), lookup, Lookup.GT);
                    }

                    if (nextBlock != null)
                    {
                        block = nextBlock;
                        goto RETRY;
                    }
                }
            }
            else
            {
                blockIndex = -1;
            }

            return false;
        }

        // TODO Test multi-block case and this attribute impact. Maybe direct call is OK without inlining
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TryFindBlock_ValidateOrGetBlockFromSource(ref DataBlock block,
            TKey key, Lookup direction, Lookup sourceDirection)
        {
            // for single block this should exist, for sourced blocks this value is updated by a last search
            // Take reference, do not work directly. Reference assignment is atomic in .NET

            if (block != null) // cached block
            {
                // Check edge cases if key is outside the block and we may need to retrieve
                // the right one from storage. We do not know anything about other blocks, so we must
                // be strictly in range so that all searches will work.

                if (block.RowLength <= 1) // with 1 there are some edge cases that penalize normal path, so make just one comparison
                {
                    block = null;
                }
                else
                {
                    var firstC = _comparer.Compare(key, block.RowIndex._vec.DangerousGetRef<TKey>(0));

                    if (firstC < 0 // not in this block even if looking LT
                        || direction == Lookup.LT // first value is >= key so LT won't find the value in this block
                                                  // Because rowLength >= 2 we do not need to check for firstC == 0 && GT
                    )
                    {
                        block = null;
                    }
                    else
                    {
                        var lastC = _comparer.Compare(key, block.RowIndex._vec.DangerousGetRef<TKey>(block.RowLength - 1));

                        if (lastC > 0
                            || direction == Lookup.GT
                        )
                        {
                            block = null;
                        }
                    }
                }
            }
            // if block is null here we have rejected it and need to get it from source
            // or it is the first search and cached block was not set yet
            if (block == null)
            {
                // Lookup sourceDirection = direction == Lookup.LT ? Lookup.LT : Lookup.LE;
                // TODO review: next line will eventually call this method for in-memory case, so how inlining possible?
                // compiler should do magic to convert all this to a loop at JIT stage, so likely it does not
                // and the question is where to break the chain. We probably could afford non-inlined
                // DataSource.TryFindAt if this method will be faster for single-block and cache-hit cases.
                if (!DataSource.TryFindAt(key, sourceDirection, out var kvp))
                {
                    block = null;
                }
                else
                {
                    if (AdditionalCorrectnessChecks.Enabled)
                    {
                        if (kvp.Value.RowLength <= 0 || _comparer.Compare(kvp.Key, kvp.Value.RowIndex.Vec.DangerousGetRef<TKey>(0)) != 0)
                        {
                            ThrowBadBlockFromSource();
                        }
                    }

                    block = kvp.Value;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DataBlock TryFindBlockAt_LookUpSource(TKey sourceKey, Lookup direction)
        {
            // TODO review: next line will eventually call this method for in-memory case, so how inlining possible?
            // compiler should do magic to convert all this to a loop at JIT stage, so likely it does not
            // and the question is where to break the chain. We probably could afford non-inlined
            // DataSource.TryFindAt if this method will be faster for single-block and cache-hit cases.
            if (!DataSource.TryFindAt(sourceKey, direction, out var kvp))
            {
                return null;
            }

            if (AdditionalCorrectnessChecks.Enabled)
            {
                if (kvp.Value.RowLength <= 0 || _comparer.Compare(kvp.Key, kvp.Value.RowIndex.DangerousGet<TKey>(0)) != 0)
                {
                    ThrowBadBlockFromSource();
                }
            }

            return kvp.Value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBadBlockFromSource()
        {
            ThrowHelper.ThrowInvalidOperationException("BaseContainer.DataSource.TryFindAt " +
                    "returned an empty block or key that doesn't match the first row index value");
        }

        public IDisposable Subscribe(IAsyncCompletable subscriber)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            var block = DataBlock;
            DataBlock = null;
            block?.Dispose();

            var ds = DataSource;
            DataSource = null;
            ds?.Dispose();
        }

        //object IDataBlockValueGetter<object>.GetValue(DataBlock block, int rowIndex)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
