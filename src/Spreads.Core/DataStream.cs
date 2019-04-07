// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using Spreads.DataTypes;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
#pragma warning disable 618

#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
#pragma warning disable HAA0401 // Possible allocation of reference type enumerator

namespace Spreads
{
    /// <summary>
    /// DataStreams are unbounded sequences of data items, either recorded or arriving in real-time.
    /// DataStreams have sequential keys.
    /// This interface should not be used and is only a definition of IDataStream, which is just <see cref="ISeries{TKey, TValue}"/> with <see cref="ulong"/> and <see cref="Timestamped{T}"/>.
    /// </summary>
    [Obsolete("Use generic T where T : ISeries<ulong, Timestamped<T>>")]
    public interface IDataStream<T> : ISeries<ulong, Timestamped<T>>
    { }

    public interface IMutableDataStream<T> : ISeries<ulong, Timestamped<T>>
    {
        /// <summary>
        /// Returns false if the version is not the next one after the current one (atomic check) or if the stream is completed.
        /// Similar to CAS logic when new values depend on previous ones and concurrent writes are possible.
        /// </summary>
        Task<bool> TryAddLast(ulong version, T value);

        Task<bool> TryAddLast(ulong version, T value, Timestamp timestamp);

        /// <summary>
        /// Atomically increments version for the added value. Returns version of the added value or zero if the stream is completed.
        /// </summary>
        ValueTask<ulong> TryAddLast(T value);

        ValueTask<ulong> TryAddLast(T value, Timestamp timestamp);

        Task Complete();
    }

    public class DataStream
    {
        private DataStream()
        { }

        /// <summary>
        /// 2**53 ought to be enough for anybody. It's 285 years of microseconds.
        /// </summary>
        public static ulong MaxVersion = (1UL << 53) - 1UL;
    }

    public readonly struct DataStream<T, TSeries, TCursor> : IDataStream<T>
        where TSeries : ISpecializedSeries<ulong, Timestamped<T>, TCursor>
        where TCursor : ISpecializedCursor<ulong, Timestamped<T>, TCursor>
    {
        private readonly TSeries _impl;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DataStream(TSeries impl)
        {
            _impl = impl;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncEnumerator<KeyValuePair<ulong, Timestamped<T>>> GetAsyncEnumerator()
        {
            return _impl.GetAsyncEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<KeyValuePair<ulong, Timestamped<T>>> GetEnumerator()
        {
            return _impl.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_impl).GetEnumerator();
        }

        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.IsCompleted;
        }

        public bool IsIndexed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.IsIndexed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ICursor<ulong, Timestamped<T>> GetCursor()
        {
            return _impl.GetSpecializedCursor();
        }

        public IAsyncCursor<ulong, Timestamped<T>> GetAsyncCursor()
        {
            return new AsyncCursor<ulong, Timestamped<T>, Cursor<ulong, Timestamped<T>>>(_impl.GetSpecializedCursor());
        }

        public KeyComparer<ulong> Comparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.Comparer;
        }

