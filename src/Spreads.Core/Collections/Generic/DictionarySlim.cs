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
using static Spreads.Utils.Constants;

namespace Spreads.Collections.Generic
{
    /// <summary>
    /// A lightweight Dictionary with three principal differences compared to <see cref="Dictionary{TKey, TValue}"/>
    ///
    /// 1) It is possible to do "get or add" in a single lookup using <see cref="GetOrAddValueRef"/>. For
    /// values that are value types, this also saves a copy of the value.
    /// 2) It assumes it is cheap to equate values.
    /// 3) It assumes the keys implement <see cref="IEquatable{TKey}"/> or else Equals() and they are cheap and sufficient.
    /// </summary>
    /// <remarks>
    /// 1) This avoids having to do separate lookups (<see cref="Dictionary{TKey, TValue}.TryGetValue(TKey, out TValue)"/>
    /// followed by <see cref="Dictionary{TKey, TValue}.Add(TKey, TValue)"/>.
    /// There is not currently an API exposed to get a value by ref without adding if the key is not present.
    /// 2) This means it can save space by not storing hash codes.
    /// 3) This means it can avoid storing a comparer, and avoid the likely virtual call to a comparer.
    /// </remarks>
    [DebuggerTypeProxy(typeof(DictionarySlimDebugView<,>))]
    [DebuggerDisplay("Count = {" + nameof(Count) + "}")]
    public partial class DictionarySlim<TKey, TValue> : IReadOnlyCollection<KeyValuePair<TKey, TValue>> where TKey : IEquatable<TKey>
    {
        private readonly TValue _defaultValue;

        // We want to initialize without allocating arrays. We also want to avoid null checks.
        // Array.Empty would give divide by zero in modulo operation. So we use static one element arrays.
        // The first add will cause a resize replacing these with real arrays of three elements.
        // Arrays are wrapped in a class to avoid being duplicated for each <TKey, TValue>
        private static readonly Entry[] InitialEntries = new Entry[1];

        private int _count;

        // 0-based index into _entries of head of free chain: -1 means empty
        private int _freeList = -1;

        // 1-based index into _entries; 0 means empty
        private int[] _buckets;
        private Entry[] _entries;

        [DebuggerDisplay("({Key}, {Value})->{Next}")]
        private struct Entry
        {
            public TKey Key;

            public TValue Value;

            // 0-based index of next entry in chain: -1 means end of chain
            // also encodes whether this entry _itself_ is part of the free list by changing sign and subtracting 3,
            // so -2 means end of free list, -3 means index 0 but on free list, -4 means index 1 but on free list, etc.
            public int Next;
        }

        /// <summary>
        /// Construct with default capacity.
        /// </summary>
        public DictionarySlim(TValue defaultValue = default!)
        {
            _buckets = HashHelpers.SizeOneIntArray;
            _entries = InitialEntries;
            _defaultValue = defaultValue;
        }

        /// <summary>
        /// Construct with at least the specified capacity for
        /// entries before resizing must occur.
        /// </summary>
        /// <param name="capacity">Requested minimum capacity</param>
        /// <param name="defaultValue"></param>
        public DictionarySlim(int capacity, TValue defaultValue = default!)
        {
            if (capacity < 0)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.value, ExceptionResource.ArgumentOutOfRange_SmallCapacity);
            if (capacity < 2)
                capacity = 2; // 1 would indicate the dummy array
            capacity = HashHelpers.PowerOf2(capacity);
            _buckets = new int[capacity];
            _entries = new Entry[capacity];
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
        }

