// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.Collections.Experimental;
using Spreads.DataTypes;
using Spreads.Utils;

namespace Spreads.Core.Tests.Collections.Experimental
{
    [TestFixture]
    public unsafe class VectorTests
    {
        [Test, Explicit("Benchmark")]
        public void SetSpeed()
        {
            Console.WriteLine(Vector<double>.IsPinnable);

            const int count = 50_000_000;
            var array = new double[count];
            var memory = new Memory<double>(array);
            var handle = memory.Pin();
            var db = new DirectBuffer((IntPtr) (count * 8), (byte*) handle.Pointer);
            var vectorTArr = new Vector<double>(array);
            var vectorTBuf = new Vector<double>(db);
            var vectorArr = new Vector(array);
            var vectorBuf = new Vector(db);

            var variant = Variant.Create(array);
            
            for (int r = 0; r < 10; r++)
            {

                using (Benchmark.Run("Array", count))
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        array[i] = i;
                    }
                }

                using (Benchmark.Run("vectorTArr", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        vectorTArr[i] = i;
                    }
                }

                using (Benchmark.Run("vectorTBuf", count))
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        vectorTBuf[i] = i;
                    }
                }

                using (Benchmark.Run("vectorArr", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        vectorArr.Set<double>(i, i);
                    }
                }

                using (Benchmark.Run("vectorBuf", count))
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        vectorBuf.Set<double>(i, i);
                    }
                }

                using (Benchmark.Run("Variant", count))
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        variant.Set<double>(i, i);
                    }
                }
            }

            Benchmark.Dump("Write");


            for (int r = 0; r < 10; r++)
            {
                var sum1 = 0.0;
                using (Benchmark.Run("Array", count))
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        sum1 = array[i];
                    }
                }

                var sum2 = 0.0;
                using (Benchmark.Run("vectorTArr", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        sum2 = vectorTArr[i];
                    }
                }

                var sum3 = 0.0;
                using (Benchmark.Run("vectorTBuf", count))
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        sum3 = vectorTBuf[i];
                    }
                }

                var sum4 = 0.0;
                using (Benchmark.Run("vectorArr", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        sum4 = vectorArr.Get<double>(i);
                    }
                }

                var sum5 = 0.0;
                using (Benchmark.Run("vectorBuf", count))
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        sum5 = vectorBuf.Get<double>(i);
                    }
                }

                var sum6 = 0.0;
                using (Benchmark.Run("Variant", count))
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        sum6= variant.Get<double>(i);
                    }
                }

                Assert.IsTrue(sum1 + sum2 + sum3 + sum4 + sum5 + sum6 > 0);
            }

            Benchmark.Dump("Read");
        }

        
    }
}
