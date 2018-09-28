// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Collections.Concurrent
{
    public interface IStorageIndexed
    {
        int StorageIndex { get; set; }
    }

    public abstract class IndexedLookup<TKey, TValue> : IDisposable, IStorageIndexed where TValue : IndexedLookup<TKey, TValue>
    {
        public int StorageIndex { get; set; }
        public abstract IndexedLockedWeakDictionary<TKey, TValue> Storage { get; }
        public abstract TKey StorageKey { get; }

        public abstract Task Cleanup();

        ~IndexedLookup()
        {
            GC.ReRegisterForFinalize(this);
            var _ = DoCleanup();
        }

        private async Task DoCleanup()
        {
            try
            {
                await Cleanup();
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error in IndexedLookup.Cleanup: " + ex);
            }

            var storage = Storage;
            storage.TryRemove(StorageKey);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        public void Dispose()
        {
            var _ = DoCleanup();
        }
    }

    // we need this catchable
    public class IndexedLockedWeakDictionaryReachedLimitException : Exception
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Throw()
        {
            DoThrow();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DoThrow()
        {
            throw new IndexedLockedWeakDictionaryReachedLimitException();
        }
    }

    /// <summary>
    /// Long-term storage where added items could be retrieved by ushort index.
    /// Pessimized write operations in favor of fast reads. Scenario is for
    /// a lot of objects stored as weak references and cleaned when no used,
    /// ensuring that an async cleanup is
    /// </summary>
    public sealed class IndexedLockedWeakDictionary<TKey, TValue> where TValue : class, IStorageIndexed
    {
#pragma warning disable CS0618 // Type or member is obsolete
        private FastDictionary<TKey, GCHandle> _inner = new FastDictionary<TKey, GCHandle>();
#pragma warning restore CS0618 // Type or member is obsolete
        private long _locker;

        private int _counter;
        private GCHandle[] _index = new GCHandle[1024];
        private Stack<int> _freeSlots = new Stack<int>();


        public int Count => _inner.Count;

        private void GrowIndexArray()
        {
            // TODO RMS-style, append chunks, no need to copy

            // Indexes remain valid, only writers could grow the array when adding a new index value.
            // This happens inside a lock and on a thread that can allow to wait.
            // TryGet by key is not affected (and it is locked anyways), TryGet by index should not
            // see new values before add exits. If reader gets an older array there should be all values
            // it might know exist.
            var newArray = new GCHandle[_index.Length * 2];
            _index.CopyTo(newArray, 0);

            // reference assignment is atomic in .NET
            _index = newArray;
        }

        public bool TryAdd(TKey key, TValue value)
        {
            if (value.StorageIndex != 0)
            {
                ThrowStorageIndexNonZero();
            }

            EnterWriteLock();
            try
            {
                var h = GCHandle.Alloc(value, GCHandleType.Weak);
                var added = _inner.TryAdd(key, h);
                if (!added)
                {
                    h.Free();
                }
                else
                {
                    var idx = _counter++; // we are inside lock, do not need: Interlocked.Increment(ref _counter);
                    if (idx >= _index.Length)
                    {
                        if (_freeSlots.Count == 0)
                        {
                            GrowIndexArray();
                        }
                        else
                        {
                            idx = _freeSlots.Pop();
                            if (Settings.AdditionalCorrectnessChecks.Enabled)
                            {
                                if (_index[idx].IsAllocated)
                                {
                                    ThrowHelper.FailFast("Free slot is allocated");
                                }
                            }
                        }
                    }

                    value.StorageIndex = idx;
                    _index[idx] = h;
                }

                return added;
            }
            finally
            {
                Volatile.Write(ref _locker, 0L);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnterWriteLock()
        {
            // Optimized for reader, writer uses CompareExchange, reader uses increment
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

        public bool TryRemove(TKey key)
        {
            EnterWriteLock();
            try
            {
                var removed = _inner.Remove(key, out var h);

                if (!removed)
                {
                    Volatile.Write(ref _locker, 0L);
                    return false;
                }

                if (Settings.AdditionalCorrectnessChecks.Enabled)
                {
                    if (!h.IsAllocated)
                    {
                        ThrowHelper.FailFast("!h.IsAllocated in TryRemove");
                    }
                }

                var value = h.Target as TValue;

                if (value == null)
                {
                    Trace.TraceWarning("Leaked GCHandle, object was collected without cleanup");
                    Volatile.Write(ref _locker, 0L);
                    // we do not know index, but know there is at least one slot with null target
                    Clean();
                    return true;
                }

                var idx = value.StorageIndex;

                _index[idx].Free();
                _index[idx] = default;
                _freeSlots.Push(idx);

                return true;
            }
            finally
            {
                Volatile.Write(ref _locker, 0L);
            }
        }

        private void Clean()
        {
            // TODO Async scan of empty weak references
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowStorageIndexNonZero()
        {
            ThrowHelper.ThrowArgumentException("StorageIndex must be zero before adding value to IndexedLockedWeakDictionary");
        }

        public bool TryGetValue(TKey key, out TValue value)
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

            if (found) // TODO what if GC between the lines?
            {
                if (h.IsAllocated)
                {
                    value = h.Target as TValue;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetByIndex(int index, out TValue value)
        {
            // all hassle above is to get this as fast as possible
            var indexArray = _index;
            var h = indexArray[index];
            var target = h.Target as TValue; // do not check IsAllocated, correct usage guarantees that, and consumer uses try/catch anyway
            return (value = target) != null;
        }
    }
}
