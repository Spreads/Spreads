// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Spreads.Utils;

#if DEBUG
using System.Diagnostics;
#endif

namespace Spreads.Collections.Generic
{
    /// <summary>
    /// SortedDeque for KeyValuePairs.
    /// </summary>
    public class SortedDeque<T> : IEnumerable<T>
    {
        internal KeyComparer<T> _comparer;
        internal int _firstOffset;
        internal int _count;
        internal int _bufferMask;
        internal T[] _buffer;

        /// <summary>
        /// Create a new instance of SortedDeque with the given comparer.
        /// </summary>
        public SortedDeque(int capacity, KeyComparer<T> comparer)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _comparer = comparer.Equals(default(KeyComparer<T>)) ? throw new ArgumentNullException(nameof(comparer)) : comparer;
            var cap = BitUtil.FindNextPositivePowerOfTwo(capacity);
            _buffer = new T[cap];
            // capacity is always a power of two and we use bitshift instead of modulo
            _bufferMask = _buffer.Length - 1;
        }

        /// <summary>
        /// Create a new instance of SortedDeque with a default comparer.
        /// </summary>
        public SortedDeque(int capacity) : this(capacity, KeyComparer<T>.Default) { }

        /// <summary>
        /// Create a new instance of SortedDeque with a default comparer.
        /// </summary>
        public SortedDeque() : this(2, KeyComparer<T>.Default) { }

        /// <summary>
        /// Number of elements in this SortedDeque.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Current capacity.
        /// </summary>
        public int Capacity => _buffer.Length;

