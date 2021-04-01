// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Spreads.Threading;

namespace Spreads.Buffers
{
    /// <summary>
    /// Base class for reference counted <see cref="MemoryManager{T}"/>>.
    /// </summary>
    public abstract class RefCountedMemory<T> : MemoryManager<T>, IRefCounted
    {
        protected RefCountedMemory()
        {
#if SPREADS
            if (LeaksDetection.Enabled)
                Tag = Environment.StackTrace;
#endif
        }

        private int _counter;

        internal ref int CounterRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _counter;
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

        /// <summary>
        /// <see cref="ReferenceCount"/> is positive, i.e. the memory is retained (borrowed).
        /// </summary>
        public bool IsRetained
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AtomicCounter.GetIsRetained(ref CounterRef);
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

        internal string? Tag
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
        ~RefCountedMemory()
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
