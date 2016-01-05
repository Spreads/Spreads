using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace TAQParse {
    public class Program {
        // this is a file from ftp://ftp.nyxdata.com/Historical%20Data%20Samples/Daily%20TAQ/EQY_US_ALL_TRADE_20150805.zip
        // 654 MB compressed, 3.8GB uncompressed. ASCII with fixed 106 byte row size + 2 bytes for \r\n
        private static string path = @"X:\Data\EQY_US_ALL_TRADE_20150805.zip";
        static unsafe void Main(string[] args) {
            var chunkSize = 100000;
            var date = new DateTime(2015, 8, 5);
            var fb = new FixedBuffer();
            var tsize = Marshal.SizeOf(typeof(TaqTrade));
            Console.WriteLine(tsize);

            var zip = ZipFile.OpenRead(path);
            var stream = zip.Entries.Single().Open();

            using (var reader = new StreamReader(stream, Encoding.ASCII))
            using (var bReader = new BinaryReader(stream, Encoding.ASCII)) {
                byte[] compressedBuffer = null;
                var byteBuffer = new byte[106];
                var tradesArray = new TaqTrade[chunkSize];
                var line = reader.ReadLine();
                Console.WriteLine(line);
                Console.WriteLine("Press enter to continue");
                Console.ReadLine();
                var sw = new Stopwatch();
                sw.Start();
                var c = 0;
                while (!reader.EndOfStream) { // && c < 100
                    // these two lines take 57% time
                    line = reader.ReadLine();
                    Encoding.ASCII.GetBytes(line, 0, 106, byteBuffer, 0);

                    fb.Wrap(byteBuffer);
                    var trade = new TaqTrade(date, fb);
                    tradesArray[c % chunkSize] = trade;

                    c++;
                    if (c % chunkSize == 0) {
                        var comrpessed = Serializer.Serialize(tradesArray);
                        var averageComprSize = comrpessed.Length * 1.0 / chunkSize;
                        var ratio = (106.0 * chunkSize) / (comrpessed.Length * 1.0);
                        //Console.WriteLine(line);
                        Console.WriteLine($"Read so far: {c}, ratio: {ratio}, comp size: {averageComprSize}");
                        //Console.WriteLine("Press enter to continue");
                        //Console.ReadLine();
                    }
                }
                sw.Stop();
                Console.WriteLine($"Lines read: ${c} in msecs: {sw.ElapsedMilliseconds}");
            }

            Console.WriteLine("Finished");
            Console.ReadLine();

        }
    }
}
