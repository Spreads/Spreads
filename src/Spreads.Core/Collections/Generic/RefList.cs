﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// Original code is based on CoreCLR List<T>, MIT licensed:

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
// ReSharper disable ConditionIsAlwaysTrueOrFalse

#nullable disable

namespace Spreads.Collections.Generic
{
    /// <summary>
    /// A <see cref="List{T}"/> with an indexer that returns a reference to an item.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [DebuggerDisplay("Count = {Count}")]
    [Serializable]
    public class RefList<T> : IList<T>, System.Collections.IList, IReadOnlyList<T>
    {
        private const int _defaultCapacity = 4;

        private T[] _items;

        [ContractPublicPropertyName("Count")]
        private int _size;

        private int _version;

        [NonSerialized]
        private object _syncRoot;

        private static readonly T[] _emptyArray = Array.Empty<T>();

        // Constructs a RefList. The list is initially empty and has a capacity
        // of zero. Upon adding the first element to the list the capacity is
        // increased to _defaultCapacity, and then increased in multiples of two
        // as required.
        public RefList()
        {
            _items = _emptyArray;
        }

        // Constructs a RefList with a given initial capacity. The list is
        // initially empty, but will have room for the given number of elements
        // before any reallocations are required.
        //
        public RefList(int capacity)
        {
            if (capacity < 0) ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.capacity, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            Contract.EndContractBlock();

            if (capacity == 0)
                _items = _emptyArray;
            else
                _items = new T[capacity];
        }

        // Constructs a RefList, copying the contents of the given collection. The
        // size and capacity of the new list will both be equal to the size of the
        // given collection.
        //
        public RefList(IEnumerable<T> collection)
        {
            if (collection == null)
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.collection);
            Contract.EndContractBlock();

            if (collection is ICollection<T> c)
            {
                int count = c.Count;
                if (count == 0)
                {
                    _items = _emptyArray;
                }
                else
                {
                    _items = new T[count];
                    c.CopyTo(_items, 0);
                    _size = count;
                }
            }
            else
            {
                _size = 0;
                _items = _emptyArray;
                AddEnumerable(collection);
            }
        }

        // Gets and sets the capacity of this list.  The capacity is the size of
        // the internal array used to hold items.  When set, the internal
        // array of the list is reallocated to the given capacity.
        //
        public int Capacity
        {
            get
            {
                Contract.Ensures(Contract.Result<int>() >= 0);
                return _items.Length;
            }
            set
            {
                if (value < _size)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.value, ExceptionResource.ArgumentOutOfRange_SmallCapacity);
                }
                Contract.EndContractBlock();

