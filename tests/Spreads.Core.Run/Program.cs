using System;
using Spreads.Core.Tests.Serialization;

namespace Spreads.Core.Run
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Settings.DoAdditionalCorrectnessChecks = false;

            PackWithHeaderBenchmark();

            Console.WriteLine("Finished, press enter to exit...");
            Console.ReadLine();
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