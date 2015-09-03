using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Spreads;
using Spreads.Collections;
using Spreads.Collections.Tests;


public class MyInc
{
    public MyInc(int value)
    {
        this.value = value;
    }
    public int value;

    public int Inc()
    {
        this.value = this.value + 1;
        return this.value;
    }
}

namespace Spreads.Tests.CSProfile {
    class Program {
        private static readonly MyInc myinc = new MyInc(0);
        static void Main(string[] args)
        {
            new ZipNTests().CouldZipManyNonContinuousInRealTime();
            //var myinc = new MyInc(0);
            //myinc.Inc();
            //Console.WriteLine(myinc.value);

            //var mi = new MyInc(0);
            //mi.Inc();
            //mi.Inc();
            ////Console.WriteLine(mi.value); // prints 2!

            //var sd = new SortedDeque<int>();
            //sd.Add(1);
            //sd.Add(2);
            //sd.Add(3);
            //sd.Add(4);

            //foreach (var v in sd) {
            //    Console.WriteLine(v);
            //}

            //var en = sd.GetEnumerator();

            //while (en.MoveNext())
            //{
            //    Console.WriteLine(en.Current);
            //}

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
            //new ZipNTests().CouldZipMillionIntsWithMoveNextContX5();
            //new MoveNextAsyncTests().CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor();
            // new BatchMapValuesCursorTests().CouldAddWitDefaultMathProvider();
            Console.ReadLine();
        }
    }
}
