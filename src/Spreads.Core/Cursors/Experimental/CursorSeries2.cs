// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Cursors
{
    public struct CursorSeries2<TKey, TValue, TCursor> : ISeries<TKey, TValue>, ISpecializedCursor<TKey, TValue, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        private readonly TCursor _cursor;

        internal CursorSeries2(TCursor cursor)
        {
            this = new CursorSeries2<TKey, TValue, TCursor>();
            _cursor = cursor;
        }

        public static CursorSeries2<TKey, TValue, TCursor> Create(TCursor cursor)
        {
            return new CursorSeries2<TKey, TValue, TCursor>(cursor);
        }



        #region ISeries members

        public IDisposable Subscribe(IObserver<KeyValuePair<TKey, TValue>> observer)
        {
            throw new NotImplementedException();
        }

        IAsyncEnumerator<KeyValuePair<TKey, TValue>> IAsyncEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return _cursor.Initialize();
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return _cursor.Initialize();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _cursor.Initialize();
        }

        public bool IsIndexed => throw new NotImplementedException();
        public bool IsReadOnly { get; }
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        public ICursor<TKey, TValue> GetCursor()
        {
            return _cursor.Initialize();
        }

        public object SyncRoot { get; }

        public Task<bool> Updated => throw new NotSupportedException();

        #endregion ISeries members



        #region ISpecializedCursor members

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return _cursor.MoveNext(cancellationToken);
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            return _cursor.MoveNext();
        }

        public void Reset()
        {
            _cursor.Reset();
        }

        public KeyValuePair<TKey, TValue> Current
        {
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Current; }
        }

        object IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ((IEnumerator)_cursor).Current; }
        }

        public void Dispose()
        {
            _cursor.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            return _cursor.MoveAt(key, direction);
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
        public bool MovePrevious()
        {
            return _cursor.MovePrevious();
        }

        public TKey CurrentKey
        {
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentKey; }
        }

        public TValue CurrentValue
        {
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentValue; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            return _cursor.MoveNextBatch(cancellationToken);
        }

        public IReadOnlySeries<TKey, TValue> CurrentBatch
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentBatch; }
        }

        public IReadOnlySeries<TKey, TValue> Source
        {
            get { return _cursor.Source; }
        }

        public bool IsContinuous
        {
            get { return _cursor.IsContinuous; }
        }

        public TCursor Initialize()
        {
            return _cursor.Initialize();
        }

        TCursor ISpecializedCursor<TKey, TValue, TCursor>.Clone()
        {
            return _cursor.Clone();
        }

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            return _cursor.Clone();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _cursor.TryGetValue(key, out value);
        }

        #endregion ISpecializedCursor members
    }
}