//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// TODO update to the new OwnedBuffer API

//using Spreads.Serialization;
//using System;
//using System.Buffers;
//using System.Diagnostics;
//using System.Runtime.CompilerServices;
//using System.Threading;

//namespace Spreads.Buffers
//{
//    // Regardless of storage we pin blittable arrays
//    // this is because TypeHelper<T>.IsBlittable is treated as JIT compile-time constant
//    // and `if` branches could be eliminated completely. Otherwise a null check on Array is required.

//    /// <summary>
//    /// DirectOwnedBuffer is an equivalent of an array T[] but supports native memory as its backing storage.
//    /// </summary>
//    public unsafe class DirectOwnedBuffer<T> : OwnedBuffer<T>
//    {
//        internal DirectOwnedBuffer<byte> _lockers;
//        private BufferHandle _lockersHandle;
//        private BufferHandle _dataHandle;
//        internal static readonly long Pid = Process.GetCurrentProcess().Id;

//        // NB this is recursive, the input lockersBuffer should have their own lockersBuffer set to null
//        // This is to have access to protected Initialize method on the lockersBuffer.

//        /// <summary>
//        /// DirectOwnedBuffer constructor. Takes a lockers buffer to store interlocked values for locking and versions.
//        /// The buffer could be null. DirectOwnedBuffer owns the lockers buffer.
//        /// </summary>
//        internal DirectOwnedBuffer(DirectOwnedBuffer<byte> lockersBuffer, T[] array) : base(array)
//        {
//            if (lockersBuffer != null && lockersBuffer.Length != 64)
//            {
//                throw new ArgumentException("Lockers buffer must have length of 64 bytes.");
//            }
//            // locker, version, next version, size, process ref count + padding to fill cache line
//            Init(lockersBuffer);
//        }

//        /// <summary>
//        /// DirectOwnedBuffer constructor. Takes a buffer to store interlocked values for locking and versions.
//        /// The buffer could be null. DirectOwnedBuffer owns the lockers buffer.
//        /// </summary>
//        internal DirectOwnedBuffer(DirectOwnedBuffer<byte> lockersBuffer, T[] array, int arrayOffset, int length, IntPtr pointer = new IntPtr()) : base(array, arrayOffset, length, pointer)
//        {
//            if (lockersBuffer != null && lockersBuffer.Length != 64)
//            {
//                throw new ArgumentException("Lockers buffer must have length of 64 bytes.");
//            }
//            Init(lockersBuffer);
//        }

//        /// <summary>
//        /// DirectOwnedBuffer constructor.
//        /// </summary>
//        public DirectOwnedBuffer(T[] array) : base(array)
//        {
//            // locker, version, next version, size, process ref count + padding to fill cache line
//            Init(null);
//        }

//        /// <summary>
//        /// DirectOwnedBuffer constructor.
//        /// </summary>
//        public DirectOwnedBuffer(T[] array, int arrayOffset, int length, IntPtr pointer = new IntPtr()) : base(array, arrayOffset, length, pointer)
//        {
//            Init(null);
//        }

//        private long* _locker => (long*)_lockersHandle.PinnedPointer;
//        private long* _version => (long*)_lockersHandle.PinnedPointer + 1;
//        private long* _nextVersion => (long*)_lockersHandle.PinnedPointer + 2;
//        private long* _size => (long*)_lockersHandle.PinnedPointer + 3;
//        private long* _processCount => (long*)_lockersHandle.PinnedPointer + 4;
//        private long* _id => (long*)_lockersHandle.PinnedPointer + 5;

//        public static DirectOwnedBuffer<T> Create(int length)
//        {
//            return new DirectOwnedBuffer<T>(new T[length]);
//        }

//        /// <summary>
//        /// Get Version.
//        /// </summary>
//        public long Version => *_version;

//        /// <summary>
//        /// Get Version with Volatile.Read.
//        /// </summary>
//        public long VersionVolatile => Volatile.Read(ref *_version);

//        /// <summary>
//        /// Get NextVersion.
//        /// </summary>
//        public long NextVersion => *_nextVersion;

//        /// <summary>
//        /// Get NextVersion with Volatile.Read.
//        /// </summary>
//        public long NextVersionVolatile => Volatile.Read(ref *_nextVersion);

//        /// <summary>
//        /// Access a buffer element by index without bounds or index check.
//        /// </summary>
//        public ref T this[int index]
//        {
//            [MethodImpl(MethodImplOptions.AggressiveInlining)]
//            get
//            {
//                if (TypeHelper<T>.IsPinnable)
//                {
//                    return ref Unsafe.Add(ref Unsafe.AsRef<T>(_dataHandle.PinnedPointer), index);
//                }
//                return ref Array[index];
//            }
//        }

//        /// <summary>
//        /// Read a value at index with locking.
//        /// </summary>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public T InterlockedRead(int index)
//        {
//            // NB This is an example of locked read.
//            // Copy-paste this method and replace the code in the region.

//            T value;
//            var spinwait = new SpinWait();
//            while (true)
//            {
//                var version = VersionVolatile;

//                #region Read code

//                value = this[index];

//                #endregion Read code

//                var nextVerion = NextVersionVolatile;

//                // NB do not return value from a loop, see BeforeWrite comments
//                if (version == nextVerion) break;
//                spinwait.SpinOnce();
//            }
//            return value;
//        }

//        /// <summary>
//        /// Write a value at index with locking.
//        /// </summary>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public void InterlockedWrite(int index, T value)
//        {
//            // NB This is an example of locked write.
//            // Copy-paste this method and replace the code in the region.

