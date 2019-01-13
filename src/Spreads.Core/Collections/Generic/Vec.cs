// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Buffers;
using Spreads.Serialization;

namespace Spreads.Collections.Generic
{
    // Follow Joe Duffy's Slices.net basic idea of accessing items efficiently. (TODO test)
    // Ownership is out of scope, Vector is constructed either from a pointer+length or T[]+offset+length.
    // Object stores an array T[].
    // Offset in bytes. When Object is not null offset also includes array object header before the first element.
    // Length in items.
    // Capacity in items.


    // What to optimize:
    // Reads! There must be separate methods to create entire vectors from arrays if ctors are not enough. 
    // We should not create vectors element-by-element, this case is only for real-time data and there is 
    // no such data that be faster than List<T>-like set/insert. Mostly it will be an update and it will be super fast.
    // Reads must be as fast as possible even element-wise, we do not only calculate aggregates.


    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public unsafe struct Vec<T>
    {
        private int _itemCapacity;
        private int _itemLength;
        private byte* _offset;
        private T[] _array;

        private readonly void* _pointer;

        //public Vec(int capacity)
        //{

        //}


        //public Vec(T[] array, int offset, int length)
        //{
        //    _array = array;
        //    _offset = (byte*)(TypeHelper<T>.ElemOffset + offset * TypeHelper<T>.ElemSize);
        //    _buffer = DirectBuffer.Invalid;
        //    _pointer = default;
        //}

        //public Vec(void* pointer, int length)
        //{
        //    _array = null;
        //    _buffer = buffer;
        //    _pointer = _buffer._pointer;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public T Get<T>(int index)
        //{
        //    if (_buffer.IsValid)
        //    {
        //        return Unsafe.ReadUnaligned<T>(Unsafe.Add<T>(_pointer, index));
        //    }
        //    else
        //    {
        //        return Unsafe.As<T[]>(_array)[index]; // _array[index];
        //    }
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public void Set<T>(int index, T value)
        //{
        //    if (_buffer.IsValid)
        //    {
        //        Unsafe.WriteUnaligned<T>(Unsafe.Add<T>(_pointer, index), value);
        //    }
        //    else
        //    {
        //        Unsafe.As<T[]>(_array)[index] = value;
        //    }
        //}
    }
}
