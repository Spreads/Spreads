using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Collections.Persistent;
using Spreads.Storage;

namespace Spreads.Extensions.Tests {
    [TestFixture]
    public class IpcListenerTests {
        [Test]
        public void CouldReactToChanges() {
            IpcLongIncrementListener l1;
            IpcLongIncrementListener l2;
            var task1 = Task.Run(() => {
                var cur = 0L;
                l1 = new IpcLongIncrementListener("../CouldReactToChanges", (lastSeen, current) => {
                    Console.WriteLine($"Task1: lastSeen {lastSeen} - current {current}");
                    cur = current;
                }, 0L);
                l1.Set(cur);
                l1.Start();
                while (cur < 20) {
                    cur++;
                    l1.Set(cur);
                    Thread.Sleep(150);
                }
                Thread.Sleep(15);
            });
            var task2 = Task.Run(() => {
                var cur = 0L;
                l2 = new IpcLongIncrementListener("../CouldReactToChanges", (lastSeen, current) => {
                    Console.WriteLine($"Task2: lastSeen {lastSeen} - current {current}");
                    cur = current;
                }, 0L);
                l2.Set(cur);
                l2.Start();
                while (cur < 20) {
                    cur++;
                    l2.Set(cur);
                    Thread.Sleep(150);
                }
                Thread.Sleep(15);
            });
            task1.Wait();
            task2.Wait();
        }

    }
}