//            // NB prevent async exceptions (ThreadAbort, OutOfMemory) with using the locking methods only in finally blocks
//            long v2 = 0L;
//            try
//            {
//                try { }
//                finally
//                {
//                    v2 = BeforeWrite();
//                }

//                #region Write code

//                this[index] = value;

//                #endregion Write code
//            }
//            finally
//            {
//                AfterWrite(v2, true);
//            }
//        }

//        /// <summary>
//        /// Perform BinarySearch of value T in the length-size segment of this buffer starting at index.
//        /// </summary>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public long BinarySearch(int index, int length, T value, KeyComparer<T> comparer)
//        {
//            if ((uint)(index) + (uint)length > (uint)Length)
//            {
//                throw new ArgumentException("Index + Length fall outside the span boundary.");
//            }
//            if (comparer == null)
//            {
//                comparer = KeyComparer<T>.Default;
//            }

//            return BinarySearchUnchecked(index, length, value, comparer);
//        }

//        /// <summary>
//        /// Perform BinarySearch of value T in this buffer.
//        /// </summary>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public long BinarySearch(T value, KeyComparer<T> comparer)
//        {
//            if (comparer == null)
//            {
//                comparer = KeyComparer<T>.Default;
//            }

//            return BinarySearchUnchecked(0, Length, value, comparer);
//        }

//        /// <summary>
//        /// Perform BinarySearch of value T in this buffer.
//        /// </summary>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public long BinarySearch(T value)
//        {
//            return BinarySearchUnchecked(0, Length, value, KeyComparer<T>.Default);
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        internal long BinarySearchUnchecked(int index, int length, T value, KeyComparer<T> comparer)
//        {
//            int lo = index;
//            int hi = index + length - 1;
//            while (lo <= hi)
//            {
//                int i = lo + ((hi - lo) >> 1);
//                int order = comparer.Compare(this[i], value);

//                if (order == 0) return i;
//                if (order < 0)
//                {
//                    lo = i + 1;
//                }
//                else
//                {
//                    hi = i - 1;
//                }
//            }
//            return ~lo;
//        }

//        /// <summary>
//        /// Takes a write lock, increments _nextVersion field and returns the current value of the _version field.
//        /// </summary>
//        /// <returns></returns>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        internal long BeforeWrite(bool takeLock = true)
//        {
//            var spinwait = new SpinWait();
//            long version = -1L;
//            // NB try{} finally{ .. code here .. } prevents method inlining, therefore should be used at the caller place, not here
//            // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
//            while (takeLock)
//            {
//                if (Interlocked.CompareExchange(ref *_locker, Pid, 0L) == 0L)
//                {
//                    // Interlocked.CompareExchange generated implicit memory barrier
//                    var nextVersion = *_nextVersion + 1L;
//                    // Volatile.Write prevents the read above to move below
//                    Volatile.Write(ref *_nextVersion, nextVersion);
//                    // Volatile.Read prevents any read/write after to to move above it
//                    // see CoreClr 6121, esp. comment by CarolEidt
//                    version = Volatile.Read(ref *_version);
//                    // do not return from a loop, see CoreClr #9692
//                    break;
//                }
//                if (spinwait.Count == 10000) // 10 000 is c.700 msec
//                {
//                    TryUnlock();
//                }
//                // NB Spinwait significantly increases performance compared to a tight loop due to PAUSE instructions
//                spinwait.SpinOnce();
//            }
//            return version;
//        }

//        /// <summary>
//        ///
//        /// </summary>
//        [MethodImpl(MethodImplOptions.NoInlining)]
//        protected virtual void TryUnlock()
//        {
//            Trace.TraceWarning("Unlocking is not implemented");
//#if DEBUG
//            throw new NotSupportedException("Unlocking is not implemented");
//#endif
//        }

//        /// <summary>
//        /// Release write lock and increment _version field or decrement _nextVersion field if no updates were made
//        /// </summary>
//        /// <param name="version"></param>
//        /// <param name="doIncrement"></param>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        internal void AfterWrite(long version, bool doIncrement = true)
//        {
//            if (version < 0L) return;

//            // Volatile.Write will prevent any read/write to move below it
//            if (doIncrement)
//            {
//                Volatile.Write(ref *_version, version + 1L);
//            }
//            else
//            {
//                // set nextVersion back to original version, no changes were made
//                Volatile.Write(ref *_nextVersion, version);
//            }
//            // release write lock
//            Volatile.Write(ref *(long*)_lockersHandle.PinnedPointer, 0L);
//        }

//        private void Init(DirectOwnedBuffer<byte> lockers)
//        {
//            if (lockers != null)
//            {
//                _lockers = lockers;
//                _lockersHandle = _lockers.Buffer.Pin();
//                Interlocked.Increment(ref *_processCount);
//            }

//            if (TypeHelper<T>.IsPinnable)
//            {
//                _dataHandle = this.Buffer.Pin();
//            }
//            else
//            {
//                if (Array == null) throw new NotSupportedException("Native storage for non-blittable types is not supported");
//            }

//        }

//        /// <summary>
//        /// Dispose this instance.
//        /// </summary>
//        protected override void Dispose(bool disposing)
//        {
//            if (disposing)
//            {
//                GC.SuppressFinalize(this);
//            }

//            if (TypeHelper<T>.IsPinnable)
//            {
//                _dataHandle.Free();
//            }
//            if (_lockers != null)
//            {
//                _lockersHandle.Free();
//                _lockers.Dispose();
//            }

//            Interlocked.Decrement(ref *_processCount);
//            base.Dispose(disposing);
//        }

//        /// <summary>
//        /// DirectOwnedBuffer finalizer.
//        /// </summary>
//        ~DirectOwnedBuffer()
//        {
//            Dispose(false);
//        }
//    }
//}