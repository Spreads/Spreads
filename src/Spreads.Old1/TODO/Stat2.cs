using System;
using System.Runtime.CompilerServices;

namespace Spreads.DataTypes
{
    /// <summary>
    /// Count, sum and variance.
    /// </summary>
    public struct Stat2<TKey>
    {
        internal TKey _start;
        internal TKey _end;

        /// <remarks>
        /// For in-place z-score calculation, otherwise would need Zip to original.
        /// </remarks>
        internal double _endValue;

        internal double _sum2;
        internal double _sum;

        /// <remarks>
        /// Division is expensive, keeping it saves one division per each operation.
        /// </remarks>
        internal double _mean;

        internal int _count;

        /// <summary>
        /// Count.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Sum.
        /// </summary>
        public double Sum => _sum;

        /// <summary>
        /// Sum of squares.
        /// </summary>
        public double Sum2 => _sum2;

        public double Mean => _mean;

        /// <summary>
        /// Unbiased sample variance (using Sum2/(Count-1), same as Excel's VAR.S function).
        /// </summary>
        public double Variance
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _count > 1 ? _sum2 / (_count - 1) : double.NaN; }
        }

        /// <summary>
        /// Unbiased sample standard deviation (using Sqrt(Sum2/(Count-1)), same as Excel's STDEV.S function).
        /// </summary>
        public double StDev
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _count > 1 ? Math.Sqrt(_sum2 / (_count - 1)) : double.NaN; }
        }

        /// <summary>
        /// Z-score.
        /// </summary>
        public double ZScore
        {
            // _end must be set, otherwise it is easy to confuse zero as a valid value.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return !Equals(_end, default(TKey)) ? (_endValue - _mean) / StDev : double.NaN; }
        }

        #region Welford's generalized algorithm for variance.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddValue(double x)
        {
            _count++;
            _sum += x;
            var nextMean = _mean + (x - _mean) / _count;
            _sum2 += (x - _mean) * (x - nextMean);
            _mean = nextMean;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveValue(double x)
        {
            if (_count == 0)
            {
                ThrowHelper.ThrowInvalidOperationException("Stat2 count is zero, cannot remove values.");
            }
            else if (_count == 1)
            {
                _count = 0;
                _sum = 0.0;
                _mean = 0.0;
                _sum2 = 0.0;
            }
            else
            {
                _sum -= x;
                var mMOld = (_count * _mean - x) / (_count - 1);
                _sum2 -= (x - _mean) * (x - mMOld);
                _mean = mMOld;
                _count--;
            }
        }

        #endregion Welford's generalized algorithm for variance.
    }

    public static class Stat2Extensions
    {
        public static double AnnualizedVolatility(this Stat2<DateTime> stat, int dayCount = 252)
        {
            throw new NotImplementedException();
        }
    }
}