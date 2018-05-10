// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using SRCS = System.Runtime.CompilerServices;

namespace Spreads
{
    public enum KeyValuePresence
    {
        BothMissing = 0,
        BothPresent = -1,
        KeyMissing = -2, // E.g. for Fill(x) we do not calculate where the previous key was, just check if it exists and if not return given value.
        ValueMissing = -3,
    }


    public static class KeyValue
    {

        public static KeyValue<TKey, TValue> Create<TKey, TValue>(in TKey key, in TValue value)
        {
            return new KeyValue<TKey, TValue>(in key, in value);
        }

        public static KeyValue<TKey, TValue> Create<TKey, TValue>(in TKey key, in TValue value, in long version)
        {
            return new KeyValue<TKey, TValue>(in key, in value, in version);
        }

        public static KeyValue<TKey, TValue> OnlyKey<TKey, TValue>(in TKey key)
        {
            return new KeyValue<TKey, TValue>(in key, default, -3, KeyValuePresence.ValueMissing);
        }

        public static KeyValue<TKey, TValue> OnlyKey<TKey, TValue>(in TKey key, in long version)
        {
            throw new NotImplementedException();
        }

        public static KeyValue<TKey, TValue> OnlyValue<TKey, TValue>(in TValue value)
        {
            return new KeyValue<TKey, TValue>(default, default, -2, KeyValuePresence.KeyMissing);
        }

        public static KeyValue<TKey, TValue> OnlyValue<TKey, TValue>(in TValue value, in long version)
        {
            throw new NotImplementedException();
        }

    }



    // NB `in` in ctor gives 95% of perf gain, not `ref struct`, but keep it as ref struct for semantics and potential upgrade to ref fields when they are implemented
    // Also it's fatter due to version so it's better to restrict saving it in arrays/fields.

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
    public struct KeyValue<TKey, TValue>
    {
        // NB See https://github.com/dotnet/csharplang/issues/1147
        // and https://github.com/dotnet/corert/blob/796aeaa64ec09da3e05683111c864b529bcc17e8/src/System.Private.CoreLib/src/System/ByReference.cs
        // Try using it when it is made public

        // ReSharper disable InconsistentNaming
        // NB could be used from cursors
        internal TKey _k;
        internal TValue _v;
        internal long _version;
        // ReSharper restore InconsistentNaming

        public TKey Key
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
#if DEBUG
                if (IsMissing) { throw new NullReferenceException("Must check IsMissing property of KeyValue before accesing Key/Value if unsure that a value exists"); }
#endif
                return _k;
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
                return _v;
            }
        }

        public bool IsMissing
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
                return _version == 0;
            }
        }

        public bool IsPresent
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
                return !IsMissing;
            }
        }

        public KeyValuePresence Presence
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_version == -1 || _version > 0)
                {
                    return KeyValuePresence.BothPresent;
                }
                return (KeyValuePresence)_version;
            }
        }

        public long Version
        {
            [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
            get
            {
                return _version;
            }
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public KeyValue(in TKey k, in TValue v)
        {
            _k = k;
            _v = v;
            _version = -1;
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public KeyValue(in TKey k, in TValue v, in long version)
        {
            if (version <= 0)
            {
                ThrowHelper.ThrowArgumentException("Version is zero or negative!");
            }
            _k = k;
            _v = v;
            _version = version;
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        internal KeyValue(in TKey k, in TValue v, in long version, in KeyValuePresence presense)
        {
            // TODO bit flags for presense, int62 shall be enough for everyone
            _k = k;
            _v = v;
            _version = version;
        }

        // TODO make implicit after refactoring all projetcs
        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public static explicit operator KeyValue<TKey, TValue>(KeyValuePair<TKey, TValue> kvp)
        {
            return new KeyValue<TKey, TValue>(kvp.Key, kvp.Value);
        }

        [SRCS.MethodImpl(SRCS.MethodImplOptions.AggressiveInlining)]
        public static explicit operator KeyValuePair<TKey, TValue>(KeyValue<TKey, TValue> kv)
        {
            return new KeyValuePair<TKey, TValue>(kv.Key, kv.Value);
        }
    }
}
