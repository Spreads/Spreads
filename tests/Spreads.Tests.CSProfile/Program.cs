using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Spreads;
using Spreads.Collections;
using Spreads.Collections.Tests;

namespace Spreads.Tests.CSProfile {
    class Program {
        static void Main(string[] args) {
            //var srlzr = new SpreadsDBSerializer();
            //var sw = new Stopwatch();
            //sw.Start();
            //decimal d;
            //for (int i = 0; i < 10000000; i++) {
            //    d = srlzr.Deserialize<decimal>(srlzr.Serialize(1.0M));
            //}
            //sw.Stop();
            //Console.WriteLine("Double: " + sw.ElapsedMilliseconds);

            //sw.Restart();
            //DateTime dt = DateTime.Now;
            //for (int i = 0; i < 10000000; i++) {
            //    dt = srlzr.Deserialize<DateTime>(srlzr.Serialize(dt));
            //}
            //sw.Stop();
            //Console.WriteLine("DateTime: " + sw.ElapsedMilliseconds);
            //var sm = new SortedMap<long, long>() as Series<long, long>;
            //var sum = sm + 1L;
            //var sum2 = sm + sm;
            //var check = sm > 1L;
            //var eq = sm == 1L;
            //var eq2 = sm == sm;
            new ZipNTests().CouldZipMillionIntsWithMoveNextContinuous();

            Console.ReadLine();
        }
    }
}
