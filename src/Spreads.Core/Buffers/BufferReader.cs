using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Buffers
{
    public ref struct BufferReader
    {
        internal int _offset;
        internal readonly Span<byte> _span;

        public BufferReader(Span<byte> span)
        {
            _offset = 0;
            _span = span;
        }

        public int Offset
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _offset;
        }

        public ref byte Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.AsRef(in _span[_offset]); // ref Unsafe.AddByteOffset(ref _span.GetPinnableReference(), (IntPtr)_offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read<T>(out T value) where T : unmanaged
        {
            var size = Unsafe.SizeOf<T>();
            var nextOffset = _offset + size;
            if (nextOffset > _span.Length)
            {
                value = default;
                return 0;
            }
            value = Unsafe.ReadUnaligned<T>(ref Current);

            _offset = nextOffset;
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DangerousRead<T>(out T value) where T : unmanaged
        {
            var size = Unsafe.SizeOf<T>();
            value = Unsafe.ReadUnaligned<T>(ref Current);
            _offset += size;
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DangerousRead2<T>() where T : unmanaged
        {
            var value = Unsafe.ReadUnaligned<T>(ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(_span), (IntPtr)_offset));
            _offset += Unsafe.SizeOf<T>();
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DangerousRead(out byte value)
        {
            value = _span[_offset];
            _offset++;
            return true;
        }

        //public byte Read()
        //{
        //    value = Unsafe.ReadUnaligned<T>(ref Unsafe.AsRef(in _span[_offset]));
        //    var size = Unsafe.SizeOf<T>();
        //    _offset++;
        //    return size;
        //}
    }
}