// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    public class OffHeapMemoryPool<T> : RetainableMemoryPool<T, OffHeapMemory<T>>
    {
        public OffHeapMemoryPool(int maxBuffersCount, int maxLength = 16 * 1024 * 1024)
            : this(Settings.LARGE_BUFFER_LIMIT, maxLength, maxBuffersCount, 0)
        { }

        public OffHeapMemoryPool(int minLength, int maxLength, int maxBuffersPerBucket, int maxBucketsToTry = 2)
            : base((p, l) => new OffHeapMemory<T>(p, l), minLength, maxLength, maxBuffersPerBucket, maxBucketsToTry)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override IMemoryOwner<T> Rent(int minimumCapacity = -1)
        {
            if (minimumCapacity == -1)
            {
                minimumCapacity = Settings.LARGE_BUFFER_LIMIT;
            }

            return RentMemory(minimumCapacity);
        }

        //        [Obsolete("Should only be used from OnNoRefs in OffHeapMemory or in tests")]
        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //        internal bool Return(OffHeapMemory<T> offHeapMemory)
        //        {
        //            // offHeapMemory must be in the same state as it was after Rent so that Rent/Return work without any other requirements

        //            // These checks for internal code that could Return directly without Dispose on memory
        //            var count = offHeapMemory.Counter.IsValid ? offHeapMemory.Counter.Count : -1;
        //            if (count != 0)
        //            {
        //                if (count > 0)
        //                {
        //                    ThrowDisposingRetained<ArrayMemorySlice<T>>();
        //                }
        //                else
        //                {
        //                    ThrowDisposed<ArrayMemorySlice<T>>();
        //                }
        //            }

        //            if (offHeapMemory.IsPooled || offHeapMemory._pool != this)
        //            {
        //                ThrowAlienOrAlreadyPooled<OffHeapMemory<T>>();
        //            }

        //            var pooled = offHeapMemory.IsPooled = _pool.Return(offHeapMemory);
        //            return pooled;
        //        }

        //        public bool Return<TImpl>(TImpl memory) where TImpl : RetainableMemory<T>
        //        {
        //            if (typeof(TImpl) == typeof(OffHeapMemory<T>))
        //            {
        //#pragma warning disable 618
        //                return Return(Unsafe.As<OffHeapMemory<T>>(memory));
        //#pragma warning restore 618
        //            }
        //            ThrowHelper.ThrowNotSupportedException();
        //            return false;
        //        }
    }
}
