// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ObjectLayoutInspector;
using Spreads.Native;

namespace Spreads.Core.Tests.Buffers
{
    [Category("CI")]
    [TestFixture]
    public class PrivateMemoryTests
    {
        [Test, Explicit("")]
        public void PrivateMemoryLayout()
        {
            TypeLayout.PrintLayout<PrivateMemory<long>>();
            var layout = TypeLayout.GetLayout<PrivateMemory<long>>();
        }


        [Test
#if !DEBUG
         , Explicit("bench")
#endif
        ]
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void CouldCreateDisposePrivateMemory()
        {
#if !DEBUG
            var count = 1_000_000;
#else
            var count = 1_000;
#endif

            var init = PrivateMemory<byte>.Create(64 * 1024);
            PrivateMemory<byte>[] items = new PrivateMemory<byte>[count];
            for (int _ = 0; _ < 20; _++)
            {
                using (Benchmark.Run("CreateDispose", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        items[i] = PrivateMemory<byte>.Create(64 * 1024);
                        items[i].Dispose();
                    }
                }
                // 

                // using (Benchmark.Run("Dispose", count))
                // {
                //     for (int i = 0; i < count; i++)
                //     {
                //         items[i].Dispose();
                //     }
                // }
            }

            init.Dispose();
            Benchmark.Dump();
            // Mem.Collect(true);
            Mem.StatsPrint();
        }
    }
}