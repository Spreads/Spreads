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
    /// This interface should not be used and is only a definition of IDataStream, which is just <see cref="ISeries{ulong, Timestamped{T}}"/>.
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
    }
}