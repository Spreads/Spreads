using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Buffers {

    // we will use this eventually for SM key sharing, but start with 
    // switching b/w ThreadStatic and pooled buffers
    [StructLayout(LayoutKind.Sequential)]
    public struct BufferWrapper<T> : IDisposable {
        public BufferWrapper(T[] buffer, bool isPooled = false) {
            _buffer = buffer;
            _isPooled = isPooled;
            _isDisposed = false;
            _counter = null;
        }

        private readonly bool _isPooled;
        private bool _isDisposed;
        private readonly T[] _buffer;
        private Counter _counter;


        public T[] Buffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _buffer; }
        }
        private bool IsPooled => _isPooled;
        private Counter Counter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Volatile.Read(ref _counter) ?? EnsureCounterCreated(); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Counter EnsureCounterCreated() {
            Interlocked.CompareExchange(ref _counter, new Counter(), null);
            return _counter;
        }

        // TODO (low) this is not automic for all use cases, but in our primary use case
        // we only subrent when we keep a reference to a buffer or its container
        // we use a weak reference, so a strong one during subrenting should prevent 
        // buffer return from a finalizer

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SubRent() {
            var tenants = Counter.Increment();
            // That situation was possibe if we decremented
            if (tenants == 0 || _isDisposed) throw new InvalidOperationException("Cannot subrent without an owner: it must be present during subrent");
            return tenants;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Return() {
            var tenants = Counter.Decrement();
            if (tenants >= 0) return tenants;

            // no subletters and we are releasing the buffer
            _isDisposed = true;
            if (_isPooled) {
                ArrayPool<T>.Shared.Return(_buffer, true);
            }
            return tenants;
        }

        public void Dispose() {
            if (Volatile.Read(ref _counter) == null) {
                _isDisposed = true;
                if (_isPooled) {
                    ArrayPool<T>.Shared.Return(_buffer, true);
                }
            } else {
                Return();
            }
        }
    }
}
