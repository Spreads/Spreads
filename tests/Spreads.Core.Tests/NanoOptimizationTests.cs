// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public unsafe class NanoOptimizationTests
    {
        public interface IIncrementable
        {
            int Increment();
        }

        public struct ThisIsSrtuct : IIncrementable
        {
            private byte[] value;
            private int* ptr;
            private GCHandle pinnedGcHandle;

            public ThisIsSrtuct(byte[] bytes)
            {
                value = bytes;
                pinnedGcHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
                ptr = (int*)pinnedGcHandle.AddrOfPinnedObject().ToPointer();
            }

            public int Increment()
            {
                *((int*)ptr) = *((int*)ptr) + 1;
                return (*((int*)ptr));
                //return (*((int*)ptr))++;
            }
        }

        public static class ThisIsStaticClass
        {
            private static byte[] value = new byte[4];
            private static IntPtr ptr;
            private static GCHandle pinnedGcHandle;

            static ThisIsStaticClass()
            {
                pinnedGcHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
                ptr = pinnedGcHandle.AddrOfPinnedObject();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int Increment()
            {
                *((int*)ptr) = *((int*)ptr) + 1;
                return (*((int*)ptr));
            }
        }

        public class ThisIsClass : IIncrementable
        {
            private byte[] value = new byte[4];
            private IntPtr ptr;
            private GCHandle pinnedGcHandle;

            public ThisIsClass()
            {
                pinnedGcHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
                ptr = pinnedGcHandle.AddrOfPinnedObject();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Increment()
            {
                *((int*)ptr) = *((int*)ptr) + 1;
                return (*((int*)ptr));
            }
        }

        public class ThisIsBaseClass : IIncrementable
        {
            private byte[] value = new byte[4];
            private IntPtr ptr;
            private GCHandle pinnedGcHandle;

            public ThisIsBaseClass()
            {
                pinnedGcHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
                ptr = pinnedGcHandle.AddrOfPinnedObject();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public virtual int Increment()
            {
                *((int*)ptr) = *((int*)ptr) + 1;
                return (*((int*)ptr));
            }
        }

        public class ThisIsDerivedClass : ThisIsBaseClass, IIncrementable
        {
            private byte[] value = new byte[4];
            private IntPtr ptr;
            private GCHandle pinnedGcHandle;

            public ThisIsDerivedClass()
            {
                pinnedGcHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
                ptr = pinnedGcHandle.AddrOfPinnedObject();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Increment()
            {
                *((int*)ptr) = *((int*)ptr) + 1;
                return (*((int*)ptr));
            }
        }

        public class ThisIsDerivedClass2 : ThisIsBaseClass, IIncrementable
        {
            private byte[] value = new byte[4];
            private IntPtr ptr;
            private GCHandle pinnedGcHandle;

            public ThisIsDerivedClass2()
            {
                pinnedGcHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
                ptr = pinnedGcHandle.AddrOfPinnedObject();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Increment()
            {
                *((int*)ptr) = *((int*)ptr) + 1;
                return (*((int*)ptr));
            }
        }

        public sealed class ThisIsSealedDerivedClass : ThisIsDerivedClass, IIncrementable
        {
            private byte[] value = new byte[4];
            private IntPtr ptr;
            private GCHandle pinnedGcHandle;

            public ThisIsSealedDerivedClass()
            {
                pinnedGcHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
                ptr = pinnedGcHandle.AddrOfPinnedObject();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Increment()
            {
                *((int*)ptr) = *((int*)ptr) + 1;
                return (*((int*)ptr));
            }
        }

        public sealed class ThisIsSealedDerivedClass2 : ThisIsDerivedClass, IIncrementable
        {
            private byte[] value = new byte[4];
            private IntPtr ptr;
            private GCHandle pinnedGcHandle;

            public ThisIsSealedDerivedClass2()
            {
                pinnedGcHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
                ptr = pinnedGcHandle.AddrOfPinnedObject();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Increment()
            {
                *((int*)ptr) = *((int*)ptr) + 1;
                return (*((int*)ptr));
            }
        }

        public sealed class ThisIsSealedClass : IIncrementable
        {
            private byte[] value = new byte[4];
            private IntPtr ptr;
            private GCHandle pinnedGcHandle;

            public ThisIsSealedClass()
            {
                pinnedGcHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
                ptr = pinnedGcHandle.AddrOfPinnedObject();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Increment()
            {
                *((int*)ptr) = *((int*)ptr) + 1;
                return (*((int*)ptr));
            }
        }

        public class ThisIsComposedClass : IIncrementable
        {
            private ThisIsClass value;

            public ThisIsComposedClass()
            {
                value = new ThisIsClass();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Increment()
            {
                return value.Increment();
            }
        }

        private void ConstrainedStruct<T>(T incrementable) where T : IIncrementable
        {
            var count = 100000000;
            var sw = new Stopwatch();
            sw.Restart();
            for (int i = 0; i < count; i++)
            {
                incrementable.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Constrained struct {sw.ElapsedMilliseconds}");
        }

        [Test, Ignore]
        public void CallVsCallVirt(int r)
        {
            var count = 100000000;
            var sw = new Stopwatch();

            sw.Restart();
            int* ptr = stackalloc int[1];

            int intValue = 0;
            for (int i = 0; i < count; i++)
            {
                *((int*)ptr) = *((int*)ptr) + 1;
                intValue = (*((int*)ptr));
            }
            sw.Stop();
            Console.WriteLine($"Value {sw.ElapsedMilliseconds} ({intValue})");

            sw.Restart();
            var str = new ThisIsSrtuct(new byte[4]);
            for (int i = 0; i < count; i++)
            {
                str.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Struct {sw.ElapsedMilliseconds}");

            sw.Restart();
            IIncrementable strAsInterface = (IIncrementable)(new ThisIsSrtuct(new byte[4]));
            for (int i = 0; i < count; i++)
            {
                strAsInterface.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Struct as Interface {sw.ElapsedMilliseconds}");

            var constrainedStr = (new ThisIsSrtuct(new byte[4]));
            ConstrainedStruct(constrainedStr);

            sw.Restart();
            for (int i = 0; i < count; i++)
            {
                ThisIsStaticClass.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Static Class {sw.ElapsedMilliseconds}");

            sw.Restart();
            var cl = new ThisIsClass();
            for (int i = 0; i < count; i++)
            {
                cl.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Class {sw.ElapsedMilliseconds}");

            sw.Restart();
            var scl = new ThisIsSealedClass();
            for (int i = 0; i < count; i++)
            {
                scl.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Sealed Class {sw.ElapsedMilliseconds}");

            sw.Restart();
            IIncrementable cli = (IIncrementable)new ThisIsClass();
            for (int i = 0; i < count; i++)
            {
                cli.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Class as Interface {sw.ElapsedMilliseconds}");

            sw.Restart();
            var dcl = new ThisIsDerivedClass();
            for (int i = 0; i < count; i++)
            {
                dcl.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Derived Class {sw.ElapsedMilliseconds}");

            sw.Restart();
            IIncrementable dcli = (IIncrementable)new ThisIsDerivedClass();
            for (int i = 0; i < count; i++)
            {
                dcli.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Derived Class as Interface {sw.ElapsedMilliseconds}");

            sw.Restart();
            var sdcl = new ThisIsSealedDerivedClass();
            for (int i = 0; i < count; i++)
            {
                sdcl.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Sealed Derived Class {sw.ElapsedMilliseconds}");

            sw.Restart();
            IIncrementable sdcli = (IIncrementable)new ThisIsSealedDerivedClass();
            for (int i = 0; i < count; i++)
            {
                sdcli.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Sealed Derived Class as Interface {sw.ElapsedMilliseconds}");

            sw.Restart();
            var comp = new ThisIsComposedClass();
            for (int i = 0; i < count; i++)
            {
                comp.Increment();
            }
            sw.Stop();
            Console.WriteLine($"Composed class {sw.ElapsedMilliseconds}");
        }

        [Test, Ignore]
        public void CallVsCallVirt()
        {
            for (int r = 0; r < 10; r++)
            {
                CallVsCallVirt(r);
                Console.WriteLine("-----------------");
            }
        }

        [Test, Ignore]
        public void SOVolatileQuestion()
        {
            long counter = 0;
            var values = new double[1000000];

            values[42] = 3.1415;
            // Is this line needed instead of simple assignment above,
            // or the implicit full-fence of Interlocked will guarantee that
            // all threads will see the values[42] after interlocked increment?
            //Volatile.Write(ref values[42], 3.1415);
            Interlocked.Increment(ref counter);
        }

        [Test, Ignore]
        public void ArrayVsListVsIListBenchmark()
        {
            for (int times = 0; times < 5; times++)
            {
                ArrayVsListVsIList();
            }
            Benchmark.Dump();
        }

        [Test, Ignore]
        public void ArrayVsListVsIList()
        {
            var len = 1000;

            var arr = new double[len];

            var buffer = new byte[len * 8];
            var fixedBuffer = new FixedBuffer(buffer);

            var unmanagedMemory = Marshal.AllocHGlobal(len * 8);
            var directBuffer = new DirectBuffer(len * 8, unmanagedMemory);

            for (int i = 0; i < len; i++)
            {
                arr[i] = i;
                fixedBuffer.WriteDouble(i * 8, i);
                directBuffer.WriteDouble(i * 8, i);
            }
            var list = new List<double>(arr);

            var ilist = list as IList<double>;

            var idxs = new int[len / 10];
            var rng = new System.Random();

            for (int i = 0; i < idxs.Length; i++)
            {
                idxs[i] = rng.Next(0, len);
            }

            var sum = 0.0;

            var maxRounds = 100000;

            using (Benchmark.Run("Array", len * maxRounds / 10))
            {
                sum = 0.0;
                for (int rounds = 0; rounds < maxRounds; rounds++)
                {
                    foreach (var idx in idxs)
                    {
                        sum += arr[idx];
                    }
                }
            }


            using (Benchmark.Run("List", len * maxRounds / 10))
            {
                sum = 0.0;
                for (int rounds = 0; rounds < maxRounds; rounds++)
                {
                    foreach (var idx in idxs)
                    {
                        sum += list[idx];
                    }
                }
            }



            using (Benchmark.Run("LockedList", len * maxRounds / 10))
            {
                sum = 0.0;
                for (int rounds = 0; rounds < maxRounds; rounds++)
                {
                    foreach (var idx in idxs)
                    {
                        lock (list)
                        {
                            sum += list[idx];
                        }
                    }
                }
            }

            using (Benchmark.Run("SpinLockedList", len * maxRounds / 10))
            {
                sum = 0.0;
                SpinLock sl = new SpinLock();
                for (int rounds = 0; rounds < maxRounds; rounds++)
                {
                    foreach (var idx in idxs)
                    {
                        bool gotLock = false;
                        gotLock = false;
                        try
                        {
                            sl.Enter(ref gotLock);
                            sum += list[idx];
                        }
                        finally
                        {
                            // Only give up the lock if you actually acquired it
                            if (gotLock) sl.Exit();
                        }
                    }
                }
            }

            using (Benchmark.Run("InterLockedList", len * maxRounds / 10))
            {
                sum = 0.0;
                var counter = 0L;
                for (int rounds = 0; rounds < maxRounds; rounds++)
                {
                    foreach (var idx in idxs)
                    {
                        var dummy = Interlocked.CompareExchange(ref counter, idx, 0L);
                        sum += list[idx] + dummy;
                    }
                }
            }

            using (Benchmark.Run("IList", len * maxRounds / 10))
            {
                sum = 0.0;
                for (int rounds = 0; rounds < maxRounds; rounds++)
                {
                    foreach (var idx in idxs)
                    {
                        sum += ilist[idx];
                    }
                }
            }

            using (Benchmark.Run("FixedBuffer", len * maxRounds / 10))
            {
                sum = 0.0;
                for (int rounds = 0; rounds < maxRounds; rounds++)
                {
                    foreach (var idx in idxs)
                    {
                        sum += fixedBuffer.ReadDouble(idx * 8);
                    }
                }
            }

            using (Benchmark.Run("IFixedBuffer", len * maxRounds / 10))
            {
                sum = 0.0;
                var ifb = fixedBuffer as IDirectBuffer;
                for (int rounds = 0; rounds < maxRounds; rounds++)
                {
                    foreach (var idx in idxs)
                    {
                        sum += ifb.ReadDouble(idx * 8);
                    }
                }
            }

            using (Benchmark.Run("DirectBuffer", len * maxRounds / 10))
            {
                sum = 0.0;
                for (int rounds = 0; rounds < maxRounds; rounds++)
                {
                    foreach (var idx in idxs)
                    {
                        sum += directBuffer.ReadDouble(idx * 8);
                    }
                }
            }

            using (Benchmark.Run("}", len * maxRounds / 10))
            {
                sum = 0.0;
                var idb = directBuffer as IDirectBuffer;
                for (int rounds = 0; rounds < maxRounds; rounds++)
                {
                    foreach (var idx in idxs)
                    {
                        sum += idb.ReadDouble(idx * 8);
                    }
                }
            }
        }

        [Test, Ignore]
        public unsafe void PinningNonBlittableThrows()
        {
            var buffer = BufferPool<string>.RentOwnedBuffer(100);
            Assert.Throws<ArgumentException>(() =>
            {
                var handle = buffer.Buffer.Pin();
            });
            Assert.False(TypeHelper<string>.IsPinnable);
            Assert.True(TypeHelper<string>.Size <= 0);
        }

        [Test, Ignore]
        public unsafe void UnsafeWorksWithRefTypes()
        {
            var buffer = BufferPool<string>.RentOwnedBuffer(100);
            ref var addr = ref GetRef(buffer);
            ref var pos = ref Unsafe.Add(ref addr, 1);
            pos = "asd";
        }

        private static bool mutable = TypeHelper<string>.IsPinnable;
        private static readonly bool ro = TypeHelper<string>.IsPinnable;

        [Test, Ignore]
        public unsafe void StaticROFieldIsOptimized()
        {
            var count = 1000000;

            var sum = 0.0;
            var sw = new Stopwatch();

            for (int r = 0; r < 10; r++)
            {
                sw.Restart();
                for (int ii = 0; ii < 1000; ii++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (mutable)
                        {
                            sum += i;
                        }
                    }
                }
                sw.Stop();
                Console.WriteLine($"mutable {sw.MOPS(count * 1000)}");

                sw.Restart();
                for (int ii = 0; ii < 1000; ii++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (ro)
                        {
                            sum += i;
                        }
                    }
                }
                sw.Stop();
                Console.WriteLine($"readonly {sw.MOPS(count * 1000)}");

                sw.Restart();
                for (int ii = 0; ii < 1000; ii++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (TypeHelper<string>.IsPinnable)
                        {
                            sum += i;
                        }
                    }
                }
                sw.Stop();
                Console.WriteLine($"TypeHelper {sw.MOPS(count * 1000)}");
            }
        }

        private ref T GetRef<T>(OwnedBuffer<T> buffer)
        {
            if (buffer.Buffer.TryGetArray(out var segment))
            {
                return ref segment.Array[0];
            }
            else
            {
                var handle = buffer.Buffer.Pin();
                return ref Unsafe.AsRef<T>(handle.PinnedPointer);
            }
        }

        private void UnsafeGenericWrite<T>(int count, OwnedBuffer<T> buffer)
        {
            var handle = buffer.Buffer.Pin();

            var sum = 0L;
            var sw = new Stopwatch();
            sw.Restart();
            for (int ii = 0; ii < 50; ii++)
            {
                for (int i = 0; i < count; i++)
                {
                    //ref var addr = ref GetRef(buffer);
                    ref var addr = ref Unsafe.AsRef<T>(handle.PinnedPointer);
                    ref var pos = ref Unsafe.Add(ref addr, i);
                    pos = (T)(object)(double)i;
                    sum++;
                }
            }
            sw.Stop();
            Console.WriteLine($"Unsafe write {sw.MOPS(count * 50)}");
        }

        private void UnsafeGenericRead<T>(int count, OwnedBuffer<T> buffer)
        {
            var handle = buffer.Buffer.Pin();

            var sum = 0.0;
            var sw = new Stopwatch();
            sw.Restart();
            for (int ii = 0; ii < 100; ii++)
            {
                for (int i = 0; i < count; i++)
                {
                    ref var addr = ref Unsafe.AsRef<T>(handle.PinnedPointer);
                    ref var pos = ref Unsafe.Add(ref addr, i);
                    sum += (double)(object)pos;
                }
            }
            sw.Stop();
            Console.WriteLine($"Unsafe read {sw.MOPS(count * 100)}");
        }

        //[Test, Ignore]
        //public unsafe void CallHiddenMethodFromBaseClass()
        //{
        //    OwnedBuffer<int> b = new DirectOwnedBuffer<int>(new int[1]);
        //    Assert.False(b.IsDisposed);
        //}

        [Test, Ignore]
        public unsafe void ArrayVsOwnedBuffer()
        {
            var count = 1000000;
            var array = new double[count];
            var buffer = BufferPool<double>.RentOwnedBuffer(count, true);
            var buffer2 = BufferPool<double>.RentOwnedBuffer(count, true);

            var handle2 = buffer2.Buffer.Pin();
            var pointer2 = handle2.PinnedPointer;

            var buffer3 = BufferPool<double>.RentOwnedBuffer(count, true);
            var handle3 = buffer3.Buffer.Pin();
            var pointer3 = handle3.PinnedPointer;

            //var buffer4 = new DirectOwnedBuffer<double>(new double[count]);

            long sum = 0L;
            var sw = new Stopwatch();

            for (int r = 0; r < 10; r++)
            {
                sw.Restart();
                for (int ii = 0; ii < 50; ii++)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i] = i;
                        sum++;
                    }
                }
                sw.Stop();
                Console.WriteLine($"Array write {sw.MOPS(count * 50)}");

                //sum = 0;
                //sw.Restart();
                //for (int ii = 0; ii < 50; ii++)
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        buffer4[i] = i;
                //        sum++;
                //    }
                //}
                //sw.Stop();
                //Console.WriteLine($"DirectOwnedBuffer write {sw.MOPS(count * 50)}");
                //Assert.True(sum < Int64.MaxValue);

                //sw.Restart();
                //for (int ii = 0; ii < 50; ii++)
                //{
                //    var span = buffer.Buffer.Span;
                //    for (int i = 0; i < count; i++)
                //    {
                //        span[i] = i;
                //        sum++;
                //    }
                //}
                //sw.Stop();
                //Console.WriteLine($"Buffer write {sw.MOPS(count * 50)}");
                //Assert.True(sum < Int64.MaxValue);

                //sw.Restart();
                //for (int ii = 0; ii < 50; ii++)
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        *((double*)pointer2 + i) = i;
                //        sum++;
                //    }
                //}
                //sw.Stop();
                //Console.WriteLine($"Pointer write {sw.MOPS(count * 50)}");
                //Assert.True(sum < Int64.MaxValue);

                //UnsafeGenericWrite<double>(count, buffer3);

                Assert.True(sum < Int64.MaxValue);
            }

            Console.WriteLine("--------------------------------");

            for (int r = 0; r < 10; r++)
            {
                double sum2 = 0;
                sw.Restart();
                for (int ii = 0; ii < 100; ii++)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        sum2 += array[i];
                    }
                }
                sw.Stop();
                Console.WriteLine($"Array read {sw.MOPS(count * 100)}");

                //sum2 = 0;
                //sw.Restart();
                //for (int ii = 0; ii < 50; ii++)
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        sum2 += buffer4[i];
                //    }
                //}
                //sw.Stop();
                //Console.WriteLine($"DirectOwnedBuffer read {sw.MOPS(count * 50)}");

                //sum2 = 0;
                //sw.Restart();
                //var span = buffer.Buffer.Span;
                //for (int ii = 0; ii < 50; ii++)
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        sum2 += span[i];
                //    }
                //}
                //sw.Stop();
                //Console.WriteLine($"Buffer read {sw.MOPS(count * 50)}");

                //sum2 = 0;
                //sw.Restart();
                //for (int ii = 0; ii < 50; ii++)
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        sum2 += *((double*)pointer2 + i);
                //    }
                //}
                //sw.Stop();
                //Console.WriteLine($"Pointer read {sw.MOPS(count * 50)}");

                //UnsafeGenericRead<double>(count, buffer3);

                Assert.True(sum2 < Int64.MaxValue);
            }
        }

        [Test, Ignore]
        public unsafe void ArrayVsOwnedBufferBinarySearchDateTime()
        {
            var count = 4096;
            var array = new DateTime[count];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = DateTime.Today.AddTicks(i);
            }

            //var buffer = new DirectOwnedBuffer<DateTime>(array);
            var sw = new Stopwatch();

            for (int r = 0; r < 10; r++)
            {
                double sum2 = 0;
                sw.Restart();
                for (int ii = 0; ii < 100; ii++)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        sum2 += Array.BinarySearch(array, array[i], KeyComparer<DateTime>.Default);
                    }
                }
                sw.Stop();
                Console.WriteLine($"Array {sw.MOPS(count * 100)}");

                //sum2 = 0;
                //sw.Restart();
                //for (int ii = 0; ii < 100; ii++)
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        sum2 += buffer.BinarySearch(array[i]);
                //    }
                //}
                //sw.Stop();
                //Console.WriteLine($"DirectOwnedBuffer {sw.MOPS(count * 100)}");

                //Assert.True(sum2 < Int64.MaxValue);
            }
        }

        [Test, Ignore]
        public unsafe void ArrayVsOwnedBufferBinarySearchInt()
        {
            var count = 40960;
            var array = new int[count];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = i;
            }

            //var buffer = new DirectOwnedBuffer<int>(array);
            var sw = new Stopwatch();

            for (int r = 0; r < 10; r++)
            {
                double sum2 = 0;
                sw.Restart();
                for (int ii = 0; ii < 100; ii++)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        sum2 += Array.BinarySearch(array, i, KeyComparer<int>.Default);
                    }
                }
                sw.Stop();
                Console.WriteLine($"Array {sw.MOPS(count * 100)}");

                //sum2 = 0;
                //sw.Restart();
                //for (int ii = 0; ii < 100; ii++)
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        sum2 += buffer.BinarySearch(array[i]);
                //    }
                //}
                //sw.Stop();
                //Console.WriteLine($"DirectOwnedBuffer {sw.MOPS(count * 100)}");

                //Assert.True(sum2 < Int64.MaxValue);
            }
        }

        [Test, Ignore]
        public void MeasureNodaTimeInstantSize()
        {
            Console.WriteLine(Unsafe.SizeOf<NodaTime.Instant>());
        }

        [Test, Ignore]
        public void LargeLongsToDoubleKeepWeekOrder()
        {
            var start = 1L << 54;
            var count = 10000000000L;

            for (long i = start; i < start + count; i++)
            {
                if ((double)i > (double)(i + 1))
                {
                    Assert.Fail($"Double cast doesn't keep order for {i}");
                }
            }

            Assert.Pass("Double keeps week order");
        }

        [Test, Ignore]
        public void IfVsArrayAccess()
        {
            // For Union2/Zip2 we could have a very unpredictable relative position,
            // which is returned by KeyComparer over keys
            // To determine which will go next, we could have c >= 0 and
            // convert this unsafely to a byte
            // `If`s could cause too many branch mispredictions

            var arr = new int[2];
            arr[0] = -1;
            arr[1] = 1;

            var count = 50000000;
            var rng = new Random();
            var values = new int[count];
            var comps = new int[count];

            for (int i = 0; i < count; i++)
            {
                values[i] = rng.Next(0, 100);
            }

            for (int i = 1; i < count - 1; i++)
            {
                var c = KeyComparer<int>.Default.Compare(values[i - 1], values[i]);
                comps[i] = c;
            }

            for (int r = 0; r < 20; r++)
            {
                var sumIf = 0;
                using (Benchmark.Run("If", count))
                {
                    for (int i = 1; i < count; i++)
                    {
                        var c = comps[i]; // values[i - 1].CompareTo(values[i]); // 
                        if (c >= 0) // values[i - 1] >= values[i]) //
                        {
                            sumIf += arr[1];
                        }
                        else
                        {
                            sumIf += arr[0];
                        }
                    }
                }

                var sumUnsafe = 0;
                using (Benchmark.Run("Unsafe", count))
                {
                    for (int i = 1; i < count; i++)
                    {
                        var c = comps[i]; // values[i - 1].CompareTo(values[i]); // 
                        //bool b = c >= 0; // values[i - 1] >= values[i]; //  
                        var idx = 1 ^ (((uint)c) >> 31); // *((byte*)(&b));
                        sumUnsafe += arr[idx];
                    }
                }

                Assert.AreEqual(sumIf, sumUnsafe);
            }

            Benchmark.Dump();

        }


        [Test]
        public void CouldSetStaticReadonlyFieldProgrammatically()
        {
            Settings.DoAdditionalCorrectnessChecks = false;

            // NB uncommenting this line will set the value to its default=true
            // before the above property is assigned due to how static fields are 
            // initialized
            //Assert.True(Settings.AdditionalCorrectnessChecks.DoChecks);


            Assert.False(Settings.DoAdditionalCorrectnessChecks);
        }

        [Test, Ignore]
        public void MeasureLoopTime()
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            var sum = 0.0;
            for (int i = 0; i < 100_000_000; i++)
            {
                sum += i * (sum + 1);
            }

            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
            
        }
    }
}