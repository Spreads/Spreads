// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Generic
{
    [DebuggerTypeProxy(typeof(HashSetDebugView<>))]
    [DebuggerDisplay("Count = {" + nameof(Count) + "}")]
    public class HashSetSlim<T> : IReadOnlyCollection<T> where T : IEquatable<T>
    {
        private readonly T _defaultValue;

        private static readonly T[] InitialEntries = new T[1];
        private static readonly int[] InitialNexts = new int[1];

        private int _count;

        // 0-based index into _entries of head of free chain: -1 means empty
        private int _freeList = -1;

        // 1-based index into _entries; 0 means empty
        private int[] _buckets;
        private int[] _nexts;
        private T[] _entries;

        public T[] Values => _entries;

        internal event Action<T[]?>? OnCapacityChange;

        /// <summary>
        /// Construct with default capacity.
        /// </summary>
        public HashSetSlim()
        {
            _buckets = HashHelpers.SizeOneIntArray;
            _entries = InitialEntries;
            _nexts = InitialNexts;
            _defaultValue = default!;
        }

        /// <summary>
        /// Construct with at least the specified capacity for
        /// entries before resizing must occur.
        /// </summary>
        /// <param name="capacity">Requested minimum capacity</param>
        /// <param name="defaultValue"></param>
        public HashSetSlim(int capacity = 0, T defaultValue = default!)
        {
            if (capacity < 0)
                ThrowHelper.ThrowArgumentOutOfRange_CapacityException();;
            if (capacity < 2)
                capacity = 2; // 1 would indicate the dummy array
            capacity = HashHelpers.PowerOf2(capacity);
            _buckets = new int[capacity];
            _entries = new T[capacity];
            _nexts = new int[capacity];
            _defaultValue = defaultValue;
        }

        /// <summary>
        /// Count of entries in the dictionary.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Clears the dictionary. Note that this invalidates any active enumerators.
        /// </summary>
        public void Clear()
        {
            _count = 0;
            _freeList = -1;
            _buckets = HashHelpers.SizeOneIntArray;
            _entries = InitialEntries;
            _nexts = InitialNexts;
        }

        /// <summary>
        /// Looks for the specified value in the hashset.
        /// </summary>
        /// <param name="value">Value to look for</param>
        /// <returns>true if the key is present, otherwise false</returns>
        public bool Contains(in T value)
        {
            T[] entries = _entries;
            int[] nexts = _nexts;
            int collisionCount = 0;
            for (int i = _buckets[value.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint) i < (uint) entries.Length);
                i = nexts.UnsafeGetAt(i))
            {
                if (value.Equals(entries[i]))
                    return true;
                if (collisionCount == entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }

                collisionCount++;
            }

            return false;
        }

        /// <summary>
        /// Gets the value if present for the specified key.
        /// </summary>
        /// <param name="value">Value to look for</param>
        /// <param name="found"></param>
        /// <returns>true if the key is present, otherwise false</returns>
        public ref readonly T TryGetValue(T value, out bool found)
        {
            T[] entries = _entries;
            int[] nexts = _nexts;
            int collisionCount = 0;
            for (int i = _buckets[value.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint) i < (uint) entries.Length);
                i = nexts.UnsafeGetAt(i))
            {
                if (value.Equals(entries[i]))
                {
                    found = true;
                    return ref entries[i];
                }

                if (collisionCount == entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }

                collisionCount++;
            }

            found = false;
            return ref _defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T GetOrDefault(T value)
        {
            T[] entries = _entries;
            int[] nexts = _nexts;
            int collisionCount = 0;
            for (int i = _buckets[value.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint) i < (uint) entries.Length);
                i = nexts.UnsafeGetAt(i))
            {
                if (value.Equals(entries[i]))
                {
                    return ref entries[i];
                }

                if (collisionCount == entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }

                collisionCount++;
            }

            return ref _defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly T GetOrDefault(in T value)
        {
            T[] entries = _entries;
            int[] nexts = _nexts;
            int collisionCount = 0;
            for (int i = _buckets[value.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked ((uint) i < (uint) entries.Length);
                i = nexts.UnsafeGetAt(i))
            {
                if (value.Equals(entries[i]))
                {
                    return ref entries[i];
                }

                if (collisionCount == entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }

                collisionCount++;
            }

            return ref _defaultValue;
        }

        public ref readonly T TryGetValueIdx(in T value, out int index)
        {
            T[] entries = _entries;
            int[] nexts = _nexts;
            int collisionCount = 0;
            for (int i = _buckets[value.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint) i < (uint) entries.Length);
                i = nexts.UnsafeGetAt(i))
            {
                if (value.Equals(entries[i]))
                {
                    index = i;
                    return ref entries[i];

                }

                if (collisionCount == entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }

                collisionCount++;
            }

            index = -1;
            return ref _defaultValue;
        }

        /// <summary>
        /// Removes the entry if present with the specified key.
        /// </summary>
        /// <param name="key">Key to look for</param>
        /// <returns>true if the key is present, false if it is not</returns>
        public bool Remove(in T key)
        {
            T[] entries = _entries;
            int[] nexts = _nexts;
            int bucketIndex = key.GetHashCode() & (_buckets.Length - 1);
            int entryIndex = _buckets[bucketIndex] - 1;

            int lastIndex = -1;
            int collisionCount = 0;
            while (entryIndex != -1)
            {
                T candidate = entries[entryIndex];
                int candidateNext = nexts.UnsafeGetAt(entryIndex);
                if (candidate.Equals(key))
                {
                    if (lastIndex != -1)
                    {
                        // Fixup preceding element in chain to point to next (if any)
                        nexts.UnsafeGetAt(lastIndex) = candidateNext;
                    }
                    else
                    {
                        // Fixup bucket to new head (if any)
                        _buckets[bucketIndex] = candidateNext + 1;
                    }

                    entries[entryIndex] = default!;

                    nexts.UnsafeGetAt(entryIndex) = -3 - _freeList; // New head of free list
                    _freeList = entryIndex;

                    _count--;
                    return true;
                }

                lastIndex = entryIndex;
                entryIndex = candidateNext;

                if (collisionCount == entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }

                collisionCount++;
            }

            return false;
        }

        // Not safe for concurrent _reads_ (at least, if either of them add)
        // For concurrent reads, prefer TryGetValue(key, out value)
        /// <summary>
        /// Gets the value for the specified key, or, if the key is not present,
        /// adds an entry and returns the value by ref. This makes it possible to
        /// add or update a value in a single look up operation.
        /// </summary>
        /// <param name="value">Value to look for</param>
        /// <returns>Reference to the new or existing value</returns>
        [return: MaybeNull]
        public ref T GetOrAddValueRef(in T value, out int index)
        {
#pragma warning disable 8619
            T[] entries = _entries;
            int[] nexts = _nexts;
            int collisionCount = 0;
            int bucketIndex = value.GetHashCode() & (_buckets.Length - 1);
            for (int i = _buckets[bucketIndex] - 1;
                unchecked ((uint) i < (uint) entries.Length);
                i = nexts.UnsafeGetAt(i))
            {
                if (value.Equals(entries[i]))
                {
                    index = i;
                    return ref entries[i];
                }
                if (collisionCount == entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }

                collisionCount++;
            }

            return ref Add(value, bucketIndex, out index);
#pragma warning restore 8619
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private ref T Add(T value, int bucketIndex, out int index)
        {
            T[] entries = _entries;
            int[] nexts = _nexts;
            int entryIndex;
            if (_freeList != -1)
            {
                entryIndex = _freeList;
                _freeList = -3 - nexts.UnsafeGetAt(_freeList);
            }
            else
            {
                if (_count == entries.Length || entries.Length == 1)
                {
                    (entries, nexts) = Resize();
                    bucketIndex = value.GetHashCode() & (_buckets.Length - 1);
                    // entry indexes were not changed by Resize
                }

                entryIndex = _count;
            }

            entries.UnsafeGetAt(entryIndex) = value;
            nexts.UnsafeGetAt(entryIndex) = _buckets[bucketIndex] - 1;
            _buckets.UnsafeGetAt(bucketIndex) = entryIndex + 1;
            _count++;
            index = entryIndex;
            return ref entries.UnsafeGetAt(entryIndex);
        }

        private (T[],int[]) Resize()
        {
            Debug.Assert(_entries.Length == _count || _entries.Length == 1); // We only copy _count, so if it's longer we will miss some
            int count = _count;
            int newSize = _entries.Length * 2;
            if ((uint) newSize > (uint) int.MaxValue) // uint cast handles overflow
                throw new InvalidOperationException("Capacity Overflow");

            var entries = new T[newSize];
            var nexts = new int[newSize];
            Array.Copy(_entries, 0, entries, 0, count);
            Array.Copy(_nexts, 0, nexts, 0, count);

            var newBuckets = new int[entries.Length];
            while (count-- > 0)
            {
                int bucketIndex = entries[count].GetHashCode() & (newBuckets.Length - 1);
                nexts[count] = newBuckets[bucketIndex] - 1;
                newBuckets[bucketIndex] = count + 1;
            }

            _buckets = newBuckets;
            _entries = entries;
            _nexts = nexts;

            OnCapacityChange?.Invoke(entries);

            return (entries, nexts);
        }

        /// <summary>
        /// Gets an enumerator over the dictionary
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(this); // avoid boxing

        /// <summary>
        /// Gets an enumerator over the dictionary
        /// </summary>
        IEnumerator<T> IEnumerable<T>.GetEnumerator() =>
            new Enumerator(this);

        /// <summary>
        /// Gets an enumerator over the dictionary
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Enumerator
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly HashSetSlim<T> _hashSetSlim;
            private int _index;
            private int _count;
            private T _current;

            internal Enumerator(HashSetSlim<T> hashSetSlim)
            {
                _hashSetSlim = hashSetSlim;
                _index = 0;
                _count = _hashSetSlim._count;
                _current = default!;
            }

            /// <summary>
            /// Move to next
            /// </summary>
            public bool MoveNext()
            {
                if (_count == 0)
                {
                    _current = default!;
                    return false;
                }

                _count--;

                while (_hashSetSlim._nexts[_index] < -1)
                    _index++;

                _current = _hashSetSlim._entries[_index++];
                return true;
            }

            /// <summary>
            /// Get current value
            /// </summary>
            public T Current => _current;

            object IEnumerator.Current => _current;

            void IEnumerator.Reset()
            {
                _index = 0;
                _count = _hashSetSlim._count;
                _current = default!;
            }

            /// <summary>
            /// Dispose the enumerator
            /// </summary>
            public void Dispose()
            {
            }
        }
    }

    internal sealed class HashSetDebugView<T> where T : struct, IEquatable<T>
    {
        private readonly HashSetSlim<T> _hashSetSlim;

        public HashSetDebugView(HashSetSlim<T> hashSetSlim)
        {
            _hashSetSlim = hashSetSlim ?? throw new ArgumentNullException(nameof(hashSetSlim));
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items => _hashSetSlim.ToArray();
    }
}
