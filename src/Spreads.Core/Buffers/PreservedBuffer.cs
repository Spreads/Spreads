// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;

namespace Spreads.Buffers
{

    // TODO IReadOnlyList

    /// <summary>
    /// A struct that wraps a System.Memory.Buffer and its DisposableReservation that is returned after calling buffer.Reserver().
    /// Increases the ref count of underlying OwnedBuffer by one.
    /// Use this struct carefully: it must always be explicitly disposed, otherwise underlying OwnedPooledArray
    /// will never be returned to the pool and memory will leak.
    /// Use Clone() method to create a copy of this buffer and ensure that the underlying OwnedPooledArray is not returned to the pool.
    /// When adding to a Spreads disposable collection (e.g. SortedMap) ownership is transfered to a collection and PreservedBuffer
    /// will be disposed during disposal of that collection. To keep ownership outside the collection, use PreservedBuffer.Clone() method and 
    /// add a cloned PreservedBuffer value to the collection.
    /// </summary>
    public struct PreservedBuffer<T> : IReadOnlyList<T>, IDisposable
    {
        private DisposableReservation<T> _reservation;

        /// <summary>
        /// Create a new PreservedBuffer structure.
        /// </summary>
        /// <param name="buffer"></param>
        public PreservedBuffer(Buffer<T> buffer)
        {
            Buffer = buffer;
            _reservation = buffer.Reserve();
        }

        /// <summary>
        /// Buffer
        /// </summary>
        public Buffer<T> Buffer { get; private set; }

        /// <summary>
        /// A shortcut to Buffer.Span property.
        /// </summary>
        public Span<T> Span => Buffer.Span;

        /// <summary>
        /// Gets the number of elements in the PreservedBuffer.
        /// </summary>
        public int Count => Buffer.Length;

        /// <summary>
        /// Gets the element at the specified index in the PreservedBuffer.
        /// </summary>
        public T this[int index] => Buffer.Span[index];

        /// <summary>
        /// Release a reference of the underlying OwnedBuffer.
        /// </summary>
        public void Dispose()
        {
            _reservation.Dispose();
            Buffer = default(Buffer<T>);
        }

        /// <summary>
        /// Increment the underlying OwnedBuffer reference count and return a copy of this preserved buffer.
        /// </summary>
        public PreservedBuffer<T> Clone()
        {
            return new PreservedBuffer<T>(Buffer);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the PreservedBuffer.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            var span = Buffer.Span;
            for (int i = 0; i < Buffer.Length; i++)
            {
                yield return span[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}