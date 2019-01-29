// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Collections.Experimental
{
    public partial class Series<TKey, TValue> // : IDataBlockValueGetter<TValue>
    {
        // TODO review & document
        // Maybe separate ICursor and IAsyncCursor, ISeries return IAsyncCursor, while specialized return TCursor

        IAsyncEnumerator<KeyValuePair<TKey, TValue>> IAsyncEnumerable<KeyValuePair<TKey, TValue>>.GetAsyncEnumerator()
        {
            return new AsyncCursor<TKey, TValue, SCursor<TKey, TValue>>(GetCursor());
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return GetCursor();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        ICursor<TKey, TValue> ISeries<TKey, TValue>.GetCursor()
        {
            return new AsyncCursor<TKey, TValue, SCursor<TKey, TValue>>(GetCursor());
        }

        SCursor<TKey, TValue> ISpecializedSeries<TKey, TValue, SCursor<TKey, TValue>>.GetCursor()
        {
            return GetCursor();
        }

        public SCursor<TKey, TValue> GetCursor()
        {
            return new SCursor<TKey, TValue>(this);
        }

        IAsyncCursor<TKey, TValue> ISeries<TKey, TValue>.GetAsyncCursor()
        {
            return GetAsyncCursor();
        }

        public AsyncCursor<TKey, TValue, SCursor<TKey, TValue>> GetAsyncCursor()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TKey> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public TValue GetValue(DataBlock block, int rowIndex)
        //{
        //    return block.Values.DangerousGetRef<TValue>(rowIndex);
        //}
    }

    /// <summary>
    /// <see cref="Series{TKey,TValue}"/> cursor implementation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SCursor<TKey, TValue> : ICursorNew<TKey, TValue>, ISpecializedCursor<TKey, TValue, SCursor<TKey, TValue>>
    {
        // ReSharper disable once FieldCanBeMadeReadOnly.Local mutable struct
        private BlockCursor<TKey, TValue, Series<TKey, TValue>> _cursor;

        public SCursor(Series<TKey, TValue> source)
        {
            _cursor = new BlockCursor<TKey, TValue, Series<TKey, TValue>>(source);
        }

        public CursorState State
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _cursor.State;
        }

        public KeyComparer<TKey> Comparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _cursor.Comparer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveFirst()
        {
            return _cursor.MoveFirst();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveLast()
        {
            return _cursor.MoveLast();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Move(long stride, bool allowPartial)
        {
            return _cursor.Move(stride, allowPartial);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMoveNextBatch(out Segment<TKey, TValue> batch)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            return _cursor.MoveNext();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            return _cursor.MovePrevious();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            return _cursor.MoveAt(key, direction);
        }

        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _cursor._currentKey;
        }

        public TValue CurrentValue
        {
            // Note: alternative to storing CV is accessing it by index, but then we need to check
            // order that was already checked during MN - otherwise CV could mismatch CK.
            // But if we do not store CV and then use it after accessing this property
            // there is still a possibility that underlying data order is changed after CV is read.
            // We cannot do anything with that and checking order in CV getter is not "more correct"
            // than caching it. Caching guarantees that the pair was correct during MN and
            // caching is significantly faster (~295 vs 250 MOPS). We now have Move(stride)
            // method so we could avoid reading not needed values.
            // MAt should use keys directly and not use Move for binary search.

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _cursor._currentValue;
        }

        Series<TKey, TValue, SCursor<TKey, TValue>> ISpecializedCursor<TKey, TValue, SCursor<TKey, TValue>>.Source
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Series<TKey, TValue, SCursor<TKey, TValue>>(this);
        }

        public IAsyncCompleter AsyncCompleter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _cursor._source;
        }

        ISeries<TKey, TValue> ICursor<TKey, TValue>.Source
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _cursor._source;
        }

        public bool IsContinuous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SCursor<TKey, TValue> Initialize()
        {
            return new SCursor<TKey, TValue>(_cursor._source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SCursor<TKey, TValue> Clone()
        {
            var c = this;
            return c;
        }

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            return Clone();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            return _cursor._source.TryGetValue(key, out value);
        }

        public void Reset()
        {
            _cursor.Reset();
        }

        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new KeyValuePair<TKey, TValue>(_cursor._currentKey, CurrentValue);
        }

        object IEnumerator.Current => ((IEnumerator)_cursor).Current;

        public void Dispose()
        {
            _cursor.Dispose();
        }

        ///////////////////////////////////////////////

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MoveNext(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MovePrevious(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        public bool IsIndexed => throw new NotImplementedException();

        public bool IsCompleted => throw new NotImplementedException();
    }
}