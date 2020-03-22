using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Spreads.Native;
using Spreads.Utils;

namespace Spreads.Collections.Concurrent
{
    [StructLayout(LayoutKind.Explicit, Size = 384)]
    // ReSharper disable once InconsistentNaming
    public class MPMCQueue
    {
        [FieldOffset(Settings.SAFE_CACHE_LINE + 8)]
        protected readonly Cell[] _enqueueBuffer;

        [FieldOffset(Settings.SAFE_CACHE_LINE + 16)]
        private int _enqueuePos;

        [FieldOffset(0)]
        protected readonly int _enqueueMask;

        [FieldOffset(Settings.SAFE_CACHE_LINE * 2 + 8)]
        private readonly Cell[] _dequeueBuffer;
        
        [FieldOffset(Settings.SAFE_CACHE_LINE * 2 + 16)]
        private int _dequeuePos;

        [FieldOffset(0)]
        protected readonly int _dequeueMask;

        public MPMCQueue(int bufferSize)
        {
            bufferSize = BitUtil.FindNextPositivePowerOfTwo(Math.Max(2, bufferSize));

            var buffer = new Cell[bufferSize];

            for (var i = 0; i < bufferSize; i++)
            {
                buffer[i] = new Cell(i, null);
            }

            _enqueueBuffer = _dequeueBuffer = buffer;
            _enqueuePos = _dequeuePos = 0;

            _enqueueMask = _dequeueMask = bufferSize - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object? Dequeue()
        {
            object result = null;
            var spinner = new SpinWait();
            do
            {
                var buffer = _dequeueBuffer;
                var pos = _dequeuePos;
                var mask = _dequeueMask;
                
                var index = pos & mask;
                ref var cell = ref buffer[index];
                if (cell.Sequence == pos + 1 && Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
                {
                    result = cell.Element;
                    cell.Element = null;
                    Volatile.Write(ref cell.Sequence, pos + 1 + mask);
                    break;
                }

                if (cell.Sequence < pos + 1)
                    break;
                
                // if (spinner.NextSpinWillYield)
                //     spinner.Reset();
                spinner.SpinOnce();
            } while (true);

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Enqueue(object item)
        {
            bool result = false;
            var spinner = new SpinWait();
            do
            {
                var buffer = _enqueueBuffer;
                var pos = _enqueuePos;
                var mask = _enqueueMask;
                var index = pos & mask;
                ref var cell = ref buffer[index];

                if (cell.Sequence == pos && Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
                {
                    cell.Element = item;
                    Volatile.Write(ref cell.Sequence, pos + 1);
                    result = true;
                    break;
                }

                if (cell.Sequence < pos)
                    break;

                // if (spinner.NextSpinWillYield)
                //     spinner.Reset();
                spinner.SpinOnce();
            } while (true);

            return result;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        protected struct Cell
        {
            [FieldOffset(0)]
            public int Sequence;

            [FieldOffset(8)]
            public object Element;

            public Cell(int sequence, object element)
            {
                Sequence = sequence;
                Element = element;
            }
        }
    }
}