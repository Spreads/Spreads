// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Spreads.Collections.Generic;

namespace Spreads.Collections.Concurrent
{
    /// <summary>
    /// Useful together with a thread pool when we repeatedly do some work over a fixed set of objects.
    /// TryPop will remove an item for processing but an item with the same key could be added back.
    /// If an item already exists it is just overwritten and the size of the dictionary remains constant.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    internal sealed class LockedDictionary<TKey, TValue> where TKey : IEquatable<TKey>
    {
        private readonly DictionarySlim<TKey, TValue> _inner = new DictionarySlim<TKey, TValue>();
        private int _locker;

        internal DictionarySlim<TKey, TValue> InnerDictionary
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _inner;
        }

        /// <summary>
        /// Sets a value regardless if the key is already present or not.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(TKey key, TValue value)
        {
            EnterLock();

#if DEBUG
            try
            {
#endif
            ref var valRef = ref _inner.GetOrAddValueRef(key);
            valRef = value;

#if DEBUG
            }
            catch
            {
                ThrowHelper.FailFast("LockedDictionary.Add must never throw.");
            }
#endif
            ExitLock();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemove(TKey key, out TValue value)
        {
            EnterLock();

            var removed = false;
            value = default;
#if DEBUG
            try
            {
#endif
            if (_inner.DangerousTryGetValue(key, out value))
            {
                removed = _inner.Remove(key);
                if (!removed)
                {
                    FailCanontRemoveExisting();
                }
            }

#if DEBUG
            }
            catch
            {
                ThrowHelper.FailFast("LockedDictionary.TryRemove must never throw.");
            }
#endif
            ExitLock();

            return removed;
        }

        /// <summary>
        /// Removes the first found value from the dictionary and returns it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPop(out KeyValuePair<TKey, TValue> pair)
        {
            EnterLock();

            var removed = false;
            pair = default;
#if DEBUG
            try
            {
#endif
            using (var e = _inner.GetEnumerator())
            {
                if (e.MoveNext())
                {
                    pair = e.Current;
                    removed = _inner.Remove(pair.Key);
                    if (!removed)
                    {
                        FailCanontRemoveExisting();
                    }
                }
                else
                {
                    pair = default;
                }
            }

#if DEBUG
            }
            catch
            {
                ThrowHelper.FailFast("LockedDictionary.TryPop must never throw.");
            }
#endif
            ExitLock();

            return removed;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FailCanontRemoveExisting()
        {
            // TODO review & remove
            ThrowHelper.ThrowInvalidOperationException("Cannot remove a value that we have just read from inside lock.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            EnterLock();
            bool found = false;
#if DEBUG
            value = default;
            try
            {
#endif
            found = _inner.DangerousTryGetValue(key, out value);

#if DEBUG
            }
            catch
            {
                ThrowHelper.FailFast("LockedDictionary.TryGetValue must never throw.");
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
