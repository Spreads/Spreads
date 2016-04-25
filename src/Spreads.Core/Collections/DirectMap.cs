// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using Spreads.Collections;
using static Spreads.Collections.SyncUtils;

namespace Spreads.Experimental.Collections.Generic {
    [DebuggerTypeProxy(typeof(IDictionaryDebugView<,>))]
    [DebuggerDisplay("Count = {Count}")]
    [System.Runtime.InteropServices.ComVisible(false)]
    public class DirectMap<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, IReadOnlyDictionary<TKey, TValue> {
        private struct Entry {
            public int hashCode;    // Lower 31 bits of hash code, -1 if unused
            public int next;        // Index of next entry, -1 if last
            public TKey key;           // Key of entry
            public TValue value;         // Value of entry
        }

        private DirectArray<int> buckets;
        private DirectArray<Entry> entries;

        // buckets header has
        // Slot0 - locker
        // Slot1 - version
        // Slot2 - nextVersion
        // Slot3 - count
        // Slot4 - freeList
        // Slot5 - freeCount

        private unsafe int count
        {
            get { return *(int*)(buckets.Slot3); }
            set { *(int*)(buckets.Slot3) = value; }
        }

        private unsafe long version => Volatile.Read(ref *(long*)(buckets.Slot1));

        private unsafe int freeList
        {
            get { return *(int*)(buckets.Slot4); }
            set { *(int*)(buckets.Slot4) = value; }
        }
        private unsafe int freeCount
        {
            get
            {
                var value = Volatile.Read(ref *(int*)(buckets.Slot5));
                return value;
            }
            set { *(int*)(buckets.Slot5) = value; }
        }


        private IEqualityComparer<TKey> comparer;
        private KeyCollection keys;
        private ValueCollection values;
        private Object _syncRoot;
        private string _fileName;

        // constants for serialization
        private const String VersionName = "Version";
        private const String HashSizeName = "HashSize";  // Must save buckets.Length
        private const String KeyValuePairsName = "KeyValuePairs";
        private const String ComparerName = "Comparer";

        public DirectMap(string fileName) : this(fileName, 0, null) { }

        public DirectMap(string fileName, int capacity) : this(fileName, capacity, null) { }

        public DirectMap(string fileName, IEqualityComparer<TKey> comparer) : this(fileName, 0, comparer) { }

        public DirectMap(string fileName, int capacity, IEqualityComparer<TKey> comparer) {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            _fileName = fileName;
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "ArgumentOutOfRange_NeedNonNegNum");
            if (capacity >= 0) Initialize(capacity);
            this.comparer = comparer ?? EqualityComparer<TKey>.Default;

        }

        public DirectMap(string fileName, IDictionary<TKey, TValue> dictionary) : this(fileName, dictionary, null) { }

