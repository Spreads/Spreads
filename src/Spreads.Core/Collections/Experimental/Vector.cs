using Spreads.Buffers;
using Spreads.Serialization;
using System;
using System.Runtime.CompilerServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Collections.Experimental
{
    // Theory:
    // Store non-pinnable types as T[]
    // Store pinnable types as pinned memory
    // Do check on TypeHelper

    public readonly unsafe struct Vector<T>
    {
        public static readonly bool IsPinnable = TypeHelper<T>.IsPinnable;

        private readonly T[] _array;

        private readonly DirectBuffer _buffer;
        private readonly void* _pointer;

        public Vector(T[] array)
        {
            _array = array;
            _buffer = DirectBuffer.Invalid;
            _pointer = default;
        }

        public Vector(DirectBuffer buffer)
        {
            _array = null;
            _buffer = buffer;
            _pointer = _buffer._pointer;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_buffer.IsValid)
                {
                    return ReadUnaligned<T>(Add<T>(_pointer, index));
                }
                else
                {
                    return _array[index]; // _array[index];
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (_buffer.IsValid)
                {
                    WriteUnaligned<T>(Add<T>(_pointer, index), value);
                }
                else
                {
                    _array[index] = value;
                }
            }
        }
    }

    public readonly unsafe struct Vector
    {
        private readonly Array _array;

        private readonly DirectBuffer _buffer;
        private readonly void* _pointer;

        public Vector(Array array)
        {
            _array = array;
            _buffer = DirectBuffer.Invalid;
            _pointer = default;
        }

        public Vector(DirectBuffer buffer)
        {
            _array = null;
            _buffer = buffer;
            _pointer = _buffer._pointer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>(int index)
        {
            if (_buffer.IsValid)
            {
                return ReadUnaligned<T>(Add<T>(_pointer, index));
            }
            else
            {
                return As<T[]>(_array)[index]; // _array[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, T value)
        {
            if (_buffer.IsValid)
            {
                WriteUnaligned<T>(Add<T>(_pointer, index), value);
            }
            else
            {
                As<T[]>(_array)[index] = value;
            }
        }
    }
}
