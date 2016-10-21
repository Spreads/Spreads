// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Benchmark
{
    using System.Reflection;

    // assembly, method, time in msecs, mops, totalMem, peakMem, gen0, gen1, gen2, gen3,typeName, comment
    using LogAction = Action<string, string, long, double, double, double, int, int, int, int, string, string>;

    public class Benchmark
    {
        private readonly LogAction _logAction;

        public Benchmark()
        {
            ////int p, int((endtMem - startMem)/1024L)
            //Console.WriteLine(message + ", #{0}, ops: {1}, mem/item: {2}",
            //  count.ToString(), p.ToString(), ((endtMem - startMem) / count).ToString())
            LogAction logAction = (assembly, method, msec, mops,
                endMem, peakMem, gen0, gen1, gen2, gen3, typeName, comment) =>
            {
                if (string.IsNullOrEmpty(comment))
                {
                    Trace.TraceInformation("{0}:{1}, msec: {2}, mops: {3}, mem:{4}, peakMem: {5}, g0: {6}, g1: {7}, g2: {8}, g3: {9}, type: {10}", assembly, method, msec, mops,
                    endMem, peakMem, gen0, gen1, gen2, gen3, typeName);
                }
                else
                {
                    Trace.TraceInformation("{0}:{1}, msec: {2}, mops: {3}, mem:{4}, peakMem: {5}, g0: {6}, g1: {7}, g2: {8}, g3: {9}, type: {10}, {11}", assembly, method, msec, mops,
                   endMem, peakMem, gen0, gen1, gen2, gen3, typeName, comment);
                }
            };
        }

        public Benchmark(LogAction logAction)
        {
            _logAction = logAction;
        }

        public void Run(Action action, long count, string assembly, string method, Type type = null, string comment = null)
        {
            GC.Collect(3, GCCollectionMode.Forced, true);
            var startMem = GC.GetTotalMemory(false);
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);
            var gen3 = GC.CollectionCount(3);
            var sw = Stopwatch.StartNew();
            action.Invoke();
            sw.Stop();
            var peakMem = GC.GetTotalMemory(false) - startMem;
            GC.Collect(3, GCCollectionMode.Forced, true);
            var endMem = GC.GetTotalMemory(false) - startMem;
            var ops = (1000L*count/sw.ElapsedMilliseconds);
            gen0 = GC.CollectionCount(0) - gen0;
            gen1 = GC.CollectionCount(1) - gen1;
            gen2 = GC.CollectionCount(2) - gen2;
            gen3 = GC.CollectionCount(3) - gen3;
            // assembly, method, time in msecs, mops, totalMem, peakMem, gen0, gen1, gen2, gen3,typeName, comment
            _logAction.Invoke(assembly, method, sw.ElapsedMilliseconds, Math.Round(ops/1000000.0, 2),
                Math.Round(endMem/1000000.0, 2), Math.Round(peakMem/1000000.0, 2), gen0, gen1, gen2, gen3, type == null ? "" : type.Name,
                comment);
            
        }

        public void Run(long count, string comment, Action action)
        {
            this.Run(action, count, Assembly.GetCallingAssembly().GetName().Name, "", null, comment);
        }
    }
}
