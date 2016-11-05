using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Buffers {

    // NB this pattern will soon be added to CoreFxLab upstream, but for now 
    // imitate the API with *Pr*eserved instead of *R*eserved names.
    // We achieve safe disposal by always passing ownership of a memory segment
    // and never having two places working with the same segment.

    public struct PreservedMemory<T> : IDisposable {
        private DisposableReservation _reservation;

        public PreservedMemory(Memory<T> memory) {
            Memory = memory;
            _reservation = memory.Reserve();
        }

        public Memory<T> Memory { get; }

        public void Dispose() {
            _reservation.Dispose();
        }
    }

    public static class PreservedMemoryExtension {
        public static PreservedMemory<T> Preserve<T>(this Memory<T> memory) {
            return new PreservedMemory<T>(memory);
        }

        public static FixedMemory<T> Fix<T>(this Memory<T> memory) {
            return new FixedMemory<T>(memory);
        }

        public static FixedMemory<T> Fix<T>(this PreservedMemory<T> memory) {
            return new FixedMemory<T>(memory.Memory);
        }
    }

    // On-demand pinning, temp solution
    public struct FixedMemory<T> : IDisposable {
        private GCHandle _handle;

        public unsafe FixedMemory(Memory<T> memory) {
            Memory = memory;
            void* tmp;
            if (memory.TryGetPointer(out tmp)) {
                Pointer = new IntPtr(tmp);
                _handle = default(GCHandle);
            } else {
                ArraySegment<T> tmp2;
                if (memory.TryGetArray(out tmp2)) {
                    _handle = GCHandle.Alloc(tmp2.Array, GCHandleType.Pinned);
                    Pointer = new IntPtr((byte*)_handle.AddrOfPinnedObject() // address of array start
                        + ((ulong)Unsafe.SizeOf<T>() * (ulong)tmp2.Offset)); // byte offset
                } else {
                    throw new ApplicationException();
                }
            }
        }

        public IntPtr Pointer { get; }

        public Memory<T> Memory { get; }

        public void Dispose() {
            if (_handle.IsAllocated) {
                _handle.Free();
            }
        }
    }


}