        public Opt<KeyValuePair<ulong, Timestamped<T>>> First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.First;
        }

        public Opt<KeyValuePair<ulong, Timestamped<T>>> Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.Last;
        }

        public Timestamped<T> LastValueOrDefault => throw new NotImplementedException();

        public Timestamped<T> this[ulong key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl[key];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(ulong key, out Timestamped<T> value)
        {
            return _impl.TryGetValue(key, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAt(long index, out KeyValuePair<ulong, Timestamped<T>> kvp)
        {
            return _impl.TryGetAt(index, out kvp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindAt(ulong key, Lookup direction, out KeyValuePair<ulong, Timestamped<T>> kvp)
        {
            return _impl.TryFindAt(key, direction, out kvp);
        }

        public IEnumerable<ulong> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.Keys;
        }

        public IEnumerable<Timestamped<T>> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.Values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator DataStream<T, TSeries, TCursor>(TSeries impl)
        {
            return new DataStream<T, TSeries, TCursor>(impl);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TSeries(DataStream<T, TSeries, TCursor> ds)
        {
            return ds._impl;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public readonly struct DataStream<T, TSeries> : IDataStream<T>
        where TSeries : ISeries<ulong, Timestamped<T>>
    {
        private readonly TSeries _impl;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DataStream(TSeries impl)
        {
            _impl = impl;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncEnumerator<KeyValuePair<ulong, Timestamped<T>>> GetAsyncEnumerator()
        {
            return _impl.GetAsyncEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<KeyValuePair<ulong, Timestamped<T>>> GetEnumerator()
        {
            return _impl.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_impl).GetEnumerator();
        }

        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.IsCompleted;
        }

        public bool IsIndexed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.IsIndexed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ICursor<ulong, Timestamped<T>> GetCursor()
        {
            return _impl.GetCursor();
        }

        public IAsyncCursor<ulong, Timestamped<T>> GetAsyncCursor()
        {
            throw new NotImplementedException();
        }

        public KeyComparer<ulong> Comparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.Comparer;
        }

        public Opt<KeyValuePair<ulong, Timestamped<T>>> First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.First;
        }

        public Opt<KeyValuePair<ulong, Timestamped<T>>> Last => _impl.Last;

        public Timestamped<T> LastValueOrDefault => throw new NotImplementedException();

        public Timestamped<T> this[ulong key] => _impl[key];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(ulong key, out Timestamped<T> value)
        {
            return _impl.TryGetValue(key, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetAt(long index, out KeyValuePair<ulong, Timestamped<T>> kvp)
        {
            return _impl.TryGetAt(index, out kvp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindAt(ulong key, Lookup direction, out KeyValuePair<ulong, Timestamped<T>> kvp)
        {
            return _impl.TryFindAt(key, direction, out kvp);
        }

        public IEnumerable<ulong> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.Keys;
        }

        public IEnumerable<Timestamped<T>> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _impl.Values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator DataStream<T, TSeries>(TSeries impl)
        {
            return new DataStream<T, TSeries>(impl);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator TSeries(DataStream<T, TSeries> ds)
        {
            return ds._impl;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public readonly struct DataStream<T> : ISpecializedSeries<ulong, Timestamped<T>, DataStreamCursor<T>>
    {
        private readonly DataStreamCursor<T> _cursor;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Series<ulong, Timestamped<T>, DataStreamCursor<T>>(DataStream<T> ds)
        {
            return ds._cursor.Source;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Series<ulong, Timestamped<T>, Cursor<ulong, Timestamped<T>>>(DataStream<T> ds)
        {
            var c = new Cursor<ulong, Timestamped<T>>(ds._cursor);
            return new Series<ulong, Timestamped<T>, Cursor<ulong, Timestamped<T>>>(c);
        }

        public IAsyncEnumerator<KeyValuePair<ulong, Timestamped<T>>> GetAsyncEnumerator()
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<ulong, Timestamped<T>>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool IsCompleted => throw new NotImplementedException();

        public bool IsIndexed => throw new NotImplementedException();

        ICursor<ulong, Timestamped<T>> ISeries<ulong, Timestamped<T>>.GetCursor()
        {
            throw new NotImplementedException();
        }

        AsyncCursor<ulong, Timestamped<T>, DataStreamCursor<T>> ISpecializedSeries<ulong, Timestamped<T>, DataStreamCursor<T>>.GetAsyncCursor()
        {
            throw new NotImplementedException();
        }

        DataStreamCursor<T> ISpecializedSeries<ulong, Timestamped<T>, DataStreamCursor<T>>.GetCursor()
        {
            throw new NotImplementedException();
        }

        IAsyncCursor<ulong, Timestamped<T>> ISeries<ulong, Timestamped<T>>.GetAsyncCursor()
        {
            throw new NotImplementedException();
        }

        public KeyComparer<ulong> Comparer => throw new NotImplementedException();

        public Opt<KeyValuePair<ulong, Timestamped<T>>> First => throw new NotImplementedException();

        public Opt<KeyValuePair<ulong, Timestamped<T>>> Last => throw new NotImplementedException();

        public Timestamped<T> LastValueOrDefault => throw new NotImplementedException();

        public Timestamped<T> this[ulong key] => throw new NotImplementedException();

        public bool TryGetValue(ulong key, out Timestamped<T> value)
        {
            throw new NotImplementedException();
        }

        public bool TryGetAt(long index, out KeyValuePair<ulong, Timestamped<T>> kvp)
        {
            throw new NotImplementedException();
        }

        public bool TryFindAt(ulong key, Lookup direction, out KeyValuePair<ulong, Timestamped<T>> kvp)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ulong> Keys => throw new NotImplementedException();

        public IEnumerable<Timestamped<T>> Values => throw new NotImplementedException();
    }

    public readonly struct DataStreamCursor<T> : ISpecializedCursor<ulong, Timestamped<T>, DataStreamCursor<T>>
    {
        public CursorState State => throw new NotImplementedException();

        public KeyComparer<ulong> Comparer => throw new NotImplementedException();

        public bool MoveFirst()
        {
            throw new NotImplementedException();
        }

        public bool MoveLast()
        {
            throw new NotImplementedException();
        }

        bool ICursor<ulong, Timestamped<T>>.MoveNext()
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

        public bool MoveAt(ulong key, Lookup direction)
        {
            throw new NotImplementedException();
        }

        public ulong CurrentKey => throw new NotImplementedException();

        public Timestamped<T> CurrentValue => throw new NotImplementedException();

        public Series<ulong, Timestamped<T>, DataStreamCursor<T>> Source => throw new NotImplementedException();

        public IAsyncCompleter AsyncCompleter => throw new NotImplementedException();

        ISeries<ulong, Timestamped<T>> ICursor<ulong, Timestamped<T>>.Source => throw new NotImplementedException();

        public bool IsContinuous => throw new NotImplementedException();

        public DataStreamCursor<T> Initialize()
        {
            throw new NotImplementedException();
        }

        DataStreamCursor<T> ISpecializedCursor<ulong, Timestamped<T>, DataStreamCursor<T>>.Clone()
        {
            throw new NotImplementedException();
        }

        public bool IsIndexed => throw new NotImplementedException();

        public bool IsCompleted => throw new NotImplementedException();

        ICursor<ulong, Timestamped<T>> ICursor<ulong, Timestamped<T>>.Clone()
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(ulong key, out Timestamped<T> value)
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

        public KeyValuePair<ulong, Timestamped<T>> Current => throw new NotImplementedException();

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}