        /// <summary>
        /// Get the first element.
        /// </summary>
        public T First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_count == 0)
                {
                    Spreads.ThrowHelper.ThrowInvalidOperationException("SortedDeque is empty");
                }
                return _buffer[_firstOffset];
            }
        }

        /// <summary>
        /// Get the last element.
        /// </summary>
        public T Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_count == 0)
                {
                    Spreads.ThrowHelper.ThrowInvalidOperationException("SortedDeque is empty");
                }
                var offset = IndexToOffset(_count - 1);
                return _buffer[offset];
            }
        }

        internal void DoubleCapacity()
        {
            T[] copyBuffer(int size)
            {
                var newArray = new T[size];
                if (0 != _firstOffset && _firstOffset + _count >= _buffer.Length)
                {
                    var lengthFromStart = _buffer.Length - _firstOffset;
                    var lengthFromEnd = _count - lengthFromStart;
                    Array.Copy(_buffer, _firstOffset, newArray, 0, lengthFromStart);
                    Array.Copy(_buffer, 0, newArray, lengthFromStart, lengthFromEnd);
                }
                else
                {
                    Array.Copy(_buffer, _firstOffset, newArray, 0, _count);
                }
                return newArray;
            }

            var newCapacity = _buffer.Length * 2;
            if (newCapacity < _count)
            {
                Spreads.ThrowHelper.ThrowInvalidOperationException("Capacity cannot be set to a value less than Count");
            }
            _buffer = copyBuffer(newCapacity);
            _firstOffset = 0;
            _bufferMask = newCapacity - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int IndexToOffset(int index)
        {
            return (index + _firstOffset) & _bufferMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int OffsetToIndex(int offset)
        {
            if (offset >= 0)
            {
                return (_buffer.Length + offset - _firstOffset) & _bufferMask;
            }
            else
            {
                return ~((_buffer.Length + (~offset) - _firstOffset) & _bufferMask);
            }
        }

        /// Offset is the place where a new element must be if we always shift
        /// existing elements to the right. Here, we could shift existing elements
        /// to the left if doing so is faster, so the new element could end up
        /// at offset-1 place.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InsertAtOffset(int insertOffset, T element)
        {
            var offset = insertOffset & _bufferMask;

            // add to the right end
            if (_count == 0 | (offset == _firstOffset + _count) | offset == _firstOffset + _count - _buffer.Length)
            {
                var destination = offset & _bufferMask; // ofset could have been equal to length
                _buffer[destination] = element;
                _count++;
            }
            else if (offset == _firstOffset) // add to the left end
            {
                _firstOffset = (offset + _buffer.Length - 1) & _bufferMask;
                _buffer[_firstOffset] = element;
                _count++;
            }
            else
            {
                // unchecked, assume that offset is inside existing range
                if (_firstOffset + _count > _buffer.Length) // is already a split
                {
                    if (offset < _firstOffset) // we are at the left part of the split [__._>    ___]
                    {
#if DEBUG
                        Trace.Assert(offset < _firstOffset + _count - _buffer.Length);
#endif
                        Array.Copy(_buffer, offset, _buffer, offset + 1, _firstOffset + _count - _buffer.Length - offset);
                    }
                    else // we are at the left part of the split [___    <__._]
                    {
                        Array.Copy(_buffer, _firstOffset, _buffer, _firstOffset - 1, (offset - _firstOffset) + 1);
                        _firstOffset = _firstOffset - 1;
                        offset = offset - 1;
#if DEBUG
                        Trace.Assert(_comparer.Compare(element, _buffer[offset - 1]) > 0);
#endif
                    }
                }
                else
                {
                    if (_firstOffset == 0) // avoid split if possible [>_____     ]
                    {
#if DEBUG
                        Trace.Assert(offset < _count);
#endif
                        Array.Copy(_buffer, offset, _buffer, offset + 1, _count - offset);
                    }
                    else if ((_count - (offset - _firstOffset) <= _count / 2)) // [   _______.>__     ]
                    {
                        if (_firstOffset + _count == _buffer.Length)
                        {
                            _buffer[0] = _buffer[_buffer.Length - 1]; // NB! do not lose the last value
                            Array.Copy(_buffer, offset, _buffer, offset + 1, _count - (offset - _firstOffset) - 1);
                        }
                        else
                        {
                            Array.Copy(_buffer, offset, _buffer, offset + 1, _count - (offset - _firstOffset));
                        }
#if DEBUG
                        Trace.Assert(_comparer.Compare(element, _buffer[offset - 1]) > 0);
#endif
                    }
                    else //[   __<._______     ]
                    {
                        Array.Copy(_buffer, _firstOffset, _buffer, _firstOffset - 1, offset - _firstOffset);
                        offset = offset - 1;
                        _firstOffset = _firstOffset - 1;
#if DEBUG
                        Trace.Assert(_comparer.Compare(element, _buffer[offset - 1]) > 0);
#endif
                    }
                }
                _buffer[offset] = element;
                _count++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T RemoveAtOffset(int removeOffset)
        {
            var offset = removeOffset & _bufferMask;
            var element = _buffer[offset];
            if (_count == 0)
            {
                Spreads.ThrowHelper.ThrowInvalidOperationException("SortedDeque is empty");
            }
            else if ((offset == _firstOffset + _count - 1) | offset == _firstOffset + _count - _buffer.Length - 1) // the right end
            {
                // at the end: this.count=this.count - 1
            }
            else if (offset == _firstOffset)
            {
                _firstOffset = (_firstOffset + 1) & _bufferMask;
                // at the end: this.count=this.count - 1
            }
            else
            {
                // unchecked, assume that offset is inside existing range
                if (_firstOffset + _count > _buffer.Length) // is already a split
                {
                    if (offset < _firstOffset) // we are at the left part of the split [__._<    ___]
                    {
                        Array.Copy(_buffer, offset + 1, _buffer, offset, _firstOffset + _count - _buffer.Length - offset - 1);
                    }
                    else
                    {
                        Array.Copy(_buffer, _firstOffset, _buffer, _firstOffset + 1, (offset - _firstOffset));
                        _firstOffset = _firstOffset + 1;
                    }
                }
                else
                {
                    if ((_count - (offset - _firstOffset) <= _count / 2))// [   _______.<__     ]
                    {
                        Array.Copy(_buffer, offset + 1, _buffer, offset, _count - (offset - _firstOffset) - 1);
                    }
                    else
                    {
                        Array.Copy(_buffer, _firstOffset, _buffer, _firstOffset + 1, offset - _firstOffset); //- 1
                        _firstOffset = _firstOffset + 1;
                    }
                }
            }

            _count = _count - 1;
            return element;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int IndexOfElement(T element)
        {
            var offset = OffsetOfElement(element);
            return OffsetToIndex(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int OffsetOfElement(T element)
        {
            // TODO for small count should do this manually

            if (_firstOffset + _count > _buffer.Length)
            {
                var c = _comparer.Compare(element, _buffer[0]);
                if (c == 0)
                {
                    return 0;
                }
                if (c < 0) // key in the right part of the buffer
                {
                    return Array.BinarySearch(_buffer, _firstOffset, _buffer.Length - _firstOffset,
                        element, _comparer);
                }
                // key in the left part of the buffer
                return Array.BinarySearch(_buffer, 0, _firstOffset - (_buffer.Length - _count),
                    element, _comparer);
            }
            else
            {
                return Array.BinarySearch(_buffer, _firstOffset, _count, element, _comparer);
            }
        }

        /// <summary>
        /// Add a new element.
        /// </summary>
        /// <param name="element"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T element)
        {
            if (!TryAdd(element))
            {
                Spreads.ThrowHelper.ThrowInvalidOperationException("Item already exists");
            }
        }

        /// <summary>
        /// Try add a new element.
        /// </summary>
        /// <param name="element"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(T element)
        {
            // ensure capacity
            if (_count == _buffer.Length)
            {
                DoubleCapacity();
            }
            if (_count == 0)
            {
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(_count - 1)]) > 0)
            {
                // adding to the end
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(0)]) < 0)
            {
                // adding to the front
                var offset = IndexToOffset(0);
                InsertAtOffset(offset, element);
            }
            else
            {
                var offset = OffsetOfElement(element);
                if (offset >= 0)
                {
                    return false;
                }
                InsertAtOffset(~offset, element);
            }
            return true;
        }

        /// <summary>
        /// Add or replace an element.
        /// </summary>
        /// <param name="element"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(T element)
        {
            // ensure capacity
            if (_count == _buffer.Length)
            {
                DoubleCapacity();
            }
            if (_count == 0)
            {
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(_count - 1)]) > 0)
            {
                // adding to the end
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(0)]) < 0)
            {
                // adding to the front
                var offset = IndexToOffset(0);
                InsertAtOffset(offset, element);
            }
            else
            {
                var offset = OffsetOfElement(element);
                if (offset >= 0)
                {
                    _buffer[offset] = element;
                }
                else
                {
                    InsertAtOffset(~offset, element);
                }
            }
        }

        /// <summary>
        /// Remove all elements.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _firstOffset = 0;
            _count = 0;
        }

        /// <summary>
        /// Remove and return the first element.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T RemoveFirst()
        {
            if (_count == 0)
            {
                Spreads.ThrowHelper.ThrowInvalidOperationException("SortedDeque is empty");
            }
            var first = _buffer[_firstOffset];
            _buffer[_firstOffset] = default(T);
            _firstOffset = (_firstOffset + 1) & _bufferMask;
            _count = _count - 1;
            return first;
        }

        /// <summary>
        /// Remove and return the last element.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T RemoveLast()
        {
            if (_count == 0)
            {
                Spreads.ThrowHelper.ThrowInvalidOperationException("SortedDeque is empty");
            }
            var offset = IndexToOffset(_count - 1);
            var last = _buffer[offset];
            _buffer[offset] = default(T);
            _count = _count - 1;
            return last;
        }

        /// <summary>
        /// Remove and return the element.
        /// </summary>
        /// <returns>
        /// Returns an element from this SortedDeque that matches to the given element according to the comparer.
        /// </returns>
        /// <remarks>
        /// This method returns an element instead of void because comparer could take only a part of the given element, and the returned value could be different.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Remove(T element)
        {
            var offset = OffsetOfElement(element);
            if (offset < 0)
            {
                throw new KeyNotFoundException("Element doesn't exist in the SortedDeque");
            }
            return RemoveAtOffset(offset);
        }

        /// <summary>
        /// Remove an element at index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T RemoveAt(int index)
        {
            if (index < 0 | index >= _count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return RemoveAtOffset(IndexToOffset(index));
        }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            return new SortedDequeEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Get an element at the index.
        /// </summary>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _buffer[IndexToOffset(index)]; }
        }

        /// <summary>
        /// SortedDeque enumerator.
        /// </summary>
        public struct SortedDequeEnumerator : IEnumerator<T>
        {
            private readonly SortedDeque<T> _source;
            private int _idx;

            /// <summary>
            /// Create a new SortedDequeEnumerator instance.
            /// </summary>
            public SortedDequeEnumerator(SortedDeque<T> source)
            {
                _source = source;
                _idx = -1;
            }

            /// <inheritdoc />
            public T Current => _source._buffer[_source.IndexToOffset(_idx)];

            object IEnumerator.Current => Current;

            /// <inheritdoc />
            public void Dispose()
            {
                _idx = -1;
            }

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (_idx < _source._count - 1)
                {
                    _idx = _idx + 1;
                    return true;
                }
                return false;
            }

            /// <inheritdoc />
            public void Reset()
            {
                _idx = -1;
            }
        }
    }

    /// <summary>
    /// SortedDeque for KeyValuePairs.
    /// </summary>
    public class SortedDeque<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        internal KVPComparer<TKey, TValue> _comparer;
        internal int _firstOffset;
        internal int _count;
        internal int _bufferMask;
        internal KeyValuePair<TKey, TValue>[] _buffer;

        /// <summary>
        /// Create a new instance of SortedDeque with the given comparer.
        /// </summary>
        public SortedDeque(int capacity, KVPComparer<TKey, TValue> comparer)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            var cap = BitUtil.FindNextPositivePowerOfTwo(capacity);
            _buffer = new KeyValuePair<TKey, TValue>[cap];
            // capacity is always a power of two and we use bitshift instead of modulo
            _bufferMask = _buffer.Length - 1;
        }

        /// <summary>
        /// Create a new instance of SortedDeque with a default comparer.
        /// </summary>
        public SortedDeque(int capacity) : this(capacity, new KVPComparer<TKey, TValue>(KeyComparer<TKey>.Default, KeyComparer<TValue>.Default)) { }

        /// <summary>
        /// Create a new instance of SortedDeque with a default comparer.
        /// </summary>
        public SortedDeque() : this(2, new KVPComparer<TKey, TValue>(KeyComparer<TKey>.Default, KeyComparer<TValue>.Default)) { }

        /// <summary>
        /// Number of elements in this SortedDeque.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Current capacity.
        /// </summary>
        public int Capacity => _buffer.Length;

        /// <summary>
        /// Get the first element.
        /// </summary>
        public KeyValuePair<TKey, TValue> First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_count == 0)
                {
                    Spreads.ThrowHelper.ThrowInvalidOperationException("SortedDeque is empty");
                }
                return _buffer[_firstOffset];
            }
        }

        internal KeyValuePair<TKey, TValue> FirstUnsafe
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _buffer[_firstOffset];
            }
        }

        /// <summary>
        /// Get the last element.
        /// </summary>
        public KeyValuePair<TKey, TValue> Last
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_count == 0)
                {
                    Spreads.ThrowHelper.ThrowInvalidOperationException("SortedDeque is empty");
                }
                var offset = IndexToOffset(_count - 1);
                return _buffer[offset];
            }
        }

        internal KeyValuePair<TKey, TValue> LastUnsafe
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var offset = IndexToOffset(_count - 1);
                return _buffer[offset];
            }
        }

        internal void DoubleCapacity()
        {
            KeyValuePair<TKey, TValue>[] copyBuffer(int size)
            {
                var newArray = new KeyValuePair<TKey, TValue>[size];
                if (0 != _firstOffset && _firstOffset + _count >= _buffer.Length)
                {
                    var lengthFromStart = _buffer.Length - _firstOffset;
                    var lengthFromEnd = _count - lengthFromStart;
                    Array.Copy(_buffer, _firstOffset, newArray, 0, lengthFromStart);
                    Array.Copy(_buffer, 0, newArray, lengthFromStart, lengthFromEnd);
                }
                else
                {
                    Array.Copy(_buffer, _firstOffset, newArray, 0, _count);
                }
                return newArray;
            }

            var newCapacity = _buffer.Length * 2;
            if (newCapacity < _count)
            {
                Spreads.ThrowHelper.ThrowInvalidOperationException("Capacity cannot be set to a value less than Count");
            }
            _buffer = copyBuffer(newCapacity);
            _firstOffset = 0;
            _bufferMask = newCapacity - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int IndexToOffset(int index)
        {
            return (index + _firstOffset) & _bufferMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int OffsetToIndex(int offset)
        {
            if (offset >= 0)
            {
                return (_buffer.Length + offset - _firstOffset) & _bufferMask;
            }
            else
            {
                return ~((_buffer.Length + (~offset) - _firstOffset) & _bufferMask);
            }
        }

        /// Offset is the place where a new element must be if we always shift
        /// existing elements to the right. Here, we could shift existing elements
        /// to the left if doing so is faster, so the new element could end up
        /// at offset-1 place.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InsertAtOffset(int insertOffset, KeyValuePair<TKey, TValue> element)
        {
            var offset = insertOffset & _bufferMask;

            // add to the right end
            if (_count == 0 | (offset == _firstOffset + _count) | offset == _firstOffset + _count - _buffer.Length)
            {
                var destination = offset & _bufferMask; // ofset could have been equal to length
                _buffer[destination] = element;
                _count++;
            }
            else if (offset == _firstOffset) // add to the left end
            {
                _firstOffset = (offset + _buffer.Length - 1) & _bufferMask;
                _buffer[_firstOffset] = element;
                _count++;
            }
            else
            {
                // unchecked, assume that offset is inside existing range
                if (_firstOffset + _count > _buffer.Length) // is already a split
                {
                    if (offset < _firstOffset) // we are at the left part of the split [__._>    ___]
                    {
#if DEBUG
                        Trace.Assert(offset < _firstOffset + _count - _buffer.Length);
#endif
                        Array.Copy(_buffer, offset, _buffer, offset + 1, _firstOffset + _count - _buffer.Length - offset);
                    }
                    else // we are at the left part of the split [___    <__._]
                    {
                        Array.Copy(_buffer, _firstOffset, _buffer, _firstOffset - 1, (offset - _firstOffset) + 1);
                        _firstOffset = _firstOffset - 1;
                        offset = offset - 1;
#if DEBUG
                        Trace.Assert(_comparer.Compare(element, _buffer[offset - 1]) > 0);
#endif
                    }
                }
                else
                {
                    if (_firstOffset == 0) // avoid split if possible [>_____     ]
                    {
#if DEBUG
                        Trace.Assert(offset < _count);
#endif
                        Array.Copy(_buffer, offset, _buffer, offset + 1, _count - offset);
                    }
                    else if ((_count - (offset - _firstOffset) <= _count / 2)) // [   _______.>__     ]
                    {
                        if (_firstOffset + _count == _buffer.Length)
                        {
                            _buffer[0] = _buffer[_buffer.Length - 1]; // NB! do not lose the last value
                            Array.Copy(_buffer, offset, _buffer, offset + 1, _count - (offset - _firstOffset) - 1);
                        }
                        else
                        {
                            Array.Copy(_buffer, offset, _buffer, offset + 1, _count - (offset - _firstOffset));
                        }
#if DEBUG
                        Trace.Assert(_comparer.Compare(element, _buffer[offset - 1]) > 0);
#endif
                    }
                    else //[   __<._______     ]
                    {
                        Array.Copy(_buffer, _firstOffset, _buffer, _firstOffset - 1, offset - _firstOffset);
                        offset = offset - 1;
                        _firstOffset = _firstOffset - 1;
#if DEBUG
                        Trace.Assert(_comparer.Compare(element, _buffer[offset - 1]) > 0);
#endif
                    }
                }
                _buffer[offset] = element;
                _count++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private KeyValuePair<TKey, TValue> RemoveAtOffset(int removeOffset)
        {
            var offset = removeOffset & _bufferMask;
            var element = _buffer[offset];
            if (_count == 0)
            {
                Spreads.ThrowHelper.ThrowInvalidOperationException("SortedDeque is empty");
            }
            else if ((offset == _firstOffset + _count - 1) | offset == _firstOffset + _count - _buffer.Length - 1) // the right end
            {
                // at the end: this.count=this.count - 1
            }
            else if (offset == _firstOffset)
            {
                _firstOffset = (_firstOffset + 1) & _bufferMask;
                // at the end: this.count=this.count - 1
            }
            else
            {
                // unchecked, assume that offset is inside existing range
                if (_firstOffset + _count > _buffer.Length) // is already a split
                {
                    if (offset < _firstOffset) // we are at the left part of the split [__._<    ___]
                    {
                        Array.Copy(_buffer, offset + 1, _buffer, offset, _firstOffset + _count - _buffer.Length - offset - 1);
                    }
                    else
                    {
                        Array.Copy(_buffer, _firstOffset, _buffer, _firstOffset + 1, (offset - _firstOffset));
                        _firstOffset = _firstOffset + 1;
                    }
                }
                else
                {
                    if ((_count - (offset - _firstOffset) <= _count / 2))// [   _______.<__     ]
                    {
                        Array.Copy(_buffer, offset + 1, _buffer, offset, _count - (offset - _firstOffset) - 1);
                    }
                    else
                    {
                        Array.Copy(_buffer, _firstOffset, _buffer, _firstOffset + 1, offset - _firstOffset); //- 1
                        _firstOffset = _firstOffset + 1;
                    }
                }
            }

            _count = _count - 1;
            return element;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int IndexOfElement(KeyValuePair<TKey, TValue> element)
        {
            var offset = OffsetOfElement(element);
            return OffsetToIndex(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int OffsetOfElement(KeyValuePair<TKey, TValue> element)
        {
            // TODO for small count should do this manually

            if (_firstOffset + _count > _buffer.Length)
            {
                var c = _comparer.Compare(element, _buffer[0]);
                if (c == 0)
                {
                    return 0;
                }
                if (c < 0) // key in the right part of the buffer
                {
                    return Array.BinarySearch(_buffer, _firstOffset, _buffer.Length - _firstOffset,
                        element, _comparer);
                }
                // key in the left part of the buffer
                return Array.BinarySearch(_buffer, 0, _firstOffset - (_buffer.Length - _count),
                    element, _comparer);
            }
            else
            {
                return Array.BinarySearch(_buffer, _firstOffset, _count, element, _comparer);
            }
        }

        /// <summary>
        /// Add a new element.
        /// </summary>
        /// <param name="element"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(KeyValuePair<TKey, TValue> element)
        {
            // ensure capacity
            if (_count == _buffer.Length)
            {
                DoubleCapacity();
            }
            if (_count == 0)
            {
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(_count - 1)]) > 0)
            {
                // adding to the end
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(0)]) < 0)
            {
                // adding to the front
                var offset = IndexToOffset(0);
                InsertAtOffset(offset, element);
            }
            else
            {
                var offset = OffsetOfElement(element);
                if (offset >= 0)
                {
                    Spreads.ThrowHelper.ThrowInvalidOperationException("Item already exists");
                }
                InsertAtOffset(~offset, element);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddUnsafe(KeyValuePair<TKey, TValue> element)
        {
            // ensure capacity
            if (_count == _buffer.Length)
            {
                DoubleCapacity();
            }
            if (_count == 0)
            {
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(_count - 1)]) > 0)
            {
                // adding to the end
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(0)]) < 0)
            {
                // adding to the front
                var offset = IndexToOffset(0);
                InsertAtOffset(offset, element);
            }
            else
            {
                var offset = OffsetOfElement(element);
                // NB This prevents inlining, will probably throw with negative buffer index, but the behavior is undefined currently.
                //if (offset >= 0)
                //{
                //    ThrowHelper.ThrowInvalidOperationException("Item already exists");
                //}
                InsertAtOffset(~offset, element);
            }
        }

        /// <summary>
        /// Try add a new element.
        /// </summary>
        /// <param name="element"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(KeyValuePair<TKey, TValue> element)
        {
            // ensure capacity
            if (_count == _buffer.Length)
            {
                DoubleCapacity();
            }
            if (_count == 0)
            {
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(_count - 1)]) > 0)
            {
                // adding to the end
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(0)]) < 0)
            {
                // adding to the front
                var offset = IndexToOffset(0);
                InsertAtOffset(offset, element);
            }
            else
            {
                var offset = OffsetOfElement(element);
                if (offset >= 0)
                {
                    return false;
                }
                InsertAtOffset(~offset, element);
            }
            return true;
        }

        /// <summary>
        /// Add or replace an element.
        /// </summary>
        /// <param name="element"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(KeyValuePair<TKey, TValue> element)
        {
            // ensure capacity
            if (_count == _buffer.Length)
            {
                DoubleCapacity();
            }
            if (_count == 0)
            {
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(_count - 1)]) > 0)
            {
                // adding to the end
                var offset = IndexToOffset(_count);
                InsertAtOffset(offset, element);
            }
            else if (_comparer.Compare(element, _buffer[IndexToOffset(0)]) < 0)
            {
                // adding to the front
                var offset = IndexToOffset(0);
                InsertAtOffset(offset, element);
            }
            else
            {
                var offset = OffsetOfElement(element);
                if (offset >= 0)
                {
                    _buffer[offset] = element;
                }
                else
                {
                    InsertAtOffset(~offset, element);
                }
            }
        }

        /// <summary>
        /// Remove all elements.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _firstOffset = 0;
            _count = 0;
        }

        /// <summary>
        /// Remove and return the first element.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KeyValuePair<TKey, TValue> RemoveFirst()
        {
            if (_count == 0)
            {
                Spreads.ThrowHelper.ThrowInvalidOperationException("SortedDeque is empty");
            }
            return RemoveFirstUnsafe();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValuePair<TKey, TValue> RemoveFirstUnsafe()
        {
            var first = _buffer[_firstOffset];
            _buffer[_firstOffset] = default(KeyValuePair<TKey, TValue>);
            _firstOffset = (_firstOffset + 1) & _bufferMask;
            _count = _count - 1;
            return first;
        }

        /// <summary>
        /// Remove and return the last element.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KeyValuePair<TKey, TValue> RemoveLast()
        {
            if (_count == 0)
            {
                Spreads.ThrowHelper.ThrowInvalidOperationException("SortedDeque is empty");
            }
            return RemoveLastUnsafe();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal KeyValuePair<TKey, TValue> RemoveLastUnsafe()
        {
            var offset = IndexToOffset(_count - 1);
            var last = _buffer[offset];
            _buffer[offset] = default(KeyValuePair<TKey, TValue>);
            _count = _count - 1;
            return last;
        }

        /// <summary>
        /// Remove and return the element.
        /// </summary>
        /// <returns>
        /// Returns an element from this SortedDeque that matches to the given element according to the comparer.
        /// </returns>
        /// <remarks>
        /// This method returns an element instead of void because comparer could take only a part of the given element, and the returned value could be different.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KeyValuePair<TKey, TValue> Remove(KeyValuePair<TKey, TValue> element)
        {
            var offset = OffsetOfElement(element);
            if (offset < 0)
            {
                throw new KeyNotFoundException("Element doesn't exist in the SortedDeque");
            }
            return RemoveAtOffset(offset);
        }

        /// <summary>
        /// Remove an element at index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public KeyValuePair<TKey, TValue> RemoveAt(int index)
        {
            if (index < 0 | index >= _count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return RemoveAtOffset(IndexToOffset(index));
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return new SortedDequeEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Get an element at the index.
        /// </summary>
        public KeyValuePair<TKey, TValue> this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _buffer[IndexToOffset(index)]; }
        }

        /// <summary>
        /// SortedDeque enumerator.
        /// </summary>
        public struct SortedDequeEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly SortedDeque<TKey, TValue> _source;
            private int _idx;

            /// <summary>
            /// Create a new SortedDequeEnumerator instance.
            /// </summary>
            public SortedDequeEnumerator(SortedDeque<TKey, TValue> source)
            {
                _source = source;
                _idx = -1;
            }

            /// <inheritdoc />
            public KeyValuePair<TKey, TValue> Current => _source._buffer[_source.IndexToOffset(_idx)];

            object IEnumerator.Current => Current;

            /// <inheritdoc />
            public void Dispose()
            {
                _idx = -1;
            }

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (_idx < _source._count - 1)
                {
                    _idx = _idx + 1;
                    return true;
                }
                return false;
            }

            /// <inheritdoc />
            public void Reset()
            {
                _idx = -1;
            }
        }
    }
}