// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Spreads.Collections.Experimental
{
    public partial class Series<TKey, TValue>
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
            throw new NotImplementedException();
        }

        public IEnumerable<TKey> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO via cursor and CurrentKey
                foreach (var keyValuePair in this)
                {
                    yield return keyValuePair.Key;
                }
            }
        }

        public IEnumerable<TValue> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO via cursor and CurrentValue
                foreach (var keyValuePair in this)
                {
                    yield return keyValuePair.Value;
                }
            }
        }
    }

    
    /// <summary>
    /// <see cref="Series{TKey,TValue}"/> cursor implementation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SCursor<TKey, TValue> : ICursorNew<TKey>, ISpecializedCursor<TKey, TValue, SCursor<TKey, TValue>>
    {
        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotImplementedException();
        }

        public CursorState State => throw new NotImplementedException();

        public KeyComparer<TKey> Comparer => throw new NotImplementedException();

        public bool MoveAt(TKey key, Lookup direction)
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

        bool ICursor<TKey, TValue>.MoveNext()
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

        public TKey CurrentKey => throw new NotImplementedException();

        public TValue CurrentValue => throw new NotImplementedException();

        Series<TKey, TValue, SCursor<TKey, TValue>> ISpecializedCursor<TKey, TValue, SCursor<TKey, TValue>>.Source => throw new NotImplementedException();

        public IAsyncCompleter AsyncCompleter => throw new NotImplementedException();

        ISeries<TKey, TValue> ICursor<TKey, TValue>.Source => throw new NotImplementedException();

        public bool IsContinuous => throw new NotImplementedException();

        public SCursor<TKey, TValue> Initialize()
        {
            throw new NotImplementedException();
        }

        SCursor<TKey, TValue> ISpecializedCursor<TKey, TValue, SCursor<TKey, TValue>>.Clone()
        {
            throw new NotImplementedException();
        }

        public bool IsIndexed => throw new NotImplementedException();

        public bool IsCompleted => throw new NotImplementedException();

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(TKey key, out TValue value)
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

        public KeyValuePair<TKey, TValue> Current => throw new NotImplementedException();

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public long Move(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        public bool TryMoveNextBatch(out object batch)
        {
            throw new NotImplementedException();
        }
    }
}