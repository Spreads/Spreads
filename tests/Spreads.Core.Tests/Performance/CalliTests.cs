using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Spreads.Utils;

namespace Spreads.Core.Tests.Performance
{
    [TestFixture]
    public class CalliTests
    {
        public class DataBlock
        {
            public Array Ks;
            public Array Vs;

            public static DataBlock Create<K, V>(int length)
            {
                var ks = new K[length];
                var vs = new V[length];

                return new DataBlock() {Ks = ks, Vs = vs};
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetValues<K, V>(int index, ref K key, ref V value)
            {
                if (Getter == null)
                {
                    key = Unsafe.As<K[]>(Ks)[index];
                    value = Unsafe.As<V[]>(Vs)[index];
                }
                else
                {
                    GetValuesViaDelegate(this, index, ref key, ref value);
                }
            }

            // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // public void GetValuesCalli<K, V>(int index, ref K key, ref V value)
            // {
            //     if (Getter == null)
            //     {
            //         key = Unsafe.As<K[]>(Ks)[index];
            //         value = Unsafe.As<V[]>(Vs)[index];
            //     }
            //     else
            //     {
            //         GetValuesViaCalli(this, index, ref key, ref value);
            //     }
            // }

            public void GetValuesViaDelegate<K, V>(DataBlock block, int index, ref K key, ref V value)
            {
                // this won't be inlined, so we need to manually inline and leave only dlg call
                // the top-level method won't be inlined, so it must do all the job
                // but keep it for the final step and compare results.
#if DEBUG
            var dlg = (Getter<K, V>) Getter;
#else
                var dlg = Unsafe.As<Getter<K, V>>(Getter);
#endif
                dlg.Invoke(block, index, ref key, ref value);
            }

            // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // public void GetValuesViaCalli<K, V>(DataBlock block, int index, ref K key, ref V value)
            // {
            //     var dlg = Unsafe.As<Getter<K, V>>(Getter);
            //     UnsafeEx.CalliDataBlock(dlg, block, index, ref key, ref value, GetterPtr);
            // }

            public object Getter;
            public IntPtr GetterPtr;
        }

        internal delegate void Getter<K, V>(DataBlock block, int index, ref K key, ref V value);

        [Test, Explicit]
        public void TestDelegate()
        {
            var blocks = 100_000;
            var length = 1000;
            var count = length * blocks;

            var db = DataBlock.Create<int, int>(blocks);

            for (int i = 0; i < length; i++)
            {
                Unsafe.As<int[]>(db.Ks)[i] = i;
                Unsafe.As<int[]>(db.Vs)[i] = i;
            }

            for (int r = 0; r < 10; r++)
            {
                db.Getter = null;
                int k = default, v = default;
                k = Direct(count, blocks, length, db, k, ref v);

                Getter<int, int> getter = (DataBlock block, int index, ref int key, ref int value) =>
                {
                    key = Unsafe.As<int[]>(block.Ks)[index];
                    value = Unsafe.As<int[]>(block.Vs)[index];
                };
                db.Getter = getter;

                k = Getter0(count, blocks, length, db, k, ref v);

                Getter<int, int> getter2 = (DataBlock block, int index, ref int key, ref int value) =>
                {
                    getter(block, index, ref key, ref value);
                    value++;
                };
                db.Getter = getter2;

                k = Getter2(count, blocks, length, db, k, ref v);

                Getter<int, int> getter3 = (DataBlock block, int index, ref int key, ref int value) =>
                {
                    getter2(block, index, ref key, ref value);
                    value++;
                };
                db.Getter = getter3;

                k = Getter3(count, blocks, length, db, k, ref v);

                // Getter<int, int> getter4 = (DataBlock block, int index, ref int key, ref int value) =>
                // {
                //     getter3(block, index, ref key, ref value);
                //     value++;
                // };
                // db.Getter = getter4;
                //
                // k = Getter4(count, blocks, length, db, k, ref v);

                var getterPtr = getter.Method.MethodHandle.GetFunctionPointer();
                db.Getter = getter;
                db.GetterPtr = getterPtr;

                // Calli(count, blocks, length, db, k, v);

                // Why it segfaults?
                // Getter<int, int> getterCalli2 = (DataBlock block, int index, ref int key, ref int value) =>
                // {
                //     UnsafeEx.CalliDataBlock(getter, block, index, ref key, ref value, getterPtr);
                //     value++;
                // };

                // var getterPtrCalli2 = getterCalli2.Method.MethodHandle.GetFunctionPointer();
                // db.Getter = getterCalli2;
                // db.GetterPtr = getterPtrCalli2;

                // Calli2(count, blocks, length, db, k, v);
            }

            Benchmark.Dump();

            Console.WriteLine(db);
            // Console.WriteLine(getter);
            // Console.WriteLine(getter2);
            // Console.WriteLine(getter3);
            // // Console.WriteLine(getter2);
            // // Console.WriteLine(getter3);
            // GC.KeepAlive(getter);
            // GC.KeepAlive(getter2);
        }

