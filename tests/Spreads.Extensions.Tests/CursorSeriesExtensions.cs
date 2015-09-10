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
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Extensions.Tests {
    [TestFixture]
    public class CursorSeriesExtensionsTests {


        [Test]
        public void CouldCalculateIncompleteMovingAverage() {
            var sm = new SortedMap<int, double>();
            for (int i = 0; i < 20; i++) {
                sm.Add(i, i);
            }

            var sma = sm.SMA(2, true).ToSortedMap();

            var c = 0;
            foreach (var kvp in sma) {
                if (c == 0) {
                    Assert.AreEqual(c, kvp.Value);
                } else {
                    Assert.AreEqual(0.5 * (c + (double)(c - 1)), kvp.Value);
                }

                c++;
            }

        }


        [Test]
        public void CouldCalculateSMAInRealTime() {
            var sm = new SortedMap<int, double>();

            Task.Run(async () => {

                for (int i = 0; i < 20; i++) {
                    sm.Add(i, i);
                }

                await Task.Delay(100);

                for (int i = 20; i < 100; i++) {
                    await Task.Delay(1); // 15 msec
                    sm.Add(i, i);
                }
                sm.IsMutable = false;
            });


            var sma = sm.SMA(10, true);

            var c = sma.GetCursor();

            while (c.MoveNext(CancellationToken.None).Result) {
                Console.WriteLine("Key: {0}, value: {1}", c.CurrentKey, c.CurrentValue);
            }

        }

        [Test]
        public void CouldCalculateComplexGraph() {
            // TODO! need real complex data to test properly
            var sm = new SortedMap<DateTime, double>();

            var dataTask = Task.Run(async () => {

                for (int i = 0; i < 1000; i++) {
                    sm.Add(DateTime.Today.AddSeconds(i), i+10000);
                    await Task.Delay(25);
                }
                sm.IsMutable = false;
            });

            Thread.Sleep(50);

            var closeSeries = sm;

            var baseLeverage = 1.0;

            var sma = closeSeries.SMA(20, true);
            var deviation = sma / closeSeries - 1.0;
            var leverage = (baseLeverage * (-(5.0 * (deviation.Map(x => Math.Abs(x)))) + 1.0));

            var smaSignal = deviation.Map(x => (double)(Math.Sign(x)));

            var smaPositionMultiple = ((smaSignal * leverage).Map(x => 0.25 * (Math.Round(x / 0.25))));
            var smaPositionMultipleMap = smaPositionMultiple.ToSortedMap();

            var traderTask = Task.Run(async () => {
                var positionCursor = smaPositionMultiple.GetCursor();
                while (await positionCursor.MoveNext(CancellationToken.None)) //
                {
                    await Task.Delay(15);
                    Console.WriteLine("Time: {0}, position: {1}", positionCursor.CurrentKey, positionCursor.CurrentValue);

                }
            });

            dataTask.Wait();
            traderTask.Wait();
        }

    }

}
