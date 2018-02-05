// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    // TODO (docs) refine the docs, merge summary and remarks with clearer wording. Remarks > summary.

    /// <summary>
    /// A struct that wraps a <see cref="Memory{T}"/> and its <see cref="MemoryHandle"/> that is returned after calling <see cref="Memory{T}.Retain"/>.
    /// Increases the ref count of underlying OwnedBuffer by one.
    /// Use this struct carefully: it must always be explicitly disposed, otherwise underlying OwnedPooledArray
    /// will never be returned to the pool and memory will leak.
    /// Use <see cref="Clone"/> method to create a copy of this buffer and ensure that the underlying <see cref="Buffers.OwnedPooledArray{T}"/> is not returned to the pool.
    /// When adding to a Spreads disposable collection (e.g. SortedMap) ownership is transfered to a collection and PreservedBuffer
    /// will be disposed during disposal of that collection. To keep ownership outside the collection, use the <see cref="Clone"/> method and
    /// add a cloned PreservedBuffer value to the collection.
    /// </summary>
    /// <remarks>
    /// <see cref="PreservedBuffer{T}"/> is the owner of <see cref="MemoryHandle"/> reservation. 
    /// When it is passed to any method or added  to any collection the reservation ownership is transfered as well. 
    /// The consuming method or collection must dispose the <see cref="MemoryHandle"/> reservation. If the caller
    /// needs to retain the buffer and must call <see cref="Clone"/> and pass the cloned buffer.
    /// </remarks>
    public struct PreservedBuffer<T> : IReadOnlyList<T>, IDisposable, IPreservedBuffer
    {
        private MemoryHandle _reservation;

        /// <summary>
        /// Create a new PreservedBuffer structure.
        /// </summary>
        /// <param name="buffer"></param>
        public PreservedBuffer(Memory<T> buffer)
        {
            Buffer = buffer;
            _reservation = buffer.Retain();
        }

        /// <summary>
        /// Buffer
        /// </summary>
        public Memory<T> Buffer { get; private set; }

        /// <summary>
        /// A shortcut to Buffer.Span property.
        /// </summary>
        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Buffer.Span; }
        }

        /// <summary>
        /// Gets the number of elements in the PreservedBuffer.
        /// </summary>
        [Obsolete("Use Length property instead")]
        public int Count => Buffer.Length;

        /// <summary>
        /// Gets the number of elements in the PreservedBuffer.
        /// </summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Buffer.Length; }
        }

        /// <summary>
        /// Gets the element at the specified index in the PreservedBuffer.
        /// </summary>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Buffer.Span[index]; }
        }

        /// <summary>
        /// Release a reference of the underlying OwnedBuffer.
        /// </summary>
        public void Dispose()
        {
            _reservation.Dispose();
            Buffer = default(Memory<T>);
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
        [Obsolete("TODO Review efficient Span enumeration, both Span and Memory do not implement IEnumeratble (yet)")]
        public IEnumerator<T> GetEnumerator()
        {  
            for (int i = 0; i < Buffer.Length; i++)
            {
                yield return Buffer.Span[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Type ElementType => typeof(T);
    }


    internal interface IPreservedBuffer : IDisposable
    {
        Type ElementType { get; }
    }
}