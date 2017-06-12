// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    /// <summary>
    /// Represents a state of an online algorithm that is updated on cursor moves.
    /// </summary>
    public interface IOnlineOp<TValue, TResult>
    {
        /// <summary>
        /// Current output result of an online algorithm.
        /// </summary>
        TResult GetResult();

        /// <summary>
        /// Update current state after moving next.
        /// </summary>
        void OnNext(Opt<TValue> oldPrevious, Opt<TValue> newNext);

        /// <summary>
        /// Update current state after moving previous.
        /// </summary>
        void OnPrevious(Opt<TValue> newPrevious, Opt<TValue> oldNext);

        /// <summary>
        /// True if only forward moves are supported. A consumer of this interface
        /// must check this property and do not call <see cref="OnPrevious"/> method
        /// if this property is true.
        /// </summary>
        bool ForwardOnly { get; }
    }

    /// <summary>
    /// Moving sum.
    /// </summary>
    public struct MSum<T> : IOnlineOp<T, T>
    {
        private T _sum;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetResult()
        {
            return _sum;
        }

        public bool ForwardOnly => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnNext(Opt<T> oldPrevious, Opt<T> newNext)
        {
            if (oldPrevious.IsPresent)
            {
                _sum = default(SubtractOp<T>).Apply(_sum, oldPrevious.Value);
            }
            if (newNext.IsPresent)
            {
                _sum = default(AddOp<T>).Apply(_sum, oldPrevious.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnPrevious(Opt<T> newPrevious, Opt<T> oldNext)
        {
            if (oldNext.IsPresent)
            {
                _sum = default(SubtractOp<T>).Apply(_sum, oldNext.Value);
            }
            if (newPrevious.IsPresent)
            {
                _sum = default(AddOp<T>).Apply(_sum, newPrevious.Value);
            }
        }
    }

    /// <summary>
    /// Moving average.
    /// </summary>
    public struct MAvg<T> : IOnlineOp<T, T>
    {
        // NB this could be nested, but for such a simple case as MAvg is is less efficient
        // because of multiple Opt.IsMissing checks
        // private MSum<T> _sum;

        private T _sum;

        private uint _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetResult()
        {
            if (typeof(T) == typeof(double))
            {
                return (T)(object)((double)(object)_sum / _count);
            }

            if (typeof(T) == typeof(float))
            {
                return (T)(object)((float)(object)_sum / _count);
            }

            if (typeof(T) == typeof(int))
            {
                return (T)(object)((int)(object)_sum / _count);
            }

            if (typeof(T) == typeof(long))
            {
                return (T)(object)((long)(object)_sum / _count);
            }

            if (typeof(T) == typeof(uint))
            {
                return (T)(object)((uint)(object)_sum / _count);
            }

            if (typeof(T) == typeof(ulong))
            {
                return (T)(object)((ulong)(object)_sum / _count);
            }

            if (typeof(T) == typeof(decimal))
            {
                return (T)(object)((decimal)(object)_sum / _count);
            }

            return GetResultDynamic();
        }

        private T GetResultDynamic()
        {
            return (T)((dynamic)_sum / _count);
        }

        public bool ForwardOnly => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnNext(Opt<T> oldPrevious, Opt<T> newNext)
        {
            if (oldPrevious.IsPresent)
            {
                _count--;
                _sum = default(SubtractOp<T>).Apply(_sum, oldPrevious.Value);
            }
            if (newNext.IsPresent)
            {
                _count++;
                _sum = default(AddOp<T>).Apply(_sum, oldPrevious.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnPrevious(Opt<T> newPrevious, Opt<T> oldNext)
        {
            if (oldNext.IsPresent)
            {
                _count--;
                _sum = default(SubtractOp<T>).Apply(_sum, oldNext.Value);
            }
            if (newPrevious.IsPresent)
            {
                _count++;
                _sum = default(AddOp<T>).Apply(_sum, newPrevious.Value);
            }
        }
    }
}