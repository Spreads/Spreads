﻿using System;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Core.Tests.Algorithms;
using Spreads.Core.Tests.Collections.Concurrent;
using Spreads.Core.Tests.Collections.Internal;
using Spreads.Core.Tests.Serialization;

namespace Spreads.Run
{
    internal class ConsoleListener : TraceListener
    {
        public override void Write(string message)
        {
            Console.Write(message);
        }

        public override void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
    }

    internal class Program
    {
        private static async Task Main(string[] args)
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new ConsoleListener());

            Settings.DoAdditionalCorrectnessChecks = false;

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            // Process.GetCurrentProcess().ProcessorAffinity = (IntPtr) 0b_00_0010_0000;
            // EquiJoinBench();
            ExecutionContext.SuppressFlow();
            Settings.SharedSpinLockNotificationPort = 53412;
            
            var test = new DataBlockTreeTests();
            test.CouldAppendBench();
            
            GC.Collect(2, GCCollectionMode.Forced, true, true);

            // Native.Mem.StatsPrint();

            Console.WriteLine("Finished, press enter to exit...");
            Console.ReadLine();
        }

        private static void IlwdBenchmark()
        {
            var test = new IndexedLockedWeakDictionaryTests();
            test.ILWDLookupBench();
        }

        private static void PackWithHeaderBenchmark()
        {
            var test = new CompressionTests();
            test.CouldPackWithHeaderBenchmark();
        }

        private static void SerializationBenchmark()
        {
            var test = new Utf8JsonTests();
            test.CompareUtf8JsonWithBinarySerializer();
        }

        private static void CompressionBenchmark()
        {
            var test = new Spreads.Core.Tests.Blosc.BloscTests();
            // test.CouldShuffleUnshuffle();

            Console.WriteLine("----------- LZ4 -----------");
            test.Lz4Benchmark();
            Console.WriteLine("----------- ZSTD -----------");
            test.ZstdBenchmark();
            Console.WriteLine("----------- GZip -----------");
            test.GZipBenchmark();
#if NETCOREAPP3_0xx
            Console.WriteLine("----------- Brotli -----------");
            test.BrotliBenchmark();
#endif
            Console.WriteLine("----------- Copy -----------");
            test.CopyViaCalliBenchmark();
            //Console.WriteLine("----------- Deflate -----------");
            //test.DeflateBenchmark();
            Console.WriteLine("Finished, press enter to exit...");
            Console.ReadLine();
        }

        //public static void EquiJoinBench()
        //{
        //    var sml = new MutableSeries<long,long>(); // alternative: SortedChunkedMap
        //    var smr = new MutableSeries<long, long>(); // alternative: SortedChunkedMap

        //    var countl = 1_000_000;
        //    var step = 10;
        //    var countr = countl * step;

        //    for (int i = 0; i < countl; i++)
        //    {
        //        sml.Add(i * step, i * step);
        //    }

        //    for (int i = 0; i < countr; i++)
        //    {
        //        smr.Add(i, i);
        //    }

        //    var rounds = 10;

        //    for (int r = 0; r < rounds; r++)
        //    {
        //        var count = 0L;
        //        var sum = 0L;
        //        using (Benchmark.Run("EquiJoin", countr))
        //        {
        //            var result = sml.Repeat().Zip(smr, (lv, rv) => lv);

        //            foreach (var keyValuePair in result)
        //            {
        //                sum += keyValuePair.Value;
        //                count++;
        //            }

        //            Console.WriteLine("COUNT: " + count);
        //        }
        //    }

        //    Benchmark.Dump();
        //}
    }
}