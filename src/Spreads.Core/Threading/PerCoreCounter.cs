using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Spreads.Native;
using Spreads.Utils;

namespace Spreads.Threading
{
    /// <summary>
    /// Multi-threaded counter.
    /// Makes precise (interlocked) count uncontended and faster (via <see cref="InterlockedAdd"/>, <see cref="InterlockedIncrement"/>, <see cref="InterlockedDecrement"/>)
    /// or approximate count more precise (via <see cref="Add"/>, <see cref="Increment"/>, <see cref="Decrement"/>).
    /// </summary>
    public class PerCoreCounter
    {
        private const int MaxCounters = 64;
        private readonly RightPaddedCounterCore[] _perCoreCounters;
        private readonly int _indexMask;

        public PerCoreCounter()
        {
            // Here unused counters is not a problem (vs object pool with round-robin)
            // so we could use power of two and avoid expensive int division.
            var length = BitUtil.FindNextPositivePowerOfTwo(Math.Min(Environment.ProcessorCount, MaxCounters));
            _indexMask = length - 1;

            var perCoreCounters = new RightPaddedCounterCore[length];
            for (int i = 0; i < perCoreCounters.Length; i++)
            {
                perCoreCounters[i] = new RightPaddedCounterCore();
            }

            _perCoreCounters = perCoreCounters;
        }

        public long Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var value = 0L;
                for (int i = 0; i < _perCoreCounters.Length; i++)
                {
                    value += IntPtr.Size == 4
                        ? Interlocked.Read(ref _perCoreCounters[i].Value)
                        : _perCoreCounters[i].Value;
                }

                return value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(long value)
        {
            Add(value, CpuIdCache.GetCurrentCpuId());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(long value, int cpuId)
        {
            if (IntPtr.Size == 4)
            {
                InterlockedAdd(value, cpuId);
            }
            else
            {
                var perCoreCounters = _perCoreCounters;
                var index = cpuId & _indexMask;
                perCoreCounters[index].Value += value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InterlockedAdd(long value)
        {
            InterlockedAdd(value, CpuIdCache.GetCurrentCpuId());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InterlockedAdd(long value, int cpuId)
        {
            var perCoreCounters = _perCoreCounters;
            var index = cpuId & _indexMask;
            Interlocked.Add(ref perCoreCounters[index].Value, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Increment()
        {
            Increment(CpuIdCache.GetCurrentCpuId());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Increment(int cpuId)
        {
            if (IntPtr.Size == 4)
            {
                InterlockedIncrement(cpuId);
            }
            else
            {
                var perCoreCounters = _perCoreCounters;
                var index = cpuId & _indexMask;
                perCoreCounters[index].Value++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InterlockedIncrement()
        {
            InterlockedIncrement(CpuIdCache.GetCurrentCpuId());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InterlockedIncrement(int cpuId)
        {
            var perCoreCounters = _perCoreCounters;
            var index = cpuId & _indexMask;
            Interlocked.Increment(ref perCoreCounters[index].Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decrement()
        {
            Decrement(CpuIdCache.GetCurrentCpuId());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Decrement(int cpuId)
        {
            if (IntPtr.Size == 4)
            {
                InterlockedDecrement(cpuId);
            }
            else
            {
                var perCoreCounters = _perCoreCounters;
                var index = cpuId & _indexMask;
                perCoreCounters[index].Value--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InterlockedDecrement()
        {
            InterlockedDecrement(CpuIdCache.GetCurrentCpuId());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InterlockedDecrement(int cpuId)
        {
            var perCoreCounters = _perCoreCounters;
            var index = cpuId & _indexMask;
            Interlocked.Decrement(ref perCoreCounters[index].Value);
        }

        private class CounterCore : LeftPad48
        {
            public long Value;
        }

        private class RightPaddedCounterCore : CounterCore
        {
#pragma warning disable 169
            private readonly Padding64 _padding0;
#pragma warning restore 169
        }
    }
}