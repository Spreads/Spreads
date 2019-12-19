// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Collections;
using Spreads.Collections.Internal;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads
{
    public partial class Series<TKey, TValue> : BaseContainer<TKey>, ISeries<TKey, TValue, SCursor<TKey, TValue>>
    {
        // There is no ctors for series because they are read-only,
        // only factories in static Series class

        protected Series(Mutability mutability = Mutability.ReadOnly,
            KeySorting keySorting = KeySorting.Strong,
            uint capacity = 0,
            KeyComparer<TKey> comparer = default,
            MovingWindowOptions<TKey>? movingWindowOptions = default)
        {
            if (keySorting != KeySorting.Strong)
            {
                throw new NotImplementedException();
            }
            Flags = new Flags(ContainerLayout.Series, keySorting, mutability);

            // TODO review/test
            var bufferSize = Math.Min(Settings.LARGE_BUFFER_LIMIT / Unsafe.SizeOf<TKey>(), Math.Max(16, (int)capacity));
            BlockSizePow2 = Math.Max((byte)4, checked((byte)(32 - IntUtil.NumberOfLeadingZeros(bufferSize - 1))));

            _comparer = comparer;

            
        }

        internal Series(TKey[] keys, TValue[] values)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (values == null) throw new ArgumentNullException(nameof(values));
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
                        ks = KeySorting.NotSorted;
                        break;
                    }
                }
            }
            Flags = new Flags((byte)((byte)Mutability.ReadOnly | (byte)ks));

            if (keys.Length == 0 && values.Length == 0)
            {
                Debug.Assert(Data == DataBlock.Empty);
                return;
            }

            var keyMemory = ArrayMemory<TKey>.Create(keys, externallyOwned: true);
            var keyVs = VecStorage.Create(keyMemory, 0, keyMemory.Length);

            var valMemory = ArrayMemory<TValue>.Create(values, externallyOwned: true);
            var valVs = VecStorage.Create(valMemory, 0, valMemory.Length);

            var block = DataBlock.SeriesCreate(rowIndex: keyVs, values: valVs, rowLength: keys.Length);

            Data = block;
        }

        #region ISeries properties

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
                if (!IsDataBlock(out var db, out var ds))
                {
                    // Opt.Missing is default and KVP is struct with default values.
                    db = ds.First.Present.Value;
                }

                if (db != null && db.RowCount > 0)
                {
                    var k = db.DangerousRowKeyRef<TKey>(0);
                    var v = db.DangerousValueRef<TValue>(0);
                    return Opt.Present(new KeyValuePair<TKey, TValue>(k, v));
                }
                return Opt<KeyValuePair<TKey, TValue>>.Missing;
            }
        }

        public Opt<KeyValuePair<TKey, TValue>> Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO Synchronize.

                if (!IsDataBlock(out var db, out var ds))
                {
                    // Opt.Missing is default and KVP is struct with default values.
                    db = ds.Last.Present.Value;
                }

                int idx;
                if (db != null && (idx = db.RowCount - 1) >= 0)
                {
                    var k = db.DangerousRowKeyRef<TKey>(idx);
                    var v = db.DangerousValueRef<TValue>(idx);
                    return new Opt<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(k, v));
                }
                return Opt<KeyValuePair<TKey, TValue>>.Missing;
            }
        }

        // TODO internal & remove from interfaces or synced
        public TValue LastValueOrDefault
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!IsDataBlock(out var db, out var ds))
                {
                    // Opt.Missing is default and KVP is struct with default values.
                    db = ds.LastValueOrDefault;
                }

                int idx;
                if (db != null && (idx = db.RowCount - 1) >= 0)
                {
                    return db.DangerousValueRef<TValue>(idx);
                }
                return default!;
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
                ThrowHelper.ThrowKeyNotFoundException($"Key {key} not found in series");
                return default;
            }
        }

        public IEnumerable<TKey> Keys
        {
#if HAS_AGGR_OPT
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
            get
            {
                using (var c = GetCursor())
                {
                    while (c.MoveNext())
                    {
                        yield return c.CurrentKey;
                    }
                }
            }
        }

        public IEnumerable<TValue> Values
        {
#if HAS_AGGR_OPT
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
            get
            {
                using (var c = GetCursor())
                {
                    while (c.MoveNext())
                    {
                        yield return c.CurrentValue;
                    }
                }
            }
        }

        #endregion ISeries properties

        #region ISeries Try... Methods

#if HAS_AGGR_OPT
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif

        public bool TryGetAt(long index, out KeyValuePair<TKey, TValue> kvp)
        {
            bool result;
            var sw = new SpinWait();

        SYNC:
            var version = Version;
            {
                if (TryGetBlockAt(index, out var block, out var blockIndex))
                {
                    var k = block.DangerousRowKeyRef<TKey>(blockIndex);
                    var v = block.DangerousValueRef<TValue>(blockIndex);
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

#if HAS_AGGR_OPT
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif

        public bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp)
        {
            bool result;
            var sw = new SpinWait();

        SYNC:
            var version = Version;
            {
                result = DoTryFindAt(key, direction, out kvp);
            }
            if (NextVersion != version)
            {
                sw.SpinOnce();
                goto SYNC;
            }

            return result;
        }

#if HAS_AGGR_OPT
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif

        public bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue value)
        {
            bool result;
            var sw = new SpinWait();

        SYNC:
            var version = Version;
            {
                result = DoTryGetValue(key, out value);
            }
            if (NextVersion != version)
            {
                sw.SpinOnce();
                goto SYNC;
            }

            return result;
        }

        #endregion ISeries Try... Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal bool DoTryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp)
        {
            if (TryFindBlockAt(ref key, direction, out var block, out var blockIndex))
            {
                // key is updated if not EQ according to direction
                var v = block.DangerousValueRef<TValue>(blockIndex);
                kvp = new KeyValuePair<TKey, TValue>(key, v);
                return true;
            }

            kvp = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal bool DoTryGetValue(TKey key, [NotNullWhen(true)] out TValue value)
        {
            if (TryGetBlock(key, out var block, out var blockIndex))
            {
                value = block.DangerousValueRef<TValue>(blockIndex);
                return true;
            }

            value = default!;
            return false;
        }
    }
}
