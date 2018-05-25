// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Xunit;
using Spreads.Buffers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Core.Tests
{
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

        public void DoCallVsCallVirt(int r)
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

        [Fact(Skip = "Long running")]
        public void CallVsCallVirt()
        {
            for (int r = 0; r < 10; r++)
            {
                DoCallVsCallVirt(r);
                Console.WriteLine("-----------------");
            }
        }

        [Fact(Skip = "Long running")]
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

        [Fact(Skip = "Long running")]
        public void ArrayVsListVsIListManyTimes()
        {
            for (int times = 0; times < 5; times++)
            {
                ArrayVsListVsIList();
            }
        }

        [Fact(Skip = "Long running")]
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

            //var vector = new Spreads.Experimental.Vector<double>(arr);
            //vector.IsSynchronized = true;

            var idxs = new int[len / 10];
            var rng = new System.Random();

            for (int i = 0; i < idxs.Length; i++)
            {
                idxs[i] = rng.Next(0, len);
            }

            var sw = new Stopwatch();
            var sum = 0.0;
            sw.Start();
            sw.Stop();

            var maxRounds = 100000;

            sw.Restart();
            sum = 0.0;
            for (int rounds = 0; rounds < maxRounds; rounds++)
            {
                foreach (var idx in idxs)
                {
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
            for (int rounds = 0; rounds < maxRounds; rounds++)
            {
                foreach (var idx in idxs)
                {
                    sum += list[idx];
                }
            }
            sw.Stop();
            Console.WriteLine($"List: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");

            //sw.Restart();
            //sum = 0.0;
            //for (int rounds = 0; rounds < maxRounds; rounds++)
            //{
            //    foreach (var idx in idxs)
            //    {
            //        sum += vector[idx];
            //    }
            //}
            //sw.Stop();
            //Console.WriteLine($"Vector: {sum}");
            //Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            //Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            //Console.WriteLine("---");

            sw.Restart();
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
            sw.Stop();
            Console.WriteLine($"LockedList: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");

            sw.Restart();
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
            sw.Stop();
            Console.WriteLine($"SpinLockedList: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");

            sw.Restart();
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
            sw.Stop();
            Console.WriteLine($"InterLockedList: {sum}");
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {(double)len * maxRounds / sw.ElapsedMilliseconds * 0.0001}");
            Console.WriteLine("---");

            sw.Restart();
            sum = 0.0;
            for (int rounds = 0; rounds < maxRounds; rounds++)
            {
                foreach (var idx in idxs)
                {
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
            for (int rounds = 0; rounds < maxRounds; rounds++)
            {
                foreach (var idx in idxs)
                {
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
            for (int rounds = 0; rounds < maxRounds; rounds++)
            {
                foreach (var idx in idxs)
                {
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
            for (int rounds = 0; rounds < maxRounds; rounds++)
            {
                foreach (var idx in idxs)
                {
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
            for (int rounds = 0; rounds < maxRounds; rounds++)
            {
                foreach (var idx in idxs)
                {
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