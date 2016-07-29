using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Serialization;
using Ractor;
using Spreads;
using Spreads.Buffers;
using Spreads.Storage;


namespace TAQParse {
    public static class StreamExtension {
        public static int ReadLineIntoBuffer(this Stream stream, byte[] buffer) {
            int byteValue;
            var idx = -1;
            while ((byteValue = stream.ReadByte()) != -1 && (byteValue != '\n'))
                if (byteValue != '\r' && byteValue != '\n') {
                    buffer[++idx] = (byte)byteValue;
                }
            return idx + 1;
        }
    }

    public class Program {
        // this is a file from ftp://ftp.nyxdata.com/Historical%20Data%20Samples/Daily%20TAQ/EQY_US_ALL_TRADE_20150805.zip
        // 654 MB compressed, 3.8GB uncompressed. ASCII with fixed 106 byte row size + 2 bytes for \r\n
        // 35232194 records
        // in MySql, storage takes 743 MB, with random access to any TAQ value
        private static string path = @"C:\EQY_US_ALL_TRADE_20150805.zip";

        private static void Main3(string[] args) {
            var t = Task.FromResult(1);
            Interlocked.Exchange(ref t, t.ContinueWith(x => {
                Console.WriteLine("Cont 1");
                return 0;
            }));
            Console.ReadLine();
        }


        static unsafe void Main(string[] args) {
            GC.Collect(3, GCCollectionMode.Forced, true);
            using (var repo = new DataRepository("../TAQSampleRepo")) {

                var date = new DateTime(2015, 8, 5);

                var tsize = Marshal.SizeOf(typeof(TaqTrade));
                Console.WriteLine(tsize);

                var zip = ZipFile.OpenRead(path);
                var stream = zip.Entries.Single().Open();

                var seriesDictionary = new Dictionary<string, IPersistentOrderedMap<DateTime, TaqTrade>>();

                using (BufferedStream bs = new BufferedStream(stream, 2 * 1024 * 1024))
                using (var reader = new StreamReader(bs, Encoding.ASCII)) {
                    byte[] compressedBuffer = null;
                    var byteBuffer = new byte[106];
                    int len;
                    var line = reader.ReadLine();
                    len = bs.ReadLineIntoBuffer(byteBuffer);
                    Console.WriteLine(line);
                    Console.WriteLine("Press enter to continue");
                    Console.ReadLine();
                    var sw = new Stopwatch();
                    sw.Start();
                    var c = 0;
                    while ((len = bs.ReadLineIntoBuffer(byteBuffer)) != 0) {
                        // && c < 100
                        var fb = new FixedBuffer(byteBuffer, 0, len);
                        var trade = new TaqTrade(date, fb);

                        var symbol = trade.Symbol.ToLowerInvariant().Trim();

                        IPersistentOrderedMap<DateTime, TaqTrade> series;
                        if (!seriesDictionary.TryGetValue(symbol, out series)) {
                            series = repo.WriteSeries<DateTime, TaqTrade>(symbol).Result;
                            seriesDictionary[symbol] = series;
                        }

                        series[trade.Time] = trade;

                        c++;
                        if (c % 100000 == 0) {
                            Console.WriteLine($"Read so far: {c}");
                            foreach (var s in seriesDictionary) {
                                s.Value.Flush();
                            }
                        }
                    }
                    sw.Stop();
                    foreach (var series in seriesDictionary) {
                        series.Value.Flush();
                    }
                    Console.WriteLine($"Lines read: ${c} in msecs: {sw.ElapsedMilliseconds}");
                }

                Console.WriteLine("Finished");
                GC.Collect(3, GCCollectionMode.Forced, true);
                Console.WriteLine($"Total memory: {GC.GetTotalMemory(true)}");
                Console.ReadLine();
            }
        }

        private static void Main2(string[] args) {
            // from Ractor.Persistence
            using (var repo = new DataRepository("../TAQSampleRepo")) {
                IReadOnlyOrderedMap<DateTime, double> aapl = repo.ReadSeries<DateTime, TaqTrade>("aapl").Result.Map(t => t.TradePrice / 10000.0);
                Console.WriteLine("Count: " + aapl.Count());
                Console.WriteLine("Open: " + aapl.First.Value);
                Console.WriteLine("High: " + aapl.Values.Max());
                Console.WriteLine("Low: " + aapl.Values.Min());
                Console.WriteLine("Close: " + aapl.Last.Value);
                Console.WriteLine("Average price: " + aapl.Values.Average());
                Console.WriteLine("Total volume: " + aapl.Values.Sum());
                //https://uk.finance.yahoo.com/q/hp?s=AAPL&b=5&a=07&c=2015&e=5&d=07&f=2015&g=d

                var msft = repo.ReadSeries<DateTime, TaqTrade>("msft").Result.Map(t => t.TradePrice / 10000.0);
                ;
                var spread = (aapl.Repeat() / msft.Repeat() - 1.0).ToSortedMap();

                Console.ReadLine();
            }
        }

    }
}
