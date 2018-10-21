using Spreads.Core.Tests.Buffers;
using Spreads.Core.Tests.Serialization;
using System;
using System.Diagnostics;
using Spreads.Core.Tests.Collections.Concurrent;

namespace Spreads.Core.Run
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
        private static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleListener());

#pragma warning disable 618
            Settings.DoAdditionalCorrectnessChecks = false;
#pragma warning restore 618

            var test = new AtomicCounterTests();
            test.ServiceBenchmark();

            //IlwdBenchmark();

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
            var test = new Tests.Blosc.BloscTests();
            // test.CouldShuffleUnshuffle();

            Console.WriteLine("----------- LZ4 -----------");
            test.Lz4Benchmark();
            Console.WriteLine("----------- ZSTD -----------");
            test.ZstdBenchmark();
            Console.WriteLine("----------- GZip -----------");
            test.GZipBenchmark();
#if NETCOREAPP2_1
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
    }
}