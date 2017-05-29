// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Utils;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Spreads
{

    /// <summary>
    /// SpecializedWrapper Extensions
    /// </summary>
    internal static class SpecializedCursorExtensions
    {
        /// <summary>
        /// Create SpecializedWrapper that wraps the result of ISeries.GetCursor() call.
        /// </summary>
        public static Cursor<TKey, TValue> GetSpecializedCursor<TKey, TValue>(this ISeries<TKey, TValue> series)
        {
            return new Cursor<TKey, TValue>(series.GetCursor());
        }

    }

    /// <summary>
    /// Unspecialized cursor wrapper. Wraps <see cref="ICursor{TKey,TValue}"/> as <see cref="ISpecializedCursor{TKey,TValue,TCursor}"/>.
    /// </summary>
    public struct Cursor<TKey, TValue> : ICursorSeries<TKey, TValue, Cursor<TKey, TValue>>
    {
        private readonly ICursor<TKey, TValue> _cursor;

        /// <summary>
        /// SpecializedWrapper constructor.
        /// </summary>
        /// <param name="cursor"></param>
        public Cursor([NotNull] ICursor<TKey, TValue> cursor)
        {
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
        }

        /// <inheritdoc />
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return _cursor.MoveNext(cancellationToken);
        }

        /// <inheritdoc />
        public Cursor<TKey, TValue> Initialize()
        {
            return new Cursor<TKey, TValue>(_cursor.Source.GetCursor());
        }

        /// <inheritdoc />
        public bool MoveNext()
        {
            return _cursor.MoveNext();
        }

        /// <inheritdoc />
        public void Reset()
        {
            _cursor.Reset();
        }

        /// <inheritdoc />
        public KeyValuePair<TKey, TValue> Current => _cursor.Current;

        /// <inheritdoc />
        object IEnumerator.Current => ((IEnumerator)_cursor).Current;

        /// <inheritdoc />
        public void Dispose()
        {
            _cursor.Dispose();
        }

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        /// <inheritdoc />
        public bool MoveAt(TKey key, Lookup direction)
        {
            return _cursor.MoveAt(key, direction);
        }

        /// <inheritdoc />
        public bool MoveFirst()
        {
            return _cursor.MoveFirst();
        }

        /// <inheritdoc />
        public bool MoveLast()
        {
            return _cursor.MoveLast();
        }

        /// <inheritdoc />
        public bool MovePrevious()
        {
            return _cursor.MovePrevious();
        }

        /// <inheritdoc />
        public TKey CurrentKey => _cursor.CurrentKey;

        /// <inheritdoc />
        public TValue CurrentValue => _cursor.CurrentValue;

        /// <inheritdoc />
        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            return _cursor.MoveNextBatch(cancellationToken);
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TValue> CurrentBatch => _cursor.CurrentBatch;

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TValue> Source => _cursor.Source;

        /// <inheritdoc />
        public bool IsContinuous => _cursor.IsContinuous;



        /// <inheritdoc />
        public Cursor<TKey, TValue> Clone()
        {
            return new Cursor<TKey, TValue>(_cursor.Clone());
        }

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            return Clone();
        }

        /// <inheritdoc />
        public bool TryGetValue(TKey key, out TValue value)
        {
            return _cursor.TryGetValue(key, out value);
        }


        #region ICursorSeries members

        /// <inheritdoc />
        public bool IsIndexed => _cursor.Source.IsIndexed;

        /// <inheritdoc />
        public bool IsReadOnly
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Source.IsReadOnly; }
        }

        /// <inheritdoc />
        public Task<bool> Updated
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Source.Updated; }
        }

        #endregion
    }
}