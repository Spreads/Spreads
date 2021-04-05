using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests
{
    public static class TestUtils
    {
        public static long GetBenchCount(long count = 1_000_000, long debugCount = -1)
        {
#if DEBUG
            if (debugCount <= 0)
            {
                return 100;
            }

            return debugCount;
#else
            return count;
#endif
        }

        private struct Padding64
        {
            public long Padding0;
            public long Padding1;
            public long Padding2;
            public long Padding3;
            public long Padding4;
            public long Padding5;
            public long Padding6;
            public long Padding7;
        }

        public class ReaderWriterCountersMonitor : IDisposable
        {
            private Padding64 Padding0;
            public long WriteCount;
            private Padding64 Padding1;
            private long _prevW;
            private Padding64 Padding2;

            public long ReadCount;
            private Padding64 Padding3;
            private long _prevR;
            private Padding64 Padding4;
            private CancellationTokenSource _cts;

            public ReaderWriterCountersMonitor()
            {
                _cts = new CancellationTokenSource();
                Task.Run(StartMonitoring);
            }

            public CancellationToken Token => _cts.Token;

            public bool IsRunning => !_cts.IsCancellationRequested;

            private async Task StartMonitoring()
            {
                _prevW = Volatile.Read(ref WriteCount);
                _prevR = Volatile.Read(ref ReadCount);

                while (!Token.IsCancellationRequested)
                {
                    await Task.Delay(1000);

                    Console.WriteLine();

                    var w = Volatile.Read(ref WriteCount);
                    var r = Volatile.Read(ref ReadCount);

                    Console.WriteLine(
                        $"R: {r:N0} - {((r - _prevR) / 1000000.0):N2} Mops \t | W: {w:N0}- {((w - _prevW) / 1000000.0):N2} Mops | GC {GC.CollectionCount(0)}-{GC.CollectionCount(1)}-{GC.CollectionCount(2)}");

                    _prevW = w;
                    _prevR = r;
                }
            }

            public void Dispose()
            {
                _cts.Cancel();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public long IncrementRead()
            {
                // var value = Volatile.Read(ref ReadCount) + 1;
                // Volatile.Write(ref ReadCount, value);
                return ++ReadCount;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public long InterlockedIncrementRead()
            {
                return Interlocked.Increment(ref ReadCount);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public long IncrementWrite()
            {
                //var value = Volatile.Read(ref WriteCount) + 1;
                //Volatile.Write(ref WriteCount, value);
                return ++WriteCount;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public long InterlockedIncrementWrite()
            {
                return Interlocked.Increment(ref WriteCount);
            }
        }
    }
}
