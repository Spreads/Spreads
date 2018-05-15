using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads
{
    public readonly struct KeyValueMemory<TKey, TValue>
    {
        public Memory<TKey> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        public Memory<TValue> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValueMemory(TKey[] keys, TValue[] values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            Keys = keys;
            Values = values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValueMemory(Memory<TKey> keys, Memory<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            Keys = keys;
            Values = values;
        }

        public KeyValuePair<TKey, TValue> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO KeyValue must be missing and do not throw if index is out of bounds
                return new KeyValuePair<TKey, TValue>(Keys.Span[index], Values.Span[index]);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                Keys.Span[index] = value.Key;
                Values.Span[index] = value.Value;
            }
        }

        public KeyValueSpan<TKey, TValue> KeyValueSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return new KeyValueSpan<TKey, TValue>(Keys, Values);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDifferentCount()
        {
            throw new ArgumentException("Keys and values have different length");
        }
    }

    public readonly struct KeyValueReadOnlyMemory<TKey, TValue>
    {
        public ReadOnlyMemory<TKey> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        public ReadOnlyMemory<TValue> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValueReadOnlyMemory(TKey[] keys, TValue[] values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            Keys = keys;
            Values = values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValueReadOnlyMemory(Memory<TKey> keys, Memory<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            Keys = keys;
            Values = values;
        }

        public KeyValuePair<TKey, TValue> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO KeyValue must be missing and do not throw if index is out of bounds
                return new KeyValuePair<TKey, TValue>(Keys.Span[index], Values.Span[index]);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDifferentCount()
        {
            throw new ArgumentException("Keys and values have different length");
        }
    }
}