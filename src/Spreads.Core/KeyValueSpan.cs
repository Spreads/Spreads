using System;
using System.Runtime.CompilerServices;

namespace Spreads
{
    public readonly ref struct KeyValueSpan<TKey, TValue>
    {
#pragma warning disable IDE0032 // NB ReSharper doesn't get it when applied, says no setter
        private readonly Span<TKey> _keys;
        private readonly Span<TValue> _values;
#pragma warning restore  IDE0032

        public Span<TKey> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _keys; }
        }

        public Span<TValue> Values
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
        internal KeyValueSpan(Span<TKey> keys, Span<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys;
            _values = values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValueSpan(TKey[] keys, TValue[] values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys;
            _values = values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValueSpan(Memory<TKey> keys, Memory<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys.Span;
            _values = values.Span;
        }

        public KeyValue<TKey, TValue> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO KeyValue must be missing and do not throw if index is out of bounds
                return new KeyValue<TKey, TValue>(in _keys[index], in _values[index]);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                // TODO KeyValue must be missing and do not throw if index is out of bounds
                _keys[index] = value.Key;
                _values[index] = value.Value;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDifferentCount()
        {
            throw new ArgumentException("Keys and values have different length");
        }
    }
}