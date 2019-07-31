// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Collections.Internal;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Spreads.Collections;

namespace Spreads
{
    // TODO Interfaces to match hierarchy

    // TODO will conflict with Data.Series when using static.
    // Prefer Data.XXX for discoverability, write good xml docs there

    //public static class Series
    //{
    //    public static void Test()
    //    {
    //    }
    //}

    ///////////////////////////////////////////////////////////////////////////////

    public partial class Series<TKey, TValue> : BaseContainer<TKey>, ISeriesNew, ISpecializedSeries<TKey, TValue, SCursor<TKey, TValue>>, ISeriesNew<TKey, TValue>
    {

        // Series could be read-only, append-only and mutable.
        // There should be no public ctor that accepts data, only static Series.X methods that take a single lock for multiple ops.
        // We could later add Series.OfArrays() method that takes ownership of the arrays, but that is not a typical use case (Series are rather primitive objects similar to List<T>)

        internal Series(Mutability mutability = Mutability.Mutable, KeySorting keySorting = KeySorting.Strong, uint capacity = 0)
        {
            if (keySorting == KeySorting.Weak)
            {
                throw new NotImplementedException();
            }
            _flags = new Flags(ContainerLayout.Series, keySorting, mutability);
        }

        [Obsolete("TODO remove usage from tests")]
        internal Series(TKey[] keys, TValue[] values)
        {
            if (keys.Length != values.Length)
            {
                throw new ArgumentException("Different keys and values length");
            }
            var ks = KeySorting.Strong;
            if (keys.Length > 1)
            {
                var cmp = KeyComparer<TKey>.Default;
                for (int i = 1; i < keys.Length; i++)
                {
                    var c = cmp.Compare(keys[i], keys[i - 1]);
                    if (c == 0)
                    {
                        ks = KeySorting.Weak;
                    }
                    else if (c < 0)
                    {
                        ks = KeySorting.NotEnforced;
                        break;
                    }
                }
            }
            _flags = new Flags((byte)((byte)Mutability.ReadOnly | (byte)ks));

            var keyMemory = ArrayMemory<TKey>.Create(keys, externallyOwned: true);
            var keyVs = VectorStorage.Create(keyMemory, 0, keyMemory.Length);

            var valMemory = ArrayMemory<TValue>.Create(values, externallyOwned: true);
            var valVs = VectorStorage.Create(valMemory, 0, valMemory.Length);

            var block = DataBlock.SeriesCreate(rowIndex: keyVs, values: valVs, rowLength: keys.Length);

            Data = block;
        }

        public KeySorting KeySorting
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _flags.KeySorting;
        }

        public Mutability Mutability
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _flags.Mutability;
        }

        // TODO old api to remove
        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _flags.IsImmutable;
        }

        // previously only strong sorting was supported
        public bool IsIndexed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !_flags.IsStronglySorted;
        }

        public KeyComparer<TKey> Comparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _comparer;
        }

        public Opt<KeyValuePair<TKey, TValue>> First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO Synchronize. Append-only may skip this, but share impl
                DataBlock block;
                if (DataSource == null)
                {
                    block = DataBlock;
                }
                else
                {
                    var opt = DataSource.First;
                    if (AdditionalCorrectnessChecks.Enabled)
                    {
                        block = opt.IsPresent ? opt.Present.Value : null;
                    }
                    else
                    {
                        // Opt.Missing is default and KVP is struct with default values.
                        block = opt.Present.Value;
                    }
                }

                if (block != null && block.RowLength > 0)
                {
                    var k = DataBlock.RowKeys.DangerousGetRef<TKey>(0);
                    var v = DataBlock.Values.DangerousGetRef<TValue>(0);
                    return Opt.Present(new KeyValuePair<TKey, TValue>(k, v));
                }
                return Opt<KeyValuePair<TKey, TValue>>.Missing;
            }
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                DataBlock block;
                if (DataSource == null)
                {
                    block = DataBlock;
                }
                else
                {
                    block = DataSource.LastValueOrDefault;
                }

                if (block != null && block.RowLength > 0)
                {
                    return false;
                }
                return true;
            }
        }

        // TODO internal & remove from interfaces or synced
        public TValue LastValueOrDefault
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                DataBlock block;
                if (DataSource == null)
                {
                    block = DataBlock;
                }
                else
                {
                    block = DataSource.LastValueOrDefault;
                }

                int idx;
                if (block != null && (idx = block.RowLength - 1) >= 0)
                {
                    return block.Values.DangerousGetRef<TValue>(idx);
                }
                return default;
            }
        }

        public Opt<KeyValuePair<TKey, TValue>> Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO Synchronize.

                DataBlock block;
                if (DataSource == null)
                {
                    block = DataBlock;
                }
                else
                {
                    var opt = DataSource.Last;
                    if (AdditionalCorrectnessChecks.Enabled)
                    {
                        block = opt.IsPresent ? opt.Present.Value : null;
                    }
                    else
                    {
                        // Opt.Missing is default and KVP is struct with default values.
                        block = opt.Present.Value;
                    }
                }

                int idx;
                if (block != null && (idx = block.RowLength - 1) >= 0)
                {
                    var k = block.RowKeys.DangerousGetRef<TKey>(idx);
                    var v = block.Values.DangerousGetRef<TValue>(idx);
                    return new Opt<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(k, v));
                }
                return Opt<KeyValuePair<TKey, TValue>>.Missing;
            }
        }

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }
                // it's tempting to add Fill Opt<TValue> field and treat Series as continuous,
                // but FillCursor is practically zero cost, don't mess with additional if branches here
                // Fill cursor is visible from signature during design time - this is actually good!
                ThrowKeyNotFound(key);
                return default;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowKeyNotFound(TKey key)
        {
            ThrowHelper.ThrowKeyNotFoundException($"Key {key} not found in series");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null)
            {
                value = default;
                return false;
            }

            // this method is already read synced
            return TryGetSeriesValue(key, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAt(long index, out KeyValuePair<TKey, TValue> kvp)
        {
            bool result;
            var sw = new SpinWait();

        SYNC:
            var version = Version;
            {
                if (TryGetBlockAt(index, out var chunk, out var chunkIndex))
                {
                    var k = chunk.RowKeys.DangerousGet<TKey>(chunkIndex);
                    var v = chunk.Values.DangerousGet<TValue>(chunkIndex);
                    kvp = new KeyValuePair<TKey, TValue>(k, v);
                    result = true;
                }
                else
                {
                    kvp = default;
                    result = false;
                }
            }
            if (NextVersion != version)
            {
                sw.SpinOnce();
                goto SYNC;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp)
        {
            bool result;
            var sw = new SpinWait();

        SYNC:
            var version = Version;
            {
                if (TryFindBlockAt(ref key, direction, out var block, out var blockIndex))
                {
                    // key is updated if not EQ according to direction
                    var v = block.Values.DangerousGetRef<TValue>(blockIndex);
                    kvp = new KeyValuePair<TKey, TValue>(key, v);
                    result = true;
                }
                else
                {
                    kvp = default;
                    result = false;
                }
            }
            if (NextVersion != version)
            {
                sw.SpinOnce();
                goto SYNC;
            }

            return result;
        }
    }
}
