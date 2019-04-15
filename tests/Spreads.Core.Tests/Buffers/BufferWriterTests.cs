// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Buffers
{
    [Category("CI")]
    [TestFixture]
    public class BufferWriterTests
    {
        [Test]
        public void SlicesDisposeSlabs()
        {
            var payload = new byte[32 * 1024];

            var tasks = new List<Task>();
            for (int t = 0; t < 100; t++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < 10000; i++)
                    {
                        var bw = BufferWriter.Create();
                        bw.WriteSpan(payload);
                        bw.WriteSpan(payload);
                        bw.WriteSpan(payload);
                        bw.WriteSpan(payload);
                        bw.Dispose();
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());


        }

        
    }
}
