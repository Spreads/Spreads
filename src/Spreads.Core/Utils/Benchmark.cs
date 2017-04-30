// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Spreads.Utils
{
    public static class Benchmark
    {
        public static bool ForceSilence { get; set; }

        private static Stopwatch _sw;
        private static bool _headerIsPrinted;
        private static ConcurrentDictionary<string, List<Stat>> _stats = new ConcurrentDictionary<string, List<Stat>>();

        public static Stat Run(string caseName, int innerLoopCount = 1, bool silent = false)
        {
            var sw = Interlocked.Exchange(ref _sw, null);
            sw = sw ?? new Stopwatch();

            var stat = new Stat(caseName, sw, innerLoopCount, silent);

            return stat;
        }

        private static void PrintHeader(int? caseLength = null)
        {
            var len = caseLength ?? 20;
            var caseDahes = new string('-', len + 1);
            var dashes = $"{caseDahes,-21}|{new string('-', 8),8}:|{new string('-', 9),9}:|{new string('-', 6),6}:|{new string('-', 6),6}:|{new string('-', 6),6}:|{new string('-', 8),8}:";
            Console.WriteLine();
            Console.WriteLine(GetHeader(caseLength));
            Console.WriteLine(dashes);
        }

        internal static string GetHeader(int? caseLength = null)
        {
            var len = caseLength ?? 20;
            var caseHeader = "Case".PadRight(len);
            return $" {caseHeader,-20}| {"MOPS",7} | {"Elapsed",8} | {"GC0",5} | {"GC1",5} | {"GC2",5} | {"Memory",7} ";
        }

        public static void Dump()
        {
            var maxLength = _stats.Keys.Select(k => k.Length).Max();

            PrintHeader(maxLength);

            var stats = _stats.Select(GetAverages).OrderByDescending(s => s.MOPS);

            foreach (var stat in stats)
            {
                Console.WriteLine(stat.ToString(maxLength));
            }

            _stats.Clear();

            _headerIsPrinted = false;

            Stat GetAverages(KeyValuePair<string, List<Stat>> kvp)
            {
                if (kvp.Value == null) throw new ArgumentException($"Null stat list for the case: {kvp.Key}");
                if (kvp.Value.Count == 0) throw new InvalidOperationException($"Empty stat list for the case: {kvp.Key}");
                var skip = kvp.Value.Count > 1
                    ? (kvp.Value.Count >= 10 ? 3 : 1)
                    : 0;
                var values = kvp.Value.Skip(skip).ToList();

                var elapsed = values.Select(l => l._statSnapshot._elapsed).Average();
                var gc0 = values.Select(l => l._statSnapshot._gc0).Average();
                var gc1 = values.Select(l => l._statSnapshot._gc1).Average();
                var gc2 = values.Select(l => l._statSnapshot._gc2).Average();
                var memory = values.Select(l => l._statSnapshot._memory).Average();

                var result = kvp.Value.First();

                result._statSnapshot._elapsed = (long)elapsed;
                result._statSnapshot._gc0 = gc0;
                result._statSnapshot._gc1 = gc1;
                result._statSnapshot._gc2 = gc2;
                result._statSnapshot._memory = memory;

                return result;
            }
        }

        public struct Stat : IDisposable
        {
            internal readonly string _caseName;
            internal Stopwatch _stopwatch;
            internal int _innerLoopCount;
            internal StatSnapshot _statSnapshot;
            internal bool _silent;

            public Stat(string caseName, Stopwatch sw, int innerLoopCount, bool silent = false)
            {
                _caseName = caseName;
                _stopwatch = sw;
                _innerLoopCount = innerLoopCount;
                _silent = silent;

                _statSnapshot = new StatSnapshot(_stopwatch, true);
            }

            public void Dispose()
            {
                var statEntry = new StatSnapshot(_stopwatch, false);
                Interlocked.Exchange(ref _sw, _stopwatch);

                _statSnapshot._elapsed = statEntry._elapsed;
                _statSnapshot._gc0 = statEntry._gc0 - _statSnapshot._gc0 - 2;
                _statSnapshot._gc1 = statEntry._gc1 - _statSnapshot._gc1 - 2;
                _statSnapshot._gc2 = statEntry._gc2 - _statSnapshot._gc2 - 2;
                _statSnapshot._memory = statEntry._memory - _statSnapshot._memory;

                var list = _stats.GetOrAdd(_caseName, (s1) => new List<Stat>());
                list.Add(this);

                if (!_silent && !ForceSilence)
                {
                    if (!_headerIsPrinted)
                    {
                        PrintHeader();
                        _headerIsPrinted = true;
                    }
                    Console.WriteLine(ToString());
                }
            }

            public double MOPS => Math.Round((_innerLoopCount * 0.001) / ((double)_statSnapshot._elapsed), 3);

            /// <inheritdoc />
            public override string ToString()
            {
                var trimmedCaseName = _caseName.Length > 20 ? _caseName.Substring(0, 17) + "..." : _caseName;
                return $"{trimmedCaseName,-20} |{MOPS,8:f2} | {_statSnapshot._elapsed,5} ms | {_statSnapshot._gc0,5:f1} | {_statSnapshot._gc1,5:f1} | {_statSnapshot._gc2,5:f1} | {_statSnapshot._memory / 1000000.0,5:f3} MB";
            }

            public string ToString(int caseAlignmentLength)
            {
                var paddedCaseName = _caseName.PadRight(caseAlignmentLength);
                return $"{paddedCaseName,-20} |{MOPS,8:f2} | {_statSnapshot._elapsed,5} ms | {_statSnapshot._gc0,5:f1} | {_statSnapshot._gc1,5:f1} | {_statSnapshot._gc2,5:f1} | {_statSnapshot._memory / 1000000.0,5:f3} MB";
            }
        }

        internal struct StatSnapshot
        {
            internal long _elapsed;
            internal double _gc0;
            internal double _gc1;
            internal double _gc2;
            internal double _memory;

            public StatSnapshot(Stopwatch sw, bool start)
            {
                this = new StatSnapshot();

                if (!start)
                {
                    // end of measurement, first stop timer then collect/count
                    sw.Stop();
                    _elapsed = sw.ElapsedMilliseconds;

                    // NB we exclude forced GC from counters,
                    // by measuring memory before forced GC we could
                    // calculate uncollected garbage
                    _memory = GC.GetTotalMemory(false);
                }

                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();

                _gc0 = GC.CollectionCount(0);
                _gc1 = GC.CollectionCount(1);
                _gc2 = GC.CollectionCount(2);

                if (start)
                {
                    _memory = GC.GetTotalMemory(false);
                    // start timer after collecting GC stat
                    sw.Restart();
                }
            }
        }
    }
}