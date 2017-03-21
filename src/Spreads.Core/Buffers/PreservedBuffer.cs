// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Buffers
{
    // NB this pattern will soon be added to CoreFxLab upstream, but for now
    // imitate the API with *Pr*eserved instead of *R*eserved names.
    // We achieve safe disposal by always passing ownership of a buffer segment
    // and never having two places working with the same segment.

    public struct PreservedBuffer<T> : IDisposable
    {
        private DisposableReservation<T> _reservation;

        public PreservedBuffer(Buffer<T> buffer)
        {
            Buffer = buffer;
            _reservation = buffer.Reserve();
        }

        public Buffer<T> Buffer { get; }

        public void Dispose()
        {
            _reservation.Dispose();
        }
    }

    public static class PreservedMemoryExtension
    {
        public static PreservedBuffer<T> Preserve<T>(this Buffer<T> buffer)
        {
            return new PreservedBuffer<T>(buffer);
        }

        public static FixedMemory<T> Fix<T>(this Buffer<T> buffer)
        {
            return new FixedMemory<T>(buffer);
        }

        public static FixedMemory<T> Fix<T>(this PreservedBuffer<T> buffer)
        {
            return new FixedMemory<T>(buffer.Buffer);
        }
    }

    // On-demand pinning, temp solution
    public struct FixedMemory<T> : IDisposable
    {
        private GCHandle _handle;

        public unsafe FixedMemory(Buffer<T> buffer)
        {
            Buffer = buffer;
            void* tmp;
            if (buffer.TryGetPointer(out tmp))
            {
                Pointer = new IntPtr(tmp);
                _handle = default(GCHandle);
            }
            else
            {
                ArraySegment<T> tmp2;
                if (buffer.TryGetArray(out tmp2))
                {
                    _handle = GCHandle.Alloc(tmp2.Array, GCHandleType.Pinned);
                    Pointer = new IntPtr((byte*)_handle.AddrOfPinnedObject() // address of array start
                        + ((ulong)Unsafe.SizeOf<T>() * (ulong)tmp2.Offset)); // byte offset
                }
                else
                {
                    throw new ApplicationException();
                }
            }
        }

        public IntPtr Pointer { get; }

        public Buffer<T> Buffer { get; }

        public void Dispose()
        {
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
    }
}