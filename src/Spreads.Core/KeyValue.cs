// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using SRCS = System.Runtime.CompilerServices;

namespace Spreads
{
    // TODO xml docs
    /// <summary>
    /// Stack-only struct that represents references to a key and value pair. It's like an
    /// Opt[KeyValuePair[TKey, TValue]], but Opt cannot have ref struct.
    /// It has IsMissing/IsPresent properties that must be checked if this struct could be
    /// `undefined`/default/null, but Key and Value properties do not check this condition
    /// for performance reasons.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public readonly unsafe ref struct KeyValue<TKey, TValue>
    {
        // NB See https://github.com/dotnet/csharplang/issues/1147
        // and https://github.com/dotnet/corert/blob/796aeaa64ec09da3e05683111c864b529bcc17e8/src/System.Private.CoreLib/src/System/ByReference.cs
        // Use it when it is made public

        private readonly IntPtr _kp;

        private readonly IntPtr _vp;

        public TKey Key
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
#if DEBUG
                if (IsMissing) { throw new NullReferenceException("Must check IsMissing property of KeyValue before accesing Key/Value if unsure that a value exists"); }
#endif
                // NB On x86_64 no visible perf diff, use Unaligned te be safe than sorry later
                return SRCS.Unsafe.ReadUnaligned<TKey>((void*)_kp);
            }
        }

        public TValue Value
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
#if DEBUG
                if (IsMissing) { throw new NullReferenceException("Must check IsMissing property of KeyValue before accesing Key/Value if unsure that a value exists"); }
#endif
                // NB On x86_64 no visible perf diff, use Unaligned te be safe than sorry later
                return SRCS.Unsafe.ReadUnaligned<TValue>((void*)_vp);
            }
        }

        public bool IsMissing
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
                return _kp == IntPtr.Zero && _vp == IntPtr.Zero;
            }
        }

        public bool IsPresent
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
                return !(_kp == IntPtr.Zero && _vp == IntPtr.Zero);
            }
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public KeyValue(in TKey k, in TValue v)
        {
            _kp = (IntPtr)SRCS.Unsafe.AsPointer(ref SRCS.Unsafe.AsRef(k));
            _vp = (IntPtr)SRCS.Unsafe.AsPointer(ref SRCS.Unsafe.AsRef(v));
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public KeyValue(TKey k, TValue v)
        {
            _kp = (IntPtr)SRCS.Unsafe.AsPointer(ref k);
            _vp = (IntPtr)SRCS.Unsafe.AsPointer(ref v);
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public KeyValue(KeyValuePair<TKey, TValue> kvp)
        {
            var k = kvp.Key;
            var v = kvp.Value;
            _kp = (IntPtr)SRCS.Unsafe.AsPointer(ref k);
            _vp = (IntPtr)SRCS.Unsafe.AsPointer(ref v);
        }

        // TODO make implicit after refactoring all projetcs
        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public static explicit operator KeyValue<TKey, TValue>(KeyValuePair<TKey, TValue> kvp)
        {
            return new KeyValue<TKey, TValue>(kvp);
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public static explicit operator KeyValuePair<TKey, TValue>(KeyValue<TKey, TValue> kv)
        {
            return new KeyValuePair<TKey, TValue>(kv.Key, kv.Value);
        }
    }
}
