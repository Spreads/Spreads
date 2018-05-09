// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads
{
    public readonly unsafe ref struct KeyValueRef<TKey, TValue>
    {
        // NB See https://github.com/dotnet/csharplang/issues/1147
        // and https://github.com/dotnet/corert/blob/796aeaa64ec09da3e05683111c864b529bcc17e8/src/System.Private.CoreLib/src/System/ByReference.cs
        // Use it when it is made public
        private readonly void* _kp;

        private readonly void* _vp;

        public TKey Key
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Unsafe.ReadUnaligned<TKey>(_kp);
            }
        }

        public TValue Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Unsafe.ReadUnaligned<TValue>(_vp);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KeyValueRef(ref TKey k, ref TValue v)
        {
            _kp = Unsafe.AsPointer(ref k);
            _vp = Unsafe.AsPointer(ref v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KeyValueRef(TKey k, TValue v)
        {
            _kp = Unsafe.AsPointer(ref k);
            _vp = Unsafe.AsPointer(ref v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KeyValueRef(KeyValuePair<TKey, TValue> kvp)
        {
            var k = kvp.Key;
            var v = kvp.Value;
            _kp = Unsafe.AsPointer(ref k);
            _vp = Unsafe.AsPointer(ref v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KeyValueRef<TKey, TValue>(KeyValuePair<TKey, TValue> kvp)
        {
            return new KeyValueRef<TKey, TValue>(kvp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator KeyValuePair<TKey, TValue>(KeyValueRef<TKey, TValue> kv)
        {
            return new KeyValuePair<TKey, TValue>(kv.Key, kv.Value);
        }
    }

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

        public KeyValueRef<TKey, TValue> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return new KeyValueRef<TKey, TValue>(ref _keys[index], ref _values[index]);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
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

    public readonly ref struct ReadOnlyKeyValueSpan<TKey, TValue>
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
        internal ReadOnlyKeyValueSpan(Span<TKey> keys, Span<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys;
            _values = values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyKeyValueSpan(ReadOnlySpan<TKey> keys, ReadOnlySpan<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys;
            _values = values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyKeyValueSpan(TKey[] keys, TValue[] values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys;
            _values = values;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyKeyValueSpan(ReadOnlyMemory<TKey> keys, ReadOnlyMemory<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys.Span;
            _values = values.Span;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyKeyValueSpan(Memory<TKey> keys, Memory<TValue> values)
        {
            if (keys.Length != values.Length)
            {
                ThrowDifferentCount();
            }
            _keys = keys.Span;
            _values = values.Span;
        }

        [Obsolete("Watch for perf. Copying values because Unsafe cannot accept readonly ref.")]
        public KeyValueRef<TKey, TValue> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return new KeyValueRef<TKey, TValue>(_keys[index], _values[index]);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDifferentCount()
        {
            throw new ArgumentException("Keys and values have different length");
        }
    }

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

        public KeyValueRef<TKey, TValue> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return new KeyValueRef<TKey, TValue>(ref Keys.Span[index], ref Values.Span[index]);
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
}
