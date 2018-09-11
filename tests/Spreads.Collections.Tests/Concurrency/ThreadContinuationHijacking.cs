//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using NUnit.Framework;

//namespace Spreads.Collections.Tests.Concurrency
//{
//    [TestFixture]
//    public class ObservableTests
//    {
//        private const string threadPoolThreadName = "Thread pool";

//        private static string Identify()
//        {
//            var thread = Thread.CurrentThread;
//            string name = thread.IsThreadPoolThread
//                ? threadPoolThreadName : thread.Name;
//            if (string.IsNullOrEmpty(name))
//                name = "#" + thread.ManagedThreadId;
//            Console.WriteLine("Continuation on: " + name);
//            return name;
//        }

//        // In experimental SM implementation we removed SemaphoreSlim to a simple
//        // TaskComepletionSource. This could lead to unexpected behavior
//        // with synchronous continuation:
//        // http://stackoverflow.com/questions/22579206/how-can-i-prevent-synchronous-continuations-on-a-task
//        // https://blogs.msdn.microsoft.com/pfxteam/2015/02/02/new-task-apis-in-net-4-6/

//        [Test]
//        [Ignore("long running")]
//        public void ContinuationOnMNAIsSynchronous()
//        {
//            var sm = new SortedMap<int, int>();
//            var cursor = sm.GetCursor();
//            var task = cursor.MoveNextAsync();
//            // Both continuations must run on the thread pool
//            task.ContinueWith(delegate
//            {
//                Assert.AreEqual(threadPoolThreadName, Identify());
//            });
//            task.ContinueWith(delegate
//            {
//                Assert.AreEqual(threadPoolThreadName, Identify());
//            }, TaskContinuationOptions.ExecuteSynchronously);
//            sm.Add(1, 1);
//            task.Wait();
//            Thread.Sleep(100);
//        }
//    }
//}