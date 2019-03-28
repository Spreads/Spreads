// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Utils
{
    // TODO median for Dump() MOPS

    /// <summary>
    /// A utility to benchmark code snippets inside a using block.
    /// </summary>
    public static class Benchmark
    {
        /// <summary>
        /// Disable console output regardless of individual <see cref="Run"/> method parameters.
        /// </summary>
        public static bool ForceSilence { get; set; }

        private static Stopwatch _sw;
        private static bool _headerIsPrinted;
        private static readonly ConcurrentDictionary<string, List<Stat>> Stats = new ConcurrentDictionary<string, List<Stat>>();

        /// <summary>
        /// Returns an <see cref="IDisposable"/> structure that starts benchmarking and stops it when its Dispose method is called.
        /// Prints benchmark results on disposal unless the silent parameter is not set to true.
        /// </summary>
        /// <param name="caseName">Benchmark case.</param>
        /// <param name="innerLoopCount">Number of iterations to calculate performance.</param>
        /// <param name="silent">True to mute console output during disposal.</param>
        /// <returns>A disposable structure that measures time and memory allocations until disposed.</returns>
        public static Stat Run(string caseName, long innerLoopCount = 1, bool silent = false)
        {
            var sw = Interlocked.Exchange(ref _sw, null);
            sw = sw ?? new Stopwatch();

            var stat = new Stat(caseName, sw, innerLoopCount, silent);

            return stat;
        }

        private static void PrintHeader(string summary, string caller, int? caseLength = null, string unit = null)
        {
            var len = caseLength ?? 20;
            var caseDahes = new string('-', len + 1);
            var dashes = $"{caseDahes,-21}|{new string('-', 8),8}:|{new string('-', 9),9}:|{new string('-', 6),6}:|{new string('-', 6),6}:|{new string('-', 6),6}:|{new string('-', 8),8}:";
            Console.WriteLine();
            if (!string.IsNullOrWhiteSpace(caller)) { Console.WriteLine($"**{caller}**"); }
            if (!string.IsNullOrWhiteSpace(summary)) { Console.WriteLine($"*{summary}*"); }
            Console.WriteLine();
            Console.WriteLine(GetHeader(caseLength, unit));
            Console.WriteLine(dashes);
        }

        internal static string GetHeader(int? caseLength = null, string unit = null)
        {
            unit = unit ?? "MOPS";
            var len = caseLength ?? 20;
            var caseHeader = "Case".PadRight(len);
            return $" {caseHeader,-20}| {unit,7} | {"Elapsed",8} | {"GC0",5} | {"GC1",5} | {"GC2",5} | {"Memory",7} ";
        }

        /// <summary>
        /// Print a table with average benchmark results for all cases.
        /// </summary>
        /// <param name="summary"></param>
        /// <param name="caller">A description of the benchmark that is printed above the table.</param>
        /// <param name="unit">Overwrite default MOPS unit of measure</param>
        public static void Dump(string summary = "", [CallerMemberName]string caller = "", string unit = null)
        {
            var maxLength = Stats.Keys.Select(k => k.Length).Max();

            PrintHeader(summary, caller, maxLength);

            var stats = Stats.Select(GetAverages).OrderByDescending(s => s.MOPS);

            foreach (var stat in stats)
            {
                Console.WriteLine(stat.ToString(maxLength));
            }

            Stats.Clear();

            _headerIsPrinted = false;

            Stat GetAverages(KeyValuePair<string, List<Stat>> kvp)
            {
                if (kvp.Value == null) throw new ArgumentException($"Null stat list for the case: {kvp.Key}");
                if (kvp.Value.Count == 0) throw new InvalidOperationException($"Empty stat list for the case: {kvp.Key}");
                var skip = kvp.Value.Count > 1
                    ? (kvp.Value.Count >= 10 ? 3 : 1)
                    : 0;
                var values = kvp.Value.Skip(skip).ToList();

                var elapsed = values.Select(l => l._statSnapshot.Elapsed).Average();
                var gc0 = values.Select(l => l._statSnapshot.Gc0).Average();
                var gc1 = values.Select(l => l._statSnapshot.Gc1).Average();
                var gc2 = values.Select(l => l._statSnapshot.Gc2).Average();
                var memory = values.Select(l => l._statSnapshot.Memory).Average();

                var result = kvp.Value.First();

                result._statSnapshot.Elapsed = (long)elapsed;
                result._statSnapshot.Gc0 = gc0;
                result._statSnapshot.Gc1 = gc1;
                result._statSnapshot.Gc2 = gc2;
                result._statSnapshot.Memory = memory;

                return result;
            }
        }

        /// <summary>
        /// Benchmark run statistics.
        /// </summary>
        public struct Stat : IDisposable
        {
            public string CaseName { get; }
            public Stopwatch Stopwatch { get; }
            public long InnerLoopCount { get; }
            public StatSnapshot _statSnapshot;
            internal bool _silent;
            private readonly string _unit;

            internal Stat(string caseName, Stopwatch sw, long innerLoopCount, bool silent = false, string unit = null)
            {
                CaseName = caseName;
                Stopwatch = sw;
                InnerLoopCount = innerLoopCount;
                _silent = silent;
                _unit = unit;

                _statSnapshot = new StatSnapshot(Stopwatch, true);
            }

            /// <inheritdoc />
            public void Dispose()
            {
                var statEntry = new StatSnapshot(Stopwatch, false);
                Interlocked.Exchange(ref _sw, Stopwatch);

                _statSnapshot.Elapsed = statEntry.Elapsed;
                _statSnapshot.Gc0 = statEntry.Gc0 - _statSnapshot.Gc0 - 2;
                _statSnapshot.Gc1 = statEntry.Gc1 - _statSnapshot.Gc1 - 2;
                _statSnapshot.Gc2 = statEntry.Gc2 - _statSnapshot.Gc2 - 2;
                _statSnapshot.Memory = statEntry.Memory - _statSnapshot.Memory;

                var list = Stats.GetOrAdd(CaseName, (s1) => new List<Stat>());
                list.Add(this);

                if (!_silent && !ForceSilence)
                {
                    if (!_headerIsPrinted)
                    {
                        PrintHeader(null, null, unit: _unit);
                        _headerIsPrinted = true;
                    }
                    Console.WriteLine(ToString());
                }
            }

            /// <summary>
            /// Million operations per second.
            /// </summary>
            // ReSharper disable once InconsistentNaming
            public double MOPS => Math.Round((InnerLoopCount * 0.001) / _statSnapshot.Elapsed, 3);

            internal StatSnapshot StatSnapshot
            {
                get { return _statSnapshot; }
            }

            /// <inheritdoc />
            public override string ToString()
            {
                var trimmedCaseName = CaseName.Length > 20 ? CaseName.Substring(0, 17) + "..." : CaseName;
                return $"{trimmedCaseName,-20} |{MOPS,8:f2} | {_statSnapshot.Elapsed,5} ms | {_statSnapshot.Gc0,5:f1} | {_statSnapshot.Gc1,5:f1} | {_statSnapshot.Gc2,5:f1} | {_statSnapshot.Memory / (1024 * 1024.0),5:f3} MB";
            }

            internal string ToString(int caseAlignmentLength)
            {
                var paddedCaseName = CaseName.PadRight(caseAlignmentLength);
                return $"{paddedCaseName,-20} |{MOPS,8:f2} | {_statSnapshot.Elapsed,5} ms | {_statSnapshot.Gc0,5:f1} | {_statSnapshot.Gc1,5:f1} | {_statSnapshot.Gc2,5:f1} | {_statSnapshot.Memory / (1024 * 1024.0),5:f3} MB";
            }
        }

        public struct StatSnapshot
        {
            public long Elapsed;
            public double Gc0;
            public double Gc1;
            public double Gc2;
            public double Memory;

            public StatSnapshot(Stopwatch sw, bool start)
            {
                this = new StatSnapshot();

                if (!start)
                {
                    // end of measurement, first stop timer then collect/count
                    sw.Stop();
                    Elapsed = sw.ElapsedMilliseconds;

                    // NB we exclude forced GC from counters,
                    // by measuring memory before forced GC we could
                    // calculate uncollected garbage
                    Memory = GC.GetTotalMemory(false);
                }

                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();


                Gc0 = GC.CollectionCount(0);
                Gc1 = GC.CollectionCount(1);
                Gc2 = GC.CollectionCount(2);

                if (start)
                {
                    Memory = GC.GetTotalMemory(false);
                    // start timer after collecting GC stat
                    sw.Restart();
                }
            }
        }
    }
}