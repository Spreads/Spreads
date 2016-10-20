using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Spreads.Collections.Experimental.Tests.Concurrency
{

    [TestFixture]
    public class ObservableTests
    {
        private const string threadPoolThreadName = "Thread pool";
        static string Identify() {
            var thread = Thread.CurrentThread;
            string name = thread.IsThreadPoolThread
                ? threadPoolThreadName : thread.Name;
            if (string.IsNullOrEmpty(name))
                name = "#" + thread.ManagedThreadId;
            Console.WriteLine("Continuation on: " + name);
            return name;
        }

        // In experimental SM implementation we removed SemaphoreSlim to a simple 
        // TaskComepletionSource. This could lead to unexpected behavior
        // with synchronous continuation:
        // http://stackoverflow.com/questions/22579206/how-can-i-prevent-synchronous-continuations-on-a-task
        // https://blogs.msdn.microsoft.com/pfxteam/2015/02/02/new-task-apis-in-net-4-6/

        [Test]
        [Ignore]
        public void ContinuationOnMNAIsSynchronous()
        {
            var sm = new SortedMap<int, int>();
            var cursor = sm.GetCursor();
            var task = cursor.MoveNextAsync();
            // Both continuations must run on the thread pool
            task.ContinueWith(delegate {
                Assert.AreEqual(threadPoolThreadName, Identify());
            });
            task.ContinueWith(delegate {
                Assert.AreEqual(threadPoolThreadName, Identify());
            }, TaskContinuationOptions.ExecuteSynchronously);
            sm.Add(1, 1);
            task.Wait();
            Thread.Sleep(100);
        }
    }
}