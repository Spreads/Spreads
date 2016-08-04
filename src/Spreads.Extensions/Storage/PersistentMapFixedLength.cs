/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/


using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Spreads.Buffers;
using Spreads.Serialization;

namespace Spreads.Storage {


    // NB Recovery process: 95% of work, but even during testing and program shutdown there was a non-exited lock.
    // If we steal a lock, we must do recovery. Before any change to data, we store 
    // enough info to do a recovery.
    // 


    [DebuggerTypeProxy(typeof(IDictionaryDebugView<,>))]
    [DebuggerDisplay("Count = {Count}")]
    [ComVisible(false)]
    public sealed class PersistentMapFixedLength<TKey, TValue> : IPersistentMap<TKey, TValue>, IDictionary, IReadOnlyDictionary<TKey, TValue> {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        [Serialization(PreferBlittable = true)]
        internal struct Entry : IBinaryConverter<Entry> {
            public int hashCode; // Lower 31 bits of hash code, -1 if unused
            public int next; // Index of next entry, -1 if last
            public TKey key; // Key of entry
            public TValue value; // Value of entry

            // NB this interface methods are only called when Entry[] is not directly pinnable
            // Otherwise more efficient direct conversion is used
            public bool IsFixedSize => TypeHelper<TKey>.Size > 0 && TypeHelper<TValue>.Size > 0;
            public int Size => IsFixedSize
                ?
                    (TypeHelper<Entry>.Size > 0
                    ? TypeHelper<TKey>.Size + TypeHelper<TValue>.Size
                    : 8 + TypeHelper<TKey>.Size + TypeHelper<TValue>.Size)
                : 0;
            public int SizeOf(Entry value, out MemoryStream temporaryStream) {
                if (IsFixedSize) {
                    temporaryStream = null;
                    return Size;
                }
                throw new NotSupportedException("This variant of persistent map does not support variable-size types.");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe int Write(Entry entry, ref DirectBuffer destination, uint offset = 0u, MemoryStream temporaryStream = null) {

                var totalSize = 8 + Size;
                if (!destination.HasCapacity(offset, totalSize)) return (int)BinaryConverterErrorCode.NotEnoughCapacity;
                var ptr = destination.Data + (int)offset;

                *(int*)ptr = entry.hashCode;
                *(int*)(ptr + 4) = entry.next;
                BinarySerializer.Write(entry.key, ref destination, 8u);
                BinarySerializer.Write(entry.value, ref destination, 8u + (uint)BinarySerializer.Size<TKey>());
                return Size;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe int Read(IntPtr ptr, ref Entry value) {
                var entry = new Entry();
                entry.hashCode = *(int*)ptr;
                entry.next = *(int*)(ptr + 4);
                var kl = BinarySerializer.Read<TKey>((ptr + 8), ref entry.key);
                var vl = BinarySerializer.Read<TValue>((ptr + 8 + BinarySerializer.Size<TKey>()), ref entry.value);
                value = entry;
                Debug.Assert(kl + vl == Size);
                return Size;
            }

            public byte Version => 0;
        }

        private const int HeaderLength = 256;
        private static readonly int EntrySize = TypeHelper<Entry>.Size;

        internal DirectFile _buckets;
        internal DirectFile _entries;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Entry GetEntry(int idx) {
            Entry temp = default(Entry);
            TypeHelper<Entry>.Read(new IntPtr(_entries._buffer.Data.ToInt64() + (HeaderLength + (long)idx * EntrySize)), ref temp);
            return temp; //_entries._buffer.Read<Entry>(HeaderLength + (long)idx * EntrySize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetEntry(int idx, Entry entry) {
            entry.Write(entry, ref _entries._buffer, (uint)(HeaderLength + idx * (long)EntrySize));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBucket(int idx) {
            return -1 + (int)_buckets._buffer.ReadUint32(HeaderLength + idx * 4L);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetBucket(int idx, int value) {
            _buckets._buffer.WriteUint32(HeaderLength + idx * 4, (uint)(value + 1));
        }


        // buckets header has 8-bytes slots:
        // Slot0 - locker
        // Slot1 - version
        // Slot2 - nextVersion
        // Slot3 - count
        // Slot4 - freeList
        // Slot5 - freeCount
        // Slot6 - generation

        internal unsafe int count
        {
            get { return *(int*)(_buckets._buffer.Data + 24); }
            private set { *(int*)(_buckets._buffer.Data + 24) = value; }
        }

        internal unsafe int countCopy
        {
            get { return *(int*)(_entries._buffer.Data + 24); }
            private set { *(int*)(_entries._buffer.Data + 24) = value; }
        }

        internal unsafe long version => Volatile.Read(ref *(long*)(_buckets._buffer.Data + 8));

        // do not set it in Initialize(), it is 0 on disk when the bucket file 
        // is new, and previous values when the file is reopened
        internal unsafe int freeList
        {
            get { return (int)(*(uint*)(_buckets._buffer.Data + 32)) - 1; }
            private set { *(uint*)(_buckets._buffer.Data + 32) = (uint)(value + 1); }
        }

        internal unsafe int freeCount
        {
            get { return *(int*)(_buckets._buffer.Data + 40); }
            private set { *(int*)(_buckets._buffer.Data + 40) = value; }
        }

        internal unsafe int generation
        {
            get { return Volatile.Read(ref *(int*)(_buckets._buffer.Data + 48)); }
            private set { Volatile.Write(ref *(int*)(_buckets._buffer.Data + 48), value); }
        }

        internal unsafe int initialGeneration
        {
            get { return Volatile.Read(ref *(int*)(_buckets._buffer.Data + 56)); }
            private set { Volatile.Write(ref *(int*)(_buckets._buffer.Data + 56), value); }
        }

        internal unsafe int keySize
        {
            get { return *(int*)(_buckets._buffer.Data + 64); }
            private set { *(int*)(_buckets._buffer.Data + 64) = value; }
        }

        internal unsafe int valueSize
        {
            get { return *(int*)(_buckets._buffer.Data + 72); }
            private set { *(int*)(_buckets._buffer.Data + 72) = value; }
        }

        internal unsafe int freeListCopy
        {
            get { return *(int*)(_entries._buffer.Data + 32); }
            private set { *(int*)(_entries._buffer.Data + 32) = value; }
        }

        internal unsafe int freeCountCopy
        {
            get { return *(int*)(_entries._buffer.Data + 40); }
            private set { *(int*)(_entries._buffer.Data + 40) = value; }
        }

        internal unsafe int indexCopy
        {
            get { return *(int*)(_entries._buffer.Data + 48); }
            private set { *(int*)(_entries._buffer.Data + 48) = value; }
        }

        internal unsafe int bucketOrLastNextCopy
        {
            get { return *(int*)(_entries._buffer.Data + 56); }
            private set { *(int*)(_entries._buffer.Data + 56) = value; }
        }

        internal unsafe int recoveryFlags
        {
            get { return *(int*)(_entries._buffer.Data); }
            // NB Volatile.Write is crucial to correctly save all copies 
            // before setting recoveryStep value because it generates a fence
            private set { Volatile.Write(ref *(int*)(_entries._buffer.Data), value); }
        }

        private IEqualityComparer<TKey> comparer;
        private KeyCollection keys;
        private ValueCollection values;
        private Object _syncRoot;
        private string _fileName;


        public PersistentMapFixedLength(string fileName) : this(fileName, 5, null) {
        }

        public PersistentMapFixedLength(string fileName, int capacity) : this(fileName, capacity, null) {
        }

        public PersistentMapFixedLength(string fileName, IEqualityComparer<TKey> comparer) : this(fileName, 5, comparer) {
        }

        public PersistentMapFixedLength(string fileName, int capacity, IEqualityComparer<TKey> comparer) {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            _fileName = fileName;
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "ArgumentOutOfRange_NeedNonNegNum");
            Initialize(capacity < 5 ? 5 : capacity);
            this.comparer = comparer ?? EqualityComparer<TKey>.Default;
        }

        public PersistentMapFixedLength(string fileName, IDictionary<TKey, TValue> dictionary) : this(fileName, dictionary, null) {
        }

        public PersistentMapFixedLength(string fileName, IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) :
            this(fileName, dictionary != null ? dictionary.Count : 5, comparer) {
            if (dictionary == null) {
                throw new ArgumentNullException(nameof(dictionary));
            }

            // It is likely that the passed-in dictionary is Dictionary<TKey,TValue>. When this is the case,
            // avoid the enumerator allocation and overhead by looping through the entries array directly.
            // We only do this when dictionary is Dictionary<TKey,TValue> and not a subclass, to maintain
            // back-compat with subclasses that may have overridden the enumerator behavior.
            if (dictionary.GetType() == typeof(PersistentMapFixedLength<TKey, TValue>)) {
                PersistentMapFixedLength<TKey, TValue> d = (PersistentMapFixedLength<TKey, TValue>)dictionary;
                int count1 = d.count;
                DirectFile entries1 = d._entries;
                for (int i = 0; i < count1; i++) {
                    Entry temp = default(Entry);
                    TypeHelper<Entry>.Read(new IntPtr(_entries._buffer.Data.ToInt64() + (HeaderLength + (long)i * EntrySize)), ref temp);
                    var e1 = temp; //_entries._buffer.Read<Entry>(HeaderLength + (long)i * EntrySize);
                    if (e1.hashCode >= 0) {
                        Add(e1.key, e1.value);
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
            get { return comparer; }
        }

        public int Count => ReadLockIf(() => CountUnchecked);
        private int CountUnchecked => count - freeCount;

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

        TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key]
        {
            get { return this[key]; }
        }

        public TValue this[TKey key]
        {
            get
            {
                var kvp =
                    ReadLockIf(() => {
                        try {
                            var kvp2 = FindEntry(key);
                            if (kvp2.Key >= 0) return new KeyValuePair<TValue, Exception>(kvp2.Value, null);
                            return new KeyValuePair<TValue, Exception>(default(TValue), new KeyNotFoundException());
                        } catch (Exception e) {
                            return new KeyValuePair<TValue, Exception>(default(TValue), e);
                        }
                    });
                if (kvp.Value == null) return kvp.Key;
                throw kvp.Value;
            }
            set
            {
                WriteLock(recover => {
                    if (recover) {
                        Recover();
                    }
                    Insert(key, value, false);
                });
            }
        }

        public void Add(TKey key, TValue value) {
            WriteLock(recover => {
                if (recover) {
                    Recover();
                }
                Insert(key, value, true);
            });
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair) {
            Add(keyValuePair.Key, keyValuePair.Value);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair) {
            var kvp = FindEntry(keyValuePair.Key);
            if (kvp.Key >= 0 && EqualityComparer<TValue>.Default.Equals(kvp.Value, keyValuePair.Value)) {
                return true;
            }
            return false;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair) {
            bool ret = false;
            WriteLock(recover => {
                var kvp = FindEntry(keyValuePair.Key);
                if (kvp.Key >= 0 && EqualityComparer<TValue>.Default.Equals(kvp.Value, keyValuePair.Value)) {
                    DoRemove(keyValuePair.Key);
                    ret = true;
                }
                ret = false;
            });
            return ret;
        }

        public void Clear() {
            WriteLock(recovery => {
                if (recovery) {
                    Recover();
                }
                recoveryFlags |= 1 << 8;
                if (count > 0) {
                    for (int i = 0; i < count; i++) {
                        SetBucket(i, -1);
                        SetEntry(i, default(Entry));
                    }
                    freeList = -1;
                    count = 0;
                    freeCount = 0;
                }
                initialGeneration = generation;
                recoveryFlags = 0;
            });
        }

        public bool ContainsKey(TKey key) {
            return FindEntry(key).Key >= 0;
        }

        public bool ContainsValue(TValue value) {
            if (value == null) {
                for (int i = 0; i < count; i++) {
                    var e = GetEntry(i);
                    if (e.hashCode >= 0 && e.value == null) return true;
                }
            } else {
                EqualityComparer<TValue> c = EqualityComparer<TValue>.Default;
                for (int i = 0; i < count; i++) {
                    var e = GetEntry(i);
                    if (e.hashCode >= 0 && c.Equals(e.value, value)) return true;
                }
            }
            return false;
        }

        private void CopyTo(KeyValuePair<TKey, TValue>[] array, int index) {
            WriteLock(recover => {
                if (array == null) {
                    throw new ArgumentNullException(nameof(array));
                }

                if (index < 0 || index > array.Length) {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "ArgumentOutOfRange_Index");
                }

                if (array.Length - index < CountUnchecked) {
                    throw new ArgumentException("Arg_ArrayPlusOffTooSmall");
                }

                int count1 = this.count;
                for (int i = 0; i < count1; i++) {
                    var e = GetEntry(i);
                    if (e.hashCode >= 0) {
                        array[index++] = new KeyValuePair<TKey, TValue>(e.key, e.value);
                    }
                }
            });
        }

        public Enumerator GetEnumerator() {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() {
            return new Enumerator(this, Enumerator.KeyValuePair);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private KeyValuePair<int, TValue> FindEntry(TKey key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            if (CountUnchecked == 0) return new KeyValuePair<int, TValue>(-1, default(TValue));

            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
            // try search all previous generations
            var initGen = initialGeneration;
            for (int gen = generation; gen >= initGen; gen--) {
                var bucket = GetBucket(hashCode % HashHelpers.primes[gen]);
                Entry e = GetEntry(bucket);
                for (int i = bucket; i >= 0; i = e.next) {
                    e = GetEntry(i);
                    if (e.hashCode == hashCode && comparer.Equals(e.key, key))
                        return new KeyValuePair<int, TValue>(i, e.value);
                }
            }
            return new KeyValuePair<int, TValue>(-1, default(TValue));
        }

        private void Initialize(int capacity) {
            if (EntrySize <= 0) throw new NotSupportedException("TKey and TValues must be of fixed size");
            // save place for a recovery snapshot for set
            capacity = capacity + 1;
            var gen = HashHelpers.GetGeneratoin(capacity);
            long bytesCapacityKeys = (long)HeaderLength + (long)HashHelpers.primes[gen] * 4L;
            long bytesCapacityValues = (long)HeaderLength + (long)HashHelpers.primes[gen] * EntrySize;
            _buckets = new DirectFile(_fileName + "-buckets", bytesCapacityKeys);
            _entries = new DirectFile(_fileName + "-entries", bytesCapacityValues);

            var keySize1 = TypeHelper<TKey>.Size;
            var valueSize1 = TypeHelper<TValue>.Size;
            if (keySize == 0) {
                keySize = keySize1;
            } else if (keySize != keySize1) {
                throw new ApplicationException("Wrong type for key. The map was initialized with a different key size");
            }
            if (valueSize == 0) {
                valueSize = valueSize1;
            } else if (valueSize != valueSize1) {
                throw new ApplicationException("Wrong type for value. The map was initialized with a different value size");
            }
            // TODO(?) store hash of TKey.FullName + TValue.FullName and compare&restrict

            if (initialGeneration == 0) {
                initialGeneration = gen;
            }
            if (generation < gen) {
                generation = gen;
                int newSize = HashHelpers.primes[gen];
                _buckets.Grow(HeaderLength + (long)newSize * 4L);
                _entries.Grow(HeaderLength + (long)newSize * (long)EntrySize);
            }
        }


        private void Recover(bool recursive = false) {
            if (recoveryFlags == 0) {
                if (!recursive) Debug.WriteLine("Nothing to recover from");
                return;
            }
            if ((recoveryFlags & (1 << 8)) > 0) {
                Debug.WriteLine("Recovering from flag 8");
                this.Clear();
            }
            if ((recoveryFlags & (1 << 7)) > 0) {
                Debug.WriteLine("Recovering from flag 7");
                freeList = freeListCopy;
                freeCount = freeCountCopy;
                _entries._buffer.WriteInt64(HeaderLength + (long)countCopy * EntrySize,
                    _entries._buffer.ReadInt64(HeaderLength - 8));

                recoveryFlags &= ~(1 << 7);
                Recover(true);
            }
            if ((recoveryFlags & (1 << 6)) > 0) {
                Debug.WriteLine("Recovering from flag 6");
                _entries._buffer.WriteInt32(HeaderLength + (long)indexCopy * EntrySize + 4L, bucketOrLastNextCopy);

                recoveryFlags &= ~(1 << 6);
                Recover(true);
            }
            if ((recoveryFlags & (1 << 5)) > 0) {
                Debug.WriteLine("Recovering from flag 5");
                SetBucket(bucketOrLastNextCopy, indexCopy);

                recoveryFlags &= ~(1 << 5);
                Recover(true);
            }
            if ((recoveryFlags & (1 << 4)) > 0) {
                Debug.WriteLine("Recovering from flag 4");
                SetBucket(bucketOrLastNextCopy, indexCopy);

                recoveryFlags &= ~(1 << 4);
                Recover(true);
            }
            if ((recoveryFlags & (1 << 3)) > 0) {
                Debug.WriteLine("Recovering from flag 3");
                count = countCopy;

                recoveryFlags &= ~(1 << 3);
                Recover(true);
            }
            if ((recoveryFlags & (1 << 2)) > 0) {
                Debug.WriteLine("Recovering from flag 2");
                freeList = freeListCopy;
                freeCount = freeCountCopy;

                recoveryFlags &= ~(1 << 2);
                Recover(true);
            }
            if ((recoveryFlags & (1 << 1)) > 0) {
                Debug.WriteLine("Recovering from flag 1");

                if (freeCount > 0) {
                    var snapShotPosition = HeaderLength + (long)freeList * EntrySize + 8L; // Tkey-TValue part only
                    var originPosition = HeaderLength + (long)indexCopy * EntrySize + 8L; // Tkey-TValue part only
                    _entries._buffer.Copy(new IntPtr(_entries._buffer.Data.ToInt64() + originPosition), snapShotPosition,
                        EntrySize - 8);
                } else {
                    // NB we always have +1 capacity
                    //if (count == (_buckets._capacity - HeaderLength) / 4) {
                    //    Resize();
                    //}
                    var snapShotPosition = HeaderLength + (long)count * EntrySize + 8L; // Tkey-TValue part only
                    var originPosition = HeaderLength + (long)indexCopy * EntrySize + 8L; // Tkey-TValue part only
                    _entries._buffer.Copy(new IntPtr(_entries._buffer.Data.ToInt64() + originPosition), snapShotPosition,
                        EntrySize - 8);
                }
                recoveryFlags &= ~(1 << 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Insert(TKey key, TValue value, bool add) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;

            // try search all previous generations
            var initGen = initialGeneration;
            for (int gen = generation; gen >= initGen; gen--) {
                int idx = hashCode % HashHelpers.primes[gen];
                var bucket = GetBucket(idx);
                Entry e = GetEntry(bucket);
                //for (int i = bucket; i >= 0; i = e.next, e = GetEntry(i)) {

                for (int i = bucket; i >= 0; i = e.next) {
                    e = GetEntry(i);
                    if (e.hashCode == hashCode && comparer.Equals(e.key, key)) {
                        if (add) {
                            throw new ArgumentException($"Key already exists: {key}");
                        }

                        var entry = e;

                        // make a snapshot copy of the old value
                        if (freeCount > 0) {
                            var snapShotPosition = HeaderLength + (long)freeList * EntrySize + 8L; // Tkey-TValue part only
                            var originPosition = HeaderLength + (long)i * EntrySize + 8L; // Tkey-TValue part only
                            _entries._buffer.Copy(new IntPtr(_entries._buffer.Data.ToInt64() + snapShotPosition), originPosition,
                                EntrySize - 8);
                        } else {
                            // NB we always have +1 capacity
                            //if (count == (_buckets._capacity - HeaderLength) / 4) {
                            //    Resize();
                            //}
                            var snapShotPosition = HeaderLength + (long)count * EntrySize + 8L; // Tkey-TValue part only
                            var originPosition = HeaderLength + (long)i * EntrySize + 8L; // Tkey-TValue part only
                            _entries._buffer.Copy(new IntPtr(_entries._buffer.Data.ToInt64() + snapShotPosition), originPosition,
                                EntrySize - 8);
                        }
                        indexCopy = i;
                        recoveryFlags |= 1 << 1;
                        ChaosMonkey.Exception(scenario: 11);

                        // by now, we have saved everything that is needed for undoing this step
                        // if we steal the lock and see recoveryStep = 1, then we failed
                        // either right here, during the next line, or during exiting lock
                        // In any case, we just need to reapply the snapshot copy on recovery to undo set operation

                        entry.value = value;
                        ChaosMonkey.Exception(scenario: 12);
                        SetEntry(i, entry);
                        ChaosMonkey.Exception(scenario: 13);
                        recoveryFlags = 0;
                        return;
                    }
                }
            }



            int targetBucket = hashCode % HashHelpers.primes[generation];
            int index;
            if (freeCount > 0) {
                index = freeList;

                // Save freeList+Count value.
                var previousFreeCount = freeCount;
                freeListCopy = freeList;
                freeCountCopy = previousFreeCount;
                recoveryFlags |= 1 << 2;
                ChaosMonkey.Exception(scenario: 21);
                // NB each of the three writes are atomic 32 bit
                // unless recoveryStep is set to 2 we will ignore them and the order
                // of writes before the barrier doesn't matter

                freeList = GetEntry(index).next; // TODO could read only next
                ChaosMonkey.Exception(scenario: 22);
                freeCount = previousFreeCount - 1;
                ChaosMonkey.Exception(scenario: 23);
            } else {
                if (count == -1 + (_buckets._capacity - HeaderLength) / 4) {
                    Resize();
                    //targetBucket = hashCode % buckets.Count;
                    targetBucket = hashCode % HashHelpers.primes[generation];
                }
                index = count;

                // save count before inceremting it.
                // if we fail after the barrier, we never know if count was already incremented or not
                countCopy = index;
                recoveryFlags |= 1 << 3;
                ChaosMonkey.Exception(scenario: 31);
                count = index + 1;
                ChaosMonkey.Exception(scenario: 32);
            }
            // NB Index is saved above indirectly
            ChaosMonkey.Exception(scenario: 24);
            ChaosMonkey.Exception(scenario: 33);
            var prevousBucketIdx = GetBucket(targetBucket);
            // save buckets state
            bucketOrLastNextCopy = targetBucket;
            ChaosMonkey.Exception(scenario: 25);
            ChaosMonkey.Exception(scenario: 34);
            indexCopy = prevousBucketIdx;
            ChaosMonkey.Exception(scenario: 26);
            ChaosMonkey.Exception(scenario: 35);
            recoveryFlags |= 1 << 4;
            ChaosMonkey.Exception(scenario: 41);
            // if we fail after that, we have enough info to undo everything and cleanup entries[index] during recovery
            var entry1 = new Entry(); // entries[index];
            entry1.hashCode = hashCode;
            entry1.next = prevousBucketIdx;
            entry1.key = key;
            entry1.value = value;
            SetEntry(index, entry1);
            ChaosMonkey.Exception(scenario: 42);
            SetBucket(targetBucket, index);
            ChaosMonkey.Exception(scenario: 43);

            recoveryFlags = 0;

            // special case, only lock should be stolen without recovery
            ChaosMonkey.Exception(scenario: 44);
        }


        private void Resize() {
            var newSize = HashHelpers.primes[generation + 1];
            _buckets.Grow(HeaderLength + (long)newSize * 4L);
            _entries.Grow(HeaderLength + (long)newSize * (long)EntrySize);
            generation++;
        }

        public bool Remove(TKey key) {
            bool ret = false;
            WriteLock(recovery => {
                if (recovery) {
                    Recover();
                }
                ret = DoRemove(key);
            });
            return ret;
        }

        private bool DoRemove(TKey key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            if (CountUnchecked >= 0) {
                int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
                // try search all previous generations
                var initGen = initialGeneration;
                for (int gen = generation; gen >= initGen; gen--) {
                    int bucketIdx = hashCode % HashHelpers.primes[gen];
                    var bucket = GetBucket(bucketIdx);
                    int last = -1;
                    var e = GetEntry(bucket);
                    for (int i = bucket; i >= 0; last = i, i = e.next) {
                        e = GetEntry(i);
                        if (e.hashCode == hashCode && comparer.Equals(e.key, key)) {
                            if (last < 0) {
                                bucketOrLastNextCopy = bucketIdx;
                                indexCopy = GetBucket(bucketIdx);
                                recoveryFlags |= 1 << 5;
                                ChaosMonkey.Exception(scenario: 51);
                                //NB entries[i].next; 
                                var ithNext =
                                    _entries._buffer.ReadInt32(HeaderLength + (long)i * EntrySize + 4);
                                SetBucket(bucketIdx, ithNext);
                                ChaosMonkey.Exception(scenario: 52);
                            } else {
                                long lastEntryOffset = HeaderLength + (long)last * EntrySize;
                                indexCopy = last;
                                // NB reuse bucketOrLastNextCopy slot to save next of the last value, instead of creating one more property
                                bucketOrLastNextCopy = _entries._buffer.ReadInt32(lastEntryOffset + 4);
                                recoveryFlags |= 1 << 6;
                                ChaosMonkey.Exception(scenario: 6);
                                //NB entries[i].next; 
                                var ithNext =
                                    _entries._buffer.ReadInt32(HeaderLength + (long)i * EntrySize + 4L);
                                _entries._buffer.WriteInt32(lastEntryOffset + 4L, ithNext);

                                // To recover
                                //entries.Buffer.WriteInt32(PersistentArray<Entry>.DataOffset + indexCopy * PersistentArray<Entry>.ItemSize + 4, bucketOrLastNextCopy);

                                //var lastEntry = entries[last];
                                //lastEntry.next = entries[i].next;
                                //entries[last] = lastEntry;
                            }

                            long entryOffset = HeaderLength + (long)i * EntrySize;
                            // TODO rename, this is not a count but the only unused copy slot
                            countCopy = i;
                            freeListCopy = freeList;
                            freeCountCopy = freeCount;
                            // Save Hash and Next fields of the entry in a special -1 position of entries
                            _entries._buffer.WriteInt64(HeaderLength - 8, _entries._buffer.ReadInt64(entryOffset));

                            // To recover:
                            //freeList = freeListCopy;
                            //freeCount = freeCountCopy;
                            //entries.Buffer.WriteInt64(PersistentArray<Entry>.DataOffset + countCopy * PersistentArray<Entry>.ItemSize,
                            //    entries.Buffer.ReadInt64(PersistentArray<Entry>.DataOffset - 1 * PersistentArray<Entry>.ItemSize));

                            recoveryFlags |= 1 << 7;
                            ChaosMonkey.Exception(scenario: 71);
                            // NB instead of these 4 lines, we write directly
                            // // var entry //= entries[i]; //new Entry();
                            // // entry.hashCode //= -1;
                            // // entry.next //= freeList;
                            // // entries[i] //= entry;
                            // NB do not cleanup key and value, because on recovery we will reuse them and undo removal
                            // // entry.key //= default(TKey);
                            // // entry.value //= default(TValue);

                            _entries._buffer.WriteInt32(entryOffset, -1);
                            ChaosMonkey.Exception(scenario: 72);
                            _entries._buffer.WriteInt32(entryOffset + 4, freeList);
                            ChaosMonkey.Exception(scenario: 73);
                            freeList = i;
                            ChaosMonkey.Exception(scenario: 74);
                            freeCount++;
                            ChaosMonkey.Exception(scenario: 75);

                            // if this write succeeds, we are done even if fail to release the lock
                            recoveryFlags = 0;

                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value) {
            var kvp = FindEntry(key);
            if (kvp.Key >= 0) {
                value = kvp.Value;
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
            var kvp = FindEntry(key);
            if (kvp.Key >= 0) {
                return kvp.Value;
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

            if (array.Length - index < CountUnchecked) {
                throw new ArgumentException("Arg_ArrayPlusOffTooSmall");
            }

            KeyValuePair<TKey, TValue>[] pairs = array as KeyValuePair<TKey, TValue>[];
            if (pairs != null) {
                CopyTo(pairs, index);
            } else if (array is DictionaryEntry[]) {
                DictionaryEntry[] dictEntryArray = array as DictionaryEntry[];
                var entries1 = this._entries;

                for (int i = 0; i < count; i++) {
                    var e = GetEntry(i);
                    if (e.hashCode >= 0) {
                        dictEntryArray[index++] = new DictionaryEntry(e.key, e.value);
                    }
                }
            } else {
                object[] objects = array as object[];
                if (objects == null) {
                    throw new ArgumentException("Argument_InvalidArrayType, nameof(array)");
                }

                try {
                    int count1 = this.count;
                    var entries1 = this._entries;
                    for (int i = 0; i < count1; i++) {
                        var e = GetEntry(i);
                        if (e.hashCode >= 0) {
                            objects[index++] = new KeyValuePair<TKey, TValue>(e.key, e.value);
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
                    Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);
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
                    var kvp = FindEntry((TKey)key);
                    if (kvp.Key >= 0) {
                        return kvp.Value;
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

        void IDictionary.Clear() {
            Clear();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Clear() {
            Clear();
        }

        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>,
            IDictionaryEnumerator {
            private PersistentMapFixedLength<TKey, TValue> _persistentMap;
            private long version;
            private int index;
            private KeyValuePair<TKey, TValue> current;
            private int getEnumeratorRetType; // What should Enumerator.Current return?

            internal const int DictEntry = 1;
            internal const int KeyValuePair = 2;

            internal Enumerator(PersistentMapFixedLength<TKey, TValue> _persistentMap, int getEnumeratorRetType) {
                this._persistentMap = _persistentMap;
                version = _persistentMap.version;
                index = 0;
                this.getEnumeratorRetType = getEnumeratorRetType;
                current = new KeyValuePair<TKey, TValue>();
            }

            public bool MoveNext() {
                if (version != _persistentMap.version) {
                    throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                }

                // Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
                // dictionary.count+1 could be negative if dictionary.count is Int32.MaxValue
                while ((uint)index < (uint)_persistentMap.count) {
                    var e = _persistentMap.GetEntry(index);
                    if (e.hashCode >= 0) {
                        current = new KeyValuePair<TKey, TValue>(e.key, e.value);
                        index++;
                        return true;
                    }
                    index++;
                }

                index = _persistentMap.count + 1;
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
                    if (index == 0 || (index == _persistentMap.count + 1)) {
                        throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                    }

                    if (getEnumeratorRetType == DictEntry) {
                        return new DictionaryEntry(current.Key, current.Value);
                    } else {
                        return new KeyValuePair<TKey, TValue>(current.Key, current.Value);
                    }
                }
            }

            void IEnumerator.Reset() {
                if (version != _persistentMap.version) {
                    throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                }

                index = 0;
                current = new KeyValuePair<TKey, TValue>();
            }

            DictionaryEntry IDictionaryEnumerator.Entry
            {
                get
                {
                    if (index == 0 || (index == _persistentMap.count + 1)) {
                        throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                    }

                    return new DictionaryEntry(current.Key, current.Value);
                }
            }

            object IDictionaryEnumerator.Key
            {
                get
                {
                    if (index == 0 || (index == _persistentMap.count + 1)) {
                        throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                    }

                    return current.Key;
                }
            }

            object IDictionaryEnumerator.Value
            {
                get
                {
                    if (index == 0 || (index == _persistentMap.count + 1)) {
                        throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                    }

                    return current.Value;
                }
            }
        }

        [DebuggerTypeProxy(typeof(DictionaryKeyCollectionDebugView<,>))]
        [DebuggerDisplay("Count = {Count}")]
        public sealed class KeyCollection : ICollection<TKey>, ICollection, IReadOnlyCollection<TKey> {
            private PersistentMapFixedLength<TKey, TValue> _persistentMap;

            public KeyCollection(PersistentMapFixedLength<TKey, TValue> _persistentMap) {
                if (_persistentMap == null) {
                    throw new ArgumentNullException(nameof(_persistentMap));
                }
                this._persistentMap = _persistentMap;
            }

            public Enumerator GetEnumerator() {
                return new Enumerator(_persistentMap);
            }

            public void CopyTo(TKey[] array, int index) {
                if (array == null) {
                    throw new ArgumentNullException(nameof(array));
                }

                if (index < 0 || index > array.Length) {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "ArgumentOutOfRange_Index");
                }

                if (array.Length - index < _persistentMap.CountUnchecked) {
                    throw new ArgumentException("Arg_ArrayPlusOffTooSmall");
                }

                int count = _persistentMap.count;

                for (int i = 0; i < count; i++) {
                    var e = _persistentMap.GetEntry(i);
                    if (e.hashCode >= 0) array[index++] = e.key;
                }
            }

            public int Count
            {
                get { return _persistentMap.CountUnchecked; }
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
                return _persistentMap.ContainsKey(item);
            }

            bool ICollection<TKey>.Remove(TKey item) {
                throw new NotSupportedException("NotSupported_KeyCollectionSet");
            }

            IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() {
                return new Enumerator(_persistentMap);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return new Enumerator(_persistentMap);
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

                if (array.Length - index < _persistentMap.CountUnchecked) {
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

                    int count = _persistentMap.count;

                    try {
                        for (int i = 0; i < count; i++) {
                            var e = _persistentMap.GetEntry(i);
                            if (e.hashCode >= 0) objects[index++] = e.key;
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
                get { return (_persistentMap)._syncRoot; }
            }

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public struct Enumerator : IEnumerator<TKey> {
                private PersistentMapFixedLength<TKey, TValue> _persistentMap;
                private int index;
                private long version;
                private TKey currentKey;

                internal Enumerator(PersistentMapFixedLength<TKey, TValue> _persistentMap) {
                    this._persistentMap = _persistentMap;
                    version = _persistentMap.version;
                    index = 0;
                    currentKey = default(TKey);
                }

                public void Dispose() {
                }

                public bool MoveNext() {
                    if (version != _persistentMap.version) {
                        throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                    }

                    while ((uint)index < (uint)_persistentMap.count) {
                        var e = _persistentMap.GetEntry(index);
                        if (e.hashCode >= 0) {
                            currentKey = e.key;
                            index++;
                            return true;
                        }
                        index++;
                    }

                    index = _persistentMap.count + 1;
                    currentKey = default(TKey);
                    return false;
                }

                public TKey Current
                {
                    get { return currentKey; }
                }

                Object IEnumerator.Current
                {
                    get
                    {
                        if (index == 0 || (index == _persistentMap.count + 1)) {
                            throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                        }

                        return currentKey;
                    }
                }

                void IEnumerator.Reset() {
                    if (version != _persistentMap.version) {
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
            private PersistentMapFixedLength<TKey, TValue> _persistentMap;

            public ValueCollection(PersistentMapFixedLength<TKey, TValue> _persistentMap) {
                if (_persistentMap == null) {
                    throw new ArgumentNullException(nameof(_persistentMap));
                }
                this._persistentMap = _persistentMap;
            }

            public Enumerator GetEnumerator() {
                return new Enumerator(_persistentMap);
            }

            public void CopyTo(TValue[] array, int index) {
                if (array == null) {
                    throw new ArgumentNullException(nameof(array));
                }

                if (index < 0 || index > array.Length) {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "ArgumentOutOfRange_Index");
                }

                if (array.Length - index < _persistentMap.CountUnchecked) {
                    throw new ArgumentException("Arg_ArrayPlusOffTooSmall");
                }

                int count = _persistentMap.count;

                for (int i = 0; i < count; i++) {
                    var e = _persistentMap.GetEntry(i);
                    if (e.hashCode >= 0) array[index++] = e.value;
                }
            }

            public int Count
            {
                get { return _persistentMap.CountUnchecked; }
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
                return _persistentMap.ContainsValue(item);
            }

            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() {
                return new Enumerator(_persistentMap);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return new Enumerator(_persistentMap);
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

                if (array.Length - index < _persistentMap.CountUnchecked)
                    throw new ArgumentException("Arg_ArrayPlusOffTooSmall");

                TValue[] values = array as TValue[];
                if (values != null) {
                    CopyTo(values, index);
                } else {
                    object[] objects = array as object[];
                    if (objects == null) {
                        throw new ArgumentException("Argument_InvalidArrayType, nameof(array)");
                    }

                    int count = _persistentMap.count;

                    try {
                        for (int i = 0; i < count; i++) {
                            var e = _persistentMap.GetEntry(i);
                            if (e.hashCode >= 0) objects[index++] = e.value;
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
                get { return (_persistentMap)._syncRoot; }
            }

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public struct Enumerator : IEnumerator<TValue> {
                private PersistentMapFixedLength<TKey, TValue> _persistentMap;
                private int index;
                private long version;
                private TValue currentValue;

                internal Enumerator(PersistentMapFixedLength<TKey, TValue> _persistentMap) {
                    this._persistentMap = _persistentMap;
                    version = _persistentMap.version;
                    index = 0;
                    currentValue = default(TValue);
                }

                public void Dispose() {
                }

                public bool MoveNext() {
                    if (version != _persistentMap.version) {
                        throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                    }

                    while ((uint)index < (uint)_persistentMap.count) {
                        var e = _persistentMap.GetEntry(index);
                        if (e.hashCode >= 0) {
                            currentValue = e.value;
                            index++;
                            return true;
                        }
                        index++;
                    }
                    index = _persistentMap.count + 1;
                    currentValue = default(TValue);
                    return false;
                }

                public TValue Current
                {
                    get { return currentValue; }
                }

                Object IEnumerator.Current
                {
                    get
                    {
                        if (index == 0 || (index == _persistentMap.count + 1)) {
                            throw new InvalidOperationException("InvalidOperation_EnumOpCantHappen");
                        }

                        return currentValue;
                    }
                }

                void IEnumerator.Reset() {
                    if (version != _persistentMap.version) {
                        throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                    }
                    index = 0;
                    currentValue = default(TValue);
                }
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe T ReadLockIf<T>(Func<T> f) {
            T value;
            var sw = new SpinWait();
            while (true) {
                var ver = Volatile.Read(ref *(long*)(_buckets._buffer.Data + 8));
                value = f.Invoke();
                var nextVer = Volatile.Read(ref *(long*)(_buckets._buffer.Data + 16));
                if (ver == nextVer) {
                    break;
                }
                if (sw.Count > 100) {
                    // TODO play with this number
                    // Take or steal write lock and recover
                    // Currently versions could be different due to premature exit of some locker
                    WriteLock(recover => {
                        Recover();
                        //if (recover) {
                        //    Recover();
                        //} else {
                        //    throw new ApplicationException("This should happen only when lock was not released");
                        //}
                    }, true);

                }
                sw.SpinOnce();
            }
            return value;
        }

        // ReSharper disable once StaticMemberInGenericType
        private static readonly int Pid = Process.GetCurrentProcess().Id;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void WriteLock(Action<bool> action, bool fixVersions = false) {
            try {
                var sw = new SpinWait();
                var cleanup = false;
                try {
                } finally {
                    while (true) {
                        var pid = Interlocked.CompareExchange(ref *(int*)(_buckets._buffer.Data), Pid, 0);
                        if (pid == 0) {
                            var l1 = *(int*)(_buckets._buffer.Data);
                            Debug.Assert(l1 == Pid);
                            Debug.Assert(*(long*)(_buckets._buffer.Data + 8) == *(long*)(_buckets._buffer.Data + 16), "Versions must be equal here.");

                            if (!fixVersions) Interlocked.Increment(ref *(long*)(_buckets._buffer.Data + 16));
                            break;
                        }
                        if (sw.Count > 100) {
                            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
                            if (pid == Pid && ChaosMonkey.Enabled) {
                                // Current process is still alive and haven't released lock
                                // We try to handle only situations when a process was killed
                                // If a process is alive, it must release a lock very soon
                                // Do nothing here unless ChaosMonkey.Enabled, since (pid == _pid) case must be covered by 
                                // Process.GetProcessById(pid) returning without exception.

                                // steal lock of this process when ChaosMonkey.Enabled
                                if (pid == Interlocked.CompareExchange(ref *(int*)(_buckets._buffer.Data), Pid, pid)) {
                                    cleanup = true;
                                    if (!fixVersions) {
                                        Interlocked.Increment(ref *(long*)(_buckets._buffer.Data + 16));
                                    }
                                    break;
                                }
                            } else {
                                try {
                                    var p = Process.GetProcessById(pid);
                                    throw new ApplicationException(
                                        $"Cannot acquire lock, process {p.Id} has it for a long time");
                                } catch (ArgumentException) {
                                    // pid is not running anymore, try to take it
                                    if (pid ==
                                        Interlocked.CompareExchange(ref *(int*)(_buckets._buffer.Data), Pid, pid)) {
                                        cleanup = true;
                                        if (!fixVersions) {
                                            Interlocked.Increment(ref *(long*)(_buckets._buffer.Data + 16));
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        sw.SpinOnce();
                    }
                }
                action.Invoke(cleanup);
#if CHAOS_MONKEY
            } catch (ChaosMonkeyException) {
                // Do nothing, do not release lock. We are testing different failures now.
                Debug.Assert(*(long*)(_buckets._buffer.Data + 8) != *(long*)(_buckets._buffer.Data + 16), "Versions must be not equal here.");
                throw;
            } catch { // same as finally below
                var pid = Interlocked.CompareExchange(ref *(int*)(_buckets._buffer.Data), 0, Pid);
                if (fixVersions) {
                    Interlocked.Exchange(ref *(long*)(_buckets._buffer.Data + 16), *(long*)(_buckets._buffer.Data + 8));
                } else {
                    Interlocked.Increment(ref *(long*)(_buckets._buffer.Data + 8));
                }
                if (pid != Pid) {
                    Trace.Fail("Cannot release lock, it was stolen while this process is still alive");
                }
                throw;
            }
            // normal case without exceptions
            var l = *(int*)(_buckets._buffer.Data);
            Debug.Assert(l == Pid);
            var pid2 = Interlocked.CompareExchange(ref *(int*)(_buckets._buffer.Data), 0, Pid);
            if (fixVersions) {
                Interlocked.Exchange(ref *(long*)(_buckets._buffer.Data + 16), *(long*)(_buckets._buffer.Data + 8));
            } else {

                Interlocked.Increment(ref *(long*)(_buckets._buffer.Data + 8));
                var v = *(long*)(_buckets._buffer.Data + 8);
                var nv = *(long*)(_buckets._buffer.Data + 16);
                Debug.Assert(v == nv, $"Versions must be equal here. V = {v}, NV = {nv}");
            }
            if (pid2 != Pid) {
                Trace.TraceWarning("Cannot release lock, it was stolen while this process is still alive");
                //Environment.FailFast("Cannot release lock, it was stolen while this process is still alive");
            }


#else
            } finally {
                var pid = Interlocked.CompareExchange(ref *(int*)(_buckets._buffer.Data), 0, Pid);
                if (fixVersions) {
                    Interlocked.Exchange(ref *(long*)(_buckets._buffer.Data + 16),
                        *(long*)(_buckets._buffer.Data + 8));
                } else {
                    Interlocked.Increment(ref *(long*)(_buckets._buffer.Data + 8));
                }
                if (pid != Pid) {
                    Environment.FailFast("Cannot release lock, it was stolen while this process is still alive");
                }
            }
#endif
        }

        public void Dispose() {
            Dispose(true);
        }

        private void Dispose(bool disposing) {
            _buckets.Dispose();
            _entries.Dispose();
            if (disposing) {
                GC.SuppressFinalize(this);
            }
        }

        public TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory) {
            TValue ret = default(TValue);
            WriteLock(recover => {
                if (recover) {
                    Recover();
                }
                var kvp = FindEntry(key);
                if (kvp.Key >= 0) {
                    ret = updateValueFactory.Invoke(key, kvp.Value);
                    Insert(key, ret, false);
                } else {
                    Insert(key, addValue, true);
                    ret = addValue;
                }

            });
            return ret;
        }

        ~PersistentMapFixedLength() {
            Dispose(false);
        }

        public void Flush() {
            _buckets.Flush();
            _entries.Flush();
        }

        public void Flush(bool flushToDisk) {
            _buckets.Flush(flushToDisk);
            _entries.Flush(flushToDisk);
        }


        public string Id { get; }
    }
}
