// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Threading;
using System;
using System.Diagnostics;
using System.Threading;

namespace Spreads.Core.Tests.Threading
{
    [TestFixture]
    public class LocalMulticastTests
    {
        [Test]
        public unsafe void CouldSendReceive()
        {
            var connection0 = new LocalMulticast<long>(51311, x => Console.WriteLine("0: " + x), "0");
            var connection1 = new LocalMulticast<long>(51311, x => Console.WriteLine("1: " + x), "1");
            var connection2 = new LocalMulticast<long>(51311, x => Console.WriteLine("2: " + x), "2");
            var connection3 = new LocalMulticast<long>(51311, x => Console.WriteLine("3: " + x), "3");

            connection0.StartReceive();
            Thread.Sleep(100);
            connection1.StartReceive();
            Thread.Sleep(100);
            connection2.StartReceive();
            Thread.Sleep(100);
            connection3.StartReceive();

            Thread.Sleep(100);

            connection0.Send(1);
            Thread.Sleep(100);
            connection0.Send(2);
            Thread.Sleep(100);
            connection0.Send(3);
            Thread.Sleep(100);
            connection0.Send(4);

            Thread.Sleep(1000);

            // connection0.Dispose();
        }

        [Test, Explicit("long running")]
        public unsafe void CouldSendReceiveBench()
        {
            using (Process p = Process.GetCurrentProcess())
            {
                p.PriorityClass = ProcessPriorityClass.Normal;
            }

            var subscriber = new Subscriber();
            var publisher = new Publisher();
            var subscriberThread = new Thread(subscriber.Run) { Name = "subscriber" };
            var publisherThread = new Thread(publisher.Run) { Name = "publisher" };
            var rateReporterThread = new Thread(new RateReporter(subscriber, publisher).Run) { Name = "rate-reporter" };

            rateReporterThread.Start();
            subscriberThread.Start();
            publisherThread.Start();

            Thread.Sleep(100000);
            //Console.WriteLine("Press any key to stop...");
            //Console.Read();

            running = false;

            subscriberThread.Join();
            publisherThread.Join();
            rateReporterThread.Join();
        }


        private static volatile bool running = true;

        public class RateReporter
        {
            internal readonly Subscriber Subscriber;
            internal readonly Publisher Publisher;
            private readonly Stopwatch _stopwatch;

            public RateReporter(Subscriber subscriber, Publisher publisher)
            {
                Subscriber = subscriber;
                Publisher = publisher;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Run()
            {
                var lastReceivedSub = Subscriber.Connection.ReceiveCounter;
                var lastReceivedPub = Publisher.Connection.ReceiveCounter;

                var lastSentSub = Subscriber.Connection.SendCounter;
                var lastSentPub = Publisher.Connection.SendCounter;

                while (running)
                {
                    Thread.Sleep(1000);

                    var duration = _stopwatch.ElapsedMilliseconds;

                    var newTotalReceivedSub = Subscriber.Connection.ReceiveCounter;
                    var receivedSub = newTotalReceivedSub - lastReceivedSub;

                    var newTotalReceivedPub = Publisher.Connection.ReceiveCounter;
                    var receivedPub = newTotalReceivedPub - lastReceivedPub;

                    var newSentSub = Subscriber.Connection.SendCounter;
                    var sentSub = newSentSub - lastSentSub;

                    var newSentPub = Publisher.Connection.SendCounter;
                    var sentPub = newSentPub - lastSentPub;

                    var cumLoss = -1.0 + ((double) lastSentSub + lastSentPub) /
                                  (0.5 * (newTotalReceivedSub + newTotalReceivedPub));

                    Console.WriteLine(
                        $"[{duration:N0}ms] | {receivedSub:N0} IN SUB | {receivedPub:N0} IN PUB | {sentSub:N0} OUT SUB | {sentPub:N0} OUT PUB | {Math.Round(100.0 * cumLoss, 2)}% -CL | {sentPub + sentSub - receivedSub} -SUB | {sentPub + sentSub - receivedPub} -PUB | GC0 {GC.CollectionCount(0)}, GC1 {GC.CollectionCount(1)}, GC2 {GC.CollectionCount(2)}");

                    _stopwatch.Restart();
                    lastReceivedSub = newTotalReceivedSub;
                    lastReceivedPub = newTotalReceivedPub;
                    lastSentSub = newSentSub;
                    lastSentPub = newSentPub;
                }
            }
        }

        public sealed class Publisher
        {
            public readonly LocalMulticast<byte> Connection;

            public Publisher()
            {
                Connection = new LocalMulticast<byte>(51311, name: "PUB");
            }

            public void Run()
            {
                // Connection.StartReceive();
                while (running)
                {
                    Connection.Send(42);
                }
            }
        }

        public class Subscriber
        {
            public readonly LocalMulticast<byte> Connection;

            public Subscriber()
            {
                Connection = new LocalMulticast<byte>(51311, name: "SUB");
            }

            public void Run()
            {
                // Connection.StartReceive();
                while (running)
                {
                    Connection.Send(43);
                }
            }
        }
    }
}