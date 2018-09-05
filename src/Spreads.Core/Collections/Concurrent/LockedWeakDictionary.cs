// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Collections.Concurrent
{
    public sealed class LockedWeakDictionary<TKey>
    {
#pragma warning disable CS0618 // Type or member is obsolete
        private FastDictionary<TKey, GCHandle> _inner = new FastDictionary<TKey, GCHandle>();
#pragma warning restore CS0618 // Type or member is obsolete
        private long _locker;

        public bool TryAdd(TKey key, object obj)
        {
            // prefer reader

            //var incr = Interlocked.Increment(ref _locker);
            //if (incr != 1L)
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
                    //if (sw.NextSpinWillYield)
                    //{
                    //    sw.Reset();
                    //}
                }
            }

            var h = GCHandle.Alloc(obj, GCHandleType.Weak);
            var added = _inner.TryAdd(key, h);
            if (!added)
            {
                h.Free();
            }

            Volatile.Write(ref _locker, 0L);
            return added;
        }

        public bool TryGetValue(TKey key, out object value)
        {
            var incr = Interlocked.Increment(ref _locker);
            if (incr != 1L)
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
                    if (sw.NextSpinWillYield)
                    {
                        sw.Reset();
                    }
                }
            }

            var found = _inner.TryGetValue(key, out var h);

            if (found) // TODO what is GC between the lines?
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

            Volatile.Write(ref _locker, 0L);

            return found;
        }
    }
}