                if (value != _items.Length)
                {
                    if (value > 0)
                    {
                        T[] newItems = new T[value];
                        if (_size > 0)
                        {
                            Array.Copy(_items, 0, newItems, 0, _size);
                        }
                        _items = newItems;
                    }
                    else
                    {
                        _items = _emptyArray;
                    }
                }
            }
        }

        // Read-only property describing how many elements are in the RefList.
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Contract.Ensures(Contract.Result<int>() >= 0);
                return _size;
            }
        }

        bool System.Collections.IList.IsFixedSize => false;

        // Is this RefList read-only?
        bool ICollection<T>.IsReadOnly => false;

        bool System.Collections.IList.IsReadOnly => false;

        // Is this RefList synchronized (thread-safe)?
        bool System.Collections.ICollection.IsSynchronized => false;

        // Synchronization root for this object.
        object System.Collections.ICollection.SyncRoot
        {
            get
            {
                if (_syncRoot == null)
                {
                    System.Threading.Interlocked.CompareExchange<object>(ref _syncRoot, new object(), null);
                }
                return _syncRoot;
            }
        }

        // Sets or Gets the element at the given index.
        //
        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_size)
                {
                    ThrowHelper.ThrowArgumentOutOfRange_IndexException();
                }
                Contract.EndContractBlock();
                return ref _items[index];
            }
        }

        T IList<T>.this[int index]
        {
            get
            {
                // Following trick can reduce the range check by one
                if ((uint)index >= (uint)_size)
                {
                    ThrowHelper.ThrowArgumentOutOfRange_IndexException();
                }
                Contract.EndContractBlock();
                return _items[index];
            }

            set
            {
                if ((uint)index >= (uint)_size)
                {
                    ThrowHelper.ThrowArgumentOutOfRange_IndexException();
                }
                Contract.EndContractBlock();
                _items[index] = value;
                _version++;
            }
        }

        T IReadOnlyList<T>.this[int index]
        {
            get
            {
                // Following trick can reduce the range check by one
                if ((uint)index >= (uint)_size)
                {
                    ThrowHelper.ThrowArgumentOutOfRange_IndexException();
                }
                Contract.EndContractBlock();
                return _items[index];
            }
        }

        private static bool IsCompatibleObject(object value)
        {
            // Non-null values are fine.  Only accept nulls if T is a class or Nullable<U>.
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>.
            return ((value is T) || (value == null && default(T) == null));
        }

        object System.Collections.IList.this[int index]
        {
            get => this[index];
            set
            {
                ThrowHelper.IfNullAndNullsAreIllegalThenThrow<T>(value, ExceptionArgument.value);

                try
                {
                    this[index] = (T)value;
                }
                catch (InvalidCastException)
                {
                    ThrowHelper.ThrowWrongValueTypeArgumentException(value, typeof(T));
                }
            }
        }

        // Adds the given object to the end of this list. The size of the list is
        // increased by one. If required, the capacity of the list is doubled
        // before adding the new element.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            var array = _items;
            var size = _size;
            _version++;
            if ((uint)size < (uint)array.Length)
            {
                _size = size + 1;
                array[size] = item;
            }
            else
            {
                AddWithResize(item);
            }
        }

        // Non-inline from RefList.Add to improve its code quality as uncommon path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddWithResize(T item)
        {
            var size = _size;
            EnsureCapacity(size + 1);
            _size = size + 1;
            _items[size] = item;
        }

        int System.Collections.IList.Add(object item)
        {
            ThrowHelper.IfNullAndNullsAreIllegalThenThrow<T>(item, ExceptionArgument.item);

            try
            {
                Add((T)item);
            }
            catch (InvalidCastException)
            {
                ThrowHelper.ThrowWrongValueTypeArgumentException(item, typeof(T));
            }

            return Count - 1;
        }

        // Adds the elements of the given collection to the end of this list. If
        // required, the capacity of the list is increased to twice the previous
        // capacity or the new size, whichever is larger.
        //
        public void AddRange(IEnumerable<T> collection)
        {
            Contract.Ensures(Count >= Contract.OldValue(Count));

            InsertRange(_size, collection);
        }

        public ReadOnlyCollection<T> AsReadOnly()
        {
            Contract.Ensures(Contract.Result<ReadOnlyCollection<T>>() != null);
            return new ReadOnlyCollection<T>(this);
        }

        // Searches a section of the list for a given element using a binary search
        // algorithm. Elements of the list are compared to the search value using
        // the given IComparer interface. If comparer is null, elements of
        // the list are compared to the search value using the IComparable
        // interface, which in that case must be implemented by all elements of the
        // list and the given search value. This method assumes that the given
        // section of the list is already sorted; if this is not the case, the
        // result will be incorrect.
        //
        // The method returns the index of the given value in the list. If the
        // list does not contain the given value, the method returns a negative
        // integer. The bitwise complement operator (~) can be applied to a
        // negative result to produce the index of the first element (if any) that
        // is larger than the given search value. This is also the index at which
        // the search value should be inserted into the list in order for the list
        // to remain sorted.
        //
        // The method uses the Array.BinarySearch method to perform the
        // search.
        //
        public int BinarySearch(int index, int count, T item, IComparer<T> comparer)
        {
            if (index < 0)
                ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
            if (count < 0)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            if (_size - index < count)
                ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);
            Contract.Ensures(Contract.Result<int>() <= index + count);
            Contract.EndContractBlock();

            return Array.BinarySearch<T>(_items, index, count, item, comparer);
        }

        public int BinarySearch(T item)
        {
            Contract.Ensures(Contract.Result<int>() <= Count);
            return BinarySearch(0, Count, item, null);
        }

        public int BinarySearch(T item, IComparer<T> comparer)
        {
            Contract.Ensures(Contract.Result<int>() <= Count);
            return BinarySearch(0, Count, item, comparer);
        }

        // Clears the contents of RefList.
        public void Clear()
        {
            if (TypeHelper<T>.FixedSize <= 0)
            {
                int size = _size;
                _size = 0;
                _version++;
                if (size > 0)
                {
                    Array.Clear(_items, 0, size); // Clear the elements so that the gc can reclaim the references.
                }
            }
            else
            {
                _size = 0;
                _version++;
            }
        }

        // Contains returns true if the specified element is in the RefList.
        // It does a linear, O(n) search.  Equality is determined by calling
        // EqualityComparer<T>.Binary.Equals().

        public bool Contains(T item)
        {
            // PERF: IndexOf calls Array.IndexOf, which internally
            // calls EqualityComparer<T>.Binary.IndexOf, which
            // is specialized for different types. This
            // boosts performance since instead of making a
            // virtual method call each iteration of the loop,
            // via EqualityComparer<T>.Binary.Equals, we
            // only make one virtual call to EqualityComparer.IndexOf.

            return _size != 0 && IndexOf(item) != -1;
        }

        bool System.Collections.IList.Contains(object item)
        {
            if (IsCompatibleObject(item))
            {
                return Contains((T)item);
            }
            return false;
        }

        //public RefList<TOutput> ConvertAll<TOutput>(Converter<T, TOutput> converter)
        //{
        //    if (converter == null)
        //    {
        //        ThrowHelper.ThrowArgumentNullException(ExceptionArgument.converter);
        //    }

        //    Contract.EndContractBlock();

        //    RefList<TOutput> list = new RefList<TOutput>(_size);
        //    for (int i = 0; i < _size; i++)
        //    {
        //        list._items[i] = converter(_items[i]);
        //    }
        //    list._size = _size;
        //    return list;
        //}

        // Copies this RefList into array, which must be of a
        // compatible array type.
        //
        public void CopyTo(T[] array)
        {
            CopyTo(array, 0);
        }

        // Copies this RefList into array, which must be of a
        // compatible array type.
        //
        void System.Collections.ICollection.CopyTo(Array array, int arrayIndex)
        {
            if ((array != null) && (array.Rank != 1))
            {
                ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_RankMultiDimNotSupported);
            }
            Contract.EndContractBlock();

            try
            {
                // Array.Copy will check for NULL.
                Array.Copy(_items, 0, array, arrayIndex, _size);
            }
            catch (ArrayTypeMismatchException)
            {
                ThrowHelper.ThrowArgumentException_Argument_InvalidArrayType();
            }
        }

        // Copies a section of this list to the given array at the given index.
        //
        // The method uses the Array.Copy method to copy the elements.
        //
        public void CopyTo(int index, T[] array, int arrayIndex, int count)
        {
            if (_size - index < count)
            {
                ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);
            }
            Contract.EndContractBlock();

            // Delegate rest of error checking to Array.Copy.
            Array.Copy(_items, index, array, arrayIndex, count);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            // Delegate rest of error checking to Array.Copy.
            Array.Copy(_items, 0, array, arrayIndex, _size);
        }

        // Ensures that the capacity of this list is at least the given minimum
        // value. If the current capacity of the list is less than min, the
        // capacity is increased to twice the current capacity or to min,
        // whichever is larger.
        private void EnsureCapacity(int min)
        {
            if (_items.Length < min)
            {
                int newCapacity = _items.Length == 0 ? _defaultCapacity : _items.Length * 2;
                // Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
                // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
                if ((uint)newCapacity > int.MaxValue) newCapacity = int.MaxValue;
                if (newCapacity < min) newCapacity = min;
                Capacity = newCapacity;
            }
        }

        public bool Exists(Predicate<T> match)
        {
            return FindIndex(match) != -1;
        }

        public T Find(Predicate<T> match)
        {
            if (match == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            }
            Contract.EndContractBlock();

            for (int i = 0; i < _size; i++)
            {
                // ReSharper disable once PossibleNullReferenceException
                if (match(_items[i]))
                {
                    return _items[i];
                }
            }
            return default(T);
        }

        public RefList<T> FindAll(Predicate<T> match)
        {
            if (match == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            }
            Contract.EndContractBlock();

            RefList<T> refList = new RefList<T>();
            for (int i = 0; i < _size; i++)
            {
                // ReSharper disable once PossibleNullReferenceException
                if (match(_items[i]))
                {
                    refList.Add(_items[i]);
                }
            }
            return refList;
        }

        public int FindIndex(Predicate<T> match)
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            return FindIndex(0, _size, match);
        }

        public int FindIndex(int startIndex, Predicate<T> match)
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < startIndex + Count);
            return FindIndex(startIndex, _size - startIndex, match);
        }

        public int FindIndex(int startIndex, int count, Predicate<T> match)
        {
            if ((uint)startIndex > (uint)_size)
            {
                ThrowHelper.ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_Index();
            }

            if (count < 0 || startIndex > _size - count)
            {
                ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count();
            }

            if (match == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            }
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < startIndex + count);
            Contract.EndContractBlock();

            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                // ReSharper disable once PossibleNullReferenceException
                if (match(_items[i])) return i;
            }
            return -1;
        }

        public T FindLast(Predicate<T> match)
        {
            if (match == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            }
            Contract.EndContractBlock();

            for (int i = _size - 1; i >= 0; i--)
            {
                // ReSharper disable once PossibleNullReferenceException
                if (match(_items[i]))
                {
                    return _items[i];
                }
            }
            return default(T);
        }

        public int FindLastIndex(Predicate<T> match)
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            return FindLastIndex(_size - 1, _size, match);
        }

        public int FindLastIndex(int startIndex, Predicate<T> match)
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() <= startIndex);
            return FindLastIndex(startIndex, startIndex + 1, match);
        }

        public int FindLastIndex(int startIndex, int count, Predicate<T> match)
        {
            if (match == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            }
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() <= startIndex);
            Contract.EndContractBlock();

            if (_size == 0)
            {
                // Special case for 0 length RefList
                if (startIndex != -1)
                {
                    ThrowHelper.ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_Index();
                }
            }
            else
            {
                // Make sure we're not out of range
                if ((uint)startIndex >= (uint)_size)
                {
                    ThrowHelper.ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_Index();
                }
            }

            // 2nd have of this also catches when startIndex == MAXINT, so MAXINT - 0 + 1 == -1, which is < 0.
            if (count < 0 || startIndex - count + 1 < 0)
            {
                ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count();
            }

            int endIndex = startIndex - count;
            for (int i = startIndex; i > endIndex; i--)
            {
                // ReSharper disable once PossibleNullReferenceException
                if (match(_items[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        public void ForEach(Action<T> action)
        {
            if (action == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action);
            }
            Contract.EndContractBlock();

            int version = _version;

            for (int i = 0; i < _size; i++)
            {
                if (version != _version)
                {
                    break;
                }
                // ReSharper disable once PossibleNullReferenceException
                action(_items[i]);
            }

            if (version != _version)
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
        }

        // Returns an enumerator for this list with the given
        // permission for removal of elements. If modifications made to the list
        // while an enumeration is in progress, the MoveNextAsync and
        // GetObject methods of the enumerator will throw an exception.
        //
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        public RefList<T> GetRange(int index, int count)
        {
            if (index < 0)
            {
                ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
            }

            if (count < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (_size - index < count)
            {
                ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);
            }
            Contract.Ensures(Contract.Result<RefList<T>>() != null);
            Contract.EndContractBlock();

            RefList<T> refList = new RefList<T>(count);
            Array.Copy(_items, index, refList._items, 0, count);
            refList._size = count;
            return refList;
        }

        // Returns the index of the first occurrence of a given value in a range of
        // this list. The list is searched forwards from beginning to end.
        // The elements of the list are compared to the given value using the
        // Object.Equals method.
        //
        // This method uses the Array.IndexOf method to perform the
        // search.
        //
        public int IndexOf(T item)
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            return Array.IndexOf(_items, item, 0, _size);
        }

        int System.Collections.IList.IndexOf(object item)
        {
            if (IsCompatibleObject(item))
            {
                return IndexOf((T)item);
            }
            return -1;
        }

        // Returns the index of the first occurrence of a given value in a range of
        // this list. The list is searched forwards, starting at index
        // index and ending at count number of elements. The
        // elements of the list are compared to the given value using the
        // Object.Equals method.
        //
        // This method uses the Array.IndexOf method to perform the
        // search.
        //
        public int IndexOf(T item, int index)
        {
            if (index > _size)
                ThrowHelper.ThrowArgumentOutOfRange_IndexException();
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            Contract.EndContractBlock();
            return Array.IndexOf(_items, item, index, _size - index);
        }

        // Returns the index of the first occurrence of a given value in a range of
        // this list. The list is searched forwards, starting at index
        // index and upto count number of elements. The
        // elements of the list are compared to the given value using the
        // Object.Equals method.
        //
        // This method uses the Array.IndexOf method to perform the
        // search.
        //
        public int IndexOf(T item, int index, int count)
        {
            if (index > _size)
                ThrowHelper.ThrowArgumentOutOfRange_IndexException();

            if (count < 0 || index > _size - count) ThrowHelper.ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count();
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            Contract.EndContractBlock();

            return Array.IndexOf(_items, item, index, count);
        }

        // Inserts an element into this list at a given index. The size of the list
        // is increased by one. If required, the capacity of the list is doubled
        // before inserting the new element.
        //
        public void Insert(int index, T item)
        {
            // Note that insertions at the end are legal.
            if ((uint)index > (uint)_size)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_ListInsert);
            }
            Contract.EndContractBlock();
            if (_size == _items.Length) EnsureCapacity(_size + 1);
            if (index < _size)
            {
                Array.Copy(_items, index, _items, index + 1, _size - index);
            }
            _items[index] = item;
            _size++;
            _version++;
        }

        void System.Collections.IList.Insert(int index, object item)
        {
            ThrowHelper.IfNullAndNullsAreIllegalThenThrow<T>(item, ExceptionArgument.item);

            try
            {
                Insert(index, (T)item);
            }
            catch (InvalidCastException)
            {
                ThrowHelper.ThrowWrongValueTypeArgumentException(item, typeof(T));
            }
        }

        // Inserts the elements of the given collection at a given index. If
        // required, the capacity of the list is increased to twice the previous
        // capacity or the new size, whichever is larger.  Ranges may be added
        // to the end of the list by setting index to the RefList's size.
        //
        public void InsertRange(int index, IEnumerable<T> collection)
        {
            if (collection == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.collection);
            }

            if ((uint)index > (uint)_size)
            {
                ThrowHelper.ThrowArgumentOutOfRange_IndexException();
            }
            Contract.EndContractBlock();

            ICollection<T> c = collection as ICollection<T>;
            if (c != null)
            {    // if collection is ICollection<T>
                int count = c.Count;
                if (count > 0)
                {
                    EnsureCapacity(_size + count);
                    if (index < _size)
                    {
                        Array.Copy(_items, index, _items, index + count, _size - index);
                    }

                    // If we're inserting a RefList into itself, we want to be able to deal with that.
                    // ReSharper disable once PossibleUnintendedReferenceComparison
                    if (this == c)
                    {
                        // Copy first part of _items to insert location
                        Array.Copy(_items, 0, _items, index, index);
                        // Copy last part of _items back to inserted location
                        Array.Copy(_items, index + count, _items, index * 2, _size - index);
                    }
                    else
                    {
                        c.CopyTo(_items, index);
                    }
                    _size += count;
                }
            }
            else if (index < _size)
            {
                // We're inserting a lazy enumerable. Call Insert on each of the constituent items.
                // ReSharper disable once PossibleNullReferenceException
                using (IEnumerator<T> en = collection.GetEnumerator())
                {
                    while (en.MoveNext())
                    {
                        Insert(index++, en.Current);
                    }
                }
            }
            else
            {
                // We're adding a lazy enumerable because the index is at the end of this list.
                AddEnumerable(collection);
            }
            _version++;
        }

        // Returns the index of the last occurrence of a given value in a range of
        // this list. The list is searched backwards, starting at the end
        // and ending at the first element in the list. The elements of the list
        // are compared to the given value using the Object.Equals method.
        //
        // This method uses the Array.LastIndexOf method to perform the
        // search.
        //
        public int LastIndexOf(T item)
        {
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(Contract.Result<int>() < Count);
            if (_size == 0)
            {  // Special case for empty list
                return -1;
            }
            else
            {
                return LastIndexOf(item, _size - 1, _size);
            }
        }

        // Returns the index of the last occurrence of a given value in a range of
        // this list. The list is searched backwards, starting at index
        // index and ending at the first element in the list. The
        // elements of the list are compared to the given value using the
        // Object.Equals method.
        //
        // This method uses the Array.LastIndexOf method to perform the
        // search.
        //
        public int LastIndexOf(T item, int index)
        {
            if (index >= _size)
                ThrowHelper.ThrowArgumentOutOfRange_IndexException();
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(((Count == 0) && (Contract.Result<int>() == -1)) || ((Count > 0) && (Contract.Result<int>() <= index)));
            Contract.EndContractBlock();
            return LastIndexOf(item, index, index + 1);
        }

        // Returns the index of the last occurrence of a given value in a range of
        // this list. The list is searched backwards, starting at index
        // index and upto count elements. The elements of
        // the list are compared to the given value using the Object.Equals
        // method.
        //
        // This method uses the Array.LastIndexOf method to perform the
        // search.
        //
        public int LastIndexOf(T item, int index, int count)
        {
            if ((Count != 0) && (index < 0))
            {
                ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
            }

            if ((Count != 0) && (count < 0))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            }
            Contract.Ensures(Contract.Result<int>() >= -1);
            Contract.Ensures(((Count == 0) && (Contract.Result<int>() == -1)) || ((Count > 0) && (Contract.Result<int>() <= index)));
            Contract.EndContractBlock();

            if (_size == 0)
            {  // Special case for empty list
                return -1;
            }

            if (index >= _size)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_BiggerThanCollection);
            }

            if (count > index + 1)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_BiggerThanCollection);
            }

            return Array.LastIndexOf(_items, item, index, count);
        }

        // Removes the element at the given index. The size of the list is
        // decreased by one.
        //
        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }

            return false;
        }

        void System.Collections.IList.Remove(object item)
        {
            if (IsCompatibleObject(item))
            {
                Remove((T)item);
            }
        }

        // This method removes all items which matches the predicate.
        // The complexity is O(n).
        public int RemoveAll(Predicate<T> match)
        {
            if (match == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            }
            Contract.Ensures(Contract.Result<int>() >= 0);
            Contract.Ensures(Contract.Result<int>() <= Contract.OldValue(Count));
            Contract.EndContractBlock();

            int freeIndex = 0;   // the first free slot in items array

            // Find the first item which needs to be removed.
            // ReSharper disable once PossibleNullReferenceException
            while (freeIndex < _size && !match(_items[freeIndex])) freeIndex++;
            if (freeIndex >= _size) return 0;

            int current = freeIndex + 1;
            while (current < _size)
            {
                // Find the first item which needs to be kept.
                // ReSharper disable once PossibleNullReferenceException
                while (current < _size && match(_items[current])) current++;

                if (current < _size)
                {
                    // copy item to the free slot.
                    _items[freeIndex++] = _items[current++];
                }
            }

            if (TypeHelper<T>.FixedSize <= 0)
            {
                Array.Clear(_items, freeIndex, _size - freeIndex); // Clear the elements so that the gc can reclaim the references.
            }

            int result = _size - freeIndex;
            _size = freeIndex;
            _version++;
            return result;
        }

        // Removes the element at the given index. The size of the list is
        // decreased by one.
        //
        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_size)
            {
                ThrowHelper.ThrowArgumentOutOfRange_IndexException();
            }
            Contract.EndContractBlock();
            _size--;
            if (index < _size)
            {
                Array.Copy(_items, index + 1, _items, index, _size - index);
            }
            if (TypeHelper<T>.FixedSize <= 0)
            {
                _items[_size] = default(T);
            }
            _version++;
        }

        // Removes a range of elements from this list.
        //
        public void RemoveRange(int index, int count)
        {
            if (index < 0)
            {
                ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
            }

            if (count < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (_size - index < count)
                ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);
            Contract.EndContractBlock();

            if (count > 0)
            {
                _size -= count;
                if (index < _size)
                {
                    Array.Copy(_items, index + count, _items, index, _size - index);
                }

                _version++;
                if (TypeHelper<T>.FixedSize <= 0)
                {
                    Array.Clear(_items, _size, count);
                }
            }
        }

        // Reverses the elements in this list.
        public void Reverse()
        {
            Reverse(0, Count);
        }

        // Reverses the elements in a range of this list. Following a call to this
        // method, an element in the range given by index and count
        // which was previously located at index i will now be located at
        // index index + (index + count - i - 1).
        //
        public void Reverse(int index, int count)
        {
            if (index < 0)
            {
                ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
            }

            if (count < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (_size - index < count)
                ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);
            Contract.EndContractBlock();

            if (count > 1)
            {
                Array.Reverse(_items, index, count);
            }
            _version++;
        }

        // Sorts the elements in this list.  Uses the default comparer and
        // Array.Sort.
        public void Sort()
        {
            Sort(0, Count, null);
        }

        // Sorts the elements in this list.  Uses Array.Sort with the
        // provided comparer.
        public void Sort(IComparer<T> comparer)
        {
            Sort(0, Count, comparer);
        }

        // Sorts the elements in a section of this list. The sort compares the
        // elements to each other using the given IComparer interface. If
        // comparer is null, the elements are compared to each other using
        // the IComparable interface, which in that case must be implemented by all
        // elements of the list.
        //
        // This method uses the Array.Sort method to sort the elements.
        //
        public void Sort(int index, int count, IComparer<T> comparer)
        {
            if (index < 0)
            {
                ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
            }

            if (count < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (_size - index < count)
                ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);
            Contract.EndContractBlock();

            if (count > 1)
            {
                Array.Sort<T>(_items, index, count, comparer);
            }
            _version++;
        }

        // ToArray returns an array containing the contents of the RefList.
        // This requires copying the RefList, which is an O(n) operation.
        public T[] ToArray()
        {
            Contract.Ensures(Contract.Result<T[]>() != null);
            Contract.Ensures(Contract.Result<T[]>().Length == Count);

            if (_size == 0)
            {
                return _emptyArray;
            }

            T[] array = new T[_size];
            Array.Copy(_items, 0, array, 0, _size);
            return array;
        }

        // Sets the capacity of this list to the size of the list. This method can
        // be used to minimize a list's memory overhead once it is known that no
        // new elements will be added to the list. To completely clear a list and
        // release all memory referenced by the list, execute the following
        // statements:
        //
        // list.Clear();
        // list.TrimExcess();
        //
        public void TrimExcess()
        {
            int threshold = (int)(((double)_items.Length) * 0.9);
            if (_size < threshold)
            {
                Capacity = _size;
            }
        }

        public bool TrueForAll(Predicate<T> match)
        {
            if (match == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            }
            Contract.EndContractBlock();

            for (int i = 0; i < _size; i++)
            {
                // ReSharper disable once PossibleNullReferenceException
                if (!match(_items[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private void AddEnumerable(IEnumerable<T> enumerable)
        {
            Debug.Assert(enumerable != null);
            Debug.Assert(!(enumerable is ICollection<T>), "We should have optimized for this beforehand.");

            using (IEnumerator<T> en = enumerable.GetEnumerator())
            {
                _version++; // Even if the enumerable has no items, we can update _version.

                while (en.MoveNext())
                {
                    // Capture Current before doing anything else. If this throws
                    // an exception, we want to make a clean break.
                    T current = en.Current;

                    if (_size == _items.Length)
                    {
                        EnsureCapacity(_size + 1);
                    }

                    _items[_size++] = current;
                }
            }
        }

        [Serializable]
        public struct Enumerator : IEnumerator<T>
        {
            private RefList<T> _refList;
            private int _index;
            private int _version;
            private T _current;

            internal Enumerator(RefList<T> refList)
            {
                _refList = refList;
                _index = 0;
                _version = refList._version;
                _current = default!;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                RefList<T> localRefList = _refList;

                if (_version == localRefList._version && ((uint)_index < (uint)localRefList._size))
                {
                    _current = localRefList._items[_index];
                    _index++;
                    return true;
                }
                return MoveNextRare();
            }

            private bool MoveNextRare()
            {
                if (_version != _refList._version)
                {
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
                }

                _index = _refList._size + 1;
                _current = default!;
                return false;
            }

            public T Current => _current;

            object System.Collections.IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || _index == _refList._size + 1)
                    {
                        ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();
                    }
                    return Current!;
                }
            }

            void System.Collections.IEnumerator.Reset()
            {
                if (_version != _refList._version)
                {
                    ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();
                }

                _index = 0;
                _current = default!;
            }
        }
    }
}
