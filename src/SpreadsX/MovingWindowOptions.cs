using Spreads.DataTypes;
using System;
using System.Collections.Generic;
using Spreads.Utils;

namespace Spreads
{
    public class MovingWindowOptions
    {
        public MovingWindowOptions<DateTime> DateTimeSpan(TimeSpan ts, bool inclusive = false)
        {
            return new MovingWindowOptions<DateTime>((f, s) => DateTimeWindowFunc(ts, f, s, inclusive));
        }

        public MovingWindowOptions<Timestamp> TimestampSpan(TimeSpan ts, bool inclusive = false)
        {
            return new MovingWindowOptions<Timestamp>((f, s) => TimestampWindowFunc(ts, f, s, inclusive));
        }

        private static bool DateTimeWindowFunc(TimeSpan ts, DateTime first, DateTime second, bool inclusive)
        {
            var delta = second - first;

            if (delta < ts)
            {
                return true;
            }

            if (inclusive && delta == ts)
            {
                return true;
            }

            return false;
        }

        private static bool TimestampWindowFunc(TimeSpan ts, Timestamp first, Timestamp second, bool inclusive)
        {
            var delta = second - first;
            

            if (delta.TimeSpan < ts)
            {
                return true;
            }

            if (inclusive && delta.TimeSpan == ts)
            {
                return true;
            }

            return false;
        }
    }

    public delegate bool MovingWindowFunc<in T>(T previous, T current);

    public class MovingWindowOptions<T>
    {
        internal readonly MovingWindowFunc<T>? MovingWindowFunc;
        internal readonly int? ItemCount;

        // TODO Key/Value delegates (e.g. to dispose or return to pool)
        // BlockSize - we could override default logic. E.g. if reference types
        // are used as values (or even keys) then we will release the entire block
        // at once. By default it is c.8k
        protected MovingWindowOptions() { }

        public MovingWindowOptions(int itemCount)
        {
            if (itemCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(itemCount));
            }
            ItemCount = itemCount;
            MovingWindowFunc = default;
        }

        public MovingWindowOptions(MovingWindowFunc<T> movingWindowFunc)
        {
            ItemCount = default;
            MovingWindowFunc = movingWindowFunc;
        }
    }

    public class MovingWindowOptions<TKey, TValue> : MovingWindowOptions<TKey>
    {
        /// <summary>
        /// Process items that are removed from the moving window.
        /// </summary>
        internal Action<KeyValuePair<TKey, TValue>>? OnRemovedHandler;

        /// <summary>
        /// Limit block size to some smaller value than the default, to release
        /// less items per block.
        /// </summary>
        internal readonly int WindowBlockSize;

        public MovingWindowOptions(Action<KeyValuePair<TKey, TValue>> onRemovedHandler)
        {
            OnRemovedHandler = onRemovedHandler;
        }

        public MovingWindowOptions(Action<KeyValuePair<TKey, TValue>> onRemovedHandler, int windowBlockSize)
        {
            OnRemovedHandler = onRemovedHandler;
            WindowBlockSize = windowBlockSize > 0 ? Math.Max(Settings.MIN_POOLED_BUFFER_LEN, BitUtil.FindNextPositivePowerOfTwo(windowBlockSize)) : 0;
        }
    }
}
