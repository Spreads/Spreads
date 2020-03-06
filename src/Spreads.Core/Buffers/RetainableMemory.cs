using Spreads.Native;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Spreads.Serialization;
using static Spreads.Buffers.BuffersThrowHelper;

namespace Spreads.Buffers
{
    internal static class RetainableMemoryTracker
    {
        internal static ConditionalWeakTable<object, string> Tags = new ConditionalWeakTable<object, string>();
    }

    /// <summary>
    /// Base class for retainable memory from a pool of arrays or from native memory.
    /// </summary>
    public abstract unsafe class RetainableMemory<T> : MemoryManager<T>, IDisposable, IRefCounted
    {
        protected RetainableMemory()
        {
#if SPREADS
            if (LeaksDetection.Enabled)
                Tag = Environment.StackTrace;
#endif
        }

        // [p*<-len---------------->] we must only check capacity at construction and then work from pointer
        // [p*<-len-[<--lenPow2-->]>] buffer could be larger, pooling always by max pow2 we could store

        protected IntPtr _pointer;
        protected int _length;

        [Obsolete("Must be used only from CounterRef or reserved for custom storage when CounterRef is overrided.")]
        internal int _counter;

        internal int _offset;

#pragma warning disable 649
        internal short Reserved;
#pragma warning restore 649

        /// <summary>
        /// 0 - externally owned;
        /// 1 - default array pool (no RM pool);
        /// 2+ - custom pool.
        /// </summary>
        internal byte PoolIndex;

        /// <summary>
        /// True if the memory is already clean (all zeros) on return. Useful for the case when
        /// the pool has <see cref="RetainableMemoryPool{T}.IsRentAlwaysClean"/> set to true
        /// but we know that the buffer is already clean. Use with caution only when cleanliness
        /// is obvious and when cost of cleaning could be high (larger buffers).
        /// </summary>
        [Obsolete("Don't use unless 100% sure.")] // Keep this hook for now, but if it's never used remove the field later. 
        internal bool SkipCleaning;

        /// <summary>
        /// A pool sets this value atomically from inside a lock.
        /// </summary>
        internal bool IsPooled => AtomicCounter.GetIsDisposed(ref CounterRef) && PoolIndex > 1;

