/*
    Copyright(c) 2014-2015 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

// inspired by https://github.com/real-logic/simple-binary-encoding/blob/b8316bba72dfbf1ea0939bad79e9f3c56626d90d/main/csharp/DirectBuffer.cs
// to be used via UnmanagedMemoryAccessor/Stream or as SafeBuffer - they are safe and do bounds check.
// For unsafe direct access to the underlying buffer use a DirectBuffer struct - it does not check bounds
// and is a struct, which makes it more lightweight than PinnedBuffer. All unsafe methods on 
// DirectBuffer are internal, but there is an implicit cast and pinned buffer could be 
// wrapped around DirectBuffer.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace Spreads.Serialization {

    // TODO pinning of arrays is only needed when
    //  - DirectBuffer property is accessed
    //  - Unmanaged Accessor/Stream is created
    // For all other cases, we could use fixed() which is much kinder to GC
    // TODO add back all read/write methods from DB, and use fixed where possible instead of pinning
    // The Move(*byte) method should work without pinning but using fixed()


    internal sealed class FixedBufferAccessor : UnmanagedMemoryAccessor {
        [SecurityCritical]
        internal FixedBufferAccessor(SafeBuffer buffer, long offset, long length, bool readOnly) {
            Debug.Assert(buffer != null, "buffer is null");
            Initialize(buffer, offset, length, readOnly ? FileAccess.Read : FileAccess.ReadWrite);
        }

        protected override void Dispose(bool disposing) {
            try {
                // todo?
            } finally {
                base.Dispose(disposing);
            }
        }
    }


    internal unsafe sealed class FixedBufferStream : UnmanagedMemoryStream {
        [SecurityCritical]
        internal FixedBufferStream(SafeBuffer buffer, long offset, long length, bool readOnly) {
            Debug.Assert(buffer != null, "buffer is null");
            Initialize(buffer, offset, length, readOnly ? FileAccess.Read : FileAccess.ReadWrite);
        }

        protected override void Dispose(bool disposing) {
            try {
                // todo?
            } finally {
                base.Dispose(disposing);
            }
        }
    }


    /// <summary>
    /// Provides read/write opertaions on a byte buffer that is fixed in memory.
    /// </summary>
    public sealed unsafe class FixedBuffer {
#if PRERELEASE
        static FixedBuffer() {
            if (!BitConverter.IsLittleEndian) {
                // NB we just do not care to support BigEndian. This must be documented. 
                // But it is OK for debugging to leave such a time bomb here.
                // See Aeron docs why BigEndian probably won't be supported even by them.
                throw new NotSupportedException("BigEndian systems are not supported with the current implementation.");
            }
        }
#endif

        /// <summary>
        /// Delegate invoked if buffer size is too small. 
        /// </summary>
        /// <param name="existingBufferSize"></param>
        /// <param name="requestedBufferSize"></param>
        /// <param name="existingBuffer">If this fixed buffer was created from a byte array, the array will be returned e.g. to add it back to a pool</param>

        /// <returns>New buffer, or null if reallocation is not possible</returns>
        public delegate byte[] BufferRecyleDelegate(long existingBufferSize, long requestedBufferSize, byte[] existingBuffer = null);

        public BufferRecyleDelegate BufferRecylce {
            get { return _bufferRecylce; }
            set { _bufferRecylce = value; }
        }
        // default recyling is to use a buffer pool from settings
        private BufferRecyleDelegate _bufferRecylce = (eSize, rSize, eBuffer) => {
            if (eBuffer != null && eBuffer.LongLength <= int.MaxValue) {
                OptimizationSettings.ArrayPool.ReturnBuffer<byte>(eBuffer);
            }
            // TODO to long. Now array pool doesn't support long and probably should not.
            // If we ever allocate large buffer > 2 Gb, we probably want to keep it forever
            if (rSize > int.MaxValue) return new byte[rSize];
            var newBuffer = OptimizationSettings.ArrayPool.TakeBuffer<byte>((int)rSize);
            return newBuffer;
        };

        private DirectBuffer _directBuffer;
        //private int _directBuffer.Length;
        //internal byte* _directBuffer.data;
        private byte[] _array;
        private bool _disposed;
        private GCHandle _pinnedGCHandle;
        private bool _needToFreeGCHandle;



        /// <summary>
        /// Attach a view to a byte[] for providing direct access.
        /// </summary>
        /// <param name="buffer">buffer to which the view is attached.</param>
        public FixedBuffer(byte[] buffer) {
            Wrap(buffer);
        }

        /// <summary>
        /// Create a new FixedBuffer with a new empty array
        /// </summary>
        /// <param name="length">buffer to which the view is attached.</param>
        public FixedBuffer(int length) {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            byte[] buffer = BufferRecylce == null ? new byte[length] : BufferRecylce(0, length, null);
            Debug.Assert(_array == null, "_buffer is null here, do not assign, avoid double recycling.");
            Wrap(buffer);
        }


        /// <summary>
        /// Attach a view to an unmanaged buffer owned by external code
        /// </summary>
        /// <param name="pBuffer">Unmanaged byte buffer</param>
        /// <param name="bufferLength">Length of the buffer</param>
        public FixedBuffer(long bufferLength, byte* pBuffer) {
            Wrap(bufferLength, pBuffer);
        }

        /// <summary>
        /// Creates a FixedBuffer that can later be wrapped
        /// </summary>
        public FixedBuffer() {
        }

        /// <summary>
        /// Recycles an existing <see cref="FixedBuffer"/>
        /// </summary>
        /// <param name="byteArray">The byte array that will act as the backing buffer.</param>
        public void Wrap(byte[] byteArray) {
            if (byteArray == null) throw new ArgumentNullException("byteArray");
            FreeGCHandle();

            if (BufferRecylce != null && _array != null) {
                // return previous buffer for recylcing
                BufferRecylce(_array.Length, 0, _array);
            }
            _array = byteArray;
        }


        // we postpone pinning array as much as possible
        private void PinArray() {
            if (_array != null && !_needToFreeGCHandle) {
                // pin the buffer so it does not get moved around by GC, this is required since we use pointers
                _pinnedGCHandle = GCHandle.Alloc(_array, GCHandleType.Pinned);
                _needToFreeGCHandle = true;
                _directBuffer = new DirectBuffer(_array.Length,
                    (IntPtr)_pinnedGCHandle.AddrOfPinnedObject().ToPointer());
            }
        }


        /// <summary>
        /// Recycles an existing <see cref="FixedBuffer"/> from an unmanaged byte buffer owned by external code
        /// </summary>
        /// <param name="pBuffer">Unmanaged byte buffer</param>
        /// <param name="bufferLength">Length of the buffer</param>
        public void Wrap(long bufferLength, byte* pBuffer) {
            if (pBuffer == null) throw new ArgumentNullException("pBuffer");
            if (bufferLength <= 0) throw new ArgumentException("Buffer size must be > 0", "bufferLength");

            FreeGCHandle();

            _directBuffer = new DirectBuffer(bufferLength, (IntPtr)pBuffer);
            _needToFreeGCHandle = false;
        }


        // TODO(?) remove this copy/move methods or add bound checks

        /// <summary>
        /// TODO Move to Bootstrapper
        /// also see this about cpblk http://frankniemeyer.blogspot.de/2014/07/methods-for-reading-structured-binary.html
        /// </summary>
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr memcpy(IntPtr dest, IntPtr src, UIntPtr count);

        /// <summary>
        /// Copy this buffer to a pointer
        /// </summary>
        public void Copy(byte* destination, long srcOffset, long length) {
            if (_array != null && (srcOffset + length < int.MaxValue)) {
                Marshal.Copy(_array, (int)srcOffset, (IntPtr)destination, (int)length);
            } else {
                memcpy((IntPtr)destination, (IntPtr)(_directBuffer.data.ToInt64() + srcOffset), (UIntPtr)length);
            }
        }

        /// <summary>
        /// Copy data and move the fixed buffer to the new location
        /// </summary>
        public FixedBuffer Move(byte* destination, int srcOffset, int length) {
            if (_array != null) {
                Marshal.Copy(_array, srcOffset, (IntPtr)destination, length);
                FreeGCHandle();
                if (BufferRecylce != null) {
                    // return previous buffer for recylcing
                    BufferRecylce(_array.Length, 0, _array);
                } else {
                    _array = null;
                }
            } else {
                memcpy((IntPtr)destination, _directBuffer.data + srcOffset, (UIntPtr)length);
            }
            Wrap(length, destination);
            return this;
        }

        [Obsolete("TODO use longs")]
        public void Copy(byte[] destination, int srcOffset, int destOffset, int length) {
            if (_array != null) {
                System.Array.Copy(_array, srcOffset, destination, destOffset, length);
                FreeGCHandle();
            } else {
                Marshal.Copy(_directBuffer.data + srcOffset, destination, destOffset, length);
            }
        }

        [Obsolete("TODO use longs")]
        public FixedBuffer Move(byte[] destination, int srcOffset, int destOffset, int length) {
            if (_array != null) {
                System.Array.Copy(_array, srcOffset, destination, destOffset, length);
                FreeGCHandle();
                if (BufferRecylce != null) {
                    // return previous buffer for recylcing
                    BufferRecylce(_array.Length, 0, _array);
                }
            } else {
                Marshal.Copy(_directBuffer.data, destination, srcOffset, length);
            }
            Wrap(destination);
            return this;
        }


        /// <summary>
        /// Capacity of the underlying buffer
        /// </summary>
        //public long Capacity {
        //    get { return _directBuffer.Capacity; }
        //}

        [Obsolete("We should not care if the buffer is backed by an array or a pointer, this is an implementation detail")]
        public byte[] Array {
            get { return _array; }
        }

        public DirectBuffer DirectBuffer {
            get {
                PinArray();
                return _directBuffer;
            }
        }



        /// <summary>
        /// Check that a given limit is not greater than the capacity of a buffer from a given offset.
        /// </summary>
        /// <param name="limit">limit access is required to.</param>
        public void CheckLimit(int limit) {
            if (limit > _directBuffer.Capacity) {
                if (BufferRecylce == null) {
                    throw new IndexOutOfRangeException(string.Format("limit={0} is beyond capacity={1}", limit,
                        _directBuffer.Capacity));
                }
                var newBuffer = BufferRecylce(_directBuffer.Capacity, limit, _array);

                if (newBuffer == null) {
                    throw new IndexOutOfRangeException(string.Format("limit={0} is beyond capacity={1}", limit,
                        _directBuffer.Capacity));
                }

                Trace.Assert(_directBuffer.Capacity <= int.MaxValue);
                Marshal.Copy(_directBuffer.data, newBuffer, 0, (int)_directBuffer.Capacity);
                Wrap(newBuffer);
            }
        }

        // using accessor could be 2x slower http://ayende.com/blog/163138/memory-mapped-files-file-i-o-performance
        // but it is bound-checked

        /// <summary>
        /// 
        /// </summary>
        public UnmanagedMemoryAccessor CreateAccessor(long offset = 0, long length = 0, bool readOnly = false) {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (length + offset > _directBuffer.Capacity) throw new ArgumentException("Length plus offset exceed capacity");

            PinArray();

            if (length == 0) {
                length = _directBuffer.Capacity - offset;
            }
            return new FixedBufferAccessor(CreateSafeBuffer(), offset, length, readOnly);
        }

        /// <summary>
        /// 
        /// </summary>
        public UnmanagedMemoryStream CreateStream(long offset = 0, long length = 0, bool readOnly = false) {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (length + offset > _directBuffer.Capacity) throw new ArgumentException("Length plus offset exceed capacity");

            PinArray();

            if (length == 0) {
                length = _directBuffer.Capacity - offset;
            }
            return new FixedBufferStream(CreateSafeBuffer(), offset, length, readOnly);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public SafeBuffer CreateSafeBuffer() {
            return new SafeFixedBuffer(this);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public new void Dispose() {
            Dispose(true);
        }

        /// <summary>
        /// Destructor for <see cref="FixedBuffer"/>
        /// </summary>
        ~FixedBuffer() {
            Dispose(false);
        }

        private void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
                GC.SuppressFinalize(this);
            }

            FreeGCHandle();
            if (BufferRecylce != null && _array != null) {
                // return previous buffer for recylcing
                BufferRecylce(_array.Length, 0, _array);
            }
            _disposed = true;

        }

        private void FreeGCHandle() {
            if (_needToFreeGCHandle) {
                _pinnedGCHandle.Free();
                _needToFreeGCHandle = false;
            }
        }


        // NB SafeBuffer Read<> is very slow compared to DirectBuffer unsafe methods
        // Use SafeBuffer only when explicitly requested
        internal sealed unsafe class SafeFixedBuffer : SafeBuffer {
            private readonly FixedBuffer _fixedBuffer;

            public SafeFixedBuffer(FixedBuffer fixedBuffer) : base(false) {
                _fixedBuffer = fixedBuffer;
                _fixedBuffer.PinArray();
                base.SetHandle(_fixedBuffer._directBuffer.Data);
                base.Initialize((uint)_fixedBuffer._directBuffer.Capacity);
            }

            protected override bool ReleaseHandle() {
                _fixedBuffer.FreeGCHandle();
                return true;
            }

            protected override void Dispose(bool disposing) {
                _fixedBuffer.Dispose(disposing);
                base.Dispose(disposing);
            }
        }

        public static implicit operator DirectBuffer(FixedBuffer fixedBuffer) {
            return new DirectBuffer(fixedBuffer._directBuffer.Capacity, fixedBuffer._directBuffer.data);
        }

        public static implicit operator FixedBuffer(DirectBuffer directBuffer) {
            return new FixedBuffer(directBuffer.capacity, (byte*)directBuffer.data);
        }
    }


    public static class FixedBufferExtension {
        /// <summary>
        /// 
        /// </summary>
        public static UnmanagedMemoryAccessor GetDirectAccessor(this ArraySegment<byte> arraySegment) {
            var db = new FixedBuffer(arraySegment.Array);
            return db.CreateAccessor(arraySegment.Offset, arraySegment.Count, false);
        }

        /// <summary>
        /// 
        /// </summary>
        public static UnmanagedMemoryStream GetDirectStream(this ArraySegment<byte> arraySegment) {
            var db = new FixedBuffer(arraySegment.Array);
            return db.CreateStream(arraySegment.Offset, arraySegment.Count, false);
        }


    }
}