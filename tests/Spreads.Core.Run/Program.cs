using System;

namespace Spreads.Core.Run
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            CompressionBenchmark();

            Console.WriteLine("Finished, press enter to exit...");
            Console.ReadLine();
        }

        private static void CompressionBenchmark()
        {
            var test = new Tests.Blosc.BloscTests();

            Console.WriteLine("----------- LZ4 -----------");
            test.Lz4Benchmark();
            Console.WriteLine("----------- ZSTD -----------");
            test.ZstdBenchmark();
            Console.WriteLine("----------- Zlib -----------");
            test.ZlibBenchmark();
            Console.WriteLine("----------- Deflate -----------");
            test.DeflateBenchmark();
//#if NETCOREAPP2_1
//            Console.WriteLine("----------- Brotli -----------");
//            new Spreads.Core.Tests.Serialization.BrotliTests().BrotliBenchmark();
//#endif
            Console.WriteLine("Finished, press enter to exit...");
            Console.ReadLine();
        }
    }
}