        internal ref int CounterRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#pragma warning disable 618
                return ref _counter;
#pragma warning restore 618
            }
        }

        internal RetainableMemoryPool<T>? Pool
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RetainableMemoryPool<T>.KnownPools[PoolIndex];
        }

        /// <summary>
        /// An array was allocated manually. Otherwise even if _pool == null we return the array to default array pool on Dispose.
        /// </summary>
        protected bool ExternallyOwned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => PoolIndex == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Increment()
        {
            return AtomicCounter.Increment(ref CounterRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int IncrementIfRetained()
        {
            return AtomicCounter.IncrementIfRetained(ref CounterRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Decrement()
        {
            var newRefCount = AtomicCounter.Decrement(ref CounterRef);
            if (newRefCount == 0)
                Dispose(true);

            return newRefCount;
        }

        // TODO check usages, they must use the return value
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int DecrementIfOne()
        {
            var newRefCount = AtomicCounter.DecrementIfOne(ref CounterRef);
            if (newRefCount == 0)
                Dispose(true);

            return newRefCount;
        }

        internal void* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (void*) _pointer;
        }

        /// <summary>
        /// Extra space (if any) is at the beginning.
        /// </summary>
        internal void* PointerPow2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.Add<T>((void*) _pointer, _length - LengthPow2);
        }

        /// <summary>
        /// The underlying memory is a pinned array or native memory.
        /// </summary>
        public bool IsPinned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pointer != IntPtr.Zero;
        }

        /// <summary>
        /// <see cref="ReferenceCount"/> is positive, i.e. the memory is retained (borrowed).
        /// </summary>
        public bool IsRetained
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AtomicCounter.GetIsRetained(ref CounterRef);
        }

        internal bool IsPoolable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => PoolIndex > 1;
        }

        public int ReferenceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AtomicCounter.GetCount(ref CounterRef);
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AtomicCounter.GetIsDisposed(ref CounterRef);
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        /// <summary>
        /// Length for pool buckets. To simplify and speedup implementation we just
        /// use default pow2 pool logic without virtual methods and complexity of
        /// calculating lengths. A buffer is pooled by max pow2 it could fit into.
        /// </summary>
        internal int LengthPow2
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BitUtil.FindPreviousPositivePowerOfTwo(_length);
        }

        /// <summary>
        /// Returns <see cref="Vec{T}"/> backed by the memory of this instance.
        /// </summary>
        public abstract Vec<T> GetVec();

        internal DirectBuffer DirectBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IsPooled)
                {
                    ThrowDisposed<RetainableMemory<T>>();
                }

                ThrowHelper.DebugAssert(_pointer != null && _length > 0, "Pointer != null && _length > 0");

                return new DirectBuffer(_length * Unsafe.SizeOf<T>(), (byte*) _pointer);
            }
        }

        [Obsolete("Prefer fixed statements on a pinnable reference for short-lived pinning")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override MemoryHandle Pin(int elementIndex = 0)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
        {
            if (unchecked((uint) elementIndex) >= _length)
            {
                ThrowIndexOutOfRange();
            }

            if (TypeHelper<T>.IsReferenceOrContainsReferences)
            {
                ThrowHelper.DebugAssert(_pointer == null, "_pointer == null");
                ThrowHelper.ThrowInvalidOperationException("RetainableMemory that is not pinned must have it's own implementation (override) of Pin method.");
            }

            Increment();
            return new MemoryHandle(Unsafe.Add<T>((void*) _pointer, elementIndex), handle: default, this);
        }

        [Obsolete("Unpin should never be called directly, it is called during disposal of MemoryHandle returned by Pin.")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override void Unpin()
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
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
        /// Retain buffer memory without pinning it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Retain(int start, int length, bool borrow = true) // TODO remove borrow param, Retain == borrow
        {
            if ((uint) start + (uint) length > (uint) _length)
            {
                ThrowBadLength();
            }

            return new RetainedMemory<T>(this, start, length, borrow: borrow);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }

        void IDisposable.Dispose()
        {
            Dispose(disposing: true);
        }

        /// <summary>
        /// Free all resources when the object is no longer pooled or used (as in finalization).
        /// </summary>
        internal abstract void Free(bool finalizing);

        /// <summary>
        /// Clear remaining object fields during <see cref="Free"/> and before object pooling.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal virtual void ClearFields()
        {
            ThrowHelper.DebugAssert(AtomicCounter.GetIsDisposed(ref CounterRef));
            ThrowHelper.DebugAssert(!IsPooled);
            _pointer = IntPtr.Zero;
            _length = default; // not -1, we have uint cast. Also len = 0 should not corrupt existing data
        }

        internal string Tag
        {
            get => RetainableMemoryTracker.Tags.TryGetValue(this, out var tag) ? tag : null;
            set
            {
                RetainableMemoryTracker.Tags.Remove(this);
                RetainableMemoryTracker.Tags.Add(this, value);
            }
        }

        /// <summary>
        /// We need a finalizer because reference count and backing memory could be a native resource.
        /// If object dies without releasing a reference then it is an error.
        /// Current code kills application by throwing in finalizer and this is what we want
        /// for DS - ensure correct memory management.
        /// </summary>
        ~RetainableMemory()
        {
            if (Environment.HasShutdownStarted || AppDomain.CurrentDomain.IsFinalizingForUnload())
                return;

            if (IsRetained)
            {
                var msg = $"Finalizing retained RetainableMemory (ReferenceCount={ReferenceCount})" + (Tag != null ? Environment.NewLine + "Tag: " + Tag : "");
#if DEBUG
                    ThrowHelper.ThrowInvalidOperationException(msg);
#else
                Trace.TraceError(msg);
#endif
            }

            // There are no more references to this object, so regardless 
            // or the CounterRef value we must free resources. Counter
            // could have left positive due to wrong usage or process
            // termination - we do not care, we should not make things
            // worse by throwing in the finalizer. We must release
            // native memory and pooled arrays, without trying to 
            // pool this object to RMP.
            // So just set the counter to disposed.
            CounterRef |= AtomicCounter.Disposed;

            Free(finalizing: true);
        }
    }
}