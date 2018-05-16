using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Experimental
{
    public readonly ref struct KeyValueReadOnlySpan<TKey, TValue>
    {
#pragma warning disable IDE0032 // NB ReSharper doesn't get it when applied, says no setter
        private readonly ReadOnlySpan<TKey> _keys;
        private readonly ReadOnlySpan<TValue> _values;
#pragma warning restore  IDE0032

        public ReadOnlySpan<TKey> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _keys; }
        }

        public ReadOnlySpan<TValue> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _values; }
        }

        public long Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _keys.Length; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValueReadOnlySpan(Span<TKey> keys, Span<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys;
            _values = values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValueReadOnlySpan(ReadOnlySpan<TKey> keys, ReadOnlySpan<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys;
            _values = values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValueReadOnlySpan(TKey[] keys, TValue[] values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys;
            _values = values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValueReadOnlySpan(ReadOnlyMemory<TKey> keys, ReadOnlyMemory<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys.Span;
            _values = values.Span;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValueReadOnlySpan(Memory<TKey> keys, Memory<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys.Span;
            _values = values.Span;
        }

        [Obsolete("Watch for perf. Copying values because Unsafe cannot accept readonly ref.")]
        public KeyValuePair<TKey, TValue> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO KeyValue must be missing and do not throw if index is out of bounds
                return new KeyValuePair<TKey, TValue>(_keys[index], _values[index]);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDifferentCount()
        {
            throw new ArgumentException("Keys and values have different length");
        }
    }
}