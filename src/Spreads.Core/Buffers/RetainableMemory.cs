using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    /// <summary>
    /// Base class for retainable pinned memory. Buffers are always pinned during initialization.
    /// Initialization could be from a pool of arrays or from native memory. When pooled, RetainableMemory
    /// is in disposed state, underlying arrays are unpinned and returned to array pool.
    /// </summary>
    public abstract unsafe class RetainableMemory<T> : MemoryManager<T>
    {
        internal AtomicCounter Counter;

        // [p*_______[offset<---len--->]___] we must only check capacity at construction and then work from pointer

        protected int _offset;
        protected int _length;
        protected void* _pointer;

        // Pool could set this value on Rent/Return
        internal bool IsPooled;

#if DEBUG
        internal string _stackTrace = Environment.StackTrace;
#endif

        protected RetainableMemory(AtomicCounter counter)
        {
            if (counter.Count != 0)
            {
                ThrowHelper.ThrowArgumentException("counter.Count != 0");
            }
            Counter = counter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int Increment()
        {
            return Counter.Increment();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Decrement()
        {
            var newRefCount = Counter.Decrement();
            if (newRefCount == 0)
            {
                OnNoReferences();
                return false;
            }
            return true;
        }

        internal DirectBuffer DirectBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new DirectBuffer(_length * Unsafe.SizeOf<T>(), (byte*)_pointer);
        }

        internal void* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.Add<T>(_pointer, _offset);
        }

        public bool IsPinned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pointer != null;
        }

        public bool IsRetained
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Counter.Count > 0;
        }

        public int ReferenceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Counter.Count;
        }

        protected void OnNoReferences()
        {
            // Pooled implementation try to return object to pool
            // If not possible to tell if object is returned then rely on finalizer
            // In steady-state that should not be a big deal
            Dispose(true);
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Counter.Count < 0;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        public override Span<T> GetSpan()
        {
            if (IsPooled)
            {
                ThrowDisposed<RetainableMemory<T>>();
            }

            return new Span<T>(_pointer, _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // hope for devirt
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            Increment();
            if (unchecked((uint)elementIndex) >= _length) // if (elementIndex < 0 || elementIndex >= _capacity)
            {
                ThrowIndexOutOfRange();
            }

            if (_pointer == null)
            {
                ThrowHelper.ThrowInvalidOperationException("RatinableMemory is not pinned.");
            }

            // NOTE: even for the array-based memory handle is create when array is taken from pool
            // and is stored in MemoryManager until the array is released back to the pool.
            GCHandle handle = default;
            return new MemoryHandle(Unsafe.Add<T>(_pointer, _offset + elementIndex), handle, this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // hope for devirt
        public override void Unpin()
        {
            Decrement();
        }

        /// <summary>
        /// Retain buffer memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain()
        {
            return RetainImpl();
        }

        /// <summary>
        /// Retain buffer memory.
        /// </summary>
        [Obsolete("This differs from Slice that takes start, could be source of error")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain(int length)
        {
            if ((uint)length > (uint)_length)
            {
                ThrowBadLength();
            }

            return RetainImpl(length: length);
        }

        /// <summary>
        /// Retain buffer memory without pinning it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain(int start, int length)
        {
            if ((uint)start + (uint)length > (uint)_length)
            {
                ThrowBadLength();
            }
            return RetainImpl(start, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private RetainedMemory<T> RetainImpl(int start = -1, int length = -1)
        {
            if (IsDisposed)
            {
                ThrowDisposed<RetainedMemory<T>>();
            }

            if (length < 0)
            {
                length = Length;
            }

            if (start < 0)
            {
                start = 0;
            }

            return new RetainedMemory<T>(this, start, length, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ClearBeforeDispose()
        {
            _pointer = null;
            _length = default;
            _offset = default;
        }

        protected override void Dispose(bool disposing)
        {
            Counter.Dispose();
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~RetainableMemory()
        {
#if DEBUG
            // always dies in Debug
            if (IsRetained)
            {
                throw new ApplicationException("Finalizing retained RM: " + _stackTrace);
            }
#endif

            // TODO review current logic, we throw when finalizing dropped retained object
            // If it is safe enough to tell that when finalized it always dropped then we
            // could ignore IsRetained when finalizing. Before that failing is better.
            // https://docs.microsoft.com/en-us/dotnet/api/system.object.finalize?redirectedfrom=MSDN&view=netframework-4.7.2#System_Object_Finalize
            // If Finalize or an override of Finalize throws an exception, and
            // the runtime is not hosted by an application that overrides the
            // default policy, the runtime terminates the process and no active
            // try/finally blocks or finalizers are executed. This behavior
            // ensures process integrity if the finalizer cannot free or destroy resources.
            Dispose(false);
        }
    }
}