        /// <summary>
        /// Looks for the specified key in the dictionary.
        /// </summary>
        /// <param name="key">Key to look for</param>
        /// <returns>true if the key is present, otherwise false</returns>
        public bool ContainsKey(in TKey key)
        {
            Entry[] entries = _entries;
            int collisionCount = 0;
            for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint)i < (uint)entries.Length);
                i = entries[i].Next)
            {
                if (key.Equals(entries[i].Key))
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
        /// <param name="key">Key to look for</param>
        /// <param name="value">Value found, otherwise default(TValue)</param>
        /// <returns>true if the key is present, otherwise false</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            Entry[] entries = _entries;
            int collisionCount = 0;
            for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint)i) < (uint)entries.Length;
                i = entries[i].Next)
            {
                if (key.Equals(entries[i].Key))
                {
                    value = entries[i].Value;
                    return true;
                }

                if (collisionCount == entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }

                collisionCount++;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Assumes no concurrent updates are possible.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        internal bool DangerousTryGetValue(TKey key, out TValue value)
        {
            Entry[] entries = _entries;

            for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint)i) < (uint)entries.Length;
                i = entries[i].Next)
            {
                if (key.Equals(entries[i].Key))
                {
                    value = entries[i].Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Gets the value if present for the specified key.
        /// </summary>
        /// <param name="key">Key to look for</param>
        /// <param name="found"></param>
        /// <returns>true if the key is present, otherwise false</returns>
        public ref readonly TValue TryGetValueRef(TKey key, out bool found)
        {
            Entry[] entries = _entries;
            int collisionCount = 0;
            for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint)i < (uint)entries.Length);
                i = entries[i].Next)
            {
                if (key.Equals(entries[i].Key))
                {
                    found = true;
                    return ref entries[i].Value;
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

        /// <summary>
        /// Assumes no concurrent updates are possible.
        /// </summary>
        [MethodImpl(MethodImplAggressiveAll)]
        internal ref readonly TValue DangerousTryGetValueRef(TKey key, out bool found)
        {
            Entry[] entries = _entries;
            for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint)i < (uint)entries.Length);
                i = entries[i].Next)
            {
                if (key.Equals(entries[i].Key))
                {
                    found = true;
                    return ref entries[i].Value;
                }
            }

            found = false;
            return ref _defaultValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref readonly TValue GetOrDefault(TKey key)
        {
            Entry[] entries = _entries;
            int collisionCount = 0;
            for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint)i < (uint)entries.Length);
                i = entries[i].Next)
            {
                if (key.Equals(entries[i].Key))
                {
                    return ref entries[i].Value;
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
        public ref readonly TValue GetOrDefault(in TKey key)
        {
            Entry[] entries = _entries;
            int collisionCount = 0;
            for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint)i < (uint)entries.Length);
                i = entries[i].Next)
            {
                if (key.Equals(entries[i].Key))
                {
                    return ref entries[i].Value;
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

        public ref readonly TValue TryGetValueIdx(in TKey key, out int index)
        {
            Entry[] entries = _entries;
            int collisionCount = 0;
            for (int i = _buckets[key.GetHashCode() & (_buckets.Length - 1)] - 1;
                unchecked((uint)i < (uint)entries.Length);
                i = entries[i].Next)
            {
                if (key.Equals(entries[i].Key))
                {
                    index = i;
                    return ref entries[i].Value;

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
        public bool Remove(in TKey key)
        {
            Entry[] entries = _entries;
            int bucketIndex = key.GetHashCode() & (_buckets.Length - 1);
            int entryIndex = _buckets[bucketIndex] - 1;

            int lastIndex = -1;
            int collisionCount = 0;
            while (entryIndex != -1)
            {
                Entry candidate = entries[entryIndex];
                if (candidate.Key.Equals(key))
                {
                    if (lastIndex != -1)
                    {
                        // Fixup preceding element in chain to point to next (if any)
                        entries[lastIndex].Next = candidate.Next;
                    }
                    else
                    {
                        // Fixup bucket to new head (if any)
                        _buckets[bucketIndex] = candidate.Next + 1;
                    }

                    entries[entryIndex] = default;

                    entries[entryIndex].Next = -3 - _freeList; // New head of free list
                    _freeList = entryIndex;

                    _count--;
                    return true;
                }

                lastIndex = entryIndex;
                entryIndex = candidate.Next;

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
        /// <param name="key">Key to look for</param>
        /// <returns>Reference to the new or existing value</returns>
        [return: MaybeNull]
        public ref TValue GetOrAddValueRef(in TKey key)
        {
#pragma warning disable 8619
            Entry[] entries = _entries;
            int collisionCount = 0;
            int bucketIndex = key.GetHashCode() & (_buckets.Length - 1);
            for (int i = _buckets[bucketIndex] - 1;
                unchecked((uint)i < (uint)entries.Length);
                i = entries[i].Next)
            {
                if (key.Equals(entries[i].Key))
                    return ref entries[i].Value;
                if (collisionCount == entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    ThrowHelper.ThrowInvalidOperationException_ConcurrentOperationsNotSupported();
                }

                collisionCount++;
            }

            return ref AddKey(key, bucketIndex);
#pragma warning restore 8619
        }

        public void Add(TKey key, TValue value)
        {
            if (ContainsKey(in key))
                ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);
            GetOrAddValueRef(in key) = value;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private ref TValue AddKey(TKey key, int bucketIndex)
        {
            Entry[] entries = _entries;
            int entryIndex;
            if (_freeList != -1)
            {
                entryIndex = _freeList;
                _freeList = -3 - entries[_freeList].Next;
            }
            else
            {
                if (_count == entries.Length || entries.Length == 1)
                {
                    entries = Resize();
                    bucketIndex = key.GetHashCode() & (_buckets.Length - 1);
                    // entry indexes were not changed by Resize
                }

                entryIndex = _count;
            }

            entries[entryIndex].Key = key;
            entries[entryIndex].Next = _buckets[bucketIndex] - 1;
            _buckets[bucketIndex] = entryIndex + 1;
            _count++;
            return ref entries[entryIndex].Value;
        }

        private Entry[] Resize()
        {
            Debug.Assert(_entries.Length == _count || _entries.Length == 1); // We only copy _count, so if it's longer we will miss some
            int count = _count;
            int newSize = _entries.Length * 2;
            if ((uint)newSize > (uint)int.MaxValue) // uint cast handles overflow
                throw new InvalidOperationException("Capacity Overflow");

            var entries = new Entry[newSize];
            Array.Copy(_entries, 0, entries, 0, count);

            var newBuckets = new int[entries.Length];
            while (count-- > 0)
            {
                int bucketIndex = entries[count].Key.GetHashCode() & (newBuckets.Length - 1);
                entries[count].Next = newBuckets[bucketIndex] - 1;
                newBuckets[bucketIndex] = count + 1;
            }

            _buckets = newBuckets;
            _entries = entries;

            return entries;
        }

        /// <summary>
        /// Gets an enumerator over the dictionary
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(this); // avoid boxing

        /// <summary>
        /// Gets an enumerator over the dictionary
        /// </summary>
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() =>
            new Enumerator(this);

        /// <summary>
        /// Gets an enumerator over the dictionary
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Enumerator
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly DictionarySlim<TKey, TValue> _dictionary;
            private int _index;
            private int _count;
            private KeyValuePair<TKey, TValue> _current;

            internal Enumerator(DictionarySlim<TKey, TValue> dictionary)
            {
                _dictionary = dictionary;
                _index = 0;
                _count = _dictionary._count;
                _current = default;
            }

            /// <summary>
            /// Move to next
            /// </summary>
            public bool MoveNext()
            {
                if (_count == 0)
                {
                    _current = default;
                    return false;
                }

                _count--;

                while (_dictionary._entries[_index].Next < -1)
                    _index++;

                _current = new KeyValuePair<TKey, TValue>(
                    _dictionary._entries[_index].Key,
                    _dictionary._entries[_index++].Value);
                return true;
            }

            /// <summary>
            /// Get current value
            /// </summary>
            public KeyValuePair<TKey, TValue> Current => _current;

            object IEnumerator.Current => _current;

            void IEnumerator.Reset()
            {
                _index = 0;
                _count = _dictionary._count;
                _current = default;
            }

            /// <summary>
            /// Dispose the enumerator
            /// </summary>
            public void Dispose()
            {
            }
        }
    }

    internal sealed class DictionarySlimDebugView<K, V> where K : struct, IEquatable<K>
    {
        private readonly DictionarySlim<K, V> _dictionary;

        public DictionarySlimDebugView(DictionarySlim<K, V> dictionary)
        {
            _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public KeyValuePair<K, V>[] Items => _dictionary.ToArray();
    }
}
