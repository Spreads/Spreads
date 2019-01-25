// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Algorithms;
using Spreads.Collections.Internal;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads.Collections
{
    /// <summary>
    /// Base class for data containers implementations.
    /// </summary>
    [CannotApplyEqualityOperator]
    public class BaseSeries // TODO rename to BaseContainer
    {
        internal DataChunkStorage DataChunk;

        #region Attributes

        private static readonly ConditionalWeakTable<BaseSeries, Dictionary<string, object>> Attributes =
            new ConditionalWeakTable<BaseSeries, Dictionary<string, object>>();

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
    }

    /// <inheritdoc />
    public class BaseContainer<TKey> : BaseSeries
    {
        // for tests only, it should have been abstract otherwise
        internal BaseContainer()
        {
        }

        internal KeyComparer<TKey> Comparer = default;
        internal DataChunkSource<TKey> DataSource;

        // TODO we are forking existing series implementation from here
        // All containers inherit this.
        // Matrix has Int64 key.
        // Matrix could be sparse, it is no more than a series of rows.
        // All series functionality should be moved to new Series

        internal bool IsSingleChunk
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => DataSource == null;
        }

        /// <summary>
        /// Returns <see cref="DataChunkStorage"/> that contains <paramref name="index"></paramref> and local index within the chunk as <paramref name="chunkIndex"></paramref>.
        /// </summary>
        /// <param name="index">Index to get element at.</param>
        /// <param name="chunk"><see cref="DataChunkStorage"/> that contains <paramref name="index"></paramref> or null if not found.</param>
        /// <param name="chunkIndex">Local index within the chunk. -1 if requested index is not range.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetChunkAt(long index, out DataChunkStorage chunk, out int chunkIndex)
        {
            // Take reference, do not work directly. Reference assignment is atomic in .NET
            chunk = null;
            chunkIndex = -1;
            var result = false;

            if (IsSingleChunk)
            {
                Debug.Assert(DataChunk != null, "Single-chunk series must always have non-null DataChunk");

                if (index < DataChunk.RowLength)
                {
                    chunk = DataChunk;
                    chunkIndex = (int)index;
                    result = true;
                }
            }
            else
            {
                // TODO check DataChunk range, probably we are looking in the same chunk
                // But for this search it is possible only for immutable or append-only
                // because we need to track first global index. For such cases maybe
                // we should just guarantee that DataSource.ConstantChunkLength > 0 and is pow2.

                var constantChunkLength = DataSource.ConstantChunkLength;
                if (constantChunkLength > 0)
                {
                    // TODO review long division. constantChunkLength should be a poser of 2
                    var sourceIndex = index / constantChunkLength;
                    if (DataSource.TryGetAt(sourceIndex, out var kvp))
                    {
                        chunk = kvp.Value;
                        chunkIndex = (int)(index - sourceIndex * constantChunkLength);
                        result = true;
                    }
                }
                else
                {
                    result = TryGetChunkAtSlow(index, out chunk, out chunkIndex);
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGetChunkAtSlow(long index, out DataChunkStorage chunk, out int chunkIndex)
        {
            // TODO slow path as non-inlined method
            throw new NotImplementedException();
            //foreach (var kvp in DataSource)
            //{
            //    // if (kvp.Value.RowLength)
            //}
        }

        /// <summary>
        /// When found, updates key by the found key if it is different, returns chunk and index whithin the chunk where the data resides.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="lookup"></param>
        /// <param name="chunk"></param>
        /// <param name="chunkIndex"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryFindChunkAt(ref TKey key, Lookup lookup, out DataChunkStorage chunk, out int chunkIndex)
        {
            // This is non-obvious part:
            // For most searches we could find the right chunk
            // by searching with LE on the source:
            // o - EQ match, x - non-existing target key
            // | - start of chunk, there is no space between start of chunk and it's first element (TODO review, had issues with that)
            // * for LE
            //      - [...] |[o...] |[<..]
            //      - [...] |[.o..] |[<..]
            //      - [...] |[ox..] |[<..]
            //      - [...] |[...o]x|[<..]
            // * for EQ
            //      - [...] |[x...] |[<..] - not possible, because with LE this would be [..o]x|[....] | [<..] since the first key must be LE, if it is not !EQ we find previous chunk
            //      - [...] |[o...] |[<..]
            //      - [...] |[..o.] |[<..]
            //      - [...] |[..x.] |[<..]
            //      - [...] |[....]x|[<..]
            // * for GE
            //      - [...] |[o...] |[<..]
            //      - [...] |[xo..] |[<..]
            //      - [...] |[..o.] |[<..]
            //      - [...] |[..xo] |[<..]
            //      - [...] |[...x] |[o..] SPECIAL CASE, SLE+SS do not find key but it is in the next chunk if it exist
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
            // Do SortedSearch on the chunk
            // If not found check if complement is after the end and

            // Notes: always optimize for LE search, it should have least branches and could try speculatively
            // even if we could detect special cases in advance. Only when we cannot find check if there was a
            // special case and process it in a slow path as non-inlined method.

            chunk = DataChunk;
            var retryOnGt = false;

            if (IsSingleChunk)
            {
                goto SINGLE_CHUNK;
            }

            // for single chunk this should exist, for sourced chunks this value is updated by a last search
            // Take reference, do not work directly. Reference assignment is atomic in .NET

            if (chunk != null) // cached chunk
            {
                // Check edge cases if key is outside the chunk and we may need to retrieve
                // the right one from storage. We do not know anything about other chunks, so we must
                // be strictly in range so that all searches will work.
                var rowLength = chunk.RowLength;
                if (rowLength <= 1) // with 1 there are some edge cases that penalize normal path, so make just one comparison
                {
                    chunk = null;
                }
                else
                {
                    var firstC = Comparer.Compare(key, chunk.RowIndex.DangerousGet<TKey>(0));

                    if (firstC < 0 // not in this chunk even if looking LT
                        || lookup == Lookup.LT // first value is >= key so LT won't find the value in this chunk
                                               // Because rowLength >= 2 we do not need to check for firstC == 0 && GT
                        )
                    {
                        chunk = null;
                    }
                    else
                    {
                        var lastC = Comparer.Compare(key, chunk.RowIndex.DangerousGet<TKey>(rowLength - 1));

                        if (lastC > 0
                            || lookup == Lookup.GT
                            )
                        {
                            chunk = null;
                        }
                    }
                }
            }

            // Try to call DataSource.TryFindAt and VectorSearch.SortedLookup from one place,
            // set local variables that we could reuse later and go through the same code
            var sourceLookup = lookup == Lookup.LT ? Lookup.LT : Lookup.LE;
            var sourceSearchKey = key;
            retryOnGt = true;

        RETRY_NEXT_CHUNK:

            // if chunk is null here we have rejected it and need to get it from source
            // or it is the first search and cached chunk was not set yet
            if (chunk == null)
            {
                // TODO review: next line will eventually call this method for in-memory case, so how inlining possible?
                // compiler should do magic to convert all this to a loop at JIT stage, so likely it does not
                // and the question is where to break the chain. We probably could afford non-inlined
                // DataSource.TryFindAt if this method will be faster for single-chunk and cache-hit cases.
                if (!DataSource.TryFindAt(sourceSearchKey, sourceLookup, out var kvp))
                {
                    Debug.Assert(chunk == null);
                    chunkIndex = -1;
                    return false;
                }

                chunk = kvp.Value;

                if (AdditionalCorrectnessChecks.Enabled)
                {
                    // knowledge of key doesn't help here for speed, but ensure correctness
                    if (chunk.RowLength <= 0 || Comparer.Compare(kvp.Key, chunk.RowIndex.DangerousGet<TKey>(0)) != 0)
                    {
                        chunk = null;
                        ThrowBadChunkFromSource();
                    }
                }
            }

        SINGLE_CHUNK:
            Debug.Assert(chunk != null);

            // Here we use internal knowledge that for series RowIndex in contiguous vec
            // TODO(?) do check if VS is pure, allow strides > 1 or just create Nth cursor?

            Debug.Assert(chunk.RowIndex._stride == 1);

            // TODO optimize this search via non-generic IVector with generic getter
            // ReSharper disable once PossibleNullReferenceException
            // ref var vecStart = ref chunk.RowIndex._vec.DangerousGetRef<TKey>(0);
            chunkIndex = VectorSearch.SortedLookup(ref chunk.RowIndex._vec.DangerousGetRef<TKey>(0), chunk.RowLength, ref key, lookup, Comparer);

            if (chunkIndex >= 0)
            {
                // Set cached value, probably next search will be close enough
                // Getting chunk from source is expensive especially with persistence.
                if (!retryOnGt)
                {
                    DataChunk = chunk;
                }

                return true;
            }

            // Check for SPECIAL CASE from the comment above
            if (retryOnGt &&
                (~chunkIndex) == chunk.RowLength
                && ((int)lookup & (int)Lookup.GT) != 0)
            {
                retryOnGt = false;
                var nextchunk = chunk.TryGetNextChunk();
                if (nextchunk == null)
                {
                    sourceLookup = Lookup.GT;
                    sourceSearchKey = chunk.RowIndex.DangerousGet<TKey>(0);
                    chunk = null; // if after RETRY_NEXT_CHUNK depends on it
                    goto RETRY_NEXT_CHUNK;
                }
            }
            
            return false;
        }

        private static void ThrowBadChunkFromSource()
        {
            ThrowHelper.ThrowInvalidOperationException(
                "BaseContainer.DataSource.TryFindAt returned an empty chunk or key that doesn't match the first row index value");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool ChunkIsValidForLookup(TKey key, Lookup lookup, DataChunkStorage chunk, out int foundChunkIndexAtEdge)
        {
            foundChunkIndexAtEdge = -1;

            if (chunk == null || chunk.RowLength == 0)
            {
                return false;
            }

            // TODO here we could detect if we found a value without SortedSearch

            var firstC = Comparer.Compare(key, chunk.RowIndex.DangerousGet<TKey>(0));

            // not in this chunk even if looking LT
            if (firstC < 0)
            {
                return false;
            }

            // first value is >= key so LT won't find the value in this chunk
            // we must search by LT in the source for this
            if (lookup == Lookup.LT)
            {
                return false;
            }

            var lastC = Comparer.Compare(key, chunk.RowIndex.DangerousGet<TKey>(chunk.RowLength - 1));

            // key is larger then the last, could work only for LE/LT
            if (lastC > 0 && (((int)lookup & (int)Lookup.LT) == 0))
            {
                return false;
            }

            return true;
        }
    }
}