// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Threading
{
    /// <summary>
    /// Counts non-negative value and stores it in a provided location.
    /// </summary>
    public static class AtomicCounter
    {
        // We use interlocked operations which are  expensive. Limit the number of bits used
        // for ref count so that other bits could be used for flags. 2^23 should be enough for
        // ref count. Interlocked incr/decr will work with flags without additional logic.

        public const int CountMask = 0b_00000000_11111111_11111111_11111111;
        public const int Disposed = CountMask;

        /// <summary>
        /// Inclusive
        /// </summary>
        public const int MaxCount = CountMask >> 1; // TODO review inclusive/exclusive usage

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int GetCount(ref int counter)
        {
            return counter & CountMask;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int Increment(ref int counter)
        {
            int newValue;
            while (true)
            {
                int currentValue = Volatile.Read(ref counter);

                if (unchecked((uint)(currentValue & CountMask)) > MaxCount)
                {
                    // Counter was negative before increment or there is a counter leak
                    // and we reached 8M values. For any conceivable use case
                    // count should not exceed that number and be even close to it.
                    // In imaginary case when this happens we decrement back before throwing.

                    ThrowBadCounter(currentValue & CountMask);
                }

                newValue = currentValue + 1;
                int existing = Interlocked.CompareExchange(ref counter, newValue, currentValue);
                if (existing == currentValue)
                {
                    // do not return from while loop
                    break;
                }

                if ((existing & ~CountMask) != (currentValue & ~CountMask))
                {
                    ThrowCounterChanged();
                }
            }

            return newValue & CountMask;
        }

        /// <summary>
        /// Decrement a positive counter. Throws if counter is zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int Decrement(ref int counter)
        {
            int newValue;
            while (true)
            {
                int currentValue = Volatile.Read(ref counter);

                // after decrement the value must remain in the range
                if (unchecked((uint)((currentValue & CountMask) - 1)) > MaxCount)
                {
                    ThrowBadCounter(currentValue & CountMask);
                }

                newValue = currentValue - 1;
                int existing = Interlocked.CompareExchange(ref counter, newValue, currentValue);
                if (existing == currentValue)
                {
                    break;
                }

                if ((existing & ~CountMask) != (currentValue & ~CountMask))
                {
                    ThrowCounterChanged();
                }
            }

            return newValue & CountMask;
        }

        /// <summary>
        /// Returns new count value if incremented or zero if the current count value is zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int IncrementIfRetained(ref int counter)
        {
            int newValue;
            while (true)
            {
                int currentValue = Volatile.Read(ref counter);
                int currentCount = currentValue & CountMask;
                if (unchecked((uint)(currentCount)) > MaxCount)
                {
                    ThrowBadCounter(currentValue & CountMask);
                }

                if (currentCount > 0)
                {
                    newValue = currentValue + 1;
                    int existing = Interlocked.CompareExchange(ref counter, newValue, currentValue);
                    if (existing == currentValue)
                    {
                        break;
                    }

                    if ((existing & ~CountMask) != (currentValue & ~CountMask))
                    {
                        ThrowCounterChanged();
                    }
                }
                else
                {
                    newValue = 0;
                    break;
                }
            }

            return newValue & CountMask;
        }

        /// <summary>
        /// Returns zero if decremented the last reference or current count if it is more than one.
        /// Throws if current count is zero.
        /// </summary>
        /// <remarks>
        /// Throwing when current count is zero is correct because this call should be made
        /// when count reaches 1. A race with self is not possible with correct usage. E.g. if two users
        /// are holding refs then RC is 3, the first calling Decrement will have remaining = 2
        /// and should not call this method. This method protects from another thread incrementing
        /// the RC while the one that saw remaining at 1 is trying to dispose or cleanup. If this
        /// methods returns a positive number > 1 then a resource is being reused and dispose
        /// should be skipped.
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static int DecrementIfOne(ref int counter)
        {
            int newValue;
            while (true)
            {
                int currentValue = Volatile.Read(ref counter);
                int currentCount = currentValue & CountMask;
                if (unchecked((uint)(currentCount - 1)) > MaxCount)
                {
                    ThrowBadCounter(currentValue & CountMask);
                }

                if (currentCount == 1)
                {
                    newValue = currentValue - 1;
                    int existing = Interlocked.CompareExchange(ref counter, newValue, currentValue);
                    if (existing == currentValue)
                    {
                        break;
                    }

                    if ((existing & ~CountMask) != (currentValue & ~CountMask))
                    {
                        ThrowCounterChanged();
                    }
                }
                else
                {
                    newValue = currentValue;
                    break;
                }
            }

            return newValue & CountMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetIsRetained(ref int counter)
        {
            return unchecked((uint)((Volatile.Read(ref counter) & CountMask) - 1)) < MaxCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetIsDisposed(ref int counter)
        {
            return (Volatile.Read(ref counter) & CountMask) == Disposed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static void Dispose(ref int counter)
        {
            int currentValue = Volatile.Read(ref counter);
            int currentCount = currentValue & CountMask;

            if ((uint)(currentValue & CountMask) > MaxCount)
                ThrowBadCounter(currentValue & CountMask);

            if (currentCount != 0)
                ThrowPositiveRefCount(currentCount);

            int newValue = currentValue | CountMask; // set all count bits to 1, make counter unusable for Incr/Decr methods

            int existing = Interlocked.CompareExchange(ref counter, newValue, currentValue);
            if (existing != currentValue)
                ThrowCounterChanged();
        }

        /// <summary>
        /// Returns zero if counter was zero and transitioned to the disposed state.
        /// Returns current count if it is positive.
        /// Returns minus one if the counter was already in disposed state.
        /// Keeps flags unchanged.
        /// </summary>
        /// <param name="counter"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int TryDispose(ref int counter)
        {
            int currentValue = Volatile.Read(ref counter);

            while (true)
            {
                int currentCount = currentValue & CountMask;
                if (currentCount != 0)
                {
                    if (currentCount == Disposed)
                        return -1;
                    if (currentCount > MaxCount)
                        ThrowBadCounter(currentCount);
                    return currentCount;
                }

                int newValue = currentValue | CountMask;

                int existing = Interlocked.CompareExchange(ref counter, newValue, currentValue);

                if (existing != currentValue)
                {
                    currentValue = existing;
                    continue;
                }

                break;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBadCounter(int current)
        {
            if (current == CountMask)
                ThrowDisposed();
            else
                ThrowHelper.ThrowInvalidOperationException($"Bad counter: current count {current}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposed()
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(AtomicCounter));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowPositiveRefCount(int count)
        {
            ThrowHelper.ThrowInvalidOperationException($"AtomicCounter.Count {count} > 0");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCounterChanged()
        {
            // If this is needed we could easily work directly with pointer.
            ThrowHelper.ThrowInvalidOperationException($"Counter flags changed during operation or count increased during disposal.");
        }
    }
}
