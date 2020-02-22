// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    public class OffHeapMemoryPool<T> : RetainableMemoryPool<T>
    {
        public OffHeapMemoryPool(int maxBuffersCount, int maxLength = 16 * 1024 * 1024, bool rentAlwaysClean = false)
            : this(Settings.LARGE_BUFFER_LIMIT, maxLength, maxBuffersCount, maxBucketsToTry: 0, rentAlwaysClean)
        { }

        public OffHeapMemoryPool(int minLength, int maxLength, int maxBuffersPerBucketPerCore, int maxBucketsToTry = 2, bool rentAlwaysClean = false)
            : base((pool, length) => new OffHeapMemory<T>(pool, length), minLength, maxLength, maxBuffersPerBucketPerCore, maxBucketsToTry, rentAlwaysClean:rentAlwaysClean)
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
    }
}
