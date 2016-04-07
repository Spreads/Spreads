using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Threading;
using Spreads.Serialization;


namespace Spreads.Core.Tests {


    [TestFixture]
    public class NanoOptimizationTests {

        [Test, Ignore]
        public void ArrayVsListVsIListManyTimes() {
            for (int times = 0; times < 5; times++) {
                ArrayVsListVsIList();
            }
        }

        [Test]
        public void ArrayVsListVsIList() {

            var len = 1000;

            var arr = new double[len];

            var buffer = new byte[len * 8];
            var fixedBuffer = new FixedBuffer(buffer);

            var unmanagedMemory = Marshal.AllocHGlobal(len * 8);
            var directBuffer = new DirectBuffer(len * 8, unmanagedMemory);


            for (int i = 0; i < len; i++) {
                arr[i] = i;
                fixedBuffer.WriteDouble(i * 8, i);
                directBuffer.WriteDouble(i * 8, i);
            }
            var list = new List<double>(arr);

            var ilist = list as IList<double>;


            var vector = new Spreads.Experimental.Vector<double>(arr);
            vector.IsSynchronized = true;

            var idxs = new int[len / 10];
            var rng = new System.Random();

            for (int i = 0; i < idxs.Length; i++) {
                idxs[i] = rng.Next(0, len);
            }

            var sw = new Stopwatch();
            var sum = 0.0;
            sw.Start();
            sw.Stop();


            var maxRounds = 100000;

            sw.Restart();
            sum = 0.0;
            for (int rounds = 0; rounds < maxRounds; rounds++) {
                foreach (var idx in idxs) {
                    sum += arr[idx];
                }
            }
            sw.Stop();
            Console.WriteLine($"Array: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");



            sw.Restart();
            sum = 0.0;
            for (int rounds = 0; rounds < maxRounds; rounds++) {
                foreach (var idx in idxs) {
                    sum += list[idx];
                }
            }
            sw.Stop();
            Console.WriteLine($"List: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");


            sw.Restart();
            sum = 0.0;
            for (int rounds = 0; rounds < maxRounds; rounds++) {
                foreach (var idx in idxs) {
                    sum += vector[idx];
                }
            }
            sw.Stop();
            Console.WriteLine($"Vector: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");


            sw.Restart();
            sum = 0.0;
            for (int rounds = 0; rounds < maxRounds; rounds++) {
                foreach (var idx in idxs) {
                    lock (list) {
                        sum += list[idx];
                    }
                }
            }
            sw.Stop();
            Console.WriteLine($"LockedList: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");


            sw.Restart();
            sum = 0.0;
            SpinLock sl = new SpinLock();
            for (int rounds = 0; rounds < maxRounds; rounds++) {
                foreach (var idx in idxs) {
                    bool gotLock = false;
                    gotLock = false;
                    try {
                        sl.Enter(ref gotLock);
                        sum += list[idx];
                    } finally {
                        // Only give up the lock if you actually acquired it
                        if (gotLock) sl.Exit();
                    }
                }
            }
            sw.Stop();
            Console.WriteLine($"SpinLockedList: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");



            sw.Restart();
            sum = 0.0;
            var counter = 0L;
            for (int rounds = 0; rounds < maxRounds; rounds++) {
                foreach (var idx in idxs) {
                    var dummy = Interlocked.CompareExchange(ref counter, idx, 0L);
                    sum += list[idx] + dummy;
                }
            }
            sw.Stop();
            Console.WriteLine($"InterLockedList: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");


            sw.Restart();
            sum = 0.0;
            for (int rounds = 0; rounds < maxRounds; rounds++) {
                foreach (var idx in idxs) {
                    sum += ilist[idx];
                }
            }
            sw.Stop();
            Console.WriteLine($"IList: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");


            sw.Restart();
            sum = 0.0;
            for (int rounds = 0; rounds < maxRounds; rounds++) {
                foreach (var idx in idxs) {
                    sum += fixedBuffer.ReadDouble(idx * 8);
                }
            }
            sw.Stop();
            Console.WriteLine($"FixedBuffer: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");


            sw.Restart();
            sum = 0.0;
            var ifb = fixedBuffer as IDirectBuffer;
            for (int rounds = 0; rounds < maxRounds; rounds++) {
                foreach (var idx in idxs) {
                    sum += ifb.ReadDouble(idx * 8);
                }
            }
            sw.Stop();
            Console.WriteLine($"IFixedBuffer: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");



            sw.Restart();
            sum = 0.0;
            for (int rounds = 0; rounds < maxRounds; rounds++) {
                foreach (var idx in idxs) {
                    sum += directBuffer.ReadDouble(idx * 8);
                }
            }
            sw.Stop();
            Console.WriteLine($"DirectBuffer: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");


            sw.Restart();
            sum = 0.0;
            var idb = directBuffer as IDirectBuffer;
            for (int rounds = 0; rounds < maxRounds; rounds++) {
                foreach (var idx in idxs) {
                    sum += idb.ReadDouble(idx * 8);
                }
            }
            sw.Stop();
            Console.WriteLine($"IDirectBuffer: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");



        }


    }
}
