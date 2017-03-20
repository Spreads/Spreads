using Spreads.Buffers;
using Spreads.Serialization;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Spreads.Core.Run
{
    class Program
    {
        static void Main(string[] args)
        {
            var rng = new Random();
            var ptr = Marshal.AllocHGlobal(1000000);
            var db = new DirectBuffer(1000000, ptr);
            var source = new decimal[10000];
            for (var i = 0; i < 10000; i++)
            {
                source[i] = i;
            }

            var len = BinarySerializer.Write(source, ref db, 0, null,
                CompressionMethod.Zstd);

            Console.WriteLine($"Useful: {source.Length * 16}");
            Console.WriteLine($"Total: {len}");

            var destination = new decimal[10000];

            var len2 = BinarySerializer.Read(db, ref destination);

            if (source.SequenceEqual(destination))
            {
                Console.WriteLine("OK");
            }
            Console.ReadLine();
        }
    }
}