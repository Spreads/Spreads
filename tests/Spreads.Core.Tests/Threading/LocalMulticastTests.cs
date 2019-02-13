// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Threading
{
    [TestFixture]
    public class LocalMulticastTests
    {
        [Test]
        public unsafe void CouldSendReceive()
        {
            var connection0 = new LocalMulticast<long>(51311);
            var connection1 = new LocalMulticast<long>(51311);
            var connection2 = new LocalMulticast<long>(51311);
            var connection3 = new LocalMulticast<long>(51311);

            connection0.StartReceive();
            connection1.StartReceive();
            connection2.StartReceive();
            connection3.StartReceive();

            Thread.Sleep(100);

            var buffer = new byte[] {1, 2, 3};

            connection0.Send(0);
            Thread.Sleep(100);
            connection1.Send(1);
            Thread.Sleep(100);
            connection2.Send(2);
            Thread.Sleep(100);
            connection3.Send(3);

            Thread.Sleep(100);

            connection0.Dispose();
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

        private static readonly int MessageLength = 1;

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
                var lastReceivedTotalBytes = Subscriber.TotalBytes();
                var lastSentTotalBytes = Publisher.TotalBytes();

                while (running)
                {
                    Thread.Sleep(1000);

                    var duration = _stopwatch.ElapsedMilliseconds;

                    var newSentTotalBytes = Publisher.TotalBytes();
                    var bytesSent = newSentTotalBytes - lastSentTotalBytes;

                    var newTotalReceivedBytes = Subscriber.TotalBytes();
                    var bytesReceived = newTotalReceivedBytes - lastReceivedTotalBytes;

                    Console.WriteLine(
                        $"Duration {duration:N0}ms | {bytesReceived / MessageLength:N0} in msgs | {bytesReceived:N0} in bytes | {bytesSent / MessageLength:N0} out msg | {bytesSent:N0} out bytes | {(bytesSent - bytesReceived) / MessageLength} lost, GC0 {GC.CollectionCount(0)}, GC1 {GC.CollectionCount(1)}, GC2 {GC.CollectionCount(2)}");

                    _stopwatch.Restart();
                    lastReceivedTotalBytes = newTotalReceivedBytes;
                    lastSentTotalBytes = newSentTotalBytes;
                }
            }
        }

        public sealed class Publisher
        {
            public readonly LocalMulticast<long> Connection;

            public Publisher()
            {
                Connection = new LocalMulticast<long>(51311);
            }

            public long TotalBytes()
            {
                return Connection.SendCounter * MessageLength;
            }

            public void Run()
            {
                var buffer = new byte[MessageLength];
                while (running)
                {
                    Connection.Send(42);
                }
            }
        }

        public class Subscriber
        {
            public readonly LocalMulticast<long> Connection;

            private long _totalBytes;

            public Subscriber()
            {
                Connection = new LocalMulticast<long>(51311);
            }

            public long TotalBytes()
            {
                return Connection.ReceiveCounter * MessageLength;
            }

            public void Run()
            {
                Connection.StartReceive();
            }
        }
    }
}