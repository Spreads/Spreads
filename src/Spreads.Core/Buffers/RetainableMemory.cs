using Spreads.Utils;
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

        // [p*<-len---------------->] we must only check capacity at construction and then work from pointer
        // [p*<-len-[<--lenPow2-->]>] buffer could be larger, pooling always by max pow2 we could store

        // protected int _offset;
        protected int _length;

        protected void* _pointer;

        // Pool could set this value on Rent/Return
        internal bool IsPooled;

        internal RetainableMemoryPool<T> _pool;
        internal int _pow2Length;

#if DEBUG
        internal string _stackTrace = Environment.StackTrace;
#endif

        protected RetainableMemory(AtomicCounter counter)
        {
            if (counter.IsValid)
            {
                if (counter.Count != 0)
                {
                    ThrowHelper.ThrowArgumentException("counter.Count != 0");
                }
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
                TryReturnThisToPoolOrFinalize();
                return false;
            }
            return true;
        }

        internal void* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pointer; //Unsafe.Add<T>(_pointer, _offset);
        }

        /// <summary>
        /// Extra space (if any) is at the beginning.
        /// </summary>
        internal void* PointerPow2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.Add<T>(_pointer, _length - LengthPow2);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void TryReturnThisToPoolOrFinalize()
        {
            if (IsPooled)
            {
                ThrowAlreadyPooled<OffHeapMemory<T>>();
            }

            _pool?.ReturnNoChecks(this, clearArray: false); // this is pinned, we do not need to clean blittables

            if (!IsPooled)
            {
                // detach from pool
                _pool = null;
                // as if finalizing
                GC.SuppressFinalize(this);
                Dispose(false);
            }
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

        internal int LengthPow2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_pow2Length == 0)
                {
                    return _pow2Length = (BitUtil.FindNextPositivePowerOfTwo(_length + 1) / 2);
                }
                return _pow2Length;
            }
        }

        public override Span<T> GetSpan()
        {
            if (IsPooled)
            {
                ThrowDisposed<RetainableMemory<T>>();
            }

            return new Span<T>(Pointer, _length);
        }

        internal DirectBuffer DirectBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new DirectBuffer(_length * Unsafe.SizeOf<T>(), (byte*)Pointer);
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
            return new MemoryHandle(Unsafe.Add<T>(_pointer, elementIndex), handle, this);
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
            return Retain(0, _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal RetainedMemory<T> RetainPow2()
        {
            var lengthPow2 = LengthPow2;
            return Retain(_length - lengthPow2, lengthPow2);
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

            return Retain(0, length);
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
            return new RetainedMemory<T>(this, start, length, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureNotRetainedAndNotDisposed()
        {
            var count = Counter.IsValid ? Counter.Count : -1;
            if (count != 0)
            {
                if (count > 0) { ThrowDisposingRetained<RetainableMemory<T>>(); }
                else { ThrowDisposed<RetainableMemory<T>>(); }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ClearOnDispose()
        {
            _pointer = null;
            _length = default;
            _pow2Length = default;
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