        public DirectMap(string fileName, IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) :
            this(fileName, dictionary != null ? dictionary.Count : 0, comparer) {
            if (dictionary == null) {
                throw new ArgumentNullException(nameof(dictionary));
            }

            // It is likely that the passed-in dictionary is Dictionary<TKey,TValue>. When this is the case,
            // avoid the enumerator allocation and overhead by looping through the entries array directly.
            // We only do this when dictionary is Dictionary<TKey,TValue> and not a subclass, to maintain
            // back-compat with subclasses that may have overridden the enumerator behavior.
            if (dictionary.GetType() == typeof(DirectMap<TKey, TValue>)) {
                DirectMap<TKey, TValue> d = (DirectMap<TKey, TValue>)dictionary;
                int count = d.count;
                DirectArray<Entry> entries = d.entries;
                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0) {
                        Add(entries[i].key, entries[i].value);
                    }
                }
                return;
            }

            foreach (KeyValuePair<TKey, TValue> pair in dictionary) {
                Add(pair.Key, pair.Value);
            }
        }

        public IEqualityComparer<TKey> Comparer
        {
            get
            {
                return comparer;
            }
        }

        public int Count => SyncUtils.ReadLockIf(buckets.Slot2, buckets.Slot1, true, () => count - freeCount);

        public KeyCollection Keys
        {
            get
            {
                Contract.Ensures(Contract.Result<KeyCollection>() != null);
                if (keys == null) keys = new KeyCollection(this);
                return keys;
            }
        }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys
        {
            get
            {
                if (keys == null) keys = new KeyCollection(this);
                return keys;
            }
        }

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
        {
            get
            {
                if (keys == null) keys = new KeyCollection(this);
                return keys;
            }
        }

        public ValueCollection Values
        {
            get
            {
                Contract.Ensures(Contract.Result<ValueCollection>() != null);
                if (values == null) values = new ValueCollection(this);
                return values;
            }
        }

        ICollection<TValue> IDictionary<TKey, TValue>.Values
        {
            get
            {
                if (values == null) values = new ValueCollection(this);
                return values;
            }
        }

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
        {
            get
            {
                if (values == null) values = new ValueCollection(this);
                return values;
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                return ReadLockIf(buckets.Slot2, buckets.Slot1, true, () => {
                    int i = FindEntry(key);
                    if (i >= 0) return entries[i].value;
                    throw new KeyNotFoundException();
                });
            }
            set
            {
                //try {
                //    EnterWriteLock(buckets.Slot0);
                //    Insert(key, value, false);
                //} finally {
                //    ExitWriteLock(buckets.Slot0);
                //}
                WriteLock(buckets.Slot0, (cleanup) => Insert(key, value, false));
            }
        }

        public void Add(TKey key, TValue value) {
            try {
                EnterWriteLock(buckets.Slot0);
                Insert(key, value, true);
            } finally {
                ExitWriteLock(buckets.Slot0);
            }
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair) {
            Add(keyValuePair.Key, keyValuePair.Value);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair) {
            int i = FindEntry(keyValuePair.Key);
            if (i >= 0 && EqualityComparer<TValue>.Default.Equals(entries[i].value, keyValuePair.Value)) {
                return true;
            }
            return false;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair) {
            try {
                EnterWriteLock(buckets.Slot0);
                int i = FindEntry(keyValuePair.Key);
                if (i >= 0 && EqualityComparer<TValue>.Default.Equals(entries[i].value, keyValuePair.Value)) {
                    DoRemove(keyValuePair.Key);
                    return true;
                }
                return false;
            } finally {
                ExitWriteLock(buckets.Slot0);
            }

        }

        public void Clear() {
            try {
                EnterWriteLock(buckets.Slot0);
                if (count > 0) {
                    for (int i = 0; i < buckets.Count; i++) buckets[i] = -1;
                    entries.Clear();
                    freeList = -1;
                    count = 0;
                    freeCount = 0;
                    //version++;
                }
            } finally {
                ExitWriteLock(buckets.Slot0);
            }
        }

        public bool ContainsKey(TKey key) {
            return FindEntry(key) >= 0;
        }

        public bool ContainsValue(TValue value) {
            if (value == null) {
                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0 && entries[i].value == null) return true;
                }
            } else {
                EqualityComparer<TValue> c = EqualityComparer<TValue>.Default;
                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0 && c.Equals(entries[i].value, value)) return true;
                }
            }
            return false;
        }

        private void CopyTo(KeyValuePair<TKey, TValue>[] array, int index) {
            try {
                EnterWriteLock(buckets.Slot0);
                if (array == null) {
                    throw new ArgumentNullException(nameof(array));
                }

                if (index < 0 || index > array.Length) {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "ArgumentOutOfRange_Index");
                }

                if (array.Length - index < Count) {
                    throw new ArgumentException("Arg_ArrayPlusOffTooSmall");
                }

                int count = this.count;
                DirectArray<Entry> entries = this.entries;
                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0) {
                        array[index++] = new KeyValuePair<TKey, TValue>(entries[i].key, entries[i].value);
                    }
                }
            } finally {
                ExitWriteLock(buckets.Slot0);
            }

        }

        public Enumerator GetEnumerator() {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindEntry(TKey key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            if (buckets != null) {
                int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
                for (int i = buckets[hashCode % buckets.Count]; i >= 0; i = entries[i].next) {
                    if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key)) return i;
                }
            }
            return -1;
        }

        private void Initialize(int capacity) {
            int size = HashHelpers.GetPrime(capacity);
            buckets = new DirectArray<int>(_fileName + "-buckets", size);
            for (int i = 0; i < buckets.Count; i++) buckets[i] = -1;
            entries = new DirectArray<Entry>(_fileName + "-entries", size);
            freeList = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Insert(TKey key, TValue value, bool add) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            if (buckets == null) Initialize(0);
            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
            int targetBucket = hashCode % buckets.Count;


            for (int i = buckets[targetBucket]; i >= 0; i = entries[i].next) {
                if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key)) {
                    if (add) {
                        throw new ArgumentException("Format(Argument_AddingDuplicate, key)");
                    }

                    var entry = entries[i];
                    
                    // make a defensive copy
                    entries[-1] = entry;
                    Thread.MemoryBarrier();

                    entry.value = value;
                    entries[i] = entry;
                    //version++;
                    return;
                }
            }

            int index;

            if (freeCount > 0) {
                index = freeList;
                freeList = entries[index].next;
                freeCount--;
            } else {
                if (count == entries.Count) {
                    Resize();
                    targetBucket = hashCode % buckets.Count;
                }
                index = count;
                count++;
            }

            var entry1 = entries[index];
            // TODO do not need a defentive copy here, the new slot is clean
            // make a defensive copy
            //entries[-1] = entry1;
            //entries.Copy(index, -1);
            //Thread.MemoryBarrier();

            entry1.hashCode = hashCode;
            entry1.next = buckets[targetBucket];
            entry1.key = key;
            entry1.value = value;
            entries[index] = entry1;
            buckets[targetBucket] = index;
            //version++;


        }

        private void Resize() {
            Resize(HashHelpers.ExpandPrime(count));
        }

        private void Resize(int newSize) {
            Contract.Assert(newSize >= entries.Count);
            buckets.Grow(newSize);
            entries.Grow(newSize);

            // Todo either incremental rehashing (Redis), or generational hashing
            for (int i = 0; i < buckets.Count; i++) buckets[i] = -1;
            for (int i = 0; i < count; i++) {
                if (entries[i].hashCode >= 0) {
                    int bucket = entries[i].hashCode % newSize;
                    var entry = entries[i];
                    entry.next = buckets[bucket];
                    entries[i] = entry;
                    buckets[bucket] = i;
                }
            }
        }

        public bool Remove(TKey key) {
            try {
                EnterWriteLock(buckets.Slot0);
                return DoRemove(key);
            } finally {
                ExitWriteLock(buckets.Slot0);
            }
        }

        private bool DoRemove(TKey key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            if (buckets != null) {
                int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
                int bucket = hashCode % buckets.Count;
                int last = -1;
                for (int i = buckets[bucket]; i >= 0; last = i, i = entries[i].next) {
                    if (entries[i].hashCode == hashCode && comparer.Equals(entries[i].key, key)) {
                        // TODO set is reverse to remove.
                        // On recover need to re-clean entries[freeList] and 
                        // just add the defensive copy back
                        // entries[freeList] remains free, must erase KV

                        // make a defensive copy
                        entries.Copy(i, -1);
                        // TODO write automic flag so that we will know our copy is made
                        // without a flag, assume we haven't deleted anything
                        Thread.MemoryBarrier();

                        if (last < 0) {
                            // TODO just write ints with correct offsets
                            buckets[bucket] = entries[i].next;
                        } else {
                            var lastEntry = entries[last];
                            // TODO just write ints with correct offsets
                            lastEntry.next = entries[i].next;
                            entries[last] = lastEntry;
                        }
                        // by this line, we have removed an entry from a linked list
                        // we must store a copy before this place and then
                        // just add KV

                        // if we failed before, 

                        var entry1 = new Entry<TKey, TValue>();
                        entry1.hashCode = -1;
                        // todo 
                        entry1.next = freeList;
                        entry1.key = default(TKey);
                        entry1.value = default(TValue);
                        entries[i] = entry1;
                        freeList = i;
                        freeCount++;
                        //version++;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value) {
            int i = FindEntry(key);
            if (i >= 0) {
                value = entries[i].value;
                return true;
            }
            value = default(TValue);
            return false;
        }

        // This is a convenience method for the internal callers that were converted from using Hashtable.
        // Many were combining key doesn't exist and key exists but null value (for non-value types) checks.
        // This allows them to continue getting that behavior with minimal code delta. This is basically
        // TryGetValue without the out param
        internal TValue GetValueOrDefault(TKey key) {
            int i = FindEntry(key);
            if (i >= 0) {
                return entries[i].value;
            }
            return default(TValue);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index) {
            CopyTo(array, index);
        }

        void ICollection.CopyTo(Array array, int index) {
            if (array == null) {
                throw new ArgumentNullException(nameof(array));
            }

            if (array.Rank != 1) {
                throw new ArgumentException("Arg_RankMultiDimNotSupported, nameof(array)");
            }

            if (array.GetLowerBound(0) != 0) {
                throw new ArgumentException("Arg_NonZeroLowerBound, nameof(array)");
            }

            if (index < 0 || index > array.Length) {
                throw new ArgumentOutOfRangeException(nameof(index), index, "ArgumentOutOfRange_Index");
            }

            if (array.Length - index < Count) {
                throw new ArgumentException("Arg_ArrayPlusOffTooSmall");
            }

            KeyValuePair<TKey, TValue>[] pairs = array as KeyValuePair<TKey, TValue>[];
            if (pairs != null) {
                CopyTo(pairs, index);
            } else if (array is DictionaryEntry[]) {
                DictionaryEntry[] dictEntryArray = array as DictionaryEntry[];
                var entries = this.entries;

                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0) {
                        dictEntryArray[index++] = new DictionaryEntry(entries[i].key, entries[i].value);
                    }
                }
            } else {
                object[] objects = array as object[];
                if (objects == null) {
                    throw new ArgumentException("Argument_InvalidArrayType, nameof(array)");
                }

                try {
                    int count = this.count;
                    var entries = this.entries;
                    for (int i = 0; i < count; i++) {
                        if (entries[i].hashCode >= 0) {
                            objects[index++] = new KeyValuePair<TKey, TValue>(entries[i].key, entries[i].value);
                        }
                    }
                } catch (ArrayTypeMismatchException) {
                    throw new ArgumentException("Argument_InvalidArrayType, nameof(array)");
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }

        bool ICollection.IsSynchronized => true;

        object ICollection.SyncRoot
        {
            get
            {
                if (_syncRoot == null) {
                    System.Threading.Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);
                }
                return _syncRoot;
            }
        }

        bool IDictionary.IsFixedSize => false;

        bool IDictionary.IsReadOnly => false;

        ICollection IDictionary.Keys => (ICollection)Keys;

        ICollection IDictionary.Values => (ICollection)Values;

        object IDictionary.this[object key]
        {
            get
            {
                if (IsCompatibleKey(key)) {
                    int i = FindEntry((TKey)key);
                    if (i >= 0) {
                        return entries[i].value;
                    }
                }
                return null;
            }
            set
            {
                if (key == null) {
                    throw new ArgumentNullException(nameof(key));
                }
                if (value == null && !(default(TValue) == null))
                    throw new ArgumentNullException(nameof(value));

                try {
                    TKey tempKey = (TKey)key;
                    try {
                        this[tempKey] = (TValue)value;
                    } catch (InvalidCastException) {
                        throw new ArgumentException("Format(Arg_WrongType, value, typeof(TValue)), nameof(value)");
                    }
                } catch (InvalidCastException) {
                    throw new ArgumentException("Format(Arg_WrongType, key, typeof(TKey)), nameof(key)");
                }
            }
        }

        private static bool IsCompatibleKey(object key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }
            return (key is TKey);
        }

        void IDictionary.Add(object key, object value) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null && !(default(TValue) == null))
                throw new ArgumentNullException(nameof(value));

            try {
                TKey tempKey = (TKey)key;

                try {
                    Add(tempKey, (TValue)value);
                } catch (InvalidCastException) {
                    throw new ArgumentException("Format(Arg_WrongType, value, typeof(TValue)), nameof(value)");
                }
            } catch (InvalidCastException) {
                throw new ArgumentException("Format(Arg_WrongType, key, typeof(TKey)), nameof(key)");
            }
        }

        bool IDictionary.Contains(object key) {
            if (IsCompatibleKey(key)) {
                return ContainsKey((TKey)key);
            }

            return false;
        }

        IDictionaryEnumerator IDictionary.GetEnumerator() {
            return new Enumerator(this, Enumerator.DictEntry);
        }

        void IDictionary.Remove(object key) {
            if (IsCompatibleKey(key)) {
                Remove((TKey)key);
            }
        }

        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>,
            IDictionaryEnumerator {
            private DirectMap<TKey, TValue> _directMap;
            private long version;
            private int index;
            private KeyValuePair<TKey, TValue> current;
            private int getEnumeratorRetType;  // What should Enumerator.Current return?

            internal const int DictEntry = 1;
            internal const int KeyValuePair = 2;

            internal Enumerator(DirectMap<TKey, TValue> _directMap, int getEnumeratorRetType) {
                this._directMap = _directMap;
                version = _directMap.version;
                index = 0;
                this.getEnumeratorRetType = getEnumeratorRetType;
                current = new KeyValuePair<TKey, TValue>();
            }

            public bool MoveNext() {
                if (version != _directMap.version) {
                    throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                }

                // Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
                // dictionary.count+1 could be negative if dictionary.count is Int32.MaxValue
                while ((uint)index < (uint)_directMap.count) {
                    if (_directMap.entries[index].hashCode >= 0) {
                        current = new KeyValuePair<TKey, TValue>(_directMap.entries[index].key, _directMap.entries[index].value);
                        index++;
                        return true;
                    }
                    index++;
                }

                index = _directMap.count + 1;
                current = new KeyValuePair<TKey, TValue>();
                return false;
            }

            public KeyValuePair<TKey, TValue> Current
            {
                get { return current; }
            }

            public void Dispose() {
            }

            object IEnumerator.Current
            {
                get
                {
                    if (index == 0 || (index == _directMap.count + 1)) {
                        throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                    }

                    if (getEnumeratorRetType == DictEntry) {
                        return new System.Collections.DictionaryEntry(current.Key, current.Value);
                    } else {
                        return new KeyValuePair<TKey, TValue>(current.Key, current.Value);
                    }
                }
            }

            void IEnumerator.Reset() {
                if (version != _directMap.version) {
                    throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                }

                index = 0;
                current = new KeyValuePair<TKey, TValue>();
            }

            DictionaryEntry IDictionaryEnumerator.Entry
            {
                get
                {
                    if (index == 0 || (index == _directMap.count + 1)) {
                        throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                    }

                    return new DictionaryEntry(current.Key, current.Value);
                }
            }

            object IDictionaryEnumerator.Key
            {
                get
                {
                    if (index == 0 || (index == _directMap.count + 1)) {
                        throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                    }

                    return current.Key;
                }
            }

            object IDictionaryEnumerator.Value
            {
                get
                {
                    if (index == 0 || (index == _directMap.count + 1)) {
                        throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                    }

                    return current.Value;
                }
            }
        }

        [DebuggerTypeProxy(typeof(DictionaryKeyCollectionDebugView<,>))]
        [DebuggerDisplay("Count = {Count}")]
        public sealed class KeyCollection : ICollection<TKey>, ICollection, IReadOnlyCollection<TKey> {
            private DirectMap<TKey, TValue> _directMap;

            public KeyCollection(DirectMap<TKey, TValue> _directMap) {
                if (_directMap == null) {
                    throw new ArgumentNullException(nameof(_directMap));
                }
                this._directMap = _directMap;
            }

            public Enumerator GetEnumerator() {
                return new Enumerator(_directMap);
            }

            public void CopyTo(TKey[] array, int index) {
                if (array == null) {
                    throw new ArgumentNullException(nameof(array));
                }

                if (index < 0 || index > array.Length) {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "ArgumentOutOfRange_Index");
                }

                if (array.Length - index < _directMap.Count) {
                    throw new ArgumentException("Arg_ArrayPlusOffTooSmall");
                }

                int count = _directMap.count;
                var entries = _directMap.entries;

                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0) array[index++] = entries[i].key;
                }
            }

            public int Count
            {
                get { return _directMap.Count; }
            }

            bool ICollection<TKey>.IsReadOnly
            {
                get { return true; }
            }

            void ICollection<TKey>.Add(TKey item) {
                throw new NotSupportedException("NotSupported_KeyCollectionSet");
            }

            void ICollection<TKey>.Clear() {
                throw new NotSupportedException("NotSupported_KeyCollectionSet");
            }

            bool ICollection<TKey>.Contains(TKey item) {
                return _directMap.ContainsKey(item);
            }

            bool ICollection<TKey>.Remove(TKey item) {
                throw new NotSupportedException("NotSupported_KeyCollectionSet");
            }

            IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() {
                return new Enumerator(_directMap);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return new Enumerator(_directMap);
            }

            void ICollection.CopyTo(Array array, int index) {
                if (array == null) {
                    throw new ArgumentNullException(nameof(array));
                }

                if (array.Rank != 1) {
                    throw new ArgumentException("Arg_RankMultiDimNotSupported, nameof(array)");
                }

                if (array.GetLowerBound(0) != 0) {
                    throw new ArgumentException("Arg_NonZeroLowerBound, nameof(array)");
                }

                if (index < 0 || index > array.Length) {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "ArgumentOutOfRange_Index");
                }

                if (array.Length - index < _directMap.Count) {
                    throw new ArgumentException("Arg_ArrayPlusOffTooSmall");
                }

                TKey[] keys = array as TKey[];
                if (keys != null) {
                    CopyTo(keys, index);
                } else {
                    object[] objects = array as object[];
                    if (objects == null) {
                        throw new ArgumentException("Argument_InvalidArrayType, nameof(array)");
                    }

                    int count = _directMap.count;
                    var entries = _directMap.entries;

                    try {
                        for (int i = 0; i < count; i++) {
                            if (entries[i].hashCode >= 0) objects[index++] = entries[i].key;
                        }
                    } catch (ArrayTypeMismatchException) {
                        throw new ArgumentException("Argument_InvalidArrayType, nameof(array)");
                    }
                }
            }

            bool ICollection.IsSynchronized
            {
                get { return false; }
            }

            Object ICollection.SyncRoot
            {
                get { return ((ICollection)_directMap).SyncRoot; }
            }

            public struct Enumerator : IEnumerator<TKey>, System.Collections.IEnumerator {
                private DirectMap<TKey, TValue> _directMap;
                private int index;
                private long version;
                private TKey currentKey;

                internal Enumerator(DirectMap<TKey, TValue> _directMap) {
                    this._directMap = _directMap;
                    version = _directMap.version;
                    index = 0;
                    currentKey = default(TKey);
                }

                public void Dispose() {
                }

                public bool MoveNext() {
                    if (version != _directMap.version) {
                        throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                    }

                    while ((uint)index < (uint)_directMap.count) {
                        if (_directMap.entries[index].hashCode >= 0) {
                            currentKey = _directMap.entries[index].key;
                            index++;
                            return true;
                        }
                        index++;
                    }

                    index = _directMap.count + 1;
                    currentKey = default(TKey);
                    return false;
                }

                public TKey Current
                {
                    get
                    {
                        return currentKey;
                    }
                }

                Object System.Collections.IEnumerator.Current
                {
                    get
                    {
                        if (index == 0 || (index == _directMap.count + 1)) {
                            throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                        }

                        return currentKey;
                    }
                }

                void System.Collections.IEnumerator.Reset() {
                    if (version != _directMap.version) {
                        throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                    }

                    index = 0;
                    currentKey = default(TKey);
                }
            }
        }

        [DebuggerTypeProxy(typeof(DictionaryValueCollectionDebugView<,>))]
        [DebuggerDisplay("Count = {Count}")]
        public sealed class ValueCollection : ICollection<TValue>, ICollection, IReadOnlyCollection<TValue> {
            private DirectMap<TKey, TValue> _directMap;

            public ValueCollection(DirectMap<TKey, TValue> _directMap) {
                if (_directMap == null) {
                    throw new ArgumentNullException(nameof(_directMap));
                }
                this._directMap = _directMap;
            }

            public Enumerator GetEnumerator() {
                return new Enumerator(_directMap);
            }

            public void CopyTo(TValue[] array, int index) {
                if (array == null) {
                    throw new ArgumentNullException(nameof(array));
                }

                if (index < 0 || index > array.Length) {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "ArgumentOutOfRange_Index");
                }

                if (array.Length - index < _directMap.Count) {
                    throw new ArgumentException("Arg_ArrayPlusOffTooSmall");
                }

                int count = _directMap.count;
                var entries = _directMap.entries;

                for (int i = 0; i < count; i++) {
                    if (entries[i].hashCode >= 0) array[index++] = entries[i].value;
                }
            }

            public int Count
            {
                get { return _directMap.Count; }
            }

            bool ICollection<TValue>.IsReadOnly
            {
                get { return true; }
            }

            void ICollection<TValue>.Add(TValue item) {
                throw new NotSupportedException("NotSupported_ValueCollectionSet");
            }

            bool ICollection<TValue>.Remove(TValue item) {
                throw new NotSupportedException("NotSupported_ValueCollectionSet");
            }

            void ICollection<TValue>.Clear() {
                throw new NotSupportedException("NotSupported_ValueCollectionSet");
            }

            bool ICollection<TValue>.Contains(TValue item) {
                return _directMap.ContainsValue(item);
            }

            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() {
                return new Enumerator(_directMap);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return new Enumerator(_directMap);
            }

            void ICollection.CopyTo(Array array, int index) {
                if (array == null) {
                    throw new ArgumentNullException(nameof(array));
                }

                if (array.Rank != 1) {
                    throw new ArgumentException("Arg_RankMultiDimNotSupported, nameof(array)");
                }

                if (array.GetLowerBound(0) != 0) {
                    throw new ArgumentException("Arg_NonZeroLowerBound, nameof(array)");
                }

                if (index < 0 || index > array.Length) {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "ArgumentOutOfRange_Index");
                }

                if (array.Length - index < _directMap.Count)
                    throw new ArgumentException("Arg_ArrayPlusOffTooSmall");

                TValue[] values = array as TValue[];
                if (values != null) {
                    CopyTo(values, index);
                } else {
                    object[] objects = array as object[];
                    if (objects == null) {
                        throw new ArgumentException("Argument_InvalidArrayType, nameof(array)");
                    }

                    int count = _directMap.count;
                    var entries = _directMap.entries;

                    try {
                        for (int i = 0; i < count; i++) {
                            if (entries[i].hashCode >= 0) objects[index++] = entries[i].value;
                        }
                    } catch (ArrayTypeMismatchException) {
                        throw new ArgumentException("Argument_InvalidArrayType, nameof(array)");
                    }
                }
            }

            bool ICollection.IsSynchronized
            {
                get { return false; }
            }

            Object ICollection.SyncRoot
            {
                get { return ((ICollection)_directMap).SyncRoot; }
            }

            public struct Enumerator : IEnumerator<TValue>, System.Collections.IEnumerator {
                private DirectMap<TKey, TValue> _directMap;
                private int index;
                private long version;
                private TValue currentValue;

                internal Enumerator(DirectMap<TKey, TValue> _directMap) {
                    this._directMap = _directMap;
                    version = _directMap.version;
                    index = 0;
                    currentValue = default(TValue);
                }

                public void Dispose() {
                }

                public bool MoveNext() {
                    if (version != _directMap.version) {
                        throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                    }

                    while ((uint)index < (uint)_directMap.count) {
                        if (_directMap.entries[index].hashCode >= 0) {
                            currentValue = _directMap.entries[index].value;
                            index++;
                            return true;
                        }
                        index++;
                    }
                    index = _directMap.count + 1;
                    currentValue = default(TValue);
                    return false;
                }

                public TValue Current
                {
                    get
                    {
                        return currentValue;
                    }
                }

                Object System.Collections.IEnumerator.Current
                {
                    get
                    {
                        if (index == 0 || (index == _directMap.count + 1)) {
                            throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                        }

                        return currentValue;
                    }
                }

                void System.Collections.IEnumerator.Reset() {
                    if (version != _directMap.version) {
                        throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                    }
                    index = 0;
                    currentValue = default(TValue);
                }
            }
        }
    }


    internal static class HashHelpers {
        // Table of prime numbers to use as hash table sizes. 
        // A typical resize algorithm would pick the smallest prime number in this array
        // that is larger than twice the previous capacity. 
        // Suppose our Hashtable currently has capacity x and enough elements are added 
        // such that a resize needs to occur. Resizing first computes 2x then finds the 
        // first prime in the table greater than 2x, i.e. if primes are ordered 
        // p_1, p_2, ..., p_i, ..., it finds p_n such that p_n-1 < 2x < p_n. 
        // Doubling is important for preserving the asymptotic complexity of the 
        // hashtable operations such as add.  Having a prime guarantees that double 
        // hashing does not lead to infinite loops.  IE, your hash function will be 
        // h1(key) + i*h2(key), 0 <= i < size.  h2 and the size must be relatively prime.
        public static readonly int[] primes = {
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369, 8639249, 10367101,
            12440537, 14928671, 17914409, 21497293, 25796759, 30956117, 37147349, 44576837, 53492207, 64190669,
            77028803, 92434613, 110921543, 133105859, 159727031, 191672443, 230006941, 276008387, 331210079,
            397452101, 476942527, 572331049, 686797261, 824156741, 988988137, 1186785773, 1424142949, 1708971541,
            2050765853, MaxPrimeArrayLength };

        public static int GetPrime(int min) {
            if (min < 0)
                throw new ArgumentException("Arg_HTCapacityOverflow");
            Contract.EndContractBlock();

            for (int i = 0; i < primes.Length; i++) {
                int prime = primes[i];
                if (prime >= min) return prime;
            }

            return min;
        }

        public static int GetMinPrime() {
            return primes[0];
        }

        // Returns size of hashtable to grow to.
        public static int ExpandPrime(int oldSize) {
            int newSize = 2 * oldSize;

            // Allow the hashtables to grow to maximum possible size (~2G elements) before encoutering capacity overflow.
            // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
            if ((uint)newSize > MaxPrimeArrayLength && MaxPrimeArrayLength > oldSize) {
                Debug.Assert(MaxPrimeArrayLength == GetPrime(MaxPrimeArrayLength), "Invalid MaxPrimeArrayLength");
                return MaxPrimeArrayLength;
            }

            return GetPrime(newSize);
        }


        // This is the maximum prime smaller than Array.MaxArrayLength
        public const int MaxPrimeArrayLength = 0x7FEFFFFD;
    }
}
