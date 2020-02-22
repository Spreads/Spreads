// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Generic;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Collections.Concurrent
{
    /// <summary>
    /// A dictionary that stores <see cref="GCHandle"/> with <see cref="GCHandleType.Weak"/> option.
    /// Used for caches when reads are significantly more frequent than writes and multi-threaded access
    /// is usually not contended. For other cases <see cref="ConcurrentDictionary{TKey,TValue}"/> may be better.
    /// </summary>
    /// <remarks>
    /// Another use case is for frequent enumeration of small concurrent dictionaries while <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// doesn't have struct enumerator. Currently we have to Enter/Exit lock manually and enumerate <see cref="InnerDictionary"/> manually,
    /// possible with try/catch if during enumeration we are doing some job that could throw.
    /// </remarks>
    public sealed class LockedWeakDictionary<TKey> where TKey : IEquatable<TKey>
    {
        private readonly DictionarySlim<TKey, GCHandle> _inner = new DictionarySlim<TKey, GCHandle>();
        private int _locker;

        internal DictionarySlim<TKey, GCHandle> InnerDictionary
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _inner;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, object obj)
        {
            EnterLock();

            bool added = true;

#if DEBUG
            try
            {
#endif
            var h = GCHandle.Alloc(obj, GCHandleType.Weak);
            ref var hr = ref _inner.GetOrAddValueRef(key);

            if (hr.IsAllocated)
            {
                h.Free();
                added = false;
            }
            else
            {
                hr = h;
            }

#if DEBUG
            }
            catch
            {
                ThrowHelper.FailFast("LockedWeakDictionary.TryAdd must never throw.");
            }
#endif
            ExitLock();

            return added;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out object value)
        {
            EnterLock();
            bool found = false;
#if DEBUG
            value = default;
            try
            {
#endif
            
            found = _inner.DangerousTryGetValue(key, out var h);

            if (found) // TODO what if GC between the lines?
            {
                if (h.IsAllocated)
                {
                    value = h.Target;
                }
                else
                {
                    _inner.Remove(key);
                    h.Free();
                    value = null;
                    found = false;
                }
            }
            else
            {
                value = null;
            }

#if DEBUG
            }
            catch
            {
                ThrowHelper.FailFast("LockedWeakDictionary.TryGetValue must never throw.");
            }
#endif

            ExitLock();

            return found;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnterLock()
        {
            var sw = new SpinWait();
            while (true)
            {
                var existing = Interlocked.CompareExchange(ref _locker, 1, 0);
                if (existing == 0)
                {
                    break;
                }
                sw.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ExitLock()
        {
            // _locker is int so no need for special x86 case with interlocked.
            Volatile.Write(ref _locker, 0);
        }
    }
}