        // [MethodImpl(MethodImplOptions.NoInlining/* | MethodImplOptions.AggressiveOptimization*/)]
        // private static void Calli2(int count, int blocks, int length, DataBlock db, int k, int v)
        // {
        //     using (Benchmark.Run("Calli2", count))
        //     {
        //         for (int _ = 0; _ < blocks; _++)
        //         {
        //             for (int i = 0; i < length; i++)
        //             {
        //                 db.GetValuesCalli(i, ref k, ref v);
        //             }
        //         }
        //     }
        // }
        //
        // [MethodImpl(MethodImplOptions.NoInlining/* | MethodImplOptions.AggressiveOptimization*/)]
        // private static void Calli(int count, int blocks, int length, DataBlock db, int k, int v)
        // {
        //     using (Benchmark.Run("Calli", count))
        //     {
        //         for (int _ = 0; _ < blocks; _++)
        //         {
        //             for (int i = 0; i < length; i++)
        //             {
        //                 db.GetValuesCalli(i, ref k, ref v);
        //             }
        //         }
        //     }
        // }

        [MethodImpl(MethodImplOptions.NoInlining /*| MethodImplOptions.AggressiveOptimization*/)]
        private static int Getter4(int count, int blocks, int length, DataBlock db, int k, ref int v)
        {
            using (Benchmark.Run("Getter4", count))
            {
                for (int _ = 0; _ < blocks; _++)
                {
                    for (int i = 0; i < length; i++)
                    {
                        db.GetValues(i, ref k, ref v);
                    }
                }
            }

            return k;
        }

        [MethodImpl(MethodImplOptions.NoInlining /*| MethodImplOptions.AggressiveOptimization*/)]
        private static int Getter3(int count, int blocks, int length, DataBlock db, int k, ref int v)
        {
            using (Benchmark.Run("Getter3", count))
            {
                for (int _ = 0; _ < blocks; _++)
                {
                    for (int i = 0; i < length; i++)
                    {
                        db.GetValues(i, ref k, ref v);
                    }
                }
            }

            return k;
        }

        [MethodImpl(MethodImplOptions.NoInlining /*| MethodImplOptions.AggressiveOptimization*/)]
        private static int Getter2(int count, int blocks, int length, DataBlock db, int k, ref int v)
        {
            using (Benchmark.Run("Getter2", count))
            {
                for (int _ = 0; _ < blocks; _++)
                {
                    for (int i = 0; i < length; i++)
                    {
                        db.GetValues(i, ref k, ref v);
                    }
                }
            }

            return k;
        }

        [MethodImpl(MethodImplOptions.NoInlining /*| MethodImplOptions.AggressiveOptimization*/)]
        private static int Getter0(int count, int blocks, int length, DataBlock db, int k, ref int v)
        {
            using (Benchmark.Run("Getter", count))
            {
                for (int _ = 0; _ < blocks; _++)
                {
                    for (int i = 0; i < length; i++)
                    {
                        db.GetValues(i, ref k, ref v);
                    }
                }
            }

            return k;
        }

        [MethodImpl(MethodImplOptions.NoInlining /*| MethodImplOptions.AggressiveOptimization*/)]
        private static int Direct(int count, int blocks, int length, DataBlock db, int k, ref int v)
        {
            using (Benchmark.Run("Direct", count))
            {
                for (int _ = 0; _ < blocks; _++)
                {
                    for (int i = 0; i < length; i++)
                    {
                        db.GetValues(i, ref k, ref v);
                    }
                }
            }

            return k;
        }
    }
}
