using Spreads.DataTypes;
using System;

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

    public struct MovingWindowOptions<T>
    {
        private readonly MovingWindowFunc<T>? _movingWindowFunc;
        private readonly int? _itemCount;

        public MovingWindowOptions(int itemCount)
        {
            _itemCount = itemCount;
            _movingWindowFunc = default;
        }

        public MovingWindowOptions(MovingWindowFunc<T> movingWindowFunc)
        {
            _itemCount = default;
            _movingWindowFunc = movingWindowFunc;
        }

        internal bool IsDefault => _itemCount == default && _movingWindowFunc == default;
    }
}
