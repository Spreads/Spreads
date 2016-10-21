// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json;
using Spreads;
using System.Threading;
using System.Threading.Tasks;
//using Spreads.RPlugin;

namespace Spreads.Extensions.Tests {

    // Deprecated for direct access via a special R package and R.NET

    //[TestFixture]
    //public class RPluginTests {
    //    [Test]
    //    public void CouldRoundtripArrays() {
    //        var arr = new double[] {1,2,3};
    //        var res = RUtils.Call("spreads_add42", arr);
    //        foreach (var item in res) {
    //            Console.WriteLine($"{item}");
    //        }
    //    }

    //    [Test]
    //    public void CouldRoundtripSeries() {
    //        var sm = new SortedMap<DateTime, double>();
    //        sm.Add(DateTime.Today, 0);
    //        sm.Add(DateTime.Today.AddSeconds(1), 1);
    //        sm.Add(DateTime.Today.AddSeconds(2), 2);
    //        var res = RUtils.Call("spreads_add42", sm as Series<DateTime, double>);
    //        foreach (var item in res) {
    //            Console.WriteLine($"{item.Key} - {item.Value}");
    //        }
    //    }

    //    [Test]
    //    public void CouldRoundtripArraysBenchmark() {

    //        var sm = new SortedMap<DateTime, double>();
    //        var arr = new double[100];
    //        for (int i = 0; i < 100; i++) {
    //            sm.Add(DateTime.Today.AddSeconds(i), i);
    //            arr[i] = i;
    //        }
            
    //        for (int i = 0; i < 2; i++) {
    //            var res = RUtils.Call("spreads_add42", sm as Series<DateTime, double>);
    //            var res2 = RUtils.Call("spreads_add42", arr);
    //        }
    //        for (int r = 5; r <= 5000; r *= 10) {
    //            var sw = new Stopwatch();
    //            sw.Start();

    //           var count = r;
    //            for (int i = 0; i < count; i++) {
    //                var res = RUtils.Call("spreads_add42", arr);
    //                //var res = RUtils.Call("spreads_echo", sm as Series<DateTime, double>);
    //            }
    //            sw.Stop();
    //            Console.WriteLine($"{count}: Elapsed: {sw.ElapsedMilliseconds}, ops: {count * 1000.0 / sw.ElapsedMilliseconds }");

    //        }
    //    }
    //}
}
