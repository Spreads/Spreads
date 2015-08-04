using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using System.Threading.Tasks;
using System.Threading;

namespace Spreads.Collections.Tests {

    [TestFixture]
    public class MoveNextAsyncTests {

        [Test]
        public void UpdateEventIsTriggered() {
            var sm = new SortedMap<DateTime, double>();
            (sm as IUpdateable<DateTime, double>).OnData += (s,x) =>
            {
                Console.WriteLine("Added {0} : {1}", x.Key, x.Value);
            };

            sm.Add(DateTime.UtcNow.Date.AddSeconds(0), 0);

        }


        [Test]
        public void CouldMoveAsyncOnEmptySM()
        {
            var sm = new SortedMap<DateTime, double>();
            
            var c = sm.GetCursor();

            var moveTask = c.MoveNext(CancellationToken.None);

            sm.Add(DateTime.UtcNow.Date.AddSeconds(0), 0);

            var result = moveTask.Result;

            Assert.IsTrue(result);

        }

        [Test]
        public void CouldReadSortedMapNewValuesWhileTheyAreAddedUsingCursor() {

            var sm = new SortedMap<DateTime, double>();
            //sm.IsSynchronized = true;
            for (int i = 0; i < 5; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            var addTask = Task.Run(async () => {
                await Task.Delay(50);
                for (int i = 5; i < 10; i++) {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //await Task.Delay(1);
                }
            });

            double sum = 0.0;
            var sumTask = Task.Run(async () => {
                var c = sm.GetCursor();
                while (c.MoveNext()) {
                    sum += c.CurrentValue;
                }
                Assert.AreEqual(10, sum);

                //await Task.Delay(50);
                while (await c.MoveNext(CancellationToken.None)) {
                    sum += c.CurrentValue;
                    //Console.WriteLine("Current key: {0}", c.CurrentKey);
                    if (c.CurrentKey == DateTime.UtcNow.Date.AddSeconds(10 - 1)) {
                        break;
                    }
                }
            });


            sumTask.Wait();
            addTask.Wait();

            double expectedSum = 0.0;
            for (int i = 0; i < 10; i++) {
                expectedSum += i;
            }
            Assert.AreEqual(expectedSum, sum);
        }

    }
}
